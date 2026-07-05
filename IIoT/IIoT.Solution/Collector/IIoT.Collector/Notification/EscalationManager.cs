// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Notification/EscalationManager.cs
//  역할: AlarmChangedEvent(Active) 수신 → 즉시 1차 알림 발송
//        → EscalateMinutes 경과 후 미ACK 상태면 2차 에스컬레이션 알림 발송
//        AlarmChangedEvent(Acked/Recovered) 수신 → 진행 중인 타이머 취소
//  C-14: 신규
//  생성: 2026-07-05
//
//  ★ 설계 원칙: AlarmStateManager 는 수정하지 않고 EventBus 구독만으로 동작
//    (기존 알람 감지·ACK 로직에 영향 없음, 독립적으로 추가/제거 가능)
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Events;
using IIoT.Collector.Notification;
using lssLib.Log;
using lssLib.Messaging;
using System.Collections.Concurrent;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 알람 에스컬레이션 관리자 (DI 싱글턴).
/// <para>
/// AlarmKey 당 1개의 <see cref="CancellationTokenSource"/> 를 보관하여
/// ACK/복귀 시 대기 중인 에스컬레이션 타이머를 취소한다.
/// </para>
/// </summary>
public sealed class EscalationManager : IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader _configLoader;
    private readonly NotificationService _notifier;

    private IDisposable? _sub;

    /// <summary>AlarmKey → 에스컬레이션 대기 취소 토큰 (ACK/복귀 시 취소)</summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _timers = new();

    // §2 ─ 생성자 ──────────────────────────────────────────

    public EscalationManager(CollectorConfigLoader configLoader, NotificationService notifier)
    {
        _configLoader = configLoader;
        _notifier = notifier;
    }

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// EventBus 구독을 시작합니다.
    /// App.xaml.cs 에서 AlarmStateManager.Initialize() 이후 호출.
    /// </summary>
    public void Initialize()
    {
        _sub = EventBus.Instance.Subscribe<AlarmChangedEvent>(_OnAlarmChanged);
        LogManager.Instance.Info("Escalation", "알람 에스컬레이션 관리자 초기화 완료");
    }

    // §4 ─ 이벤트 핸들러 ───────────────────────────────────

    private void _OnAlarmChanged(AlarmChangedEvent e)
    {
        if (e.Status == AlarmStatus.Active)
        {
            _StartEscalation(e);
        }
        else
        {
            // Acked 또는 Recovered → 대기 중인 에스컬레이션 취소
            _CancelEscalation(e.AlarmKey);
        }
    }

    private void _StartEscalation(AlarmChangedEvent e)
    {
        // 이미 진행 중이면 중복 시작 방지
        if (_timers.ContainsKey(e.AlarmKey)) return;

        var entry = _FindAlarmEntry(e.TagId);
        if (entry is null) return;

        var cts = new CancellationTokenSource();
        _timers[e.AlarmKey] = cts;

        // ① 즉시 1차 알림 발송
        _ = _SendNotificationAsync(entry, e, escalated: false);

        // ② EscalateMinutes > 0 이면 지연 후 에스컬레이션 예약
        if (entry.EscalateMinutes > 0)
            _ = _EscalateAfterDelayAsync(e, entry.EscalateMinutes, cts.Token);
    }

    private async Task _EscalateAfterDelayAsync(AlarmChangedEvent e, int minutes, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(minutes), ct);

            // 여기까지 취소되지 않았다면 = 아직 ACK 안 된 상태 → 에스컬레이션 발송
            var entry = _FindAlarmEntry(e.TagId);
            if (entry is not null)
                await _SendNotificationAsync(entry, e, escalated: true);
        }
        catch (TaskCanceledException)
        {
            // ACK 처리로 인한 정상 취소 — 로그 불필요
        }
        finally
        {
            _timers.TryRemove(e.AlarmKey, out _);
        }
    }

    private void _CancelEscalation(string alarmKey)
    {
        if (_timers.TryRemove(alarmKey, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    // §5 ─ 알람 라이브러리 조회 ────────────────────────────

    private AlarmEntryDto? _FindAlarmEntry(string tagId)
    {
        foreach (var plc in _configLoader.Plcs)
        {
            var tag = plc.Tags.FirstOrDefault(t => t.Id == tagId);
            if (tag is null) continue;
            if (string.IsNullOrWhiteSpace(tag.AlarmEntryId)) return null;

            return _configLoader.AlarmLibrary.TryGetValue(tag.AlarmEntryId, out var entry)
                ? entry
                : null;
        }
        return null;
    }

    // §6 ─ 알림 발송 ───────────────────────────────────────

    private async Task _SendNotificationAsync(AlarmEntryDto entry, AlarmChangedEvent e, bool escalated)
    {
        var prefix = escalated ? "[에스컬레이션] " : "[알람] ";
        var subject = $"{prefix}{e.Level} - {e.TagName}";
        var body =
            $"태그: {e.TagName}\n" +
            $"레벨: {e.Level}\n" +
            $"메시지: {e.Message}\n" +
            $"현재값: {e.CurrentEngValue:F2}\n" +
            $"발생시각: {e.OccurredAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

        if (!string.IsNullOrWhiteSpace(entry.NotifyEmail))
            await _notifier.SendEmailAsync(entry.NotifyEmail, subject, body);

        if (!string.IsNullOrWhiteSpace(entry.NotifyPhone))
            await _notifier.SendWebhookAsync(entry.NotifyPhone, $"{subject}\n{body}");
    }

    // §7 ─ 정리 ────────────────────────────────────────────

    public void Dispose()
    {
        _sub?.Dispose();
        foreach (var cts in _timers.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _timers.Clear();
    }
}