// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/TagAggregationService.cs
//  역할: tag_history → 1분/1시간/1일 단위 평균·최소·최대·샘플수 집계
//        AsyncScheduler 로 주기 실행, GROUP BY 로 직접 집계 INSERT
//        settings.json Storage.Provider = "SQLite" 일 때만 동작
//        (InfluxDB 는 자체 다운샘플링(Task) 기능 사용 권장 — 이 서비스 범위 밖)
//
//  ━━━ 생성 테이블 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  tag_agg_minute / tag_agg_hour / tag_agg_day (동일 스키마):
//    tag_id TEXT, period_start TEXT(ISO8601, 집계 구간 시작·UTC),
//    avg_value REAL, min_value REAL, max_value REAL, sample_count INTEGER
//
//  ━━━ 알려진 제약 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  앱 시작 시점 기준 주기 실행 — 정각(00분/00시/자정) 정렬은 미지원.
//  예) 09:03 에 앱 시작 시 분 집계는 09:04, 09:05... 시점에 실행됨.
//
//  C-17: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using lssLib.DB.Core;
using lssLib.DB.Sqlite;
using lssLib.Log;
using lssLib.Messaging;
using System.Data;
using System.IO;

namespace IIoT.Collector.Storage;

/// <summary>
/// Tag 이력 집계 서비스 (DI 싱글턴).
/// <para>
/// tag_history 원본 데이터를 1분/1시간/1일 단위로 미리 집계하여
/// 장기간 트렌드 조회 성능을 확보한다.
/// </para>
/// </summary>
public sealed class TagAggregationService : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorSettingsLoader _settingsLoader;

    private SqliteDbContext? _ctx;
    private bool             _enabled;

    private ScheduledTask? _minuteTask;
    private ScheduledTask? _hourTask;
    private ScheduledTask? _dayTask;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public TagAggregationService(CollectorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// 집계 테이블 생성 + 주기 스케줄 등록.
    /// Provider != "SQLite" 이면 즉시 반환하여 비활성 상태로 둔다.
    /// App.xaml.cs 에서 ITimeSeriesStore.InitializeAsync() 이후 호출.
    /// </summary>
    public async Task InitializeAsync()
    {
        var storage = _settingsLoader.Settings.Storage;

        if (!storage.Provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            LogManager.Instance.Info("Aggregation",
                "Provider=InfluxDB — SQLite 집계 서비스 비활성화 (InfluxDB 자체 다운샘플링 권장)");
            _enabled = false;
            return;
        }

        var dbPath = storage.SQLite.DbPath;
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);

        var cfg = new RelationalDbConfig(
            DbProviderType.Sqlite,
            $"Data Source={dbPath}",
            commandTimeoutSec: 30);

        _ctx = new SqliteDbContext(cfg);
        await _ctx.OpenAsync();

        await _ctx.EnsureTableAsync(_CreateTableSql("tag_agg_minute"));
        await _ctx.EnsureTableAsync(_CreateTableSql("tag_agg_hour"));
        await _ctx.EnsureTableAsync(_CreateTableSql("tag_agg_day"));

        _minuteTask = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromMinutes(1),
            ct => _AggregateAsync("tag_agg_minute", TimeSpan.FromMinutes(1), ct),
            name: "agg:minute");

        _hourTask = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromHours(1),
            ct => _AggregateAsync("tag_agg_hour", TimeSpan.FromHours(1), ct),
            name: "agg:hour");

        _dayTask = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromDays(1),
            ct => _AggregateAsync("tag_agg_day", TimeSpan.FromDays(1), ct),
            name: "agg:day");

        _enabled = true;

        LogManager.Instance.Info("Aggregation",
            $"집계 서비스 초기화 완료 — {dbPath} (분/시간/일 집계 활성)");
    }

    // §4 ─ 테이블 스키마 ───────────────────────────────────

    private static string _CreateTableSql(string tableName) => $"""
        CREATE TABLE IF NOT EXISTS {tableName} (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            tag_id       TEXT    NOT NULL,
            period_start TEXT    NOT NULL,
            avg_value    REAL    NOT NULL,
            min_value    REAL    NOT NULL,
            max_value    REAL    NOT NULL,
            sample_count INTEGER NOT NULL,
            UNIQUE(tag_id, period_start)
        );
        CREATE INDEX IF NOT EXISTS idx_{tableName}_tag_ts
            ON {tableName} (tag_id, period_start);
        """;

    // §5 ─ 집계 실행 ───────────────────────────────────────

    /// <summary>
    /// 직전 window 구간의 tag_history 를 tag_id 별로 GROUP BY 집계하여
    /// 대상 테이블에 INSERT (중복 구간은 UNIQUE 제약으로 무시).
    /// </summary>
    private async Task _AggregateAsync(string tableName, TimeSpan window, CancellationToken ct)
    {
        if (_ctx is null) return;

        var toUtc   = DateTimeOffset.UtcNow;
        var fromUtc = toUtc - window;

        try
        {
            var result = await _ctx.ExecuteAsync($"""
                INSERT OR IGNORE INTO {tableName}
                    (tag_id, period_start, avg_value, min_value, max_value, sample_count)
                SELECT
                    tag_id,
                    @periodStart AS period_start,
                    AVG(eng_value), MIN(eng_value), MAX(eng_value), COUNT(*)
                FROM tag_history
                WHERE timestamp >= @from AND timestamp < @to
                GROUP BY tag_id
                """,
                CommandType.Text,
                [
                    DbParam.In("@periodStart", fromUtc.ToString("O")),
                    DbParam.In("@from",        fromUtc.ToString("O")),
                    DbParam.In("@to",          toUtc.ToString("O")),
                ],
                ct);

            if (!result.IsOk)
                LogManager.Instance.Warn("Aggregation", $"{tableName} 집계 실패: {result.Message}");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("Aggregation", $"{tableName} 집계 예외: {ex.Message}");
        }
    }

    // §6 ─ 정리 ────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _minuteTask?.Cancel();
        _hourTask?.Cancel();
        _dayTask?.Cancel();

        if (_ctx is not null)
            await _ctx.DisposeAsync();
    }
}
