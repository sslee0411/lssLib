// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/JsonWriteService.cs
//  역할: 설정 JSON 파일 원자적 저장 (.tmp → rename → .bak) + SHA-256
//  Phase 1 Update: comm-library / location-library 저장 추가
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
/// <list type="bullet">
///   <item>device.json        — ConfigTree (장비 트리)</item>
///   <item>scale-library.json — ScaleConfig 목록</item>
///   <item>alarm-library.json — AlarmRule 목록</item>
///   <item>comm-library.json  — CommConfig 목록 (신규)</item>
///   <item>location-library.json — Location 목록 (신규)</item>
/// </list>
/// 저장 패턴: .tmp 기록 → File.Replace(원자적 교체, .bak 보존) → SHA-256 사이드카
/// </summary>
public sealed class JsonWriteService
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSource = "JsonWriteService";

    private readonly string _devicePath;
    private readonly string _scalePath;
    private readonly string _alarmPath;
    private readonly string _commPath;      // ← 신규
    private readonly string _locationPath;  // ← 신규

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public JsonWriteService(string configDirectory)
    {
        Guard.NotWhiteSpace(configDirectory);
        Directory.CreateDirectory(configDirectory);

        _devicePath = Path.Combine(configDirectory, "device.json");
        _scalePath = Path.Combine(configDirectory, "scale-library.json");
        _alarmPath = Path.Combine(configDirectory, "alarm-library.json");
        _commPath = Path.Combine(configDirectory, "comm-library.json");
        _locationPath = Path.Combine(configDirectory, "location-library.json");
    }

    // §3 ─ 저장 메서드 ────────────────────────────────────────

    /// <summary>ConfigTree(장비 트리)를 device.json 에 원자적으로 저장합니다.</summary>
    public void SaveDeviceTree(ConfigTree tree)
    {
        Guard.NotNull(tree);
        string json = tree.ToJson();
        _AtomicWrite(_devicePath, json);
        LogManager.Instance.Info(LogSource,
            $"device.json 저장 완료 ({json.Length:N0} bytes)");
    }

    /// <summary>ScaleConfig 컬렉션을 scale-library.json 에 저장합니다.</summary>
    public void SaveScaleLibrary(IEnumerable<ScaleConfig> scales)
        => _SaveLibrary(_scalePath, "scale-library.json",
                        scales, sc => (sc.SectionKey, sc.ToConfigEntries()));

    /// <summary>AlarmRule 컬렉션을 alarm-library.json 에 저장합니다.</summary>
    public void SaveAlarmLibrary(IEnumerable<AlarmRule> rules)
        => _SaveLibrary(_alarmPath, "alarm-library.json",
                        rules, ar => (ar.SectionKey, ar.ToConfigEntries()));

    /// <summary>CommConfig 컬렉션을 comm-library.json 에 저장합니다.</summary>
    public void SaveCommLibrary(IEnumerable<CommConfig> configs)
        => _SaveLibrary(_commPath, "comm-library.json",
                        configs, cc => (cc.SectionKey, cc.ToConfigEntries()));

    /// <summary>Location 컬렉션을 location-library.json 에 저장합니다.</summary>
    public void SaveLocationLibrary(IEnumerable<Location> locations)
        => _SaveLibrary(_locationPath, "location-library.json",
                        locations, loc => (loc.SectionKey, loc.ToConfigEntries()));

    // §4 ─ 경로 프로퍼티 ──────────────────────────────────────
    public string DevicePath => _devicePath;
    public string ScalePath => _scalePath;
    public string AlarmPath => _alarmPath;
    public string CommPath => _commPath;
    public string LocationPath => _locationPath;

    // §5 ─ 내부 공통 헬퍼 ─────────────────────────────────────

    /// <summary>라이브러리 컬렉션을 ConfigManager → JSON → 원자적 저장합니다.</summary>
    private void _SaveLibrary<T>(
        string targetPath,
        string logName,
        IEnumerable<T> items,
        Func<T, (string section, Dictionary<string, string> entries)> selector)
    {
        var cfg = ConfigManager.CreateNew();
        cfg.Set("Meta", "Version", "1.0");
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

        LogManager.Instance.Info(LogSource, $"{logName} 저장 완료");
    }

    /// <summary>JSON 문자열을 .tmp 에 쓰고 File.Replace 로 원자적 교체합니다.</summary>
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

    /// <summary>ConfigManager.Save() 가 이미 .tmp 에 저장한 경우 교체만 수행합니다.</summary>
    private static void _AtomicWriteFromTmp(string tmpPath, string targetPath)
    {
        if (!File.Exists(tmpPath)) return;

        string bakPath = targetPath + ".bak";
        string json = File.ReadAllText(tmpPath, System.Text.Encoding.UTF8);

        if (File.Exists(targetPath))
            File.Replace(tmpPath, targetPath, bakPath);
        else
            File.Move(tmpPath, targetPath);

        IntegrityGuard.SaveHash(json, targetPath);
    }
}