// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/DbBackupService.cs
//  역할: SQLite DB 파일을 매일 1회 Backup 폴더로 복사, 오래된 백업 자동 정리
//  C-EX-06: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using lssLib.Log;
using lssLib.Messaging;
using System.IO;
using System.Linq;

namespace IIoT.Collector.Storage;

public sealed class DbBackupService : IDisposable
{
    private readonly CollectorSettingsLoader _settingsLoader;
    private ScheduledTask? _task;

    public DbBackupService(CollectorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    public void Initialize()
    {
        var s = _settingsLoader.Settings.Backup;
        if (!s.Enabled)
        {
            LogManager.Instance.Info("Backup", "DB 자동 백업 비활성화 (설정)");
            return;
        }

        if (!_settingsLoader.Settings.Storage.Provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            LogManager.Instance.Info("Backup", "Provider=InfluxDB — SQLite 전용 백업 비활성화");
            return;
        }

        _task = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromHours(24), _BackupAsync, name: "backup:daily");

        LogManager.Instance.Info("Backup", $"DB 자동 백업 활성화 — 최대 {s.MaxBackupCount}개 보관");
    }

    private Task _BackupAsync(CancellationToken ct)
    {
        try
        {
            var storage = _settingsLoader.Settings.Storage;
            var backup  = _settingsLoader.Settings.Backup;

            var dbPath = storage.SQLite.DbPath;
            if (!Path.IsPathRooted(dbPath))
                dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);

            if (!File.Exists(dbPath))
            {
                LogManager.Instance.Warn("Backup", $"백업 대상 DB 파일 없음: {dbPath}");
                return Task.CompletedTask;
            }

            var backupDir = backup.BackupDir;
            if (!Path.IsPathRooted(backupDir))
                backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, backupDir);
            Directory.CreateDirectory(backupDir);

            var fileName   = $"collector_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.db";
            var backupPath = Path.Combine(backupDir, fileName);

            // SQLite 파일을 사용 중에도 복사 가능하도록 File.Copy 대신 스트림 복사
            using (var src = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var dst = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write))
            {
                src.CopyTo(dst);
            }

            LogManager.Instance.Info("Backup", $"DB 백업 완료 → {backupPath}");

            _CleanupOldBackups(backupDir, backup.MaxBackupCount);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("Backup", $"DB 백업 실패: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private static void _CleanupOldBackups(string dir, int maxCount)
    {
        var files = Directory.GetFiles(dir, "collector_*.db")
            .OrderByDescending(f => f)
            .ToList();

        foreach (var old in files.Skip(maxCount))
        {
            try { File.Delete(old); }
            catch (Exception ex) { LogManager.Instance.Warn("Backup", $"오래된 백업 삭제 실패: {ex.Message}"); }
        }
    }

    public void Dispose() => _task?.Cancel();
}
