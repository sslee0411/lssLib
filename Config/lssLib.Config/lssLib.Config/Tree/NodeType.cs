// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Tree/NodeType.cs
//  역할: 설정 트리 노드 유형 열거형 (계층: 루트→그룹→장비→장치)
// ══════════════════════════════════════════════════════════════════════════
namespace lssLib.Config.Tree;

/// <summary>
/// 설정 트리 노드의 유형을 나타내는 열거형.
/// </summary>
/// <remarks>
/// 계층 구조: <c>Root → Group → Device → Sensor | Tag | Other</c>
/// <para>
/// 같은 레벨의 노드도 서로 다른 유형을 가질 수 있습니다.
/// 예: Device 아래에 Sensor 와 Tag 가 혼합 배치 가능.
/// </para>
/// </remarks>
public enum NodeType
{
    /// <summary>루트 노드 — 트리당 1개. 직접 생성하지 않고 <see cref="ConfigTree"/> 가 관리.</summary>
    Root = 0,

    /// <summary>그룹 노드 — 공장·라인·사이트 등 논리적 묶음.</summary>
    Group = 1,

    /// <summary>장비 노드 — PLC, 서버, 컨트롤러 등 물리적 장비.</summary>
    Device = 2,

    /// <summary>센서 노드 — 온도·압력·유량 등 아날로그/디지털 입출력 센서.</summary>
    Sensor = 3,

    /// <summary>태그 노드 — PLC 메모리 주소, OPC-UA 노드 등 데이터 포인트.</summary>
    Tag = 4,

    /// <summary>기타 — 위 유형으로 분류되지 않는 노드.</summary>
    Other = 99
}