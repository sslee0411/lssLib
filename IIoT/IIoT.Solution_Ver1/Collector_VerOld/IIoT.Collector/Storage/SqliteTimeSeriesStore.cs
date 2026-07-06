// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/SqliteTimeSeriesStore.cs
//  역할: SQLite 기반 시계열 저장소 구현 (기본값)
//        lssLib.DB.Sqlite (SqliteDbContext + SqliteRepository) 사용
//        CommandQueue 경유로 DB 쓰기 순서 보장
//
//  ━━━ SQLite 환경 구성 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  [csproj 참조 추가 필요]
//  <ProjectReference Include="..\..\..\..\Base\BCL\DB\lssLib.DB\lssLib.DB.csproj" />
//  <ProjectReference Include="..\..\..\..\Base\BCL\DB\lssLib.DB.Sqlite\lssLib.DB.Sqlite.csproj" />
//
//  [DB 파일 경로]
//  기본: {실행파일경로}\Data\collector.db
//  settings.json 의 Storage.SQLite.DbPath 로 변경 가능
//
//  [확인 도구]
//  DB Browser for SQLite (무료): https://sqlitebrowser.org/dl/
//  → File → Open Database → collector.db 선택
//  → Browse Data 탭 → 테이블 선택 → 행 확인
//
//  ━━━ 테이블 구조 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  tag_history:
//    tag_id TEXT, tag_name TEXT, plc_id TEXT,
//    raw_value REAL, eng_value REAL, unit TEXT, quality TEXT,
//    timestamp TEXT (ISO8601)
//
//  alarm_history:
//    alarm_key TEXT, tag_id TEXT, tag_name TEXT, plc_id TEXT,
//    level TEXT, status TEXT, message TEXT, eng_value REAL,
//    occurred_at TEXT (ISO8601)
//
//  collector_stats:
//    plc_id TEXT, poll_count INTEGER, error_count INTEGER,
//    avg_poll_ms REAL, tag_count INTEGER,
//    timestamp TEXT (ISO8601)
//  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  C-07: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using lssLib.DB.Contracts;
using lssLib.DB.Core;
using lssLib.DB.Sqlite;
using lssLib.Log;
using lssLib.Messaging;
using System.IO;

namespace IIoT.Collector.Storage;

/// <summary>
/// SQLite 기반 시계열 저장소.
/// <para>
/// <b>환경 구성:</b><br/>
/// IIoT.Collector.csproj 에 lssLib.DB 와 lssLib.DB.Sqlite ProjectReference 추가 필요.
/// settings.json 의 Storage.Provider = "SQLite" 일 때 DI 에 등록됨.
/// </para>
/// <para>
/// <b>CommandQueue 패턴:</b><br/>
/// 모든 DB INSERT 는 CommandQueue 를 통해 순서를 보장한다.
/// FlowEngine 폴링 스레드 → CommandQueue 큐잉 → 순차 실행 → SQLite INSERT.
/// </para>
/// </summary>
public sealed class SqliteTimeSeriesStore : ITimeSeriesStore
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorSettingsLoader _settingsLoader;
    private          SqliteDbContext?        _ctx;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public SqliteTimeSeriesStore(CollectorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// SQLite DB 파일을 열고 테이블을 초기화합니다.
    /// App 시작 시 1회 호출.
    /// </summary>
    public async Task InitializeAsync()
    {
        var dbPath = _settingsLoader.Settings.Storage.SQLite.DbPath;

        // 상대 경로 → 실행파일 기준 절대 경로 변환
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var cfg = new RelationalDbConfig(
            DbProviderType.Sqlite,
            $"Data Source={dbPath}",
            commandTimeoutSec: 30);

        _ctx = new SqliteDbContext(cfg);
        await _ctx.OpenAsync();

        // WAL 모드: 읽기와 쓰기를 동시에 허용 (성능 향상)
        await _ctx.SetPragmaAsync("journal_mode", "WAL");
        await _ctx.SetPragmaAsync("synchronous",  "NORMAL");

        // 테이블 생성 (없으면)
        await _ctx.EnsureTableAsync("""
            CREATE TABLE IF NOT EXISTS tag_history (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                tag_id      TEXT    NOT NULL,
                tag_name    TEXT    NOT NULL,
                plc_id      TEXT    NOT NULL,
                raw_value   REAL    NOT NULL,
                eng_value   REAL    NOT NULL,
                unit        TEXT    DEFAULT '',
                quality     TEXT    DEFAULT 'Good',
                timestamp   TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_tag_history_tag_ts
                ON tag_history (tag_id, timestamp);
            """);

        await _ctx.EnsureTableAsync("""
            CREATE TABLE IF NOT EXISTS alarm_history (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                alarm_key   TEXT    NOT NULL,
                tag_id      TEXT    NOT NULL,
                tag_name    TEXT    NOT NULL,
                plc_id      TEXT    NOT NULL,
                level       TEXT    NOT NULL,
                status      TEXT    NOT NULL,
                message     TEXT    DEFAULT '',
                eng_value   REAL    DEFAULT 0,
                occurred_at TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_alarm_history_ts
                ON alarm_history (occurred_at);
            """);

        await _ctx.EnsureTableAsync("""
            CREATE TABLE IF NOT EXISTS collector_stats (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                plc_id       TEXT    NOT NULL,
                poll_count   INTEGER DEFAULT 0,
                error_count  INTEGER DEFAULT 0,
                avg_poll_ms  REAL    DEFAULT 0,
                tag_count    INTEGER DEFAULT 0,
                timestamp    TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_stats_ts
                ON collector_stats (timestamp);
            """);

        LogManager.Instance.Info("SQLiteStore",
            $"SQLite 초기화 완료: {dbPath}");
    }

    // §4 ─ Tag 값 이력 저장 ────────────────────────────────

    /// <summary>
    /// Tag 값 이력을 CommandQueue 경유로 SQLite 에 INSERT 합니다.
    /// SDT 필터 통과한 값만 전달받으므로 모두 저장합니다.
    /// </summary>
    public Task WriteTagHistoryAsync(TagHistoryRecord r, CancellationToken ct = default)
    {
        CommandQueue.Instance.Enqueue(LambdaCommand.Create(async innerCt =>
        {
            if (_ctx is null) return;

            var result = await _ctx.ExecuteAsync("""
                INSERT INTO tag_history
                    (tag_id, tag_name, plc_id, raw_value, eng_value, unit, quality, timestamp)
                VALUES
                    (@tag_id, @tag_name, @plc_id, @raw, @eng, @unit, @quality, @ts)
                """,
                System.Data.CommandType.Text,
                [
                    DbParam.In("@tag_id",   r.TagId),
                    DbParam.In("@tag_name", r.TagName),
                    DbParam.In("@plc_id",   r.PlcId),
                    DbParam.In("@raw",      r.RawValue),
                    DbParam.In("@eng",      r.EngValue),
                    DbParam.In("@unit",     r.Unit),
                    DbParam.In("@quality",  r.Quality),
                    DbParam.In("@ts",       r.Timestamp.ToString("O")),
                ],
                innerCt);

            if (!result.IsOk)
                LogManager.Instance.Warn("SQLiteStore",
                    $"tag_history INSERT 실패: {result.Message}");
        }, CommandPriority.Normal));

        return Task.CompletedTask;
    }

    // §5 ─ 알람 이력 저장 ──────────────────────────────────

    public Task WriteAlarmHistoryAsync(AlarmHistoryRecord r, CancellationToken ct = default)
    {
        CommandQueue.Instance.Enqueue(LambdaCommand.Create(async innerCt =>
        {
            if (_ctx is null) return;

            var result = await _ctx.ExecuteAsync("""
                INSERT INTO alarm_history
                    (alarm_key, tag_id, tag_name, plc_id, level, status, message, eng_value, occurred_at)
                VALUES
                    (@key, @tag_id, @tag_name, @plc_id, @level, @status, @msg, @eng, @ts)
                """,
                System.Data.CommandType.Text,
                [
                    DbParam.In("@key",      r.AlarmKey),
                    DbParam.In("@tag_id",   r.TagId),
                    DbParam.In("@tag_name", r.TagName),
                    DbParam.In("@plc_id",   r.PlcId),
                    DbParam.In("@level",    r.Level),
                    DbParam.In("@status",   r.Status),
                    DbParam.In("@msg",      r.Message),
                    DbParam.In("@eng",      r.EngValue),
                    DbParam.In("@ts",       r.OccurredAt.ToString("O")),
                ],
                innerCt);

            if (!result.IsOk)
                LogManager.Instance.Warn("SQLiteStore",
                    $"alarm_history INSERT 실패: {result.Message}");
        }, CommandPriority.Normal));

        return Task.CompletedTask;
    }

    // §6 ─ 수집 통계 저장 ──────────────────────────────────

    public Task WriteStatsAsync(CollectorStatsRecord r, CancellationToken ct = default)
    {
        CommandQueue.Instance.Enqueue(LambdaCommand.Create(async innerCt =>
        {
            if (_ctx is null) return;

            var result = await _ctx.ExecuteAsync("""
                INSERT INTO collector_stats
                    (plc_id, poll_count, error_count, avg_poll_ms, tag_count, timestamp)
                VALUES
                    (@plc_id, @poll, @err, @avg, @tag, @ts)
                """,
                System.Data.CommandType.Text,
                [
                    DbParam.In("@plc_id", r.PlcId),
                    DbParam.In("@poll",   r.PollCount),
                    DbParam.In("@err",    r.ErrorCount),
                    DbParam.In("@avg",    r.AvgPollMs),
                    DbParam.In("@tag",    r.TagCount),
                    DbParam.In("@ts",     r.Timestamp.ToString("O")),
                ],
                innerCt);

            if (!result.IsOk)
                LogManager.Instance.Warn("SQLiteStore",
                    $"collector_stats INSERT 실패: {result.Message}");
        }, CommandPriority.Low));

        return Task.CompletedTask;
    }

    // §7 ─ Flush (SQLite 는 즉시 쓰기 — no-op) ────────────

    public Task FlushAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    // §8 ─ 리소스 해제 ─────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_ctx is not null)
        {
            await _ctx.CloseAsync();
            await _ctx.DisposeAsync();
        }
        LogManager.Instance.Info("SQLiteStore", "SQLite 연결 해제 완료");
    }
}
