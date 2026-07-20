// ══════════════════════════════════════════════════════════
//  IIoT.HMI · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  HM-Base-0: 생성자만
//  HM-Base-1~2: HmiMainViewModel(DataContext) 주입
//  HM-02: CollectorManageView 주입 (ContentControl 호스트, DI 필요 View 패턴)
//  HM-03: LayoutCanvasView 주입 (레이아웃 편집 탭 호스트)
//  HM-14: AlarmView 주입 (알람 탭 호스트 — 초기 데이터 로드 불필요, Aggregator가
//         CollectorConnectionManager.AlarmChanged 를 자체 구독해 채움)
//  HM-15: LogPanelView 주입 (로그 탭 호스트 — 초기 데이터 로드 불필요, 생성자에서
//         자체적으로 LogManager.Instance.LogAdded 를 구독해 채움)
//  C-SET-01 후속 (HMI): SettingsView 주입 (환경설정 탭 호스트 — 자체 Loaded 에서
//         hmi.json 을 다시 로드해 채움, CollectorManageView 와 동일 패턴)
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Views.Alarm;
using IIoT.HMI.Views.CollectorManage;
using IIoT.HMI.Views.LayoutCanvas;
using IIoT.HMI.Views.Log;
using IIoT.HMI.Views.Settings;
using System.Windows;

namespace IIoT.HMI;

public partial class MainWindow : Window
{
    // §1 ─ 생성자 ─────────────────────────────────────────────

    public MainWindow(HmiMainViewModel vm,
                      CollectorManageView collectorManageView,
                      LayoutCanvasView    layoutCanvasView,
                      AlarmView           alarmView,
                      LogPanelView        logPanelView,
                      SettingsView        settingsView)   // ★ C-SET-01 후속
    {
        InitializeComponent();

        DataContext = vm;

        // ★ HM-02: DI 필요 View → ContentControl + 코드 주입 패턴
        //   (CollectorManageView 자체가 Loaded 이벤트에서 hmi.json 로드를 수행 —
        //   Monitor CollectorManageView.Loaded 패턴과 동일)
        CollectorManageHost.Content = collectorManageView;

        // ★ HM-03: 레이아웃 편집 캔버스 주입 (초기 데이터 로드 불필요 — 메모리 상태만)
        LayoutCanvasHost.Content = layoutCanvasView;

        // ★ HM-14: 알람 탭 주입 (초기 데이터 로드 불필요 — 실시간 이벤트로만 채워짐)
        AlarmHost.Content = alarmView;

        // ★ HM-15: 로그 탭 주입 (초기 데이터 로드 불필요 — 실시간 이벤트로만 채워짐)
        LogHost.Content = logPanelView;

        // ★ C-SET-01 후속: 환경설정 탭 주입 (자체 Loaded 에서 hmi.json 재로드)
        SettingsHost.Content = settingsView;
    }
}
