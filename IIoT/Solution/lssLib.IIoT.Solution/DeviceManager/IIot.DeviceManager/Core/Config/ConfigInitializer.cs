// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/ConfigInitializer.cs
//  역할: 프로그램 시작 시 JSON 설정 파일 초기화
//
//  처리 우선순위:
//   ① Config/*.json 존재 → 그대로 사용 (아무것도 안 함)
//   ② Config/*.json 없음 → 실행파일 내장 리소스에서 읽어 생성
//   ③ 내장 리소스도 없음 → 최소 기본값 JSON 자동 생성
//
//  Fix2: MSBuild Target / CopyToOutputDirectory 방식 제거
//        EmbeddedResource 방식으로 변경
//        → 출력폴더 파일 의존성 없음, .json 없을 때만 동작 보장
// ══════════════════════════════════════════════════════════

using System.IO;
using System.Reflection;
using System.Text;
using lssLib.Log;
using lssLib.Utils;

namespace IIoT.DeviceManager.Core.Config;

/// <summary>
/// 프로그램 시작 시 설정 JSON 파일 5종의 존재를 보장합니다.
///
/// .json.sample 파일은 실행파일 내부에 EmbeddedResource 로 내장되어 있으며,
/// Config/*.json 이 없을 때만 해당 리소스를 추출하여 파일을 생성합니다.
/// Config/*.json 이 이미 존재하면 절대 건드리지 않습니다.
/// </summary>
public static class ConfigInitializer
{
    // §1 ─ 상수 ───────────────────────────────────────────────
    private const string LogSource = "ConfigInitializer";

    /// <summary>
    /// 임베디드 리소스 이름 접두사.
    /// csproj RootNamespace + 폴더 경로
    /// → "IIoT.DeviceManager.Config."
    /// </summary>
    private const string ResourcePrefix = "IIoT.DeviceManager.Config.";

    // §2 ─ 파일 정의 ──────────────────────────────────────────
    private static readonly IReadOnlyList<ConfigFileSpec> _Specs =
    [
        new("device.json",
            DefaultContent: """
            {
              "id": "root",
              "name": "Root",
              "type": "Root",
              "properties": {},
              "children": []
            }
            """),

        new("scale-library.json",
            DefaultContent: """
            {
              "Meta": {
                "Version": "1.0",
                "UpdatedAt": "2025-01-01T00:00:00Z"
              }
            }
            """),

        new("alarm-library.json",
            DefaultContent: """
            {
              "Meta": {
                "Version": "1.0",
                "UpdatedAt": "2025-01-01T00:00:00Z"
              }
            }
            """),

        new("comm-library.json",
            DefaultContent: """
            {
              "Meta": {
                "Version": "1.0",
                "UpdatedAt": "2025-01-01T00:00:00Z"
              }
            }
            """),

        new("location-library.json",
            DefaultContent: """
            {
              "Meta": {
                "Version": "1.0",
                "UpdatedAt": "2025-01-01T00:00:00Z"
              }
            }
            """),
    ];

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// Config 디렉터리의 JSON 설정 파일 5종을 보장합니다.
    /// </summary>
    /// <param name="configDirectory">설정 파일 디렉터리 (예: [Exe]/Config)</param>
    public static void EnsureConfigFiles(string configDirectory)
    {
        Guard.NotWhiteSpace(configDirectory);
        Directory.CreateDirectory(configDirectory);

        LogManager.Instance.Info(LogSource,
            $"설정 파일 초기화 → {configDirectory}");

        foreach (var spec in _Specs)
            _EnsureFile(configDirectory, spec);

        LogManager.Instance.Info(LogSource, "설정 파일 초기화 완료");
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────

    private static void _EnsureFile(string configDir, ConfigFileSpec spec)
    {
        string jsonPath = Path.Combine(configDir, spec.FileName);

        // ① .json 파일이 이미 존재하면 절대 건드리지 않음
        if (File.Exists(jsonPath))
        {
            LogManager.Instance.Debug(LogSource,
                $"[OK] {spec.FileName} — 기존 파일 사용");
            return;
        }

        // ② 실행파일 내장 리소스에서 .json.sample 읽기
        string? sampleContent = _ReadEmbeddedSample(spec.FileName + ".sample");

        if (sampleContent != null)
        {
            string json = _NormalizeJson(sampleContent);
            File.WriteAllText(jsonPath, json, Encoding.UTF8);
            IntegrityGuard.SaveHash(json, jsonPath);

            LogManager.Instance.Info(LogSource,
                $"[INIT] {spec.FileName} — 내장 sample 에서 생성");
            return;
        }

        // ③ 내장 리소스도 없으면 최소 기본값으로 생성 (최후 수단)
        string defaultJson = _NormalizeJson(spec.DefaultContent);
        File.WriteAllText(jsonPath, defaultJson, Encoding.UTF8);
        IntegrityGuard.SaveHash(defaultJson, jsonPath);

        LogManager.Instance.Warn(LogSource,
            $"[NEW] {spec.FileName} — 내장 리소스 없음, 기본값으로 생성");
    }

    /// <summary>
    /// 실행파일에 내장된 .json.sample 리소스를 문자열로 읽습니다.
    ///
    /// 리소스 이름: "IIoT.DeviceManager.Config.device.json.sample"
    /// csproj:     &lt;EmbeddedResource Include="Config\*.json.sample" /&gt;
    /// </summary>
    private static string? _ReadEmbeddedSample(string sampleFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resName = ResourcePrefix + sampleFileName;

        // 디버그: 실제 내장된 리소스 이름 목록 확인 (개발 시 유용)
        LogManager.Instance.Debug(LogSource,
            $"리소스 로드 시도: {resName}");

        using var stream = assembly.GetManifestResourceStream(resName);

        if (stream == null)
        {
            // 리소스를 못 찾은 경우 실제 이름 목록 로그 (트러블슈팅용)
            var available = assembly.GetManifestResourceNames()
                                    .Where(n => n.Contains("Config"))
                                    .ToArray();
            if (available.Length > 0)
                LogManager.Instance.Debug(LogSource,
                    $"사용 가능한 Config 리소스: {string.Join(", ", available)}");

            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string _NormalizeJson(string json)
        => json.Trim().ReplaceLineEndings("\n");

    // §5 ─ 내부 타입 ──────────────────────────────────────────
    private record ConfigFileSpec(string FileName, string DefaultContent);
}