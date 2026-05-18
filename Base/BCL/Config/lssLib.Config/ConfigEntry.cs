// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · ConfigEntry.cs
//  역할: 단일 설정 항목 (섹션 + 키 + 값 + 암호화 여부)
// ══════════════════════════════════════════════════════════════════════════
namespace lssLib.Config;

/// <summary>
/// 단일 설정 항목.
/// </summary>
/// <remarks>
/// 값(<see cref="Value"/>)은 항상 메모리상 <b>복호화된 평문</b>으로 보관됩니다.
/// <see cref="IsEncrypted"/> 가 <see langword="true"/> 이면
/// 파일 저장 시 <see cref="Encryption.ConfigEncryptor"/> 를 통해 AES-256-GCM 암호화됩니다.
/// <example><code>
/// var entry = new ConfigEntry("Network", "Password", "secret", IsEncrypted: true);
/// Console.WriteLine(entry); // [Network] Password = *** (encrypted)
/// </code></example>
/// </remarks>
/// <param name="Section">소속 섹션. INI의 <c>[Section]</c>, JSON/XML의 그룹 키.</param>
/// <param name="Key">항목 키.</param>
/// <param name="Value">복호화된 평문 값.</param>
/// <param name="IsEncrypted">파일 저장 시 암호화 여부. 기본값 <see langword="false"/>.</param>
public sealed record ConfigEntry(
    string Section,
    string Key,
    string Value,
    bool IsEncrypted = false)
{
    #region §1 ─ 파생 속성

    /// <summary>
    /// 섹션과 키를 결합한 복합 조회 키 (대소문자 무시, 정규화).
    /// </summary>
    /// <remarks>
    /// <see cref="ConfigStore"/> 내부 Dictionary 조회에 사용됩니다.
    /// </remarks>
    public string CompositeKey =>
        $"{Section?.ToUpperInvariant() ?? string.Empty}::{Key?.ToUpperInvariant() ?? string.Empty}";

    #endregion

    #region §2 ─ 문자열 표현

    /// <summary>암호화 항목은 값을 마스킹하여 반환합니다.</summary>
    public override string ToString() =>
        IsEncrypted
            ? $"[{Section}] {Key} = *** (encrypted)"
            : $"[{Section}] {Key} = {Value}";

    #endregion
}