// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/ConfigInitializer.cs
//  역할: 프로그램 시작 시 JSON 설정 파일 초기화
//        ① config/{name}.json 존재 → 그대로 사용
//        ② config/{name}.json 없고 .json.sample 있음 → sample 복사하여 생성
//        ③ .json.sample 도 없음 → 최소 기본값 JSON 자동 생성
//  Phase 1 Update: 신규 추가
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Utils;
using System.IO;

namespace IIoT.DeviceManager.Core.Config;

/// <summary>
/// 프로그램 시작 시 설정 JSON 파일 5종의 존재를 보장합니다.
/// App.xaml.cs 의 OnStartup 에서 LogManager 초기화 직후 호출합니다.
/// </summary>
/// <example><code>
/// // App.xaml.cs OnStartup
/// _InitLogManager();
/// ConfigInitializer.EnsureConfigFiles(ConfigDirectory);
/// </code></example>
public static class ConfigInitializer
{
    // §1 ─ 상수 ───────────────────────────────────────────────
    private const string LogSource = "ConfigInitializer";

    // §2 ─ 파일 정의 ──────────────────────────────────────────

    /// <summary>
    /// 관리할 설정 파일 정보 목록.
    /// FileName : 실제 JSON 파일명
    /// DefaultContent : .json 과 .sample 모두 없을 때 생성할 최소 JSON
    /// </summary>
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
    /// config 디렉터리의 JSON 설정 파일 5종을 보장합니다.
    /// 없는 파일은 .sample 복사 또는 기본값 생성으로 만들어냅니다.
    /// </summary>
    /// <param name="configDirectory">설정 파일 디렉터리 (예: [Exe]/config)</param>
    public static void EnsureConfigFiles(string configDirectory)
    {
        Guard.NotWhiteSpace(configDirectory);

        // config 디렉터리 생성 (없으면)
        Directory.CreateDirectory(configDirectory);

        LogManager.Instance.Info(LogSource,
            $"설정 파일 초기화 시작 → {configDirectory}");

        foreach (var spec in _Specs)
            _EnsureFile(configDirectory, spec);

        LogManager.Instance.Info(LogSource, "설정 파일 초기화 완료");
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>단일 설정 파일의 존재를 보장합니다.</summary>
    private static void _EnsureFile(string configDir, ConfigFileSpec spec)
    {
        string jsonPath = Path.Combine(configDir, spec.FileName);
        string samplePath = Path.Combine(configDir, spec.FileName + ".sample");

        // ① JSON 파일이 이미 존재하면 건드리지 않음
        if (File.Exists(jsonPath))
        {
            LogManager.Instance.Debug(LogSource,
                $"[OK] {spec.FileName} — 기존 파일 사용");
            return;
        }

        // ② .sample 파일이 있으면 복사하여 JSON 생성
        if (File.Exists(samplePath))
        {
            File.Copy(samplePath, jsonPath, overwrite: false);

            // 복사한 JSON 에 무결성 해시 생성
            string content = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
            IntegrityGuard.SaveHash(content, jsonPath);

            LogManager.Instance.Info(LogSource,
                $"[COPY] {spec.FileName} ← {spec.FileName}.sample");
            return;
        }

        // ③ 둘 다 없으면 기본값 JSON 생성
        string defaultJson = _NormalizeJson(spec.DefaultContent);
        File.WriteAllText(jsonPath, defaultJson, System.Text.Encoding.UTF8);
        IntegrityGuard.SaveHash(defaultJson, jsonPath);

        LogManager.Instance.Warn(LogSource,
            $"[NEW] {spec.FileName} — .sample 없음, 기본값으로 생성");
    }

    /// <summary>JSON 문자열 앞뒤 공백 정리 + UTF-8 BOM 없음 보장.</summary>
    private static string _NormalizeJson(string json)
        => json.Trim().ReplaceLineEndings("\n");

    // §5 ─ 내부 타입 ──────────────────────────────────────────

    /// <summary>설정 파일 스펙 레코드</summary>
    private record ConfigFileSpec(
        string FileName,
        string DefaultContent);
}