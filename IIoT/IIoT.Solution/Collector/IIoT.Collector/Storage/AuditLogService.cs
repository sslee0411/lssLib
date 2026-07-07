// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/AuditLogService.cs
//  역할: 강제쓰기/일시정지·재개/알람ACK/설정재로드 등 주요 조작 이력을
//        SQLite audit_log 테이블에 기록 (TagAggregationService 와 동일하게
//        자체 SQLite 연결을 관리 — ITimeSeriesStore 와 독립적)
//  C-EX-03: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using lssLib.DB.Core;
using lssLib.DB.Sqlite;
using lssLib.Log;
using System.Data;
using System.IO;

namespace IIoT.Collector.Storage;

/// <summary>감사 로그 서비스 (DI 싱글턴).</summary>
public sealed class AuditLogService : IAsyncDisposable
{
    private readonly CollectorSettingsLoader _settingsLoader;
    private SqliteDbContext? _ctx;

    public AuditLogService(CollectorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    public async Task InitializeAsync()
    {
        var dbPath = _settingsLoader.Settings.Storage.SQLite.DbPath;
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);

        var cfg = new RelationalDbConfig(DbProviderType.Sqlite, $"Data Source={dbPath}", commandTimeoutSec: 30);
        _ctx = new SqliteDbContext(cfg);
        await _ctx.OpenAsync();

        await _ctx.EnsureTableAsync("""
            CREATE TABLE IF NOT EXISTS audit_log (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                ts         TEXT    NOT NULL,
                action     TEXT    NOT NULL,
                target     TEXT    NOT NULL,
                detail     TEXT    NOT NULL,
                is_success INTEGER NOT NULL,
                actor      TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_audit_log_ts ON audit_log (ts);
            """);

        LogManager.Instance.Info("Audit", "감사 로그 서비스 초기화 완료");
    }

    /// <summary>
    /// 감사 로그 1건 기록. actor 는 현재 사용자 계정 체계가 없으므로 "Local" 고정
    /// (향후 Manager 에서 사용자 관리 도입 시 확장 지점).
    /// </summary>
    public async Task LogAsync(string action, string target, string detail, bool isSuccess, string actor = "Local")
    {
        if (_ctx is null) return;

        try
        {
            await _ctx.ExecuteAsync(
                "INSERT INTO audit_log (ts, action, target, detail, is_success, actor) VALUES (@ts, @action, @target, @detail, @ok, @actor)",
                CommandType.Text,
                [
                    DbParam.In("@ts",     DateTimeOffset.UtcNow.ToString("O")),
                    DbParam.In("@action", action),
                    DbParam.In("@target", target),
                    DbParam.In("@detail", detail),
                    DbParam.In("@ok",     isSuccess ? 1 : 0),
                    DbParam.In("@actor",  actor),
                ]);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("Audit", $"감사 로그 기록 실패: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ctx is not null) await _ctx.DisposeAsync();
    }
}
