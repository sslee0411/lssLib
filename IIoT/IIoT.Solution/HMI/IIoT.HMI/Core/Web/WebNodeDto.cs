// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Web/WebNodeDto.cs
//  역할: 웹 브라우저 표시용 노드 스냅샷 DTO.
//        AbstractLayoutNode(WPF 전용 ObservableObject)를 그대로 직렬화하지
//        않고 이 경량 DTO 로 변환해 REST(GET /api/layout)·SignalR
//        ("NodesChanged" Push) 양쪽에서 동일하게 사용한다.
//  HM-11: 신규
//  HM-21: ACK/ForceWrite 를 웹에서도 지원하기 위해 필드 5개 추가 — AlarmKey
//         (ACK 요청 시 그대로 전달), BoundCollectorId/BoundPlcId/BoundTagId
//         (ForceWrite 요청 라우팅 — "발생 출처로만 전송" 원칙 유지),
//         BoundTagName(ForceWrite 모달 표시용). 전부 HmiWebHub 가 nodeId 로
//         노드를 다시 찾아 서버 쪽에서 검증하므로, 클라이언트가 이 값을
//         변조해 보내도 실제 라우팅에는 영향이 없다(클라이언트는 표시 참고용).
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
    string  AlarmTimeText,
    string  AlarmKey,
    string  BoundCollectorId,
    string  BoundPlcId,
    string  BoundTagId,
    string  BoundTagName);
