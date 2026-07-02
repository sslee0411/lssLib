// ══════════════════════════════════════════════════════════
//  IIoT.Collector · SignalR/IIoTHub.cs
//  역할: 웹 브라우저 / 외부 클라이언트용 SignalR Hub
//        클라이언트가 ws://localhost:5000/iiot 로 연결하면
//        실시간 Tag 값 / 알람 이벤트를 Push 받는다
//  C-11: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using Microsoft.AspNetCore.SignalR;

namespace IIoT.Collector.SignalR;

/// <summary>
/// IIoT Collector SignalR Hub.
/// <para>
/// 웹 브라우저/외부 클라이언트가 연결하는 진입점.
/// 서버→클라이언트 Push 전용 (클라이언트→서버 메서드는 없음).
/// </para>
/// <para>
/// Hub 는 인스턴스가 요청마다 생성·소멸됩니다.
/// 실제 Push 는 <see cref="IIoTHubPusher"/> 를 통해 수행합니다.
/// </para>
/// </summary>
public sealed class IIoTHub : Hub
{
    /// <summary>
    /// 클라이언트 연결 시 현재 수집 중인 PLC/Tag 수를 전송합니다.
    /// (선택적 구현 — 연결 직후 상태 동기화용)
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}

/// <summary>
/// SignalR Push 유틸리티 (DI 싱글턴).
/// <para>
/// EventBus 핸들러에서 직접 IHubContext 를 사용하여
/// 모든 연결된 클라이언트에 메시지를 Push 합니다.
/// </para>
/// </summary>
public sealed class IIoTHubPusher
{
    private readonly IHubContext<IIoTHub> _hub;

    public IIoTHubPusher(IHubContext<IIoTHub> hub)
    {
        _hub = hub;
    }

    /// <summary>
    /// Tag 값 갱신을 모든 클라이언트에 Push합니다.
    /// 클라이언트 JS: connection.on("TagValue", (data) => { ... })
    /// </summary>
    public Task PushTagValueAsync(object payload)
        => _hub.Clients.All.SendAsync("TagValue", payload);

    /// <summary>
    /// 알람 이벤트를 모든 클라이언트에 Push합니다.
    /// 클라이언트 JS: connection.on("AlarmChanged", (data) => { ... })
    /// </summary>
    public Task PushAlarmAsync(object payload)
        => _hub.Clients.All.SendAsync("AlarmChanged", payload);
}
