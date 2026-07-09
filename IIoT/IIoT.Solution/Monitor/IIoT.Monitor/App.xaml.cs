// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · App.xaml.cs
//  역할: 애플리케이션 진입점
//        ① 테마 복원 (창 표시 전 — 색상 준비)
//        ② LogManager.Instance.Start() (DI 빌드 전 필수)
//        ③ DI 서비스 구성 (_ConfigureServices)
//        ④ 창 생성 (AddSingleton — Transient 사용 시 이중 창 버그)
//
//  MN-Base-0: 최초 생성 — 빈 창 + 테마만 적용.
//  MN-Base-1: MonitorMainViewModel DI 등록 추가.
//  MN-01: MonitorSettingsLoader / CollectorManageViewModel / CollectorManageView
//         DI 등록 추가. monitor.json 로드는 CollectorManageView.Loaded 에서 수행되므로
//         여기서는 등록만 한다 (Studio-P04와 동일하게 "요구하는 곳"+"등록하는 곳" 세트 확인).
//         CommandQueue.Instance.Start() 는 EventBus 사용 시점(MN-01B)에 추가 예정.
//  MN-01B: CollectorConnectionManager DI 등록 추가 (CollectorId↔HubConnection 관리자)
//  MN-02: LiveTagAggregator / LiveTagViewModel / LiveTagView DI 등록 추가
//  MN-02B: DashboardViewModel / DashboardView DI 등록 추가 (대시보드 탭)
//  MN-03: AlarmAggregator / AlarmViewModel / AlarmView DI 등록 추가
//  MN-04: DetectorHost DI 등록 + 예시 Detector/Responder 부트스트랩 등록
//  MN-05: MonitorHostService DI 등록 (자체 SignalR Hub — 웹 브라우저 연동).
//         실제 시작(StartAsync)은 MainWindow.Loaded 에서 수행 (Collector의
//         win.Loaded 패턴과 동일 — 창이 뜬 뒤 웹 서버 기동)
//  MN-06: ChartViewModel / ChartView DI 등록 추가 (실시간 차트 탭)
//  FIX(2026-07-08): 앱 종료 시 CollectorConnectionManager(N개 HubConnection/
//                   HttpClient)가 정리되지 않아 프로세스가 정상 종료되지
//                   않던 문제 수정 — OnExit에서 함께 Dispose하도록 추가
//  생성: 2026-07-07 / 수정: 2026-07-08 (종료 처리 수정)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Core.Connection;
using IIoT.Monitor.Core.Detection;
using IIoT.Monitor.Core.Detection.Detectors;
using IIoT.Monitor.Core.Detection.Responders;
using IIoT.Monitor.SignalR;
using IIoT.Monitor.ViewModels;
using IIoT.Monitor.Views.Alarm;
using IIoT.Monitor.Views.Chart;
using IIoT.Monitor.Views.CollectorManage;
using IIoT.Monitor.Views.Dashboard;
using IIoT.Monitor.Views.LiveTag;
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.Monitor;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private ThemeSettingsService? _themeSettings;
    private IServiceProvider?     _services;

    // §2 ─ 시작 ───────────────────────────────────────────────

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① 테마 (가장 먼저 — 창 표시 전 색상 준비)
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        // ② LogManager 시작 (반드시 DI 빌드 전에 호출)
        LogManager.Instance.Start(new LogConfig
        {
            LogRootPath         = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log"),
            ValidDays           = 30,
            FileFormat          = LogFileFormat.Both,
            MinimumLevel        = LogLevel.Debug,
            MinimumConsoleLevel = LogLevel.Info,
            MaxDisplayCount     = 2000
        });

        LogManager.Instance.Info("App", "IIoT.Monitor 시작");

        // ③ DI 빌드
        _services = _ConfigureServices();

        // ★ MN-04: 예시 Detector/Responder 등록
        //   실제 운영 시에는 여기에 프로젝트에 맞는 Detector/Responder 를 자유롭게
        //   추가하면 된다 (AbstractDetector 상속 + IDetectionResponder 구현).
        //   TagId "T001"은 예시값 — 실제 감시할 Tag ID로 교체해서 사용할 것.
        var detectorHost = _services.GetRequiredService<DetectorHost>();
        detectorHost.RegisterResponder(new LogResponder());
        detectorHost.RegisterDetector(new RateOfChangeDetector(tagId: "T001", maxRatePerSec: 5.0));

        // ④ 창 생성 및 표시
        _services.GetRequiredService<MainWindow>().Show();
    }

    // §3 ─ 종료 ───────────────────────────────────────────────

    protected override void OnExit(ExitEventArgs e)
    {
        // ★ FIX(2026-07-08): CollectorConnectionManager 가 종료 시 전혀 정리되지
        //   않아 N개 Collector에 대한 SignalR HubConnection/HttpClient가 살아있는
        //   채로 남아 프로세스가 정상 종료되지 않던 문제 수정.
        //   MonitorHostService 와 동일하게 짧은 타임아웃 내 블로킹 대기로 정리한다.
        _services?.GetService<CollectorConnectionManager>()?.DisposeAsync().AsTask().GetAwaiter().GetResult();

        // ★ MN-05: 웹 Hub 정상 종료 (동기 컨텍스트이므로 짧은 타임아웃 내 블로킹 대기)
        _services?.GetService<MonitorHostService>()?.DisposeAsync().AsTask().GetAwaiter().GetResult();

        _themeSettings?.Dispose();   // 이벤트 구독 해제 필수
        LogManager.Instance.Info("App", "IIoT.Monitor 종료");
        base.OnExit(e);
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ★ MN-02 신규: 전체 Collector 통합 실시간 Tag 집계기 + 화면
        //   (CollectorConnectionManager 보다 먼저 등록 — 생성자 의존성)
        services.AddSingleton<LiveTagAggregator>();
        services.AddSingleton<LiveTagViewModel>();
        services.AddSingleton<LiveTagView>();

        // ★ MN-03 신규: 전체 Collector 통합 실시간 알람 집계기 + 화면
        services.AddSingleton<AlarmAggregator>();
        services.AddSingleton<AlarmViewModel>();
        services.AddSingleton<AlarmView>();

        // ★ MN-04 신규: AbstractDetector 커스텀 확장 호스트
        //   (LiveTagAggregator 의존 — 위에서 먼저 등록됨)
        services.AddSingleton<DetectorHost>();

        // ★ MN-01B 신규: Collector 연결 관리자 (CollectorId ↔ HubConnection)
        services.AddSingleton<CollectorConnectionManager>();

        // ★ MN-01 신규: monitor.json 설정 + Collector 관리 화면
        services.AddSingleton<MonitorSettingsLoader>();
        services.AddSingleton<CollectorManageViewModel>();
        services.AddSingleton<CollectorManageView>();

        // ★ MN-Base-1: MainWindow DataContext
        services.AddSingleton<MonitorMainViewModel>();

        // ★ MN-02B 신규: 대시보드 탭 (CollectorManageViewModel/LiveTagAggregator 의존)
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DashboardView>();

        // ★ MN-05 신규: Monitor 자체 SignalR Hub (웹 브라우저 연동)
        //   (LiveTagAggregator/AlarmAggregator/MonitorSettingsLoader 의존 — 위에서 먼저 등록됨)
        services.AddSingleton<MonitorHostService>();

        // ★ MN-06 신규: 실시간 차트 (CollectorManageViewModel/LiveTagAggregator 의존)
        services.AddSingleton<ChartViewModel>();
        services.AddSingleton<ChartView>();

        // ★ 반드시 AddSingleton (Transient → 이중 창 버그)
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<MonitorMainViewModel>(),
                sp.GetRequiredService<CollectorManageView>(),
                sp.GetRequiredService<LiveTagView>(),
                sp.GetRequiredService<AlarmView>(),
                sp.GetRequiredService<DashboardView>(),
                sp.GetRequiredService<ChartView>(),
                sp.GetRequiredService<MonitorHostService>()));

        return services.BuildServiceProvider();
    }
}
