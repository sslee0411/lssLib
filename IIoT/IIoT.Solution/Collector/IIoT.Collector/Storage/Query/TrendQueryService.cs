// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/Query/TrendQueryService.cs
//  역할: SQLite tag_history 테이블 시간 범위 조회 서비스
//        SqliteDbContext.QueryTableAsync() → DataTable → TrendPoint 매핑
//        (SqliteDbContext 에는 QueryAsync<T> 없음 — QueryTableAsync 사용)
//  C-13: 신규
//  C-EX-09: 장기 구간(2일/30일 초과) 조회 시 집계 테이블(tag_agg_hour/day)로
//           자동 전환 — 원본 tag_history 풀스캔을 피해 조회 성능 확보
//  생성: 2026-07-01 / 수정: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using lssLib.DB.Core;
using lssLib.DB.Sqlite;
using lssLib.Log;
using System.Data;
using System.IO;

namespace IIoT.Collector.Storage.Query;

/// <summary>조회된 단일 이력 포인트</summary>
public sealed record TrendPoint(DateTimeOffset Timestamp, double EngValue, double RawValue);

/// <summary>Tag 선택 정보 (드롭다운 표시용)</summary>
public sealed record TrendTagItem(string TagId, string TagName, string PlcId, string Unit);

/// <summary>
/// Trend View SQLite 조회 서비스 (DI 싱글턴).
/// <para>
/// SqliteDbContext.QueryTableAsync() 로 DataTable 을 받아
/// 수동으로 TrendPoint 목록으로 변환한다.
/// (SqliteDbContext 는 QueryAsync&lt;T&gt; 미지원 — QueryTableAsync + 수동 매핑 패턴)
/// </para>
/// <para>
/// ★ C-EX-09: 조회 구간이 2일을 넘으면 <c>tag_agg_hour</c>, 30일을 넘으면
/// <c>tag_agg_day</c> 집계 테이블을 대신 사용한다 (C-17 에서 미리 쌓아둔 집계).
/// 집계 조회는 평균값(avg_value)만 사용하므로 Raw/Eng 라인이 동일하게 표시된다.
/// </para>
/// </summary>
public sealed class TrendQueryService
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorSettingsLoader _settingsLoader;
    private readonly CollectorConfigLoader   _configLoader;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public TrendQueryService(
        CollectorSettingsLoader settingsLoader,
        CollectorConfigLoader   configLoader)
    {
        _settingsLoader = settingsLoader;
        _configLoader   = configLoader;
    }

    // §3 ─ DB 경로 ─────────────────────────────────────────

    private string DbPath
    {
        get
        {
            var path = _settingsLoader.Settings.Storage.SQLite.DbPath;
            return Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }
    }

    // §4 ─ Tag 목록 조회 ──────────────────────────────────

    /// <summary>
    /// 트렌드 조회 가능한 Tag 목록 (CollectorConfigLoader 기반, DB 조회 불필요).
    /// </summary>
    public IReadOnlyList<TrendTagItem> GetAvailableTags()
    {
        var result = new List<TrendTagItem>();
        foreach (var plc in _configLoader.Plcs)
            foreach (var tag in plc.Tags.Where(t => t.IsEnabled))
                result.Add(new TrendTagItem(tag.Id, tag.Name, plc.Name, tag.Unit));
        return result;
    }

    // §5 ─ 이력 조회 ──────────────────────────────────────

    /// <summary>
    /// 시간 범위 + Tag 로 이력을 조회합니다.
    /// 구간이 짧으면(≤2일) tag_history 원본을, 길면 집계 테이블을 사용합니다.
    /// 원본 조회 시 건수가 maxPoints 초과하면 ROW_NUMBER 기반 균등 다운샘플링을 적용합니다.
    /// </summary>
    public async Task<IReadOnlyList<TrendPoint>> QueryAsync(
        string         tagId,
        DateTimeOffset from,
        DateTimeOffset to,
        int            maxPoints = 3000)
    {
        // ★ C-EX-09 신규: 장기 구간은 집계 테이블 사용 (원본 풀스캔 회피)
        var span = to - from;
        if (span > TimeSpan.FromDays(30))
            return await _QueryAggregateAsync(tagId, from, to, "tag_agg_day");
        if (span > TimeSpan.FromDays(2))
            return await _QueryAggregateAsync(tagId, from, to, "tag_agg_hour");

        if (!File.Exists(DbPath))
        {
            LogManager.Instance.Warn("TrendQuery", $"DB 파일 없음: {DbPath}");
            return [];
        }

        try
        {
            var cfg = new RelationalDbConfig(
                DbProviderType.Sqlite,
                $"Data Source={DbPath}",
                commandTimeoutSec: 30);

            await using var ctx = new SqliteDbContext(cfg);
            await ctx.OpenAsync();

            // ① 전체 건수 조회 (다운샘플링 여부 결정)
            // ★ 버그 수정: DB 저장 시각은 UTC(+00:00) 인데 from/to 는 로컬 시간(+09:00 등)이라
            //   문자열 비교(SQLite TEXT 비교) 시 offset 불일치로 범위가 어긋나던 문제.
            //   반드시 UTC 로 변환하여 저장 형식과 동일한 offset(+00:00)으로 맞춘다.
            var fromUtc = from.ToUniversalTime();
            var toUtc   = to.ToUniversalTime();

            var countResult = await ctx.QueryTableAsync(
                "SELECT COUNT(*) AS cnt FROM tag_history WHERE tag_id = @tagId AND timestamp >= @from AND timestamp <= @to",
                CommandType.Text,
                [
                    DbParam.In("@tagId", tagId),
                    DbParam.In("@from",  fromUtc.ToString("O")),
                    DbParam.In("@to",    toUtc.ToString("O")),
                ]);

            if (!countResult.IsOk || countResult.Value is null)
            {
                LogManager.Instance.Warn("TrendQuery", $"건수 조회 실패: {countResult.Message}");
                return [];
            }

            var totalCount = Convert.ToInt64(countResult.Value.Rows[0]["cnt"]);
            if (totalCount == 0) return [];

            // ② stride 계산 (균등 다운샘플링)
            var stride = totalCount <= maxPoints
                ? 1
                : (int)Math.Ceiling((double)totalCount / maxPoints);

            // ③ 데이터 조회
            var sql = stride <= 1
                ? """
                  SELECT timestamp, eng_value, raw_value
                  FROM tag_history
                  WHERE tag_id = @tagId
                    AND timestamp >= @from
                    AND timestamp <= @to
                  ORDER BY timestamp ASC
                  """
                : $"""
                  SELECT timestamp, eng_value, raw_value FROM (
                      SELECT timestamp, eng_value, raw_value,
                             ROW_NUMBER() OVER (ORDER BY timestamp ASC) AS rn
                      FROM tag_history
                      WHERE tag_id = @tagId
                        AND timestamp >= @from
                        AND timestamp <= @to
                  ) WHERE rn % {stride} = 1
                  ORDER BY timestamp ASC
                  """;

            var dataResult = await ctx.QueryTableAsync(
                sql,
                CommandType.Text,
                [
                    DbParam.In("@tagId", tagId),
                    DbParam.In("@from",  fromUtc.ToString("O")),
                    DbParam.In("@to",    toUtc.ToString("O")),
                ]);

            if (!dataResult.IsOk || dataResult.Value is null)
            {
                LogManager.Instance.Warn("TrendQuery", $"데이터 조회 실패: {dataResult.Message}");
                return [];
            }

            // ④ DataTable → TrendPoint 변환
            var points = new List<TrendPoint>(dataResult.Value.Rows.Count);
            foreach (DataRow row in dataResult.Value.Rows)
            {
                var ts  = DateTimeOffset.Parse(row["timestamp"].ToString()!);
                var eng = Convert.ToDouble(row["eng_value"]);
                var raw = Convert.ToDouble(row["raw_value"]);
                points.Add(new TrendPoint(ts, eng, raw));
            }

            LogManager.Instance.Info("TrendQuery",
                $"[{tagId}] {from:HH:mm}~{to:HH:mm} → {points.Count:#,0}건 " +
                $"(전체 {totalCount:#,0}건, stride={stride})");
            return points;
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("TrendQuery", $"조회 예외: {ex.Message}");
            return [];
        }
    }

    // §6 ─ 집계 테이블 조회 (C-EX-09 신규) ─────────────────

    /// <summary>
    /// tag_agg_hour/tag_agg_day 집계 테이블에서 평균값(avg_value) 기준으로 조회합니다.
    /// C-17 TagAggregationService 가 미리 쌓아둔 집계 데이터를 사용하므로
    /// 원본 tag_history 를 풀스캔하지 않아 장기 구간 조회가 빠릅니다.
    /// </summary>
    private async Task<IReadOnlyList<TrendPoint>> _QueryAggregateAsync(
        string tagId, DateTimeOffset from, DateTimeOffset to, string tableName)
    {
        if (!File.Exists(DbPath))
        {
            LogManager.Instance.Warn("TrendQuery", $"DB 파일 없음: {DbPath}");
            return [];
        }

        try
        {
            var cfg = new RelationalDbConfig(
                DbProviderType.Sqlite,
                $"Data Source={DbPath}",
                commandTimeoutSec: 30);

            await using var ctx = new SqliteDbContext(cfg);
            await ctx.OpenAsync();

            var fromUtc = from.ToUniversalTime();
            var toUtc   = to.ToUniversalTime();

            var result = await ctx.QueryTableAsync(
                $"""
                SELECT period_start AS timestamp, avg_value AS eng_value, avg_value AS raw_value
                FROM {tableName}
                WHERE tag_id = @tagId AND period_start >= @from AND period_start <= @to
                ORDER BY period_start ASC
                """,
                CommandType.Text,
                [
                    DbParam.In("@tagId", tagId),
                    DbParam.In("@from",  fromUtc.ToString("O")),
                    DbParam.In("@to",    toUtc.ToString("O")),
                ]);

            if (!result.IsOk || result.Value is null)
            {
                LogManager.Instance.Warn("TrendQuery", $"집계 조회 실패({tableName}): {result.Message}");
                return [];
            }

            var points = new List<TrendPoint>(result.Value.Rows.Count);
            foreach (DataRow row in result.Value.Rows)
            {
                var ts  = DateTimeOffset.Parse(row["timestamp"].ToString()!);
                var eng = Convert.ToDouble(row["eng_value"]);
                var raw = Convert.ToDouble(row["raw_value"]);
                points.Add(new TrendPoint(ts, eng, raw));
            }

            LogManager.Instance.Info("TrendQuery",
                $"[{tagId}] {tableName} 집계 조회 → {points.Count:#,0}건");
            return points;
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("TrendQuery", $"집계 조회 예외({tableName}): {ex.Message}");
            return [];
        }
    }
}
