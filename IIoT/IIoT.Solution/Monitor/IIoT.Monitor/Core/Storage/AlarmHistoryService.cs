// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Storage/AlarmHistoryService.cs
//  역할: AlarmAggregator.AlarmRecorded 이벤트(생성+상태전이 모두)를 구독하여
//        SQLite alarm_history 테이블에 영구 저장한다. Collector의
//        AuditLogService.cs 와 동일한 패턴(자체 SqliteDbContext 관리).
//        재시작해도 알람 이력이 사라지지 않도록 하는 것이 목적(실무강화).
//  MN-EX-02: 신규
//  생성: 2026-07-08
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;
using lssLib.DB.Core;
using lssLib.DB.Sqlite;
using lssLib.Log;
using System.Data;
using System.IO;

namespace IIoT.Monitor.Core.Storage;

/// <summary>알람 이력 SQLite 저장 서비스 (DI 싱글턴).</summary>
public sealed class AlarmHistoryService : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private SqliteDbContext? _ctx;

    /// <summary>DB 파일 보존 기간(일). 이보다 오래된 이력은 초기화 시 자동 삭제.</summary>
    private const int RetentionDays = 90;

    // §2 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// SQLite DB 를 열고 테이블을 초기화한다. 앱 시작 시(MainWindow.Loaded) 1회 호출.
    /// </summary>
    public async Task InitializeAsync()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "monitor.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var cfg = new RelationalDbConfig(
            DbProviderType.Sqlite,
            $"Data Source={dbPath}",
            commandTimeoutSec: 30);

        _ctx = new SqliteDbContext(cfg);
        await _ctx.OpenAsync();

        await _ctx.SetPragmaAsync("journal_mode", "WAL");
        await _ctx.SetPragmaAsync("synchronous", "NORMAL");

        await _ctx.EnsureTableAsync("""
            CREATE TABLE IF NOT EXISTS alarm_history (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                collector_id   TEXT    NOT NULL,
                collector_name TEXT    NOT NULL,
                alarm_key      TEXT    NOT NULL,
                tag_id         TEXT    NOT NULL,
                plc_id         TEXT    NOT NULL,
                tag_name       TEXT    DEFAULT '',
                level          TEXT    NOT NULL,
                status         TEXT    NOT NULL,
                message        TEXT    DEFAULT '',
                eng_value      REAL    DEFAULT 0,
                occurred_at    TEXT    NOT NULL,
                recorded_at    TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_alarm_history_ts
                ON alarm_history (occurred_at);
            CREATE INDEX IF NOT EXISTS idx_alarm_history_collector
                ON alarm_history (collector_id);
            """);

        // 보존 기간 초과 이력 정리
        var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays).ToString("O");
        await _ctx.ExecuteAsync(
            "DELETE FROM alarm_history WHERE occurred_at < @cutoff",
            CommandType.Text,
            [DbParam.In("@cutoff", cutoff)]);

        LogManager.Instance.Info("AlarmHistory", $"알람 이력 DB 초기화 완료: {dbPath}");
    }

    // §3 ─ 저장 ────────────────────────────────────────────

    /// <summary>알람 1건(생성 또는 상태전이)을 이력 테이블에 INSERT 한다.</summary>
    public async Task RecordAsync(AlarmRow row)
    {
        if (_ctx is null) return;

        try
        {
            await _ctx.ExecuteAsync(
                """
                INSERT INTO alarm_history
                    (collector_id, collector_name, alarm_key, tag_id, plc_id, tag_name,
                     level, status, message, eng_value, occurred_at, recorded_at)
                VALUES
                    (@collectorId, @collectorName, @alarmKey, @tagId, @plcId, @tagName,
                     @level, @status, @message, @engValue, @occurredAt, @recordedAt)
                """,
                CommandType.Text,
                [
                    DbParam.In("@collectorId",   row.CollectorId),
                    DbParam.In("@collectorName", row.CollectorName),
                    DbParam.In("@alarmKey",      row.AlarmKey),
                    DbParam.In("@tagId",         row.TagId),
                    DbParam.In("@plcId",         row.PlcId),
                    DbParam.In("@tagName",       row.TagName),
                    DbParam.In("@level",         row.Level),
                    DbParam.In("@status",        row.Status),
                    DbParam.In("@message",       row.Message),
                    DbParam.In("@engValue",      row.EngValue),
                    DbParam.In("@occurredAt",    row.OccurredAt.ToString("O")),
                    DbParam.In("@recordedAt",    DateTimeOffset.UtcNow.ToString("O")),
                ]);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("AlarmHistory", $"알람 이력 저장 실패: {ex.Message}");
        }
    }

    // §4 ─ 정리 ────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_ctx is not null)
        {
            await _ctx.CloseAsync();
            _ctx = null;
        }
    }
}
