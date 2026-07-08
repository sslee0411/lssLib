// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/MonitorMainViewModel.cs
//  역할: MainWindow DataContext — 탭 전환 상태만 관리
//  MN-Base-1: 신규
//  MN-01: IsCollectorTab / ShowPlaceholder 추가
//  MN-02: IsTagTab 추가 — [태그현황] 탭(0)도 실제 View(LiveTagView) 표시
//         ShowPlaceholder 는 이제 탭 1(알람)/3(로그)에서만 true
//  생성: 2026-07-07 / 수정: 2026-07-07 (MN-02)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IIoT.Monitor.ViewModels;

/// <summary>
/// MainWindow 의 DataContext.
/// <para>
/// 탭바(태그현황=0 / 알람=1 / Collector 관리=2 / 로그=3) 전환 상태만 관리한다.
/// 태그현황(MN-02)·Collector 관리(MN-01) 탭은 실제 View 가 있고,
/// 알람(MN-03)·로그(추후) 탭은 아직 임시 안내 문구를 표시한다.
/// </para>
/// </summary>
public partial class MonitorMainViewModel : ObservableObject
{
    // §1 ─ 탭 상태 ────────────────────────────────────────

    /// <summary>현재 선택된 탭 인덱스 (0=태그현황, 1=알람, 2=Collector관리, 3=로그)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabPlaceholderText))]
    [NotifyPropertyChangedFor(nameof(IsTagTab))]
    [NotifyPropertyChangedFor(nameof(IsCollectorTab))]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    private int _activeTabIndex;

    /// <summary>
    /// 본문 임시 표시 문구.
    /// 실제 View 가 아직 없는 탭(1=알람, 3=로그)에서만 사용된다.
    /// </summary>
    public string TabPlaceholderText => ActiveTabIndex switch
    {
        1 => "[알람] 탭 — MN-03에서 구현 예정",
        3 => "[로그] 탭 — 추후 구현 예정",
        _ => "탭 내용 준비 중"
    };

    /// <summary>★ MN-02 신규: [태그현황] 탭(0) 선택 여부. true 면 TagStatusHost 표시</summary>
    public bool IsTagTab => ActiveTabIndex == 0;

    /// <summary>[Collector 관리] 탭(2) 선택 여부. true 면 CollectorManageHost 표시</summary>
    public bool IsCollectorTab => ActiveTabIndex == 2;

    /// <summary>임시 안내 문구(TextBlock) 표시 여부. 탭 1/3에서만 true</summary>
    public bool ShowPlaceholder => ActiveTabIndex is 1 or 3;

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
