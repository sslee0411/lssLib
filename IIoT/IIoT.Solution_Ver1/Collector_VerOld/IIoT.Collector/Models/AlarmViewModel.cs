// ══════════════════════════════════════════════════════════
//  IIoT.Collector · ViewModels/AlarmViewModel.cs
//  역할: 알람 탭(AlarmView) ViewModel
//        AlarmChangedEvent 구독 → 활성 알람 목록 + 이력 관리
//  C-06: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Collector.Core.Engine;
using IIoT.Collector.Core.Events;
using IIoT.Collector.Models;
using lssLib.Messaging;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;

namespace IIoT.Collector.ViewModels;

/// <summary>
/// 알람 탭 ViewModel (DI 싱글턴).
/// <para>
/// EventBus 의 AlarmChangedEvent 를 구독하여<br/>
/// 활성 알람(<see cref="ActiveAlarms"/>) 과 이력(<see cref="AlarmHistory"/>) 을 관리한다.
/// </para>
/// </summary>
public partial class AlarmViewModel : ObservableObject, IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly AlarmStateManager _alarmManager;
    private IDisposable?               _alarmSub;

    private const int MaxHistory = 500;

    // §2 ─ 바인딩 컬렉션 ───────────────────────────────────

    /// <summary>현재 활성(발생·ACK) 알람 목록 — AlarmKey 기준 중복 없음</summary>
    public ObservableCollection<AlarmRowViewModel> ActiveAlarms { get; } = new();

    /// <summary>전체 알람 이력 (발생→ACK→복귀 순서, 최대 500건)</summary>
    public ObservableCollection<AlarmRowViewModel> AlarmHistory { get; } = new();

    // §3 ─ 통계 ────────────────────────────────────────────

    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _ackedCount;

    // §4 ─ 선택 ────────────────────────────────────────────

    [ObservableProperty]
    private AlarmRowViewModel? _selectedAlarm;

    // §5 ─ 생성자 ──────────────────────────────────────────

    public AlarmViewModel(AlarmStateManager alarmManager)
    {
        _alarmManager = alarmManager;
        BindingOperations.EnableCollectionSynchronization(ActiveAlarms, new object());
        BindingOperations.EnableCollectionSynchronization(AlarmHistory, new object());
    }

    // §6 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// EventBus 구독 시작. App.xaml.cs 에서 AlarmStateManager.Initialize() 직후 호출.
    /// </summary>
    public void Initialize()
    {
        _alarmSub?.Dispose();
        _alarmSub = EventBus.Instance.Subscribe<AlarmChangedEvent>(_OnAlarmChanged);
    }

    // §7 ─ 이벤트 핸들러 ──────────────────────────────────

    private void _OnAlarmChanged(AlarmChangedEvent e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            // 이력 항목 추가
            var histRow = new AlarmRowViewModel(e);
            AlarmHistory.Insert(0, histRow);
            if (AlarmHistory.Count > MaxHistory)
                AlarmHistory.RemoveAt(AlarmHistory.Count - 1);

            // 활성 목록 갱신
            var existing = ActiveAlarms.FirstOrDefault(a => a.AlarmKey == e.AlarmKey);

            if (e.Status == AlarmStatus.Active)
            {
                if (existing is null)
                    ActiveAlarms.Insert(0, new AlarmRowViewModel(e));
                else
                    existing.UpdateStatus(e);
            }
            else if (e.Status == AlarmStatus.Acked)
            {
                existing?.UpdateStatus(e);
            }
            else // Recovered
            {
                if (existing is not null)
                    ActiveAlarms.Remove(existing);
            }

            // 통계 갱신
            ActiveCount = ActiveAlarms.Count(a => a.Status == AlarmStatus.Active);
            AckedCount  = ActiveAlarms.Count(a => a.Status == AlarmStatus.Acked);
        });
    }

    // §8 ─ 커맨드 ─────────────────────────────────────────

    /// <summary>선택한 알람 ACK 처리</summary>
    [RelayCommand]
    private void AckAlarm(AlarmRowViewModel? row)
    {
        if (row is null) return;
        _alarmManager.Acknowledge(row.AlarmKey);
    }

    /// <summary>전체 활성 알람 ACK 처리</summary>
    [RelayCommand]
    private void AckAll()
    {
        foreach (var alarm in ActiveAlarms.Where(a => a.Status == AlarmStatus.Active).ToList())
            _alarmManager.Acknowledge(alarm.AlarmKey);
    }

    // §9 ─ 정리 ────────────────────────────────────────────

    public void Dispose()
    {
        _alarmSub?.Dispose();
    }
}
