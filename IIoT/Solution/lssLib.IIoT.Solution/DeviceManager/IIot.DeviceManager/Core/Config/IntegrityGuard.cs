// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/IntegrityGuard.cs
//  역할: JSON 파일 SHA-256 해시 저장·검증 (변조 감지)
//        lssLib 에 SHA-256 기능 없으므로 BCL 직접 사용
//  Phase 1: Core 기반 구조
// ══════════════════════════════════════════════════════════

using System.IO;
using System.Security.Cryptography;
using System.Text;
using lssLib.Log;
using lssLib.Utils;

namespace IIoT.DeviceManager.Core.Config;

/// <summary>
/// JSON 설정 파일의 SHA-256 해시를 .hash 사이드카 파일로 관리합니다.
/// </summary>
/// <example><code>
/// // 저장 시
/// IntegrityGuard.SaveHash(jsonContent, "device.json");
///
/// // 로드 시
/// bool ok = IntegrityGuard.Verify(jsonContent, "device.json");
/// </code></example>
public static class IntegrityGuard
{
    // §1 ─ 상수 ───────────────────────────────────────────────
    private const string LogSource = "IntegrityGuard";
    private const string HashSuffix = ".hash";

    // §2 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// JSON 문자열의 SHA-256 해시를 [filePath].hash 파일에 저장합니다.
    /// </summary>
    public static void SaveHash(string jsonContent, string filePath)
    {
        Guard.NotWhiteSpace(jsonContent);
        Guard.NotWhiteSpace(filePath);

        string hash = Compute(jsonContent);
        string hashPath = filePath + HashSuffix;

        File.WriteAllText(hashPath, hash, Encoding.UTF8);
        LogManager.Instance.Debug(LogSource, $"해시 저장: {Path.GetFileName(filePath)} → {hash[..12]}...");
    }

    /// <summary>
    /// JSON 문자열과 [filePath].hash 파일의 해시를 비교하여 무결성을 검증합니다.
    /// </summary>
    /// <returns>일치하면 true, 불일치·파일 없음이면 false</returns>
    public static bool Verify(string jsonContent, string filePath)
    {
        Guard.NotWhiteSpace(jsonContent);
        Guard.NotWhiteSpace(filePath);

        string hashPath = filePath + HashSuffix;

        if (!File.Exists(hashPath))
        {
            LogManager.Instance.Warn(LogSource, $"해시 파일 없음: {hashPath}");
            return false;
        }

        string stored = File.ReadAllText(hashPath, Encoding.UTF8).Trim();
        string current = Compute(jsonContent);
        bool matched = string.Equals(stored, current, StringComparison.OrdinalIgnoreCase);

        if (!matched)
            LogManager.Instance.Warn(LogSource,
                $"무결성 불일치: {Path.GetFileName(filePath)} — 파일이 외부에서 변조되었을 수 있습니다.");

        return matched;
    }

    /// <summary>
    /// [filePath].hash 사이드카 파일을 삭제합니다.
    /// </summary>
    public static void DeleteHash(string filePath)
    {
        string hashPath = filePath + HashSuffix;
        if (File.Exists(hashPath))
            File.Delete(hashPath);
    }

    // §3 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>문자열의 SHA-256 해시를 소문자 hex 문자열로 반환합니다.</summary>
    private static string Compute(string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}