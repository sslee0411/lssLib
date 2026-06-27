// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Enums/DriverEnums.cs
//  역할: 드라이버 파라미터 UI 타입 + 드라이버 지원 기능 열거형
//  CS8954 수정: 단일 파일에 namespace 1개 (ParameterType + DriverCapability 통합)
//  생성: 2026-06-27 / 수정: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

/// <summary>
/// 드라이버 파라미터 입력 타입.
/// Studio가 이 값을 읽어 PlcEditorView에 적절한 UI 컨트롤을 자동 렌더링한다.
/// </summary>
public enum ParameterType
{
    /// <summary>문자열 입력 (TextBox)</summary>
    String   = 0,

    /// <summary>정수 입력 (NumericBox)</summary>
    Int      = 1,

    /// <summary>부울 토글 (CheckBox)</summary>
    Bool     = 2,

    /// <summary>선택 목록 (ComboBox) — EnumValues 에 항목 목록 지정</summary>
    Enum     = 3,

    /// <summary>비밀번호 (PasswordBox — 마스킹)</summary>
    Password = 4
}

/// <summary>
/// 드라이버 지원 기능.
/// IProtocolPlugin.GetCapabilities() 반환값으로 사용된다.
/// </summary>
public enum DriverCapability
{
    /// <summary>태그 값 읽기 (폴링)</summary>
    Read        = 0,

    /// <summary>태그 값 쓰기</summary>
    Write       = 1,

    /// <summary>변경 구독 (OPC-UA DataChange, MQTT Subscribe 등)</summary>
    Subscribe   = 2,

    /// <summary>일괄 읽기 최적화 (레지스터 범위 묶음 읽기)</summary>
    BatchRead   = 3,

    /// <summary>진단 정보 제공 (연결 지연·오류율 등)</summary>
    Diagnostics = 4
}
