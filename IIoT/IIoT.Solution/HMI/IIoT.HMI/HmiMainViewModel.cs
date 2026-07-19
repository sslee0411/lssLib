// ══════════════════════════════════════════════════════════
//  IIoT.HMI · HmiMainViewModel.cs
//  역할: MainWindow DataContext — 탭 전환 상태 관리
//  HM-Base-1: 신규 — StatusText 만 보유
//  HM-Base-2: 탭 상태(ActiveTabIndex/SwitchTab) 추가
//        탭 인덱스: 0=현황판 1=레이아웃 편집 2=Collector 관리 3=알람 4=로그
//        (Manager MG-04 패턴과 동일 — 메인 VM 은 프로젝트 루트에 고정)
//  HM-13(정리): 별도 "현황판" 탭 제거 — HM-03~12 가 전부 [레이아웃 편집] 탭
//        (카드 배치·실시간 값·Z순서·다중 화면·알람 배지·ForceWrite)에 구현되어
//        레이아웃 편집 탭이 이미 사실상 생산현황판 역할을 하고 있었음. 별도로
//        비워둔 "현황판" placeholder 탭은 중복이라 사용자 확인 후 제거함.
//        탭 인덱스 재정렬: 0=레이아웃 편집 1=Collector 관리 2=알람 3=로그
//        (IsDashboardTab 제거, 나머지 Is*Tab 은 번호만 1씩 당김)
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
    /// ★ HM-13(정리): "현황판" 탭 제거로 재정렬 — 0=레이아웃 편집 1=Collector 관리
    /// 2=알람 3=로그 (레이아웃 편집 탭이 이미 생산현황판 역할을 겸함)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLayoutTab))]
    [NotifyPropertyChangedFor(nameof(IsCollectorTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsLogTab))]
    private int _activeTabIndex;

    public bool IsLayoutTab    => ActiveTabIndex == 0;
    public bool IsCollectorTab => ActiveTabIndex == 1;
    public bool IsAlarmTab     => ActiveTabIndex == 2;
    public bool IsLogTab       => ActiveTabIndex == 3;

    // §2 ─ 명령 ──────────────────────────────────────────────

    /// <summary>탭 전환 (규칙: CommandParameter는 string → ViewModel int.TryParse 처리)</summary>
    [RelayCommand]
    private void SwitchTab(string index)
    {
        if (int.TryParse(index, out var i))
            ActiveTabIndex = i;
    }
}
