// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/DataRetentionService.cs
//  역할: 매일 지정 시각에 RetentionDays 보다 오래된 tag_history 원본 삭제
//        (tag_agg_minute/hour/day 집계 테이블은 삭제하지 않음 — 장기 추세 보존)
//  C-EX-04: 신규
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

public sealed class DataRetentionService : IAsyncDisposable
{
    private readonly CollectorSettingsLoader _settingsLoader;
    private SqliteDbContext? _ctx;
    private ScheduledTask?   _task;

    public DataRetentionService(CollectorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    public async Task InitializeAsync()
    {
        var s = _settingsLoader.Settings.Retention;
        if (!s.Enabled)
        {
            LogManager.Instance.Info("Retention", "데이터 보존 정책 비활성화 (설정)");
            return;
        }

        if (!_settingsLoader.Settings.Storage.Provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            LogManager.Instance.Info("Retention", "Provider=InfluxDB — SQLite 전용 보존 정책 비활성화");
            return;
        }

        var dbPath = _settingsLoader.Settings.Storage.SQLite.DbPath;
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);

        var cfg = new RelationalDbConfig(DbProviderType.Sqlite, $"Data Source={dbPath}", commandTimeoutSec: 60);
        _ctx = new SqliteDbContext(cfg);
        await _ctx.OpenAsync();

        // 하루 1회 실행 (앱 시작 시점 기준 24시간 주기 — 정각 정렬은 C-17과 동일하게 미지원)
        _task = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromHours(24), _PurgeAsync, name: "retention:purge");

        LogManager.Instance.Info("Retention",
            $"데이터 보존 정책 활성화 — {s.RetentionDays}일 이전 tag_history 원본 자동 삭제");
    }

    private async Task _PurgeAsync(CancellationToken ct)
    {
        if (_ctx is null) return;

        var s        = _settingsLoader.Settings.Retention;
        var cutoffUtc = DateTimeOffset.UtcNow.AddDays(-s.RetentionDays);

        try
        {
            var result = await _ctx.ExecuteAsync(
                "DELETE FROM tag_history WHERE timestamp < @cutoff",
                CommandType.Text,
                [DbParam.In("@cutoff", cutoffUtc.ToString("O"))],
                ct);

            LogManager.Instance.Info("Retention",
                result.IsOk
                    ? $"보존 정책 정리 완료 — {s.RetentionDays}일 이전 데이터 삭제 (기준: {cutoffUtc:yyyy-MM-dd})"
                    : $"보존 정책 정리 실패: {result.Message}");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("Retention", $"보존 정책 정리 예외: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _task?.Cancel();
        if (_ctx is not null) await _ctx.DisposeAsync();
    }
}
