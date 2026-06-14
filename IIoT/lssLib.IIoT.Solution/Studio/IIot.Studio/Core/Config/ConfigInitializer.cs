// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/ConfigInitializer.cs
//  역할: 프로그램 시작 시 JSON 설정 파일 초기화
//  V3 Step3: ResourcePrefix 변경
//    이전: "IIoT.DeviceManager.Config." 또는 "IIoT.ConfigApp.Config."
//    현재: "IIoT.Studio.Config."
//    이유: AssemblyName/RootNamespace가 IIoT.Studio로 변경됨
//         GetManifestResourceStream() 이름이 네임스페이스 기반
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
    private const string LogSource = "ConfigInitializer";

    /// <summary>
    /// ★ V3 Step3 수정: IIoT.Studio 로 변경
    /// 임베디드 리소스 이름 접두사 = RootNamespace + 폴더 경로
    /// </summary>
    private const string ResourcePrefix = "IIoT.Studio.Config.";

    // §2 ─ 파일 정의 ──────────────────────────────────────────
    private static readonly IReadOnlyList<ConfigFileSpec> _Specs =
    [
        new("device.json",           DefaultContent: _DeviceDefault()),
        new("scale-library.json",    DefaultContent: _LibraryDefault()),
        new("alarm-library.json",    DefaultContent: _LibraryDefault()),
        new("comm-library.json",     DefaultContent: _LibraryDefault()),
        new("location-library.json", DefaultContent: _LibraryDefault()),
        new("collect.json",          DefaultContent: _CollectDefault()),
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
        string jsonPath = Path.Combine(configDir, spec.FileName);

        if (File.Exists(jsonPath))
        {
            LogManager.Instance.Debug(LogSource, $"[OK] {spec.FileName} — 기존 파일 사용");
            return;
        }

        string? sampleContent = _ReadEmbeddedSample(spec.FileName + ".sample");

        if (sampleContent != null)
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
        var asm = Assembly.GetExecutingAssembly();
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
}

// §6 ─ 내부 레코드 ────────────────────────────────────────
file record ConfigFileSpec(string FileName, string DefaultContent);
