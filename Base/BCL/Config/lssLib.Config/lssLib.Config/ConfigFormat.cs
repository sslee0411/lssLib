// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · ConfigFormat.cs
//  역할: 설정 파일 형식 열거형
// ══════════════════════════════════════════════════════════════════════════
namespace lssLib.Config;

/// <summary>
/// 설정 파일 형식을 나타내는 열거형.
/// </summary>
/// <remarks>
/// <see cref="ConfigManager"/> 는 세 가지 형식을 모두 읽고 쓸 수 있으며,
/// 형식이 명시되지 않으면 파일 확장자를 기준으로 자동 감지합니다.
/// <list type="table">
///   <item><term>.ini</term><description>INI 형식</description></item>
///   <item><term>.json</term><description>JSON 형식</description></item>
///   <item><term>.xml</term><description>XML 형식</description></item>
/// </list>
/// </remarks>
public enum ConfigFormat
{
    /// <summary>INI 형식 — <c>[Section]</c> + <c>key = value</c> 구조.</summary>
    Ini = 0,

    /// <summary>JSON 형식 — 중첩 객체로 섹션·키·값 표현.</summary>
    Json = 1,

    /// <summary>XML 형식 — <c><Config></Config></c> 루트 아래 섹션·엔트리 요소.</summary>
    Xml = 2
}