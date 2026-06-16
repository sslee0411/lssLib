// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/Enums.cs
//  역할: 프로젝트 전체 Enum + 확장 메서드 통합 관리
//
//  ★ Enum 을 별도 파일로 분리한 이유:
//    - 각 모델 파일에 Enum 이 흩어지면 중복 정의 위험
//    - 새 Enum 값 추가 시 이 파일 한 곳만 수정
//    - 다른 어셈블리(Collector·Monitor)에서 참조 시 경로 명확
//    - IIoT.Shared 로 이동 시 파일 단위 이동 가능
//
//  포함 Enum 목록:
//    ① NodeCommType   — 트리 노드(장비·PLC) 통신 방식
//    ② ScaleMode      — 스케일 변환 모드 (ScaleLibrary.cs 에서 이동)
//
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

namespace IIoT.Studio.Models;

// §1 ─ 통신 방식 Enum ─────────────────────────────────────

/// <summary>
/// 장비·PLC 노드 통신 방식.
/// ★ string 대신 Enum 사용 이유:
///   - 오타 방지 (컴파일 타임 검증)
///   - switch 패턴 매칭 exhaustive 체크
///   - JSON 직렬화 시 숫자 대신 이름 저장 (JsonStringEnumConverter)
///   - 새 프로토콜 추가 시 이 파일 한 줄 추가
/// </summary>
public enum NodeCommType
{
    /// <summary>통신 없음 — 장비 기본값 (PLC 는 사용 불가)</summary>
    None,
    /// <summary>Modbus TCP/IP — 레지스터 읽기/쓰기</summary>
    ModbusTcp,
    /// <summary>RS-232/RS-485 직렬 통신 — Modbus RTU 등</summary>
    Serial,
    /// <summary>MQTT 브로커 — Publish/Subscribe 방식</summary>
    Mqtt,
    /// <summary>OPC Unified Architecture — 표준 산업 프로토콜</summary>
    OpcUa
    // ★ 새 프로토콜 추가 예시:
    // Profinet,
    // EthernetIp,
    // BacNet,
}

/// <summary>
/// NodeCommType 표시용 확장 메서드.
/// XAML 에서 콤보박스 표시 문자열이 필요할 때 사용.
/// ObjectDataProvider 로 Enum 값 목록 바인딩 시
/// ItemTemplate 에서 {Binding Converter={...}} 로 호출.
/// </summary>
public static class NodeCommTypeExtensions
{
    /// <summary>Enum 값 → 한글/영문 표시 레이블 변환</summary>
    public static string ToLabel(this NodeCommType t) => t switch
    {
        NodeCommType.None      => "없음",
        NodeCommType.ModbusTcp => "Modbus TCP",
        NodeCommType.Serial    => "Serial",
        NodeCommType.Mqtt      => "MQTT",
        NodeCommType.OpcUa     => "OPC-UA",
        _                      => t.ToString()
    };
}

// §2 ─ 스케일 변환 모드 Enum ──────────────────────────────

/// <summary>
/// 스케일 변환 모드.
/// ScaleEntry.Mode 에서 참조.
/// </summary>
public enum ScaleMode
{
    /// <summary>
    /// 선형 변환: Y = Slope × X + Offset
    /// RawMin/RawMax → EngMin/EngMax 범위 지정으로 자동 계산
    /// </summary>
    Linear,

    /// <summary>
    /// 수식 변환: NCalc Expression 엔진 사용
    /// 변수 x = Raw 값. 예) (x / 4000.0) * 10.0
    /// </summary>
    Expression
}
