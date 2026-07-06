// ══════════════════════════════════════════════════════════
//  IIoT.Collector · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  Col-Base-0: DI 생성자 + DataContext 주입
//  C-04: StatusView 를 DI 로 생성하여 StatusViewHost 에 주입
//        StatusViewModel.Initialize() 는 ConfigLoader 로드 완료 후 호출되어야 하므로
//        App.xaml.cs win.Loaded 핸들러에서 LoadAsync 이후 시점에 맞춰 별도 호출
//  생성: 2026-06-29 / 수정: 2026-06-29
//  규칙:
//    ★ 기본 생성자(매개변수 없음) 절대 사용 금지
//      — App.xaml.cs 의 AddSingleton 팩토리와 충돌
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Views.Alarm;
using IIoT.Collector.Views.Flow;
using IIoT.Collector.Views.Trend;
using IIoT.Collector.Views.Status;
using System.Windows;

namespace IIoT.Collector;

public partial class MainWindow : Window
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly MainViewModel _vm;

    /// <summary>
    /// StatusView 인스턴스 (App.xaml.cs 에서 ConfigLoader.LoadAsync 완료 후
    /// StatusViewModel.Initialize() 를 호출할 수 있도록 외부에 노출).
    /// </summary>
    public StatusView StatusView { get; }
    public AlarmView  AlarmView  { get; }
    public FlowView   FlowView   { get; }
    public TrendView  TrendView  { get; }

    // §2 ─ 생성자 ─────────────────────────────────────────────

    /// <summary>
    /// DI 컨테이너에서 ViewModel 주입받아 DataContext 설정.
    /// ★ 기본 생성자 절대 금지 — App.xaml.cs AddSingleton 팩토리 충돌
    /// </summary>
    public MainWindow(MainViewModel vm, StatusView statusView, AlarmView alarmView, FlowView flowView, TrendView trendView)
    {
        _vm = vm;
        StatusView = statusView;
        AlarmView  = alarmView;
        FlowView   = flowView;
        TrendView  = trendView;

        InitializeComponent();
        DataContext = vm;

        // ★ C-04: StatusView 코드 주입
        StatusViewHost.Content = StatusView;
        // ★ C-06: AlarmView 코드 주입
        AlarmViewHost.Content  = AlarmView;
        // ★ C-09: FlowView 코드 주입
        FlowViewHost.Content   = FlowView;
        // ★ C-13: TrendView 코드 주입
        TrendViewHost.Content  = TrendView;
    }
}
