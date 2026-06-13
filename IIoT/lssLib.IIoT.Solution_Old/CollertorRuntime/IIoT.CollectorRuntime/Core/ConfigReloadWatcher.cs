// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Core/ConfigReloadWatcher.cs
//  역할: DeviceManager 가 생성한 .signal 파일 감지 → 재시작 신호
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using System.IO;
using System.Text.Json;

namespace IIoT.CollectorRuntime.Core;

public sealed class ConfigReloadWatcher : IDisposable
{
    private const string LogSrc = "ConfigReloadWatcher";
    private readonly string _configDir;
    private FileSystemWatcher? _fsw;
    private bool _disposed;

    public event Action<string>? ReloadRequested;

    public ConfigReloadWatcher(string configDir) => _configDir = configDir;

    public void Start()
    {
        if (!Directory.Exists(_configDir))
            Directory.CreateDirectory(_configDir);

        // 시작 시 미처리 신호 처리
        foreach (var f in Directory.GetFiles(_configDir, "*.signal"))
            _Process(f);

        _fsw = new FileSystemWatcher(_configDir, "*.signal")
        {
            NotifyFilter        = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _fsw.Created += (_, e) => { Thread.Sleep(100); _Process(e.FullPath); };
        _fsw.Renamed += (_, e) => { Thread.Sleep(100); _Process(e.FullPath); };

        LogManager.Instance.Info(LogSrc, $"설정 변경 감시 시작 → {_configDir}");
    }

    public void Stop()
    {
        if (_fsw is not null) _fsw.EnableRaisingEvents = false;
    }

    private void _Process(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            string reason = "file-change";
            try
            {
                var doc  = JsonDocument.Parse(File.ReadAllText(path));
                reason   = doc.RootElement.TryGetProperty("reason", out var r)
                           ? r.GetString() ?? reason : reason;
            }
            catch { }

            try { File.Delete(path); } catch { }

            LogManager.Instance.Info(LogSrc, $"설정 변경 신호 수신 (사유: {reason})");
            ReloadRequested?.Invoke(reason);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn(LogSrc, $"신호 처리 오류: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _fsw?.Dispose();
        _disposed = true;
    }
}
