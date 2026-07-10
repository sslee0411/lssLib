// ══════════════════════════════════════════════════════════
//  IIoT.Manager · App.xaml.cs
//  역할: 애플리케이션 진입점
//        ① 테마 복원 (창 표시 전 — 색상 준비)
//        ② LogManager.Instance.Start() (DI 빌드 전 필수)
//        ③ DI 서비스 구성 (_ConfigureServices)
//        ④ 창 생성 (AddSingleton — Transient 사용 시 이중 창 버그)
//
//  MG-Base-0: 최초 생성 — 빈 창 + 테마만 적용.
//  MG-01: ManagerMainViewModel / ProcessStatusView DI 등록 추가,
//         MainWindow 생성자 주입으로 변경.
//  MG-02: ManagerSettingsLoader / ProcessManager DI 등록 추가.
//         (ProcessManager 는 핸들을 보관하지 않으므로 OnExit 정리 불필요 —
//          Manager 가 시작한 프로세스는 Manager 종료 후에도 계속 실행: 의도된 동작)
//  MG-03: HealthCheckService DI 등록 추가 (NamedPipe 핑 클라이언트 —
//         매 호출 시 파이프 생성·해제, 핸들 미보관 → OnExit 정리 불필요)
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-03)
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Core;
using IIoT.Manager.Core.Config;
using IIoT.Manager.ViewModels;
using IIoT.Manager.Views.ProcessStatus;
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.Manager;

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

        LogManager.Instance.Info("App", "IIoT.Manager 시작");

        // ③ DI 빌드
        _services = _ConfigureServices();

        // ④ 창 생성 및 표시
        _services.GetRequiredService<MainWindow>().Show();
    }

    // §3 ─ 종료 ───────────────────────────────────────────────

    protected override void OnExit(ExitEventArgs e)
    {
        // ★ Monitor 버그 #10/#11 교훈: 리소스 보유 싱글턴(NamedPipe 등)이
        //   추가되는 Step(MG-03~)부터는 여기서 _WaitWithTimeout 세트로
        //   반드시 정리할 것. (ProcessManager 는 핸들 미보관 — 대상 아님)
        _themeSettings?.Dispose();   // 이벤트 구독 해제 필수
        LogManager.Instance.Info("App", "IIoT.Manager 종료");
        base.OnExit(e);
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ★ MG-02 신규: manager.json 로더 (로드는 MainWindow.Loaded 에서)
        services.AddSingleton<ManagerSettingsLoader>();

        // ★ MG-02 신규: 프로세스 시작/정지/재시작 실행자
        services.AddSingleton<ProcessManager>();

        // ★ MG-03 신규: NamedPipe 헬스체크 클라이언트
        services.AddSingleton<HealthCheckService>();

        // ★ MG-01 신규: MainWindow DataContext (카드 목록 + 2초 갱신 타이머)
        services.AddSingleton<ManagerMainViewModel>();

        // ★ MG-01 신규: 프로세스 상태 화면 (DataContext 는 MainWindow 상속)
        services.AddSingleton<ProcessStatusView>();

        // ★ 반드시 AddSingleton (Transient → 이중 창 버그)
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<ManagerMainViewModel>(),
                sp.GetRequiredService<ProcessStatusView>()));

        return services.BuildServiceProvider();
    }
}
