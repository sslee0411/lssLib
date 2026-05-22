// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/JsonConfigLoader.cs
//  역할: JSON 설정 파일 5종 로드 + 무결성 검증
//        ★ ConfigManager.Store.GetSections/GetKeys 미존재
//          → System.Text.Json.JsonDocument 직접 파싱으로 수정
//  Phase 1 Update: lssLib.Config API 정정
// ══════════════════════════════════════════════════════════

using System.Text.Json;
using lssLib.Config;
using lssLib.Config.Tree;
using lssLib.Config.Validation;
using lssLib.Log;
using lssLib.Utils;
using IIoT.DeviceManager.Core.DataModel;
using System.IO;

namespace IIoT.DeviceManager.Core.Config;

/// <summary>
/// 장비 설정 JSON 파일 5종을 로드하고 검증합니다.
/// <list type="bullet">
///   <item>device.json        — lssLib.Config.ConfigTree.FromJson() 로드</item>
///   <item>scale-library.json — JsonDocument 직접 파싱 (섹션 열거)</item>
///   <item>alarm-library.json — JsonDocument 직접 파싱</item>
///   <item>comm-library.json  — JsonDocument 직접 파싱</item>
///   <item>location-library.json — JsonDocument 직접 파싱</item>
/// </list>
/// </summary>
public sealed class JsonConfigLoader
{
    // §1 ─ 필드 ───────────────────────────────────────────────
    private const string LogSource = "JsonConfigLoader";

    private readonly string _devicePath;
    private readonly string _scalePath;
    private readonly string _alarmPath;
    private readonly string _commPath;
    private readonly string _locationPath;

    // §2 ─ JSON 옵션 ──────────────────────────────────────────
    private static readonly JsonDocumentOptions _JsonOpt = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // §3 ─ 생성자 ─────────────────────────────────────────────
    public JsonConfigLoader(string configDirectory)
    {
        Guard.NotWhiteSpace(configDirectory);

        _devicePath = Path.Combine(configDirectory, "device.json");
        _scalePath = Path.Combine(configDirectory, "scale-library.json");
        _alarmPath = Path.Combine(configDirectory, "alarm-library.json");
        _commPath = Path.Combine(configDirectory, "comm-library.json");
        _locationPath = Path.Combine(configDirectory, "location-library.json");
    }

    // §4 ─ device.json (ConfigTree) ───────────────────────────

    /// <summary>
    /// device.json 을 ConfigTree 로 로드합니다.
    /// 파일이 없으면 빈 ConfigTree 를 반환합니다 (신규 프로젝트).
    /// </summary>
    public (ConfigTree Tree, bool IsIntegrityOk) LoadDeviceTree()
    {
        var tree = new ConfigTree();

        if (!File.Exists(_devicePath))
        {
            LogManager.Instance.Info(LogSource, "device.json 없음 — 빈 트리로 시작");
            return (tree, true);
        }

        string json = File.ReadAllText(_devicePath, System.Text.Encoding.UTF8);
        bool integrityOk = IntegrityGuard.Verify(json, _devicePath);

        if (!integrityOk)
            LogManager.Instance.Warn(LogSource,
                "device.json 무결성 경고 — 계속 로드합니다.");

        try
        {
            // lssLib.Config.ConfigTree.FromJson()
            tree.FromJson(json);
            LogManager.Instance.Info(LogSource,
                $"device.json 로드 완료 — 노드 {tree.Flatten().Count()}개");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error(LogSource,
                $"device.json 파싱 오류: {ex.Message} → 빈 트리로 대체");
            tree = new ConfigTree();
        }

        return (tree, integrityOk);
    }

    // §5 ─ 라이브러리 로드 ────────────────────────────────────

    /// <summary>scale-library.json 에서 ScaleConfig 목록을 로드합니다.</summary>
    public List<ScaleConfig> LoadScaleLibrary()
        => _LoadLibrary(_scalePath, "scale-library.json", "ScaleLibrary:",
                        ScaleConfig.FromConfigEntries);

    /// <summary>alarm-library.json 에서 AlarmRule 목록을 로드합니다.</summary>
    public List<AlarmRule> LoadAlarmLibrary()
        => _LoadLibrary(_alarmPath, "alarm-library.json", "AlarmLibrary:",
                        AlarmRule.FromConfigEntries);

    /// <summary>comm-library.json 에서 CommConfig 목록을 로드합니다.</summary>
    public List<CommConfig> LoadCommLibrary()
        => _LoadLibrary(_commPath, "comm-library.json", "CommLibrary:",
                        CommConfig.FromConfigEntries);

    /// <summary>location-library.json 에서 Location 목록을 로드합니다.</summary>
    public List<Location> LoadLocationLibrary()
        => _LoadLibrary(_locationPath, "location-library.json", "LocationLibrary:",
                        Location.FromConfigEntries);

    // §6 ─ 전체 일괄 로드 ─────────────────────────────────────

    /// <summary>설정 파일 5종을 모두 로드하여 ConfigBundle 로 반환합니다.</summary>
    public ConfigBundle LoadAll()
    {
        var (tree, integrityOk) = LoadDeviceTree();
        return new ConfigBundle
        {
            DeviceTree = tree,
            IsIntegrityOk = integrityOk,
            Scales = LoadScaleLibrary(),
            AlarmRules = LoadAlarmLibrary(),
            CommConfigs = LoadCommLibrary(),
            Locations = LoadLocationLibrary(),
        };
    }

    // §7 ─ 경로 프로퍼티 ──────────────────────────────────────
    public string DevicePath => _devicePath;
    public string ScalePath => _scalePath;
    public string AlarmPath => _alarmPath;
    public string CommPath => _commPath;
    public string LocationPath => _locationPath;

    // §8 ─ 내부 공통 헬퍼 ─────────────────────────────────────

    /// <summary>
    /// JSON 라이브러리 파일을 JsonDocument 로 직접 파싱하여 항목 목록을 반환합니다.
    ///
    /// JSON 구조:
    /// {
    ///   "Meta":               { "Version": "1.0", ... },  ← 무시
    ///   "ScaleLibrary:sc-001": { "name": "...", ... },    ← 파싱
    ///   "ScaleLibrary:sc-002": { ... }
    /// }
    ///
    /// lssLib.Config.ConfigManager 를 사용하지 않고
    /// System.Text.Json.JsonDocument 로 직접 섹션/키 열거합니다.
    /// </summary>
    private List<T> _LoadLibrary<T>(
        string filePath,
        string logName,
        string sectionPrefix,
        Func<string, IReadOnlyDictionary<string, string>, T> factory)
    {
        var result = new List<T>();

        if (!File.Exists(filePath))
        {
            LogManager.Instance.Info(LogSource, $"{logName} 없음 — 빈 목록 반환");
            return result;
        }

        try
        {
            string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);

            // Meta 스키마 검증 — ConfigManager 로드 후 Validate
            _ValidateMeta(filePath, logName);

            // JsonDocument 로 섹션 직접 열거
            using var doc = JsonDocument.Parse(json, _JsonOpt);

            foreach (var sectionProp in doc.RootElement.EnumerateObject())
            {
                // sectionPrefix 로 시작하는 섹션만 처리 (예: "ScaleLibrary:")
                if (!sectionProp.Name.StartsWith(sectionPrefix,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                // ID 추출: "ScaleLibrary:sc-001" → "sc-001"
                string id = sectionProp.Name[sectionPrefix.Length..];

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                // 섹션 내 key-value 딕셔너리 빌드
                var entries = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

                if (sectionProp.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var kvProp in sectionProp.Value.EnumerateObject())
                    {
                        // 값이 string 이면 그대로, 숫자/bool 이면 ToString
                        string val = kvProp.Value.ValueKind switch
                        {
                            JsonValueKind.String => kvProp.Value.GetString() ?? string.Empty,
                            JsonValueKind.Number => kvProp.Value.GetRawText(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null => string.Empty,
                            _ => kvProp.Value.GetRawText(),
                        };

                        entries[kvProp.Name] = val;
                    }
                }

                result.Add(factory(id, entries));
            }

            LogManager.Instance.Info(LogSource,
                $"{logName} 로드 완료 — {result.Count}개");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error(LogSource,
                $"{logName} 로드 오류: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// ConfigManager 를 통해 Meta 섹션 필수 항목을 검증합니다.
    /// (ConfigManager.Get() 은 README 공식 API 이므로 사용 가능)
    /// </summary>
    private static void _ValidateMeta(string filePath, string logName)
    {
        try
        {
            var cfg = ConfigManager.CreateNew();
            cfg.Load(filePath, ConfigFormat.Json);

            var schema = new ConfigSchema()
                .Require("Meta", "Version", ConfigValueType.String)
                .Require("Meta", "UpdatedAt", ConfigValueType.String);

            var result = cfg.Validate(schema, applyDefaults: false);

            if (!result.IsValid)
                LogManager.Instance.Warn(LogSource,
                    $"{logName} Meta 검증 경고: {string.Join(", ", result.Errors)}");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn(LogSource,
                $"{logName} Meta 검증 실패 (계속 진행): {ex.Message}");
        }
    }
}