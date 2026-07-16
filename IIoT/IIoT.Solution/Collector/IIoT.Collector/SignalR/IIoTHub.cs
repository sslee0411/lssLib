// ══════════════════════════════════════════════════════════
//  IIoT.Collector · SignalR/IIoTHub.cs
//  역할: 웹 브라우저 / 외부 클라이언트용 SignalR Hub
//        클라이언트가 ws://localhost:5000/iiot 로 연결하면
//        실시간 Tag 값 / 알람 이벤트를 Push 받는다
//  C-11: 신규
//  C-EX-12: AcknowledgeAlarm(alarmKey) 서버 메서드 추가
//           Monitor(및 웹 클라이언트)가 conn.invoke("AcknowledgeAlarm", alarmKey)
//           로 호출 → AlarmStateManager.Acknowledge() 위임.
//           AlarmStateManager 가 내부적으로 EventBus.Publish(AlarmChangedEvent)
//           를 발행하므로, 이미 구독 중인 SignalRPushService 가 자동으로
//           "AlarmChanged"(Status=Acked) 를 전체 클라이언트에 Push 한다
//           (Hub 에서 별도로 SendAsync 를 호출할 필요 없음 — 기존 파이프라인 재사용).
//           ★ AlarmStateManager 는 WPF DI 컨테이너의 싱글턴이며,
//             SignalRHostService.StartAsync() 에서 ASP.NET Core 빌더의
//             Services 에도 동일 인스턴스를 등록해 Hub 생성자에서 주입받는다
//             (기존 DeviceInstanceService 클로저 재사용 패턴과 동일 원칙).
//  C-EX-13: ForceWrite(plcId,tagId,value,apiKey) 서버 메서드 추가
//           IIoT.HMI(및 웹 클라이언트)가
//           conn.invoke("ForceWrite", plcId, tagId, value, apiKey) 로 호출
//           → ForceWriteService.WriteAsync() 위임 (C-15 에서 이미 구현된
//           Enabled/ApiKey/Tag존재/활성/형식 검증 + AuditLogService 기록을
//           그대로 재사용 — Hub 는 위임만 하고 별도 검증 로직을 두지 않는다).
//           AcknowledgeAlarm 과 동일 원칙: ForceWriteService 는 WPF DI 컨테이너의
//           싱글턴이며, SignalRHostService.StartAsync() 에서 ASP.NET Core
//           빌더의 Services 에도 동일 인스턴스를 등록해 Hub 생성자에서
//           주입받는다.
//  생성: 2026-06-29 / 수정: 2026-07-07 (C-EX-12) / 2026-07-16 (C-EX-13)
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Engine;
using Microsoft.AspNetCore.SignalR;

namespace IIoT.Collector.SignalR;

/// <summary>
/// IIoT Collector SignalR Hub.
/// <para>
/// 웹 브라우저/외부 클라이언트가 연결하는 진입점.
/// 서버→클라이언트 Push 는 <see cref="IIoTHubPusher"/> 를 통해 별도로 수행하고,
/// 클라이언트→서버 명령은 이 클래스의 public 메서드로 노출한다(C-EX-12, C-EX-13).
/// </para>
/// <para>
/// Hub 는 인스턴스가 요청(호출)마다 생성·소멸됩니다 — 필드에 상태를 두지 말 것.
/// </para>
/// </summary>
public sealed class IIoTHub : Hub
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly AlarmStateManager _alarmManager;
    private readonly ForceWriteService _forceWriteService;   // ★ C-EX-13 신규

    // §2 ─ 생성자 ──────────────────────────────────────────

    /// <summary>
    /// ASP.NET Core DI 가 Hub 인스턴스 생성 시 자동 주입.
    /// AlarmStateManager / ForceWriteService 는 SignalRHostService.StartAsync() 에서
    /// builder.Services.AddSingleton(...) 로 등록된 WPF DI 싱글턴 인스턴스가
    /// 그대로 전달된다.
    /// </summary>
    public IIoTHub(AlarmStateManager alarmManager, ForceWriteService forceWriteService)
    {
        _alarmManager       = alarmManager;
        _forceWriteService  = forceWriteService;
    }

    // §3 ─ 연결 ────────────────────────────────────────────

    /// <summary>
    /// 클라이언트 연결 시 현재 수집 중인 PLC/Tag 수를 전송합니다.
    /// (선택적 구현 — 연결 직후 상태 동기화용)
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    // §4 ─ 클라이언트 → 서버 명령 (C-EX-12, C-EX-13) ───────

    /// <summary>
    /// 알람 ACK 요청.
    /// <para>
    /// 클라이언트 호출 예: <c>await conn.InvokeAsync("AcknowledgeAlarm", "T001:HH");</c>
    /// </para>
    /// <para>
    /// 실제 상태 변경과 클라이언트 Push 는 AlarmStateManager.Acknowledge() →
    /// EventBus.Publish(AlarmChangedEvent) → SignalRPushService 구독 경로로
    /// 자동 처리되므로, 이 메서드는 위임만 하고 별도 Push 를 수행하지 않는다.
    /// </para>
    /// </summary>
    public Task AcknowledgeAlarm(string alarmKey)
    {
        _alarmManager.Acknowledge(alarmKey);
        return Task.CompletedTask;
    }

    /// <summary>
    /// ★ C-EX-13 신규: Tag 강제값 쓰기(Force Write) 요청.
    /// <para>
    /// 클라이언트 호출 예:
    /// <c>var result = await conn.InvokeAsync&lt;ForceWriteResult&gt;(
    ///     "ForceWrite", plcId, tagId, value, apiKey);</c>
    /// </para>
    /// <para>
    /// 검증(기능 활성화·API Key·Tag 존재·활성 상태·값 형식)과 실제 통신,
    /// 감사 로그 기록까지 모두 ForceWriteService.WriteAsync() (C-15) 에 위임한다.
    /// Hub 는 원격 호출 진입점 역할만 수행하며 별도 검증 로직을 두지 않는다.
    /// </para>
    /// </summary>
    /// <param name="plcId">대상 PLC/Device ID</param>
    /// <param name="tagId">대상 Tag ID</param>
    /// <param name="value">쓸 값 (문자열, Raw 값 기준)</param>
    /// <param name="apiKey">
    /// settings.json Security.ForceWriteApiKey 가 설정된 경우 일치해야 함.
    /// 미설정 시 빈 문자열("")로 호출 가능(검증 생략).
    /// </param>
    public Task<ForceWriteResult> ForceWrite(string plcId, string tagId, string value, string apiKey = "")
        => _forceWriteService.WriteAsync(plcId, tagId, value, apiKey);
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

    /// <summary>
    /// ★ C-EX-13 신규: Tag 강제쓰기(Force Write) 실행 결과를 모든 클라이언트에 Push합니다.
    /// 클라이언트 JS: connection.on("ForceWriteResult", (data) => { ... })
    /// </summary>
    public Task PushForceWriteResultAsync(object payload)
        => _hub.Clients.All.SendAsync("ForceWriteResult", payload);
}
