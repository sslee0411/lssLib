// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/ConfigInitializer.cs
//  역할: 프로그램 시작 시 JSON 설정 파일 초기화
//  Fix CS9051: file record → private sealed record
//              ('file' 한정자 타입을 public static 멤버에 사용 불가)
// ══════════════════════════════════════════════════════════

using System.IO;
using System.Reflection;
using System.Text;
using lssLib.Log;
using lssLib.Utils;

namespace IIoT.Studio.Core.Config;

public static class ConfigInitializer
{
    // §1 ─ 상수 ───────────────────────────────────────────────
    private const string LogSource      = "ConfigInitializer";
    private const string ResourcePrefix = "IIoT.Studio.Config.";

    // §2 ─ 파일 정의 ──────────────────────────────────────────
    // ★ Fix CS9051: 'file record' → 'private sealed record'
    //   file 한정자 타입은 파일 내부 전용이지만,
    //   static readonly 필드(public class 멤버)의 타입으로 사용하면 CS9051 발생.
    //   → private sealed record 로 변경
    private static readonly IReadOnlyList<ConfigFileSpec> _Specs =
    [
        new("device.json",           _DeviceDefault()),
        new("scale-library.json",    _LibraryDefault()),
        new("alarm-library.json",    _LibraryDefault()),
        new("comm-library.json",     _LibraryDefault()),
        new("location-library.json", _LibraryDefault()),
        new("collect.json",          _CollectDefault()),
    ];

    // §3 ─ 공개 메서드 ────────────────────────────────────────
    public static void EnsureConfigFiles(string configDirectory)
    {
        Guard.NotWhiteSpace(configDirectory);
        Directory.CreateDirectory(configDirectory);
        LogManager.Instance.Info(LogSource, $"설정 파일 초기화 → {configDirectory}");

        foreach (var spec in _Specs)
            _EnsureFile(configDirectory, spec);

        LogManager.Instance.Info(LogSource, "설정 파일 초기화 완료");
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────
    private static void _EnsureFile(string configDir, ConfigFileSpec spec)
    {
        var jsonPath = Path.Combine(configDir, spec.FileName);

        if (File.Exists(jsonPath))
        {
            LogManager.Instance.Debug(LogSource, $"[OK] {spec.FileName} — 기존 파일 사용");
            return;
        }

        var sampleContent = _ReadEmbeddedSample(spec.FileName + ".sample");

        if (sampleContent is not null)
        {
            File.WriteAllText(jsonPath, sampleContent, Encoding.UTF8);
            LogManager.Instance.Info(LogSource, $"[INIT] {spec.FileName} — 내장 sample 생성");
            return;
        }

        File.WriteAllText(jsonPath, spec.DefaultContent, Encoding.UTF8);
        LogManager.Instance.Warn(LogSource, $"[NEW] {spec.FileName} — 기본값으로 생성");
    }

    private static string? _ReadEmbeddedSample(string sampleFileName)
    {
        var asm          = Assembly.GetExecutingAssembly();
        var resourceName = ResourcePrefix + sampleFileName;
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // §5 ─ 기본값 JSON ────────────────────────────────────────
    private static string _DeviceDefault() => """
        {
          "id": "root",
          "name": "Root",
          "type": "Root",
          "properties": {},
          "children": []
        }
        """;

    private static string _LibraryDefault() => """
        {
          "Meta": { "Version": "1.0", "UpdatedAt": "2025-01-01T00:00:00Z" }
        }
        """;

    private static string _CollectDefault() => """
        {
          "nodes": [],
          "edges": []
        }
        """;

    // §6 ─ ★ Fix CS9051: file → private sealed ────────────────
    private sealed record ConfigFileSpec(string FileName, string DefaultContent);
}
