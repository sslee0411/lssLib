// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/JsonServices.cs
//  역할: JSON 설정 파일 저장(JsonWriteService) + 로딩(JsonConfigLoader)
//  Fix CS8954: 파일 내 namespace 2개 → 단일 namespace 로 통합
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Utils;
using System.IO;
using System.Text;
using System.Text.Json;

namespace IIoT.Studio.Core.Config;   // ★ 파일 전체에 namespace 1개만 허용

// ══════════════════════════════════════════════════════════
//  JsonWriteService
// ══════════════════════════════════════════════════════════

/// <summary>
/// JSON 설정 파일 원자적 저장 서비스.
/// .tmp → File.Replace → .bak 패턴으로 데이터 손실을 방지합니다.
/// </summary>
public sealed class JsonWriteService
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private const string LogSrc = "JsonWriteService";

    private readonly string _configDir;

    private static readonly JsonSerializerOptions _JsonOpts = new()
    {
        WriteIndented = true,
        Encoder       = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public JsonWriteService(string configDirectory)
    {
        Guard.NotWhiteSpace(configDirectory);
        _configDir = configDirectory;
        Directory.CreateDirectory(configDirectory);
    }

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>객체를 JSON으로 직렬화하여 원자적으로 저장합니다.</summary>
    public void Save<T>(string fileName, T data)
    {
        Guard.NotWhiteSpace(fileName);
        var json = JsonSerializer.Serialize(data, _JsonOpts);
        _AtomicWrite(Path.Combine(_configDir, fileName), json);
        LogManager.Instance.Info(LogSrc, $"저장 완료: {fileName}");
    }

    /// <summary>JSON 문자열을 원자적으로 저장합니다.</summary>
    public void SaveRaw(string fileName, string json)
    {
        Guard.NotWhiteSpace(fileName);
        Guard.NotWhiteSpace(json);
        _AtomicWrite(Path.Combine(_configDir, fileName), json);
        LogManager.Instance.Info(LogSrc, $"저장 완료(raw): {fileName}");
    }

    /// <summary>.signal 파일을 생성하여 Collector 재시작을 트리거합니다.</summary>
    public void WriteSignal(string sourceFile, string reason = "save")
    {
        var signalPath = Path.Combine(_configDir, $"{sourceFile}.signal");
        File.WriteAllText(signalPath,
            $"{{\"source\":\"{sourceFile}\",\"reason\":\"{reason}\"," +
            $"\"at\":\"{DateTime.UtcNow:O}\"}}",
            Encoding.UTF8);
        LogManager.Instance.Info(LogSrc, $".signal 생성: {Path.GetFileName(signalPath)}");
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────
    private static void _AtomicWrite(string path, string content)
    {
        var tmp = path + ".tmp";
        var bak = path + ".bak";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        if (File.Exists(path))
            File.Replace(tmp, path, bak);
        else
            File.Move(tmp, path);
    }
}

// ══════════════════════════════════════════════════════════
//  JsonConfigLoader
// ══════════════════════════════════════════════════════════

/// <summary>
/// JSON 설정 파일 로딩 서비스.
/// ConfigBundle.Loader 에 등록되어 StudioMainViewModel에서 사용합니다.
/// </summary>
public sealed class JsonConfigLoader
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private const string LogSrc = "JsonConfigLoader";

    private readonly string _configDir;

    private static readonly JsonSerializerOptions _JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public JsonConfigLoader(string configDirectory)
    {
        Guard.NotWhiteSpace(configDirectory);
        _configDir = configDirectory;
    }

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>모든 설정 파일을 로드하여 ConfigData 묶음으로 반환합니다.</summary>
    public ConfigData LoadAll()
    {
        return new ConfigData
        {
            Scales      = _LoadJson<object?>("scale-library.json"),
            AlarmRules  = _LoadJson<object?>("alarm-library.json"),
            CommConfigs = _LoadJson<object?>("comm-library.json"),
        };
    }

    /// <summary>단일 JSON 파일을 T로 역직렬화합니다.</summary>
    public T? Load<T>(string fileName) => _LoadJson<T>(fileName);

    // §4 ─ 내부 메서드 ────────────────────────────────────────
    private T? _LoadJson<T>(string fileName)
    {
        var path = Path.Combine(_configDir, fileName);
        if (!File.Exists(path))
        {
            LogManager.Instance.Debug(LogSrc, $"파일 없음: {fileName}");
            return default;
        }
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(json, _JsonOpts);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn(LogSrc, $"로드 실패 {fileName}: {ex.Message}");
            return default;
        }
    }
}

// ══════════════════════════════════════════════════════════
//  ConfigData (LoadAll 반환 묶음)
// ══════════════════════════════════════════════════════════

/// <summary>SwitchTab에서 각 Library ViewModel에 전달하는 데이터 묶음</summary>
public sealed class ConfigData
{
    public object? Scales      { get; init; }
    public object? AlarmRules  { get; init; }
    public object? CommConfigs { get; init; }
}
