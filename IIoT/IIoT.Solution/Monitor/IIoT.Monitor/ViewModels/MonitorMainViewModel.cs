// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/MonitorMainViewModel.cs
//  역할: MainWindow DataContext — 탭 전환 상태만 관리
//        (Studio StudioMainViewModel / Collector CollectorMainViewModel과
//         동일한 Base-1 패턴: 실제 탭 내용은 이후 Step에서 View 교체)
//  MN-Base-1: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IIoT.Monitor.ViewModels;

/// <summary>
/// MainWindow 의 DataContext.
/// <para>
/// 탭바(태그현황=0 / 알람=1 / Collector 관리=2 / 로그=3) 전환 상태만 관리한다.
/// 각 탭의 실제 내용(View)은 MN-01(Collector 관리) / MN-02(태그현황) /
/// MN-03(알람) 이후 Step 에서 교체된다.
/// </para>
/// </summary>
public partial class MonitorMainViewModel : ObservableObject
{
    // §1 ─ 탭 상태 ────────────────────────────────────────

    /// <summary>현재 선택된 탭 인덱스 (0=태그현황, 1=알람, 2=Collector관리, 3=로그)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabPlaceholderText))]
    private int _activeTabIndex;

    /// <summary>
    /// 본문 임시 표시 문구.
    /// MN-Base-1 단계에서는 실제 View 없이 안내 문구만 표시한다.
    /// </summary>
    public string TabPlaceholderText => ActiveTabIndex switch
    {
        0 => "[태그현황] 탭 — MN-02에서 구현 예정",
        1 => "[알람] 탭 — MN-03에서 구현 예정",
        2 => "[Collector 관리] 탭 — MN-01에서 구현 예정",
        3 => "[로그] 탭 — 추후 구현 예정",
        _ => "탭 내용 준비 중"
    };

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
