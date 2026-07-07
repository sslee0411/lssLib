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
//  생성: 2026-07-07 / 수정: 2026-07-07 (MN-01B)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Core.Connection;
using IIoT.Monitor.ViewModels;
using IIoT.Monitor.Views.CollectorManage;
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

        // ④ 창 생성 및 표시
        _services.GetRequiredService<MainWindow>().Show();
    }

    // §3 ─ 종료 ───────────────────────────────────────────────

    protected override void OnExit(ExitEventArgs e)
    {
        _themeSettings?.Dispose();   // 이벤트 구독 해제 필수
        LogManager.Instance.Info("App", "IIoT.Monitor 종료");
        base.OnExit(e);
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ★ MN-01B 신규: Collector 연결 관리자 (CollectorId ↔ HubConnection)
        services.AddSingleton<CollectorConnectionManager>();

        // ★ MN-01 신규: monitor.json 설정 + Collector 관리 화면
        services.AddSingleton<MonitorSettingsLoader>();
        services.AddSingleton<CollectorManageViewModel>();
        services.AddSingleton<CollectorManageView>();

        // ★ MN-Base-1: MainWindow DataContext
        services.AddSingleton<MonitorMainViewModel>();

        // ★ 반드시 AddSingleton (Transient → 이중 창 버그)
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<MonitorMainViewModel>(),
                sp.GetRequiredService<CollectorManageView>()));

        return services.BuildServiceProvider();
    }
}
