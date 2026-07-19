// ══════════════════════════════════════════════════════════
//  IIoT.HMI · App.xaml.cs
//  역할: 애플리케이션 진입점
//        ① 테마 복원 (창 표시 전 — 색상 준비)
//        ② LogManager.Instance.Start() (DI 빌드 전 필수)
//        ③ 헬스체크 응답 서버 시작 (Manager 가 핑을 보냄)
//        ④ DI 서비스 구성 (_ConfigureServices)
//        ⑤ 창 생성 (AddSingleton — Transient 사용 시 이중 창 버그)
//
//  HM-Base-0: 최초 생성 — 빈 창 + 테마만 적용.
//  HM-Base-1~2: HmiMainViewModel DI 등록 추가.
//  HM-01: HmiSettingsLoader / CollectorConnectionManager DI 등록 추가
//         (hmi.json 로드는 CollectorManageView.Loaded 에서 수행 —
//          Monitor MN-01 과 동일하게 "요구하는 곳"+"등록하는 곳" 세트로 확인)
//  HM-02: CollectorManageViewModel / CollectorManageView DI 등록 추가
//  HM-03: LayoutCanvasViewModel / LayoutCanvasView DI 등록 추가 (레이아웃 편집 탭)
//  HM-07: HmiLayoutLoader DI 등록 추가 (LayoutCanvasViewModel 보다 먼저 —
//         생성자 의존성. hmi-layout.json 로드는 LayoutCanvasView.Loaded 에서 수행)
//  HM-11: HmiWebHostService DI 등록 추가 — MainWindow.Show() 후 win.Loaded 에서
//         시작(Collector App.xaml.cs 의 "win.Loaded 오케스트레이션" 패턴 최초 도입 —
//         이전까지 HMI 는 각 View 가 각자 Loaded 에서 독립적으로 초기화했으나,
//         웹 서버 시작은 특정 View 에 속하지 않으므로 App 레벨에서 직접 기동한다).
//         OnExit 에도 CollectorConnectionManager 와 동일한 5초 타임아웃 정리 추가.
//  HM-14: AlarmAggregator / AlarmViewModel / AlarmView DI 등록 추가 (알람 탭)
//  HM-15: LogPanelView DI 등록 추가 (로그 탭 — ViewModel 없음, 자체 완결형 View)
//  HM-16: AlarmHistoryService DI 등록 추가 — win.Loaded 에서 SQLite 이력 DB
//         초기화(HM-11 웹 서버와 동일한 "win.Loaded 오케스트레이션" 패턴),
//         AlarmAggregator.AlarmRecorded 구독은 DI 빌드 직후(Show 전)에 연결.
//         OnExit 에도 동일한 5초 타임아웃 정리 추가.
//  HM-19: ShutdownMode = OnMainWindowClose 명시 추가 — 다중 모니터 지원으로
//         Owner 없는 독립 보조 창(SecondaryDisplayWindow)이 열릴 수 있게 됐는데,
//         WPF 기본값(OnLastWindowClose)을 그대로 두면 메인 창을 닫아도 보조
//         창이 남아있는 한 프로세스가 종료되지 않는다. 메인 창이 닫히면 보조
//         창도 함께 정리되도록 명시적으로 고정.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.Contracts.Health;
using IIoT.HMI.Core.Aggregation;
using IIoT.HMI.Core.Config;
using IIoT.HMI.Core.Connection;
using IIoT.HMI.Core.Storage;
using IIoT.HMI.Core.Web;
using IIoT.HMI.ViewModels;
using IIoT.HMI.Views.Alarm;
using IIoT.HMI.Views.CollectorManage;
using IIoT.HMI.Views.LayoutCanvas;
using IIoT.HMI.Views.Log;
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.HMI;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private ThemeSettingsService? _themeSettings;
    private IServiceProvider?     _services;

    /// <summary>Manager 헬스체크 응답 서버 (NamedPipe 핑/퐁)</summary>
    private HealthPipeServer?     _healthServer;

    // §2 ─ 시작 ───────────────────────────────────────────────

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ★ HM-19: 메인 창이 닫히면 다중 모니터 보조 창(Owner 없음)도 함께
        //   닫히도록 명시 고정 — WPF 기본값(OnLastWindowClose)이면 보조 창이
        //   열려 있는 한 프로세스가 종료되지 않는다.
        ShutdownMode = ShutdownMode.OnMainWindowClose;

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

        LogManager.Instance.Info("App", "IIoT.HMI 시작");

        // ③ 헬스체크 응답 서버 시작 (Manager 가 핑을 보냄 — Studio/Collector/Monitor 와 동일 패턴)
        _healthServer = new HealthPipeServer(
            "IIoT.HMI",
            statusProvider: () => "HMI 정상",
            onLog: m => LogManager.Instance.Debug("Health", m));
        _healthServer.Start();

        // ④ DI 빌드
        _services = _ConfigureServices();

        // ★ HM-16: 알람 생성/상태전이 시마다 SQLite 이력 저장 (fire-and-forget) —
        //   AlarmHistoryService.InitializeAsync() 가 아직 끝나지 않은 시점에 호출돼도
        //   RecordAsync() 내부에서 _ctx null 가드로 조용히 무시되므로 안전하다.
        var alarmAggregator = _services.GetRequiredService<AlarmAggregator>();
        var alarmHistory    = _services.GetRequiredService<AlarmHistoryService>();
        alarmAggregator.AlarmRecorded += row => _ = alarmHistory.RecordAsync(row);

        // ⑤ 창 생성 및 표시
        var win = _services.GetRequiredService<MainWindow>();
        win.Show();

        // ★ HM-11: 웹 표시 서버(자체 Kestrel+SignalR Hub+wwwroot) 시작.
        //   hmi.json 로드는 서비스 내부에서 자체적으로 재확인하므로 [Collector 관리]
        //   탭의 Loaded 순서와 무관하게 안전하다(Collector App.xaml.cs 의 win.Loaded
        //   오케스트레이션 패턴 참고).
        win.Loaded += async (_, _) =>
        {
            await _services.GetRequiredService<HmiWebHostService>().StartAsync();

            // ★ HM-16: 알람 이력 SQLite DB 초기화 (Monitor MainWindow.Loaded 와
            //   동일 시점 — 창이 뜬 뒤 1회)
            await _services.GetRequiredService<AlarmHistoryService>().InitializeAsync();
        };
    }

    // §3 ─ 종료 ───────────────────────────────────────────────

    protected override void OnExit(ExitEventArgs e)
    {
        // ★ HM-01: CollectorConnectionManager 정리 (N개 Collector 의 SignalR
        //   HubConnection/HttpClient 를 순서대로 종료) — Monitor OnExit FIX 교훈과
        //   동일하게 5초 타임아웃으로 무한 대기를 방어한다.
        _WaitWithTimeout(_services?.GetService<CollectorConnectionManager>()?.DisposeAsync().AsTask());

        // ★ HM-11: 웹 표시 서버(Kestrel) 정리 — 동일하게 5초 타임아웃으로 방어.
        _WaitWithTimeout(_services?.GetService<HmiWebHostService>()?.DisposeAsync().AsTask());

        // ★ HM-16: 알람 이력 DB 연결 정리
        _WaitWithTimeout(_services?.GetService<AlarmHistoryService>()?.DisposeAsync().AsTask());

        // ★ 헬스체크 파이프 정리 (내부 2초 타임아웃 + 외부 5초 이중 방어)
        _WaitWithTimeout(_healthServer?.DisposeAsync().AsTask());

        _themeSettings?.Dispose();   // 이벤트 구독 해제 필수
        LogManager.Instance.Info("App", "IIoT.HMI 종료");
        base.OnExit(e);
    }

    /// <summary>
    /// 종료 정리 Task 를 최대 5초까지만 기다린다. 그 안에 끝나지 않으면
    /// 로그만 남기고 포기한다 — 앱 종료 자체가 무한정 멈추는 것을 방지하는 안전장치.
    /// (Monitor App.xaml.cs 와 동일 패턴)
    /// </summary>
    private static void _WaitWithTimeout(Task? task)
    {
        if (task is null) return;

        try
        {
            if (!task.Wait(TimeSpan.FromSeconds(5)))
                LogManager.Instance.Warn("App", "종료 정리 작업이 5초 내 완료되지 않아 건너뜁니다.");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("App", $"종료 정리 중 예외(무시하고 계속 종료): {ex.Message}");
        }
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ★ HM-01 신규: hmi.json 설정 + Collector 연결 관리자
        //   (CollectorManageViewModel 보다 먼저 등록 — 생성자 의존성)
        services.AddSingleton<HmiSettingsLoader>();
        services.AddSingleton<CollectorConnectionManager>();

        // ★ HM-02 신규: [Collector 관리] 탭
        services.AddSingleton<CollectorManageViewModel>();
        services.AddSingleton<CollectorManageView>();

        // ★ HM-03 신규: [레이아웃 편집] 탭
        // ★ HM-07 신규: hmi-layout.json 로더 — LayoutCanvasViewModel 보다 먼저 등록
        // ★ HM-12: LayoutCanvasViewModel 이 HmiSettingsLoader 도 생성자로 요구함
        //   (화면 잠금 기본값 조회) — 이미 위에서 등록되어 있으므로 추가 등록 불필요
        services.AddSingleton<HmiLayoutLoader>();
        services.AddSingleton<LayoutCanvasViewModel>();
        services.AddSingleton<LayoutCanvasView>();

        // ★ HM-11 신규: 웹 브라우저 표시 확장 (LayoutCanvasViewModel 보다 뒤에 등록 —
        //   생성자 의존성. HmiSettingsLoader 는 이미 위에서 등록됨)
        services.AddSingleton<HmiWebHostService>();

        // ★ HM-14 신규: [알람] 탭 — AlarmAggregator 가 CollectorConnectionManager
        //   (이미 위에서 등록됨)를 생성자로 요구하므로 그 뒤에 등록
        services.AddSingleton<AlarmAggregator>();
        services.AddSingleton<AlarmViewModel>();
        services.AddSingleton<AlarmView>();

        // ★ HM-15 신규: [로그] 탭 — ViewModel 없이 자체적으로 LogManager.Instance
        //   구독을 처리하는 완결형 View (Studio/Collector/Monitor 와 동일 패턴)
        services.AddSingleton<LogPanelView>();

        // ★ HM-16 신규: 알람 이력 SQLite 영구 저장 (AlarmAggregator 보다 뒤에 등록 —
        //   실제 의존은 없지만 등록 순서를 기능 도입 순서와 맞춤)
        services.AddSingleton<AlarmHistoryService>();

        // ★ HM-Base-1: MainWindow DataContext
        services.AddSingleton<HmiMainViewModel>();

        // ★ 반드시 AddSingleton (Transient → 이중 창 버그)
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<HmiMainViewModel>(),
                sp.GetRequiredService<CollectorManageView>(),
                sp.GetRequiredService<LayoutCanvasView>(),
                sp.GetRequiredService<AlarmView>(),
                sp.GetRequiredService<LogPanelView>()));

        return services.BuildServiceProvider();
    }
}
