// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · SignalR/MonitorHub.cs
//  역할: Monitor 자체 SignalR Hub (웹 브라우저 연동, MN-05)
//        Collector의 IIoTHub(C-11)와 동일한 패턴 — 웹 클라이언트가
//        ws://localhost:7879/monitor-hub 로 접속하면 Monitor 가 집계한
//        "전체 Collector 통합" Tag/알람 값을 실시간으로 Push 받는다.
//  MN-05: 신규
//  생성: 2026-07-08
// ══════════════════════════════════════════════════════════

using Microsoft.AspNetCore.SignalR;

namespace IIoT.Monitor.SignalR;

/// <summary>
/// Monitor 자체 SignalR Hub. 웹 브라우저/외부 클라이언트가 연결하는 진입점.
/// <para>
/// 클라이언트 JS 예:
/// <code>
/// const conn = new signalR.HubConnectionBuilder()
///     .withUrl("http://localhost:7879/monitor-hub")
///     .build();
/// conn.on("TagValue", (data) => console.log(data));      // collectorId 포함
/// conn.on("AlarmChanged", (data) => console.log(data));  // collectorId 포함
/// await conn.start();
/// </code>
/// </para>
/// </summary>
public sealed class MonitorHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}

/// <summary>
/// MonitorHub Push 유틸리티 (DI 싱글턴).
/// MonitorHostService 가 LiveTagAggregator/AlarmAggregator 의 실시간 갱신을
/// 구독하여 이 클래스를 통해 모든 웹 클라이언트에 Push 한다.
/// </summary>
public sealed class MonitorHubPusher
{
    private readonly IHubContext<MonitorHub> _hub;

    public MonitorHubPusher(IHubContext<MonitorHub> hub)
    {
        _hub = hub;
    }

    /// <summary>Tag 값 갱신을 모든 웹 클라이언트에 Push. (payload에 collectorId 포함)</summary>
    public Task PushTagValueAsync(object payload)
        => _hub.Clients.All.SendAsync("TagValue", payload);

    /// <summary>알람 이벤트를 모든 웹 클라이언트에 Push. (payload에 collectorId 포함)</summary>
    public Task PushAlarmAsync(object payload)
        => _hub.Clients.All.SendAsync("AlarmChanged", payload);
}
