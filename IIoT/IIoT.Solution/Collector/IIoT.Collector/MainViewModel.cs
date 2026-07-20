// ══════════════════════════════════════════════════════════
//  IIoT.Collector · MainViewModel.cs
//  역할: 메인 창 탭 전환 + 로그 패널 토글 ViewModel
//  Col-Base-0: 신규
//  C-13: 트렌드 탭(3) 추가 — 로그 토글은 별도 버튼으로 유지
//  생성: 2026-06-29 / 수정: 2026-07-01
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IIoT.Collector;

/// <summary>
/// 메인 창 ViewModel (DI 싱글턴).
/// 탭: 0=수집현황 / 1=알람 / 2=수집흐름 / 3=트렌드(C-13)
/// 로그 패널: 별도 토글 버튼 (탭 인덱스와 독립)
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // §1 ─ 탭 인덱스 ──────────────────────────────────────

    /// <summary>
    /// 현재 활성 탭 인덱스.
    /// 0=수집현황 / 1=알람 / 2=수집흐름 / 3=트렌드(C-13)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsFlowTab))]
    [NotifyPropertyChangedFor(nameof(IsTrendTab))]
    [NotifyPropertyChangedFor(nameof(IsDeviceTab))]   // ★ C-EX-01-6 신규
    [NotifyPropertyChangedFor(nameof(IsSettingsTab))] // ★ C-SET-01 신규
    private int _activeTabIndex;

    // §2 ─ 탭 가시성 ──────────────────────────────────────

    /// <summary>수집 현황 탭 (0)</summary>
    public bool IsStatusTab => ActiveTabIndex == 0;

    /// <summary>알람 탭 (1)</summary>
    public bool IsAlarmTab  => ActiveTabIndex == 1;

    /// <summary>수집 흐름 탭 (2)</summary>
    public bool IsFlowTab   => ActiveTabIndex == 2;

    /// <summary>수집 이력 조회 탭 (3) — C-13</summary>
    public bool IsTrendTab  => ActiveTabIndex == 3;

    /// <summary>장비 탭 (4) — C-EX-01-6 신규</summary>
    public bool IsDeviceTab => ActiveTabIndex == 4;

    /// <summary>환경설정 탭 (5) — C-SET-01 신규</summary>
    public bool IsSettingsTab => ActiveTabIndex == 5;

    // §3 ─ 로그 패널 ──────────────────────────────────────

    /// <summary>
    /// 하단 로그 패널 표시 여부.
    /// "📋 로그" 버튼 클릭 → 토글 (탭 인덱스와 독립).
    /// Studio-P04 LogPanelVisible 와 동일 패턴.
    /// </summary>
    [ObservableProperty]
    private bool _logPanelVisible = false;

    // §4 ─ 커맨드 ─────────────────────────────────────────

    /// <summary>
    /// 탭 전환 커맨드.
    /// XAML CommandParameter 는 string → int.TryParse.
    /// "L" → LogPanelVisible 토글 (탭 전환 없음).
    /// </summary>
    [RelayCommand]
    private void SwitchTab(string tabParam)
    {
        // ★ 로그 버튼 → 패널 토글 (탭 전환 없음)
        if (tabParam == "L")
        {
            LogPanelVisible = !LogPanelVisible;
            return;
        }

        if (int.TryParse(tabParam, out var idx))
            ActiveTabIndex = idx;
    }
}
