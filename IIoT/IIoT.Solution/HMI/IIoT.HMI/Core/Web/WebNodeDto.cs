// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Web/WebNodeDto.cs
//  역할: 웹 브라우저 표시용 노드 스냅샷 DTO.
//        AbstractLayoutNode(WPF 전용 ObservableObject)를 그대로 직렬화하지
//        않고 이 경량 DTO 로 변환해 REST(GET /api/layout)·SignalR
//        ("NodesChanged" Push) 양쪽에서 동일하게 사용한다.
//  HM-11: 신규
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

namespace IIoT.HMI.Core.Web;

/// <summary>웹 페이지가 카드 1개를 그리는 데 필요한 전체 정보(구조+실시간 상태).</summary>
public sealed record WebNodeDto(
    string  NodeId,
    string  NodeType,
    string  Label,
    string  IconGlyph,
    string  CategoryColor,
    double  X,
    double  Y,
    int     ZIndex,
    bool    IsBound,
    string  ValueText,
    string  ValueQuality,
    double? EngValue,
    bool    HasActiveAlarm,
    string  AlarmLevel,
    string  AlarmStatusText,
    string  AlarmMessage,
    string  AlarmTimeText);
