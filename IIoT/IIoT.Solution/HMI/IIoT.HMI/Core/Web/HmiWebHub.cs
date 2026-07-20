// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Web/HmiWebHub.cs
//  역할: 웹 브라우저 표시용 SignalR Hub.
//        HmiWebHostService 가 IHubContext<HmiWebHub> 를 통해 "NodesChanged"
//        이벤트를 Push 한다(서버→클라이언트, 기존 그대로).
//  HM-11: 신규 — 당시에는 읽기 전용 표시만 구현(클라이언트→서버 호출 메서드 없음).
//  HM-21: 클라이언트(웹 페이지)가 호출할 수 있는 AcknowledgeAsync/ForceWriteAsync
//         2개 메서드를 추가했다. WPF 아이콘 더블클릭(HM-09)/알람 팝업 ACK
//         (HM-08) 과 동일하게 "발생 출처로만 전송" 원칙을 지킨다 — nodeId 로
//         LayoutCanvasViewModel.Nodes 에서 노드를 다시 찾아 그 노드의
//         BoundCollectorId 로만 CollectorConnectionManager 를 호출한다(웹
//         클라이언트가 임의의 collectorId 를 직접 지정해 보낼 수 없음).
//         보안(HM-12) 검토 결과: ForceWrite 는 WPF 쪽 LayoutCanvasViewModel.
//         IsForceWriteLocked(기본값=잠금) 를 그대로 재사용해 웹에서도 동일하게
//         잠가둔다 — WPF 콘솔에서 🔒 해제 조작을 하기 전에는 웹에서도
//         ForceWrite 가 불가능하다(신규 인증 체계를 새로 만들지 않고 기존
//         안전장치를 웹 경로까지 확장하는 방식). API Key 는 기존과 동일하게
//         Collector 측 Security.ForceWriteApiKey 검증을 그대로 통과해야 한다.
//         ACK 는 WPF 알람 팝업의 ACK 버튼과 동일하게 잠금 여부와 무관하게 허용
//         (WPF 쪽도 ACK 는 IsForceWriteLocked 체크를 하지 않음 — 일관성 유지).
//         ⚠ 잔여 리스크: HmiWebHostService 의 CORS 정책이 SetIsOriginAllowed(_ =>
//         true) 로 전체 허용(HM-11 원안 그대로, 변경 없음)이므로, 이 두 메서드는
//         "같은 네트워크에서 웹 포트에 접근 가능한 누구나" 호출을 시도할 수 있다.
//         실제 통제는 위 잠금 플래그 + API Key 2단계로 이루어진다 — 더 강한
//         네트워크 격리/인증이 필요하면 후속 Step 에서 별도로 검토.
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Connection;
using IIoT.HMI.Core.Layout;
using IIoT.HMI.Models;
using IIoT.HMI.ViewModels;
using Microsoft.AspNetCore.SignalR;
using System.Linq;
using System.Windows;

namespace IIoT.HMI.Core.Web;

/// <summary>
/// 웹 브라우저 표시용 Hub — 서버→클라이언트 Push("NodesChanged")에 더해
/// HM-21 부터는 클라이언트→서버 호출(ACK/ForceWrite) 도 지원한다.
/// </summary>
public sealed class HmiWebHub : Hub
{
    private readonly CollectorConnectionManager _connectionManager;
    private readonly LayoutCanvasViewModel      _canvasVm;

    public HmiWebHub(CollectorConnectionManager connectionManager, LayoutCanvasViewModel canvasVm)
    {
        _connectionManager = connectionManager;
        _canvasVm          = canvasVm;
    }

    /// <summary>
    /// 웹 카드의 알람 배지 클릭 → 알람 ACK 요청. nodeId 로 노드를 다시 찾아
    /// 그 노드의 BoundCollectorId 로만 전송한다(클라이언트가 보낸 collectorId 는
    /// 없음 — nodeId/alarmKey 만 받는다).
    /// </summary>
    public async Task<bool> AcknowledgeAsync(string nodeId, string alarmKey)
    {
        var collectorId = await _FindNodeFieldAsync(nodeId, n => n.BoundCollectorId);
        if (string.IsNullOrEmpty(collectorId)) return false;

        await _connectionManager.AcknowledgeAlarmAsync(collectorId, alarmKey);
        return true;
    }

    /// <summary>
    /// 웹 카드 클릭 → 강제쓰기 요청. WPF 의 화면 잠금(IsForceWriteLocked) 을
    /// 그대로 재사용해 잠금 상태면 즉시 거부한다.
    /// </summary>
    public async Task<ForceWriteResult> ForceWriteAsync(string nodeId, string value, string apiKey)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return new ForceWriteResult(false, "서버 내부 오류: UI 스레드를 찾을 수 없습니다.");

        var (locked, collectorId, plcId, tagId, isBound) = await dispatcher.InvokeAsync(() =>
        {
            var node = _canvasVm.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
            return (
                _canvasVm.IsForceWriteLocked,
                node?.BoundCollectorId ?? string.Empty,
                node?.BoundPlcId ?? string.Empty,
                node?.BoundTagId ?? string.Empty,
                node?.IsBound ?? false);
        });

        if (locked)
            return new ForceWriteResult(false, "화면이 잠금 모드입니다. WPF 쪽 툴바의 🔒 버튼으로 잠금을 해제한 뒤 다시 시도하세요.");

        if (!isBound)
            return new ForceWriteResult(false, "Tag가 바인딩되지 않은 카드입니다.");

        return await _connectionManager.ForceWriteAsync(collectorId, plcId, tagId, value, apiKey);
    }

    private async Task<string> _FindNodeFieldAsync(string nodeId, Func<AbstractLayoutNode, string> selector)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return string.Empty;

        return await dispatcher.InvokeAsync(() =>
        {
            var node = _canvasVm.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
            return node is null ? string.Empty : selector(node);
        });
    }
}
