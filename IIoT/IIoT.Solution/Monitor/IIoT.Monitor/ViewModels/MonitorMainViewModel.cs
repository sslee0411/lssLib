// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/MonitorMainViewModel.cs
//  역할: MainWindow DataContext — 탭 전환 + 하단 로그 패널 토글 +
//        헤더 고정 연결상태 요약 배지(MN-EX-04) 상태 관리
//  MN-Base-1: 신규
//  MN-01: IsCollectorTab 추가
//  MN-02: IsTagTab 추가
//  MN-02B: IsDashboardTab 추가
//  MN-03: IsAlarmTab 추가
//  MN-06: IsChartTab 추가
//  변경(2026-07-08): [로그]를 콘텐츠 탭(인덱스 3)에서 "하단 고정 패널 토글"
//         방식으로 전환. 콘텐츠 탭은 5개로 재편성(0=태그현황,1=알람,
//         2=Collector관리,3=대시보드,4=차트), 로그는 SwitchTab("L") 호출 시
//         ActiveTabIndex 변경 없이 LogPanelVisible 만 토글한다.
//  MN-EX-04: ConnectedCollectorCount/TotalCollectorCount 추가 — 헤더에
//         항상 표시되는 "N개 중 M개 연결됨" 배지용 (2초 주기 재계산,
//         DashboardViewModel과 동일한 타이머 패턴)
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-EX-04)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace IIoT.Monitor.ViewModels;

/// <summary>
/// MainWindow 의 DataContext.
/// <para>
/// 콘텐츠 탭(태그현황=0 / 알람=1 / Collector 관리=2 / 대시보드=3 / 차트=4),
/// 하단 로그 패널(탭과 독립적으로 토글), 헤더 고정 연결상태 배지를 관리한다.
/// </para>
/// </summary>
public partial class MonitorMainViewModel : ObservableObject
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly ObservableCollection<Models.CollectorEndpoint> _collectors;
    private readonly DispatcherTimer _badgeTimer;

    // §2 ─ 탭 상태 ────────────────────────────────────────

    /// <summary>현재 선택된 콘텐츠 탭 인덱스 (0=태그현황,1=알람,2=Collector관리,3=대시보드,4=차트)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTagTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsCollectorTab))]
    [NotifyPropertyChangedFor(nameof(IsDashboardTab))]
    [NotifyPropertyChangedFor(nameof(IsChartTab))]
    [NotifyPropertyChangedFor(nameof(IsSettingsTab))]   // ★ C-SET-01 후속
    private int _activeTabIndex;

    public bool IsTagTab       => ActiveTabIndex == 0;
    public bool IsAlarmTab     => ActiveTabIndex == 1;
    public bool IsCollectorTab => ActiveTabIndex == 2;
    public bool IsDashboardTab => ActiveTabIndex == 3;
    public bool IsChartTab     => ActiveTabIndex == 4;

    /// <summary>환경설정 탭 (5) — C-SET-01 후속</summary>
    public bool IsSettingsTab  => ActiveTabIndex == 5;

    // §3 ─ 하단 로그 패널 (탭과 독립) ───────────────────────

    /// <summary>
    /// 하단 로그 패널 표시 여부. "📋 로그" 버튼 클릭 시 토글.
    /// 콘텐츠 탭(ActiveTabIndex)과 독립적이므로, 어떤 탭을 보고 있어도
    /// 로그 패널을 함께 펼쳐둘 수 있다(Studio-P04/Collector와 동일 패턴).
    /// </summary>
    [ObservableProperty]
    private bool _logPanelVisible;

    // §4 ─ 헤더 연결상태 배지 (MN-EX-04) ────────────────────

    [ObservableProperty] private int _connectedCollectorCount;
    [ObservableProperty] private int _totalCollectorCount;

    // §5 ─ 생성자 ──────────────────────────────────────────

    public MonitorMainViewModel(CollectorManageViewModel collectorManageViewModel)
    {
        _collectors = collectorManageViewModel.Collectors;

        _badgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _badgeTimer.Tick += (_, _) => _RecalculateBadge();
        _badgeTimer.Start();

        _collectors.CollectionChanged += (_, _) => _RecalculateBadge();
        _RecalculateBadge();
    }

    private void _RecalculateBadge()
    {
        TotalCollectorCount     = _collectors.Count;
        ConnectedCollectorCount = _collectors.Count(c => c.StatusText == "연결됨");
    }

    // §6 ─ 명령 ───────────────────────────────────────────

    /// <summary>
    /// 탭바 버튼 클릭 시 호출.
    /// CommandParameter 가 "L" 이면 로그 패널 토글(탭 전환 없음),
    /// 그 외에는 숫자로 파싱하여 ActiveTabIndex 를 변경한다.
    /// </summary>
    [RelayCommand]
    private void SwitchTab(string index)
    {
        if (index == "L")
        {
            LogPanelVisible = !LogPanelVisible;
            return;
        }

        if (int.TryParse(index, out var i))
            ActiveTabIndex = i;
    }
}
