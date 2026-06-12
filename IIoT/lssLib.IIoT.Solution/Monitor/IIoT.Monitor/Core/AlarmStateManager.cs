// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/AlarmStateManager.cs
//  역할: 알람 생명주기 상태머신 관리
//        Fired → Acked → Cleared
//        UI 바인딩용 ObservableCollection 유지
//  Phase 10: 신규
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Messaging;
using System.Collections.ObjectModel;

namespace IIoT.Monitor.Core;

/// <summary>
/// 알람 상태머신 관리자.
///
/// 책임:
///   · AlarmRecord 생성/갱신 (Fired → Acked → Cleared)
///   · Active 알람 목록 유지 (UI 바인딩)
///   · 이력 저장 (최근 500건)
///   · EventBus 발행 (AlarmFired / AlarmAcked / AlarmCleared)
///
/// 스레드 안전:
///   · 모든 컬렉션 수정은 WPF Dispatcher 에서 실행
/// </summary>
public sealed class AlarmStateManager : IDisposable
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc    = "AlarmStateManager";
    private const int    MaxHistory = 500;

    private readonly SemaphoreSlim _lock   = new(1, 1);
    private bool _disposed;

    // §2 ─ 컬렉션 (UI 바인딩) ─────────────────────────────────

    /// <summary>현재 활성 알람 목록 (Fired / Acked 상태)</summary>
    public ObservableCollection<AlarmRecord> ActiveAlarms { get; } = [];

    /// <summary>전체 알람 이력 (최근 500건, 최신 순)</summary>
    public ObservableCollection<AlarmRecord> AlarmHistory { get; } = [];

    // §3 ─ 통계 프로퍼티 ──────────────────────────────────────
    public int TotalFired   => AlarmHistory.Count;
    public int ActiveCount  => ActiveAlarms.Count;
    public int UnackedCount => ActiveAlarms.Count(a => a.State == AlarmState.Fired);

    // §4 ─ 알람 발생 처리 ─────────────────────────────────────

    /// <summary>
    /// 새 알람을 발생시킵니다.
    /// MonitorEngine 이 AbstractDetector 이상 감지 시 호출합니다.
    /// </summary>
    public async Task FireAsync(AlarmRecord alarm)
    {
        await _lock.WaitAsync();
        try
        {
            // 동일 DetectorId + 미복귀 알람이 이미 있으면 레벨 갱신만
            var existing = ActiveAlarms.FirstOrDefault(
                a => a.DetectorId == alarm.DetectorId
                  && a.State != AlarmState.Cleared);

            if (existing is not null)
            {
                // 이미 활성 알람 존재 — 레벨만 업데이트 (중복 발생 방지)
                LogManager.Instance.Debug(LogSrc,
                    $"알람 중복 발생 무시: {alarm.DetectorId} [{alarm.Level}]");
                return;
            }

            // UI 스레드에서 컬렉션 수정
            await _DispatchAsync(() =>
            {
                ActiveAlarms.Insert(0, alarm);
                AlarmHistory.Insert(0, alarm);
                while (AlarmHistory.Count > MaxHistory)
                    AlarmHistory.RemoveAt(AlarmHistory.Count - 1);
            });

            LogManager.Instance.Warn(LogSrc,
                $"[FIRE] {alarm.DetectorId} | {alarm.Level} | {alarm.Message}");

            EventBus.Instance.Publish(new AlarmFiredEvent(alarm));
        }
        finally
        {
            _lock.Release();
        }
    }

    // §5 ─ ACK 처리 ───────────────────────────────────────────

    /// <summary>알람을 확인(ACK) 처리합니다.</summary>
    public async Task AckAsync(string alarmId, string ackedBy = "Operator")
    {
        await _lock.WaitAsync();
        try
        {
            var alarm = ActiveAlarms.FirstOrDefault(
                a => a.AlarmId == alarmId && a.State == AlarmState.Fired);

            if (alarm is null)
            {
                LogManager.Instance.Warn(LogSrc,
                    $"ACK 대상 알람 없음: {alarmId}");
                return;
            }

            alarm.State   = AlarmState.Acked;
            alarm.AckedAt = DateTime.Now;
            alarm.AckedBy = ackedBy;

            await _DispatchAsync(() =>
            {
                // ObservableCollection 변경 알림 (record이므로 직접 수정 후 Replace)
                int idx = ActiveAlarms.IndexOf(alarm);
                if (idx >= 0) ActiveAlarms[idx] = alarm;

                int hidx = AlarmHistory.IndexOf(alarm);
                if (hidx >= 0) AlarmHistory[hidx] = alarm;
            });

            LogManager.Instance.Info(LogSrc,
                $"[ACK] {alarm.DetectorId} by {ackedBy}");

            EventBus.Instance.Publish(new AlarmAckedEvent(alarmId, ackedBy));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>모든 미확인 알람을 일괄 ACK 처리합니다.</summary>
    public async Task AckAllAsync(string ackedBy = "Operator")
    {
        var unacked = ActiveAlarms
            .Where(a => a.State == AlarmState.Fired)
            .Select(a => a.AlarmId)
            .ToList();

        foreach (var id in unacked)
            await AckAsync(id, ackedBy);
    }

    // §6 ─ 복귀 처리 ──────────────────────────────────────────

    /// <summary>
    /// 감지기 ID 기준으로 알람을 복귀(Cleared) 처리합니다.
    /// AbstractDetector 가 정상 상태 복귀 시 MonitorEngine 이 호출합니다.
    /// </summary>
    public async Task ClearByDetectorAsync(string detectorId)
    {
        await _lock.WaitAsync();
        try
        {
            var alarm = ActiveAlarms.FirstOrDefault(
                a => a.DetectorId == detectorId
                  && a.State != AlarmState.Cleared);

            if (alarm is null) return;

            alarm.State     = AlarmState.Cleared;
            alarm.ClearedAt = DateTime.Now;

            await _DispatchAsync(() =>
            {
                // Active 목록에서 제거
                ActiveAlarms.Remove(alarm);

                // 이력 업데이트
                int hidx = AlarmHistory.IndexOf(alarm);
                if (hidx >= 0) AlarmHistory[hidx] = alarm;
            });

            LogManager.Instance.Info(LogSrc,
                $"[CLEAR] {detectorId} — 복귀 완료");

            EventBus.Instance.Publish(new AlarmClearedEvent(alarm.AlarmId));
        }
        finally
        {
            _lock.Release();
        }
    }

    // §7 ─ 조회 메서드 ────────────────────────────────────────

    /// <summary>특정 태그의 최신 활성 알람을 반환합니다.</summary>
    public AlarmRecord? GetActiveByTag(string tagId)
        => ActiveAlarms.FirstOrDefault(
            a => a.TagId == tagId && a.State != AlarmState.Cleared);

    /// <summary>알람 이력을 태그 기준으로 필터링합니다.</summary>
    public IEnumerable<AlarmRecord> GetHistoryByTag(string tagId)
        => AlarmHistory.Where(a => a.TagId == tagId);

    // §8 ─ 내부 헬퍼 ──────────────────────────────────────────
    private static Task _DispatchAsync(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app is null || app.Dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return app.Dispatcher.InvokeAsync(action).Task;
    }

    // §9 ─ IDisposable ────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _lock.Dispose();
        _disposed = true;
    }
}
