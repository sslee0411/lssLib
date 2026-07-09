// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/MonitorMainViewModel.cs
//  역할: MainWindow DataContext — 탭 전환 상태만 관리
//  MN-Base-1: 신규
//  MN-01: IsCollectorTab / ShowPlaceholder 추가
//  MN-02: IsTagTab 추가
//  MN-02B: IsDashboardTab 추가 — 탭 5개로 확장
//  MN-03: IsAlarmTab 추가
//  MN-06: IsChartTab 추가 — 탭 6개로 확장 (태그현황/알람/Collector관리/
//         로그/대시보드/차트). 실제 View가 없는 탭은 여전히 3(로그) 뿐.
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-06)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IIoT.Monitor.ViewModels;

/// <summary>
/// MainWindow 의 DataContext.
/// <para>
/// 탭바(태그현황=0 / 알람=1 / Collector 관리=2 / 로그=3 / 대시보드=4 / 차트=5) 전환
/// 상태를 관리한다. 로그(추후) 탭만 아직 임시 안내 문구를 표시한다.
/// </para>
/// </summary>
public partial class MonitorMainViewModel : ObservableObject
{
    // §1 ─ 탭 상태 ────────────────────────────────────────

    /// <summary>현재 선택된 탭 인덱스 (0=태그현황,1=알람,2=Collector관리,3=로그,4=대시보드,5=차트)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabPlaceholderText))]
    [NotifyPropertyChangedFor(nameof(IsTagTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsCollectorTab))]
    [NotifyPropertyChangedFor(nameof(IsDashboardTab))]
    [NotifyPropertyChangedFor(nameof(IsChartTab))]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    private int _activeTabIndex;

    /// <summary>본문 임시 표시 문구. 실제 View 가 아직 없는 탭(3=로그)에서만 사용된다.</summary>
    public string TabPlaceholderText => "[로그] 탭 — 추후 구현 예정";

    /// <summary>[태그현황] 탭(0) 선택 여부. true 면 TagStatusHost 표시</summary>
    public bool IsTagTab => ActiveTabIndex == 0;

    /// <summary>[알람] 탭(1) 선택 여부. true 면 AlarmHost 표시</summary>
    public bool IsAlarmTab => ActiveTabIndex == 1;

    /// <summary>[Collector 관리] 탭(2) 선택 여부. true 면 CollectorManageHost 표시</summary>
    public bool IsCollectorTab => ActiveTabIndex == 2;

    /// <summary>[대시보드] 탭(4) 선택 여부. true 면 DashboardHost 표시</summary>
    public bool IsDashboardTab => ActiveTabIndex == 4;

    /// <summary>★ MN-06 신규: [차트] 탭(5) 선택 여부. true 면 ChartHost 표시</summary>
    public bool IsChartTab => ActiveTabIndex == 5;

    /// <summary>임시 안내 문구(TextBlock) 표시 여부. 탭 3(로그)에서만 true</summary>
    public bool ShowPlaceholder => ActiveTabIndex == 3;

    // §2 ─ 명령 ───────────────────────────────────────────

    /// <summary>
    /// 탭바 버튼 클릭 시 호출.
    /// CommandParameter 는 XAML 상 항상 string 이므로 반드시 TryParse 사용.
    /// </summary>
    [RelayCommand]
    private void SwitchTab(string index)
    {
        if (int.TryParse(index, out var i))
            ActiveTabIndex = i;
    }
}
