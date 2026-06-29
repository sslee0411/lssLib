// ══════════════════════════════════════════════════════════
//  IIoT.Collector · MainViewModel.cs
//  역할: Collector 메인 ViewModel
//  Col-Base-0: 탭 전환 + LogPanelVisible 토글 (최소 구현)
//  Col-Base-1: 수집현황 / 알람 / 로그 탭 View 연결 예정
//  C-01~    : 수집 엔진·상태 프로퍼티 추가 예정
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IIoT.Collector;

public partial class MainViewModel : ObservableObject
{
    // §1 ─ 탭 전환 ────────────────────────────────────────────

    /// <summary>
    /// 현재 활성 탭 인덱스.
    /// 0 = 수집현황, 1 = 알람, 2 = 로그(예정)
    /// 변경 시 IsXxxTab 파생 프로퍼티 전체 갱신.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsFlowTab))]
    [NotifyPropertyChangedFor(nameof(IsLogTab))]
    private int _activeTabIndex;

    // §2 ─ 탭 가시성 ─────────────────────────────────────────

    /// <summary>수집 현황 탭 (0)</summary>
    public bool IsStatusTab => ActiveTabIndex == 0;

    /// <summary>알람 탭 (1)</summary>
    public bool IsAlarmTab  => ActiveTabIndex == 1;

    /// <summary>수집 흐름 탭 (2)</summary>
    public bool IsFlowTab   => ActiveTabIndex == 2;

    /// <summary>
    /// 로그 탭 (3) — Studio 패턴과 동일하게 탭 인덱스 비교용.
    /// 실제 로그 패널은 LogPanelVisible 로 토글.
    /// </summary>
    public bool IsLogTab    => ActiveTabIndex == 3;

    // §3 ─ 로그 패널 ─────────────────────────────────────────

    /// <summary>
    /// 하단 로그 패널 표시 여부.
    /// "📋 로그" 버튼 클릭 → SwitchTab("3") → 토글.
    /// Studio-P04 LogPanelVisible 와 동일 패턴.
    /// </summary>
    [ObservableProperty]
    private bool _logPanelVisible = false;

    // §4 ─ 커맨드 ─────────────────────────────────────────────

    /// <summary>
    /// 탭 전환 커맨드.
    /// XAML CommandParameter 는 string → int.TryParse 필수.
    /// "3" (로그 버튼) → LogPanelVisible 토글 (탭 전환 없음).
    /// </summary>
    [RelayCommand]
    private void SwitchTab(string tabParam)
    {
        if (!int.TryParse(tabParam, out var idx)) return;

        // ★ 로그 버튼(3) → 패널 토글 (탭 ActiveTabIndex 변경 없음)
        if (idx == 3)
        {
            LogPanelVisible = !LogPanelVisible;
            return;
        }

        ActiveTabIndex = idx;
    }
}
