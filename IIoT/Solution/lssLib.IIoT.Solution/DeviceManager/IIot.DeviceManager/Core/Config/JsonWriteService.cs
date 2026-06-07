// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/JsonWriteService.cs
//  역할: 설정 JSON 파일 원자적 저장 (.tmp → rename → .bak) + SHA-256
//  Phase 1: 초기 구현
//  Phase 6: ConfigWatcher 주입 → 저장 후 자동 신호 발행
//           SaveDeviceTree 완료 시 device.json.signal 생성
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.Core.DataModel;
using lssLib.Config;
using lssLib.Config.Tree;
using lssLib.Log;
using lssLib.Utils;
using System.IO;

namespace IIoT.DeviceManager.Core.Config;

/// <summary>
/// 장비 설정 JSON 파일 5종을 원자적으로 저장합니다.
/// Phase 6: ConfigWatcher 주입 시 저장 완료 후 자동으로 변경 신호를 발행합니다.
/// </summary>
public sealed class JsonWriteService
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc = "JsonWriteService";

    private readonly string _devicePath;
    private readonly string _scalePath;
    private readonly string _alarmPath;
    private readonly string _commPath;
    private readonly string _locationPath;

    // ★ Phase 6: ConfigWatcher (null 허용 — 미주입 시 신호 발행 없음)
    private readonly ConfigWatcher? _watcher;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    /// <summary>
    /// Phase 1 ~ 5 호환 생성자 (ConfigWatcher 없음).
    /// </summary>
    public JsonWriteService(string configDirectory)
        : this(configDirectory, null) { }

    /// <summary>
    /// Phase 6 생성자 — ConfigWatcher 주입으로 저장 후 자동 신호 발행.
    /// </summary>
    public JsonWriteService(string configDirectory, ConfigWatcher? watcher)
    {
        Guard.NotWhiteSpace(configDirectory);
        Directory.CreateDirectory(configDirectory);

        _devicePath   = Path.Combine(configDirectory, "device.json");
        _scalePath    = Path.Combine(configDirectory, "scale-library.json");
        _alarmPath    = Path.Combine(configDirectory, "alarm-library.json");
        _commPath     = Path.Combine(configDirectory, "comm-library.json");
        _locationPath = Path.Combine(configDirectory, "location-library.json");

        _watcher = watcher;
    }

    // §3 ─ 저장 메서드 ────────────────────────────────────────

    /// <summary>
    /// ConfigTree(장비 트리)를 device.json 에 원자적으로 저장합니다.
    /// ★ Phase 6: 저장 완료 후 ConfigWatcher 를 통해 변경 신호 발행.
    /// </summary>
    public void SaveDeviceTree(ConfigTree tree, string reason = "manual-save")
    {
        Guard.NotNull(tree);
        string json = tree.ToJson();
        _AtomicWrite(_devicePath, json);

        // ★ Phase 6: 신호 발행
        _watcher?.NotifyDeviceConfigChanged(reason);

        LogManager.Instance.Info(LogSrc,
            $"device.json 저장 완료 ({json.Length:N0} bytes)");
    }

    /// <summary>ScaleConfig 컬렉션을 scale-library.json 에 저장합니다.</summary>
    public void SaveScaleLibrary(IEnumerable<ScaleConfig> scales)
    {
        _SaveLibrary(_scalePath, "scale-library.json",
                     scales, sc => (sc.SectionKey, sc.ToConfigEntries()));
        _watcher?.NotifyLibraryChanged("scale-library.json");
    }

    /// <summary>AlarmRule 컬렉션을 alarm-library.json 에 저장합니다.</summary>
    public void SaveAlarmLibrary(IEnumerable<AlarmRule> rules)
    {
        _SaveLibrary(_alarmPath, "alarm-library.json",
                     rules, ar => (ar.SectionKey, ar.ToConfigEntries()));
        _watcher?.NotifyLibraryChanged("alarm-library.json");
    }

    /// <summary>CommConfig 컬렉션을 comm-library.json 에 저장합니다.</summary>
    public void SaveCommLibrary(IEnumerable<CommConfig> configs)
    {
        _SaveLibrary(_commPath, "comm-library.json",
                     configs, cc => (cc.SectionKey, cc.ToConfigEntries()));
        _watcher?.NotifyLibraryChanged("comm-library.json");
    }

    /// <summary>Location 컬렉션을 location-library.json 에 저장합니다.</summary>
    public void SaveLocationLibrary(IEnumerable<Location> locations)
        => _SaveLibrary(_locationPath, "location-library.json",
                        locations, loc => (loc.SectionKey, loc.ToConfigEntries()));

    // §4 ─ 경로 프로퍼티 ──────────────────────────────────────
    public string DevicePath   => _devicePath;
    public string ScalePath    => _scalePath;
    public string AlarmPath    => _alarmPath;
    public string CommPath     => _commPath;
    public string LocationPath => _locationPath;

    // §5 ─ 내부 공통 헬퍼 ─────────────────────────────────────

    private void _SaveLibrary<T>(
        string targetPath, string logName,
        IEnumerable<T> items,
        Func<T, (string section, Dictionary<string, string> entries)> selector)
    {
        var cfg = ConfigManager.CreateNew();
        cfg.Set("Meta", "Version",   "1.0");
        cfg.Set("Meta", "UpdatedAt", DateTime.UtcNow.ToString("o"));

        foreach (var item in items)
        {
            var (section, entries) = selector(item);
            foreach (var (key, val) in entries)
                cfg.Set(section, key, val);
        }

        string tmpPath = targetPath + ".tmp";
        cfg.Save(tmpPath, ConfigFormat.Json);
        _AtomicWriteFromTmp(tmpPath, targetPath);

        LogManager.Instance.Info(LogSrc, $"{logName} 저장 완료");
    }

    private static void _AtomicWrite(string targetPath, string json)
    {
        string tmpPath = targetPath + ".tmp";
        string bakPath = targetPath + ".bak";

        File.WriteAllText(tmpPath, json, System.Text.Encoding.UTF8);

        if (File.Exists(targetPath))
            File.Replace(tmpPath, targetPath, bakPath);
        else
            File.Move(tmpPath, targetPath);

        IntegrityGuard.SaveHash(json, targetPath);
    }

    private static void _AtomicWriteFromTmp(string tmpPath, string targetPath)
    {
        if (!File.Exists(tmpPath)) return;

        string bakPath = targetPath + ".bak";
        string json    = File.ReadAllText(tmpPath, System.Text.Encoding.UTF8);

        if (File.Exists(targetPath))
            File.Replace(tmpPath, targetPath, bakPath);
        else
            File.Move(tmpPath, targetPath);

        IntegrityGuard.SaveHash(json, targetPath);
    }
}
