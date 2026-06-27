// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Models/ParameterDefinition.cs
//  역할: 드라이버 파라미터 스키마 — Studio UI 자동 렌더링에 사용
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

/// <summary>
/// 드라이버 파라미터 정의 (스키마).
/// <para>
/// IProtocolPlugin.GetParameterSchema() 반환값으로 사용된다.
/// Studio는 이 목록을 읽어 PlcEditorView에 입력 폼을 자동으로 렌더링한다.
/// </para>
/// <example>
/// <code>
/// // 미쓰비시 드라이버 스키마 예시
/// new ParameterDefinition(
///     Key:             "Host",
///     DisplayName:     "IP 주소",
///     Type:            ParameterType.String,
///     DefaultValue:    "192.168.0.1",
///     Description:     "PLC IP 주소 또는 호스트명",
///     IsRequired:      true,
///     ValidationRegex: @"^\d+\.\d+\.\d+\.\d+$"
/// )
/// </code>
/// </example>
/// </summary>
/// <param name="Key">파라미터 식별 키 (DriverParams 딕셔너리 Key와 일치)</param>
/// <param name="DisplayName">UI 표시 레이블</param>
/// <param name="Type">입력 컨트롤 타입</param>
/// <param name="DefaultValue">기본값 (문자열 표현)</param>
/// <param name="Description">툴팁·설명 (null 허용)</param>
/// <param name="IsRequired">필수 입력 여부 (true 시 레이블에 * 표시)</param>
/// <param name="ValidationRegex">유효성 검사 정규식 (null = 검사 생략)</param>
/// <param name="EnumValues">Enum 타입일 때 선택 항목 목록 (null = 미사용)</param>
public sealed record ParameterDefinition(
    string        Key,
    string        DisplayName,
    ParameterType Type,
    string?       DefaultValue    = null,
    string?       Description     = null,
    bool          IsRequired      = false,
    string?       ValidationRegex = null,
    string[]?     EnumValues      = null
);
