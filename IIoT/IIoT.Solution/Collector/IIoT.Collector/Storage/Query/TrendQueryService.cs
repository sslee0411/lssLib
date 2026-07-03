// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/Query/TrendQueryService.cs
//  역할: SQLite tag_history 테이블 시간 범위 조회 서비스
//        SqliteDbContext.QueryTableAsync() → DataTable → TrendPoint 매핑
//        (SqliteDbContext 에는 QueryAsync<T> 없음 — QueryTableAsync 사용)
//  C-13: 신규
//  생성: 2026-07-01
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
    /// tag_history 테이블에서 시간 범위 + Tag 로 이력을 조회합니다.
    /// 건수가 maxPoints 초과 시 ROW_NUMBER 기반 균등 다운샘플링을 적용합니다.
    /// </summary>
    public async Task<IReadOnlyList<TrendPoint>> QueryAsync(
        string         tagId,
        DateTimeOffset from,
        DateTimeOffset to,
        int            maxPoints = 3000)
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

            // ① 전체 건수 조회 (다운샘플링 여부 결정)
            var countResult = await ctx.QueryTableAsync(
                "SELECT COUNT(*) AS cnt FROM tag_history WHERE tag_id = @tagId AND timestamp >= @from AND timestamp <= @to",
                CommandType.Text,
                [
                    DbParam.In("@tagId", tagId),
                    DbParam.In("@from",  from.ToString("O")),
                    DbParam.In("@to",    to.ToString("O")),
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
                    DbParam.In("@from",  from.ToString("O")),
                    DbParam.In("@to",    to.ToString("O")),
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
}
