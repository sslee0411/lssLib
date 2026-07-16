// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/Storage/EventHistoryDbService.cs
//  역할: EventHistoryService.Recorded 이벤트를 구독하여 SQLite
//        event_history 테이블에 영구 저장한다 (재시작해도 이력 유지).
//        Monitor AlarmHistoryService(MN-EX-02) 와 동일 패턴
//        (자체 SqliteDbContext 관리 · WAL · 보존기간 자동 정리).
//  MG-EX-04: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Models;
using lssLib.DB.Core;
using lssLib.DB.Sqlite;
using lssLib.Log;
using System.Data;
using System.IO;

namespace IIoT.Manager.Core.Storage;

/// <summary>이벤트 이력 SQLite 저장 서비스 (DI 싱글턴).</summary>
public sealed class EventHistoryDbService : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private SqliteDbContext? _ctx;

    /// <summary>보존 기간(일). 이보다 오래된 이력은 초기화 시 자동 삭제.</summary>
    private const int RetentionDays = 90;

    // §2 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// SQLite DB 를 열고 테이블을 초기화한다.
    /// 앱 시작 시(ManagerMainViewModel.InitializeAsync — MainWindow.Loaded 경유) 1회 호출.
    /// </summary>
    public async Task InitializeAsync()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "manager.db");
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
            CREATE TABLE IF NOT EXISTS event_history (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                program     TEXT    NOT NULL,
                severity    TEXT    NOT NULL,
                text        TEXT    NOT NULL,
                occurred_at TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_event_history_ts
                ON event_history (occurred_at);
            CREATE INDEX IF NOT EXISTS idx_event_history_program
                ON event_history (program);
            """);

        // 보존 기간 초과 이력 정리
        var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays).ToString("O");
        await _ctx.ExecuteAsync(
            "DELETE FROM event_history WHERE occurred_at < @cutoff",
            CommandType.Text,
            [DbParam.In("@cutoff", cutoff)]);

        LogManager.Instance.Info("EventHistoryDb", $"이벤트 이력 DB 초기화 완료: {dbPath}");
    }

    // §3 ─ 저장 ────────────────────────────────────────────

    /// <summary>이벤트 1건을 이력 테이블에 INSERT 한다 (fire-and-forget 호출용 — 자체 try/catch).</summary>
    public async Task RecordAsync(EventRow row, EventSeverity severity)
    {
        if (_ctx is null) return;   // 초기화 전 발생 이벤트는 건너뜀 (메모리 이력엔 존재)

        try
        {
            await _ctx.ExecuteAsync(
                """
                INSERT INTO event_history (program, severity, text, occurred_at)
                VALUES (@program, @severity, @text, @occurredAt)
                """,
                CommandType.Text,
                [
                    DbParam.In("@program",    row.Program),
                    DbParam.In("@severity",   severity.ToString()),
                    DbParam.In("@text",       row.Text),
                    DbParam.In("@occurredAt", DateTimeOffset.Now.ToString("O")),
                ]);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("EventHistoryDb", $"이벤트 이력 저장 실패: {ex.Message}");
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
