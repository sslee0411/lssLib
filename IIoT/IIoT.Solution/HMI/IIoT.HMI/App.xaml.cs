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
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.Contracts.Health;
using IIoT.HMI.Core.Config;
using IIoT.HMI.Core.Connection;
using IIoT.HMI.ViewModels;
using IIoT.HMI.Views.CollectorManage;
using IIoT.HMI.Views.LayoutCanvas;
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

        // ⑤ 창 생성 및 표시
        _services.GetRequiredService<MainWindow>().Show();
    }

    // §3 ─ 종료 ───────────────────────────────────────────────

    protected override void OnExit(ExitEventArgs e)
    {
        // ★ HM-01: CollectorConnectionManager 정리 (N개 Collector 의 SignalR
        //   HubConnection/HttpClient 를 순서대로 종료) — Monitor OnExit FIX 교훈과
        //   동일하게 5초 타임아웃으로 무한 대기를 방어한다.
        _WaitWithTimeout(_services?.GetService<CollectorConnectionManager>()?.DisposeAsync().AsTask());

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
        services.AddSingleton<HmiLayoutLoader>();
        services.AddSingleton<LayoutCanvasViewModel>();
        services.AddSingleton<LayoutCanvasView>();

        // ★ HM-Base-1: MainWindow DataContext
        services.AddSingleton<HmiMainViewModel>();

        // ★ 반드시 AddSingleton (Transient → 이중 창 버그)
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<HmiMainViewModel>(),
                sp.GetRequiredService<CollectorManageView>(),
                sp.GetRequiredService<LayoutCanvasView>()));

        return services.BuildServiceProvider();
    }
}
