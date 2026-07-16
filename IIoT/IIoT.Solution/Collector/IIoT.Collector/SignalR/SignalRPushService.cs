// ══════════════════════════════════════════════════════════
//  IIoT.Collector · SignalR/SignalRPushService.cs
//  역할: EventBus 구독 → IIoTHubPusher 를 통해 웹 클라이언트에 Push
//        SignalRHostService 가 시작된 후 Initialize() 호출
//  C-11: 신규
//  C-EX-13: TagForceWriteEvent 구독 추가 — "ForceWrite" 원격 호출로 값이
//           바뀌었을 때, 호출한 클라이언트뿐 아니라 연결된 모든 클라이언트
//           (다른 HMI 화면·Monitor 등)에도 발생 사실을 Push 하여 실시간으로
//           인지할 수 있게 함 (TagValue 는 그 다음 폴링 주기에 갱신되지만,
//           강제쓰기 발생 자체는 즉시 알리는 것이 운영상 유용함).
//  생성: 2026-06-29 / 수정: 2026-07-16 (C-EX-13)
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Events;
using lssLib.Log;
using lssLib.Messaging;

namespace IIoT.Collector.SignalR;

/// <summary>
/// EventBus → SignalR Hub Push 연결 서비스 (DI 싱글턴).
/// </summary>
public sealed class SignalRPushService : IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly SignalRHostService _hostService;

    private IDisposable? _tagValueSub;
    private IDisposable? _alarmSub;
    private IDisposable? _forceWriteSub;   // ★ C-EX-13 신규

    // §2 ─ 생성자 ──────────────────────────────────────────

    public SignalRPushService(SignalRHostService hostService)
    {
        _hostService = hostService;
    }

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// EventBus 구독을 시작합니다.
    /// SignalRHostService.StartAsync() 완료 후 호출해야 Pusher 가 준비됩니다.
    /// </summary>
    public void Initialize()
    {
        if (_hostService.Pusher is null)
        {
            LogManager.Instance.Warn("SignalR",
                "SignalR Hub 비활성 — Push 서비스 구독 생략");
            return;
        }

        _tagValueSub   = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(_OnTagValue);
        _alarmSub      = EventBus.Instance.Subscribe<AlarmChangedEvent>(_OnAlarmChanged);
        _forceWriteSub = EventBus.Instance.Subscribe<TagForceWriteEvent>(_OnForceWrite);   // ★ C-EX-13

        LogManager.Instance.Info("SignalR", "SignalR Push 서비스 구독 시작");
    }

    // §4 ─ 핸들러 ──────────────────────────────────────────

    private void _OnTagValue(TagValueUpdatedEvent e)
    {
        if (_hostService.Pusher is null) return;

        var payload = new
        {
            tagId    = e.Value.TagId,
            plcId    = e.PlcId,
            rawValue = e.Value.RawValue is double d ? d : 0.0,
            engValue = e.EngValue,
            unit     = e.Unit,
            quality  = e.Value.Quality.ToString(),
            ts       = e.Value.Timestamp.ToString("O")
        };

        // fire-and-forget (SignalR Push 실패가 수집에 영향 없도록)
        _ = _hostService.Pusher.PushTagValueAsync(payload);
    }

    private void _OnAlarmChanged(AlarmChangedEvent e)
    {
        if (_hostService.Pusher is null) return;

        var payload = new
        {
            alarmKey = e.AlarmKey,
            tagId    = e.TagId,
            tagName  = e.TagName,
            plcId    = e.PlcId,
            level    = e.Level.ToString(),
            status   = e.Status.ToString(),
            message  = e.Message,
            engValue = e.CurrentEngValue,
            ts       = e.OccurredAt.ToString("O")
        };

        _ = _hostService.Pusher.PushAlarmAsync(payload);
    }

    /// <summary>
    /// ★ C-EX-13 신규: Tag 강제쓰기(Force Write) 실행 결과를 모든 클라이언트에 Push.
    /// 클라이언트 JS: connection.on("ForceWriteResult", (data) => { ... })
    /// </summary>
    private void _OnForceWrite(TagForceWriteEvent e)
    {
        if (_hostService.Pusher is null) return;

        var payload = new
        {
            plcId     = e.PlcId,
            tagId     = e.TagId,
            tagName   = e.TagName,
            address   = e.Address,
            value     = e.Value,
            isSuccess = e.IsSuccess,
            error     = e.Error,
            ts        = e.OccurredAt.ToString("O")
        };

        _ = _hostService.Pusher.PushForceWriteResultAsync(payload);
    }

    // §5 ─ 정리 ────────────────────────────────────────────

    public void Dispose()
    {
        _tagValueSub?.Dispose();
        _alarmSub?.Dispose();
        _forceWriteSub?.Dispose();   // ★ C-EX-13
    }
}
