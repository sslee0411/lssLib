// ══════════════════════════════════════════════════════════
//  IIoT.HMI · HmiMainViewModel.cs
//  역할: MainWindow DataContext — 탭 전환 상태 관리
//  HM-Base-1: 신규 — StatusText 만 보유
//  HM-Base-2: 탭 상태(ActiveTabIndex/SwitchTab) 추가
//        탭 인덱스: 0=현황판 1=레이아웃 편집 2=Collector 관리 3=알람 4=로그
//        (Manager MG-04 패턴과 동일 — 메인 VM 은 프로젝트 루트에 고정)
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IIoT.HMI;

/// <summary>
/// MainWindow 의 DataContext.
/// <para>
/// 현재는 탭 전환 상태만 보유한다. 각 탭의 실제 데이터 로드는 해당 탭 View/
/// ViewModel 이 자체적으로 담당한다(Monitor CollectorManageView.Loaded 패턴과 동일 —
/// 메인 VM 이 하위 탭의 초기화까지 오케스트레이션하지 않는다).
/// </para>
/// </summary>
public partial class HmiMainViewModel : ObservableObject
{
    // §1 ─ 관찰 속성 ─────────────────────────────────────────

    /// <summary>하단 상태 문구</summary>
    [ObservableProperty]
    private string _statusText = "IIoT.HMI 시작됨";

    /// <summary>
    /// ★ HM-Base-2: 현재 선택된 탭 인덱스.
    /// 0=현황판 1=레이아웃 편집 2=Collector 관리 3=알람 4=로그
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardTab))]
    [NotifyPropertyChangedFor(nameof(IsLayoutTab))]
    [NotifyPropertyChangedFor(nameof(IsCollectorTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsLogTab))]
    private int _activeTabIndex;

    public bool IsDashboardTab => ActiveTabIndex == 0;
    public bool IsLayoutTab    => ActiveTabIndex == 1;
    public bool IsCollectorTab => ActiveTabIndex == 2;
    public bool IsAlarmTab     => ActiveTabIndex == 3;
    public bool IsLogTab       => ActiveTabIndex == 4;

    // §2 ─ 명령 ──────────────────────────────────────────────

    /// <summary>탭 전환 (규칙: CommandParameter는 string → ViewModel int.TryParse 처리)</summary>
    [RelayCommand]
    private void SwitchTab(string index)
    {
        if (int.TryParse(index, out var i))
            ActiveTabIndex = i;
    }
}
