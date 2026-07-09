// ══════════════════════════════════════════════════════════
//  IIoT.Manager · App.xaml.cs
//  역할: 애플리케이션 진입점
//        ① 테마 복원 (창 표시 전 — 색상 준비)
//        ② LogManager.Instance.Start() (DI 빌드 전 필수)
//        ③ DI 서비스 구성 (_ConfigureServices)
//        ④ 창 생성 (AddSingleton — Transient 사용 시 이중 창 버그)
//
//  MG-Base-0: 최초 생성 — 빈 창 + 테마만 적용.
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

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
        // ★ Monitor 버그 #10/#11 교훈: 리소스 보유 싱글턴(프로세스 핸들·
        //   NamedPipe 등)이 추가되는 Step(MG-02/MG-03)부터는 여기서
        //   _WaitWithTimeout 세트로 반드시 정리할 것.
        _themeSettings?.Dispose();   // 이벤트 구독 해제 필수
        LogManager.Instance.Info("App", "IIoT.Manager 종료");
        base.OnExit(e);
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ★ 반드시 AddSingleton (Transient → 이중 창 버그)
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
