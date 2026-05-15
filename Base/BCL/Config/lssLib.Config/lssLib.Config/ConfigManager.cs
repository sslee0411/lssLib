// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · ConfigManager.cs  (v2)
//  역할: 설정 파일 관리 싱글톤 — INI/JSON/XML R/W, AES-256-GCM 암호화,
//        FileWatcher, Transaction/Undo/Redo, Validate, Profile, Log 연동
// ══════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using lssLib.Config.Encryption;
using lssLib.Config.Transaction;
using lssLib.Config.Validation;
using lssLib.Config.Watcher;

namespace lssLib.Config;

/// <summary>
/// 설정 파일 관리 싱글톤 (v2).
/// </summary>
public sealed class ConfigManager
{
    #region §1 ─ 싱글톤

    private static readonly Lazy<ConfigManager> _instance =
        new(() => new ConfigManager(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>스레드 안전 싱글톤 인스턴스.</summary>
    public static ConfigManager Instance => _instance.Value;

    private ConfigManager() { }

    /// <summary>싱글톤과 독립된 새 인스턴스를 생성합니다 (테스트·데모용).</summary>
    public static ConfigManager CreateNew() => new();

    #endregion

    #region §2 ─ 필드

    private readonly ConfigStore _store = new();
    private readonly ConfigFileWatcher _watcher = new();
    private readonly UndoRedoStack _undoRedo = new(maxDepth: 50);

    private string? _strLastFilePath;
    private ConfigFormat _lastFormat = ConfigFormat.Json;

    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #endregion

    #region §3 ─ 이벤트

    /// <summary>감시 파일이 변경·재로드될 때 발생합니다. (백그라운드 스레드)</summary>
    public event Action<string, ConfigStore>? ConfigChanged;

    /// <summary>트랜잭션이 커밋될 때 발생합니다.</summary>
    public event Action<IReadOnlyList<ChangeRecord>>? TransactionCommitted;

    #endregion

    #region §4 ─ 암호화

    /// <summary>암호화/복호화 패스워드를 설정합니다.</summary>
    public void SetPassword(string password) => ConfigEncryptor.SetPassword(password);

    /// <summary>원시 32바이트 AES-256 키를 직접 설정합니다.</summary>
    public void SetKey(byte[] key) => ConfigEncryptor.SetKey(key);

    #endregion

    #region §5 ─ 파일 로드

    /// <summary>
    /// 설정 파일을 로드합니다. 기존 저장소에 병합(덮어쓰기)됩니다.
    /// </summary>
    /// <param name="filePath">설정 파일 경로.</param>
    /// <param name="format">파일 형식. null 이면 확장자 자동 감지.</param>
    /// <param name="optional">true 이면 파일 없을 때 예외 없이 무시합니다.</param>
    public void Load(string filePath, ConfigFormat? format = null, bool optional = false)
    {
        if (!File.Exists(filePath))
        {
            if (optional) return;
            throw new FileNotFoundException("설정 파일을 찾을 수 없습니다.", filePath);
        }
        var fmt = format ?? DetectFormat(filePath);
        var raw = File.ReadAllText(filePath, Encoding.UTF8);
        var loaded = ParseByFormat(raw, fmt);
        _store.Merge(loaded);
        _strLastFilePath = Path.GetFullPath(filePath);
        _lastFormat = fmt;
    }

    /// <summary>비동기 파일 로드.</summary>
    public async Task LoadAsync(string filePath, ConfigFormat? format = null,
        bool optional = false, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            if (optional) return;
            throw new FileNotFoundException("설정 파일을 찾을 수 없습니다.", filePath);
        }
        var fmt = format ?? DetectFormat(filePath);
        var raw = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        var loaded = ParseByFormat(raw, fmt);
        _store.Merge(loaded);
        _strLastFilePath = Path.GetFullPath(filePath);
        _lastFormat = fmt;
    }

    #endregion

    #region §6 ─ 파일 저장

    /// <summary>현재 설정을 파일로 저장합니다.</summary>
    public void Save(string? filePath = null, ConfigFormat? format = null)
    {
        var path = filePath ?? _strLastFilePath
                   ?? throw new InvalidOperationException("저장 경로가 지정되지 않았습니다.");
        var fmt = format ?? _lastFormat;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory());
        File.WriteAllText(path, SerializeByFormat(_store, fmt), Encoding.UTF8);
    }

    /// <summary>비동기 저장.</summary>
    public async Task SaveAsync(string? filePath = null, ConfigFormat? format = null,
        CancellationToken ct = default)
    {
        var path = filePath ?? _strLastFilePath
                   ?? throw new InvalidOperationException("저장 경로가 지정되지 않았습니다.");
        var fmt = format ?? _lastFormat;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory());
        await File.WriteAllTextAsync(path, SerializeByFormat(_store, fmt), Encoding.UTF8, ct);
    }

    #endregion

    #region §7 ─ 값 읽기 / 쓰기

    /// <summary>설정 값 반환. 없으면 null.</summary>
    public string? Get(string section, string key) => _store.Get(section, key);

    /// <summary>설정 값 반환. 없으면 fallback.</summary>
    public string GetOr(string section, string key, string fallback) =>
        _store.GetOr(section, key, fallback);

    /// <summary>int 변환 반환. 실패 시 fallback.</summary>
    public int GetInt(string section, string key, int fallback = 0) =>
        int.TryParse(_store.Get(section, key), out var v) ? v : fallback;

    /// <summary>double 변환 반환. 실패 시 fallback.</summary>
    public double GetDouble(string section, string key, double fallback = 0.0) =>
        double.TryParse(_store.Get(section, key),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    /// <summary>bool 변환. "true"/"1"/"yes"/"on" → true.</summary>
    public bool GetBool(string section, string key, bool fallback = false)
    {
        var raw = _store.Get(section, key);
        if (raw is null) return fallback;
        return raw.ToUpperInvariant() is "TRUE" or "1" or "YES" or "ON";
    }

    /// <summary>값을 씁니다 (즉시 반영, Undo 지원 안 됨 — 트랜잭션 권장).</summary>
    public void Set(string section, string key, string value, bool isEncrypted = false) =>
        _store.Set(section, key, value, isEncrypted);

    /// <summary>키 존재 여부.</summary>
    public bool Contains(string section, string key) => _store.Contains(section, key);

    /// <summary>키 제거.</summary>
    public bool Remove(string section, string key) => _store.Remove(section, key);

    /// <summary>전체 초기화 (Undo 스택도 초기화).</summary>
    public void Clear() { _store.Clear(); _undoRedo.Clear(); }

    /// <summary>내부 ConfigStore 직접 접근.</summary>
    public ConfigStore Store => _store;

    #endregion

    #region §8 ─ 트랜잭션 / Undo / Redo

    /// <summary>
    /// 설정 변경 트랜잭션을 시작합니다.
    /// </summary>
    /// <returns>ConfigTransaction — using 블록 이탈 시 커밋 없으면 자동 롤백.</returns>
    /// <example><code>
    /// using var tx = ConfigManager.Instance.BeginTransaction();
    /// tx.Set("Network", "Host", "10.0.0.1");
    /// tx.Set("Network", "Port", "1502");
    /// tx.Commit();
    /// </code></example>
    public ConfigTransaction BeginTransaction() =>
        new(_store, changes =>
        {
            _undoRedo.Push(changes);
            TransactionCommitted?.Invoke(changes);
        });

    /// <summary>마지막 커밋을 되돌립니다. Undo 불가면 null.</summary>
    public IReadOnlyList<ChangeRecord>? Undo() => _undoRedo.Undo(_store);

    /// <summary>마지막 Undo 를 다시 적용합니다. Redo 불가면 null.</summary>
    public IReadOnlyList<ChangeRecord>? Redo() => _undoRedo.Redo(_store);

    /// <summary>Undo 가능 여부.</summary>
    public bool CanUndo => _undoRedo.CanUndo;

    /// <summary>Redo 가능 여부.</summary>
    public bool CanRedo => _undoRedo.CanRedo;

    /// <summary>Undo 스택 깊이.</summary>
    public int UndoDepth => _undoRedo.UndoDepth;

    #endregion

    #region §9 ─ 검증 (Validation)

    /// <summary>
    /// 현재 저장소를 ConfigSchema 규칙으로 검증합니다.
    /// </summary>
    /// <example><code>
    /// var schema = new ConfigSchema()
    ///     .Require("Network", "Host", ConfigValueType.IpAddress)
    ///     .Require("Network", "Port", ConfigValueType.Port)
    ///     .Optional("App",    "Debug", ConfigValueType.Bool, defaultValue: "false");
    ///
    /// ConfigManager.Instance.Validate(schema).ThrowIfInvalid();
    /// </code></example>
    public ValidationResult Validate(ConfigSchema schema, bool applyDefaults = true) =>
        ConfigValidator.Validate(_store, schema, applyDefaults);

    /// <summary>검증 실패 시 ConfigValidationException 을 throw 합니다.</summary>
    public void ValidateOrThrow(ConfigSchema schema) =>
        ConfigValidator.Validate(_store, schema).ThrowIfInvalid();

    #endregion

    #region §10 ─ FileWatcher

    /// <summary>마지막 로드 파일 변경 감지를 시작합니다.</summary>
    public void StartWatch()
    {
        if (_strLastFilePath is null)
            throw new InvalidOperationException("감시할 파일이 없습니다. Load() 를 먼저 호출하세요.");
        StartWatch(_strLastFilePath);
    }

    /// <summary>지정 파일 변경 감지를 시작합니다.</summary>
    public void StartWatch(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        _watcher.Watch(fullPath);
        _watcher.FileChanged -= OnFileChanged;
        _watcher.FileChanged += OnFileChanged;
    }

    /// <summary>모든 감시를 중단합니다.</summary>
    public void StopWatch() { _watcher.FileChanged -= OnFileChanged; _watcher.Pause(); }

    /// <summary>지정 파일 감시를 중단합니다.</summary>
    public void StopWatch(string filePath) => _watcher.Unwatch(filePath);

    private void OnFileChanged(string fullPath)
    {
        try
        {
            var fmt = DetectFormat(fullPath);
            var raw = File.ReadAllText(fullPath, Encoding.UTF8);
            var loaded = ParseByFormat(raw, fmt);
            _store.Merge(loaded, overwrite: true);
            ConfigChanged?.Invoke(fullPath, _store);
        }
        catch { /* 파일 잠금 등 일시적 오류 무시 */ }
    }

    #endregion

    #region §11 ─ INI 파서 / 직렬화

    private ConfigStore ParseIni(string raw)
    {
        var store = new ConfigStore();
        var strCur = "Default";
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { strCur = line[1..^1].Trim(); continue; }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            bool enc = ConfigEncryptor.IsEncryptedValue(val);
            var plain = enc && ConfigEncryptor.HasKey ? ConfigEncryptor.FromStoredValue(val) : val;
            store.Set(strCur, key, plain, enc);
        }
        return store;
    }

    private string SerializeIni(ConfigStore store)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# lssLib.Config  generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        foreach (var section in store.GetSections())
        {
            sb.AppendLine().AppendLine($"[{section}]");
            foreach (var entry in store.GetSection(section))
            {
                var v = entry.IsEncrypted && ConfigEncryptor.HasKey
                    ? ConfigEncryptor.ToStoredValue(entry.Value) : entry.Value;
                sb.AppendLine($"{entry.Key} = {v}");
            }
        }
        return sb.ToString();
    }

    #endregion

    #region §12 ─ JSON 파서 / 직렬화

    private ConfigStore ParseJson(string raw)
    {
        var store = new ConfigStore();
        using var doc = JsonDocument.Parse(raw);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return store;
        foreach (var section in doc.RootElement.EnumerateObject())
        {
            if (section.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var kv in section.Value.EnumerateObject())
                {
                    string rawVal; bool isEnc;
                    if (kv.Value.ValueKind == JsonValueKind.Object
                        && kv.Value.TryGetProperty("value", out var valElem)
                        && kv.Value.TryGetProperty("encrypted", out var encElem))
                    { rawVal = valElem.GetString() ?? string.Empty; isEnc = encElem.GetBoolean(); }
                    else
                    {
                        rawVal = kv.Value.ValueKind == JsonValueKind.String
                            ? kv.Value.GetString() ?? string.Empty : kv.Value.GetRawText();
                        isEnc = ConfigEncryptor.IsEncryptedValue(rawVal);
                    }
                    var plain = isEnc && ConfigEncryptor.HasKey
                        ? ConfigEncryptor.FromStoredValue(rawVal) : rawVal;
                    store.Set(section.Name, kv.Name, plain, isEnc);
                }
            }
            else if (section.Value.ValueKind == JsonValueKind.String)
            {
                var rawVal = section.Value.GetString() ?? string.Empty;
                var isEnc = ConfigEncryptor.IsEncryptedValue(rawVal);
                var plain = isEnc && ConfigEncryptor.HasKey
                    ? ConfigEncryptor.FromStoredValue(rawVal) : rawVal;
                store.Set("Default", section.Name, plain, isEnc);
            }
        }
        return store;
    }

    private string SerializeJson(ConfigStore store)
    {
        var root = new Dictionary<string, object>();
        foreach (var section in store.GetSections())
        {
            var sec = new Dictionary<string, object>();
            foreach (var entry in store.GetSection(section))
            {
                if (entry.IsEncrypted && ConfigEncryptor.HasKey)
                    sec[entry.Key] = new { value = ConfigEncryptor.ToStoredValue(entry.Value), encrypted = true };
                else
                    sec[entry.Key] = entry.Value;
            }
            root[section] = sec;
        }
        return JsonSerializer.Serialize(root, _jsonOpts);
    }

    #endregion

    #region §13 ─ XML 파서 / 직렬화

    private ConfigStore ParseXml(string raw)
    {
        var store = new ConfigStore();
        var root = XDocument.Parse(raw).Root;
        if (root is null) return store;
        foreach (var secElem in root.Elements("Section"))
        {
            var strSec = secElem.Attribute("name")?.Value ?? "Default";
            foreach (var entry in secElem.Elements("Entry"))
            {
                var key = entry.Attribute("key")?.Value;
                var rawVal = entry.Attribute("value")?.Value ?? string.Empty;
                var isEnc = entry.Attribute("encrypted")?.Value == "true"
                             || ConfigEncryptor.IsEncryptedValue(rawVal);
                if (key is null) continue;
                var plain = isEnc && ConfigEncryptor.HasKey
                    ? ConfigEncryptor.FromStoredValue(rawVal) : rawVal;
                store.Set(strSec, key, plain, isEnc);
            }
        }
        return store;
    }

    private string SerializeXml(ConfigStore store)
    {
        var root = new XElement("Config",
            new XAttribute("generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        foreach (var section in store.GetSections())
        {
            var secElem = new XElement("Section", new XAttribute("name", section));
            foreach (var entry in store.GetSection(section))
            {
                var v = entry.IsEncrypted && ConfigEncryptor.HasKey
                    ? ConfigEncryptor.ToStoredValue(entry.Value) : entry.Value;
                secElem.Add(new XElement("Entry",
                    new XAttribute("key", entry.Key),
                    new XAttribute("value", v),
                    new XAttribute("encrypted", entry.IsEncrypted.ToString().ToLower())));
            }
            root.Add(secElem);
        }
        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).ToString();
    }

    #endregion

    #region §14 ─ 내부 헬퍼

    private ConfigStore ParseByFormat(string raw, ConfigFormat fmt) => fmt switch
    {
        ConfigFormat.Ini => ParseIni(raw),
        ConfigFormat.Json => ParseJson(raw),
        ConfigFormat.Xml => ParseXml(raw),
        _ => throw new NotSupportedException($"지원하지 않는 형식: {fmt}")
    };

    private string SerializeByFormat(ConfigStore store, ConfigFormat fmt) => fmt switch
    {
        ConfigFormat.Ini => SerializeIni(store),
        ConfigFormat.Json => SerializeJson(store),
        ConfigFormat.Xml => SerializeXml(store),
        _ => throw new NotSupportedException($"지원하지 않는 형식: {fmt}")
    };

    private static ConfigFormat DetectFormat(string filePath) =>
        Path.GetExtension(filePath).ToUpperInvariant() switch
        {
            ".INI" => ConfigFormat.Ini,
            ".JSON" => ConfigFormat.Json,
            ".XML" => ConfigFormat.Xml,
            var ext => throw new NotSupportedException(
                $"파일 형식을 자동 감지할 수 없습니다: '{ext}'. format 파라미터를 명시하세요.")
        };

    #endregion
}