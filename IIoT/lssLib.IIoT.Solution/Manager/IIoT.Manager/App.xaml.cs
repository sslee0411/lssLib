// ══════════════════════════════════════════════════════════
//  IIoT.Manager · App.xaml.cs
//  역할: Manager 앱 진입점 — DI 구성 + 테마 + 로그 초기화
//  Phase 12: 신규
//  V3 Step1: AddTransient<MainWindow> → AddSingleton<MainWindow> 수정
//            (이중 창 버그 해결)
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Core;
using IIoT.Manager.ViewModels;
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
    private IServiceProvider? _services;

    // §2 ─ 시작 ───────────────────────────────────────────────
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① 테마 — 반드시 첫 번째
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        // ② LogManager
        LogManager.Instance.Start(new LogConfig
        {
            LogRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
            ValidDays = 30,
            FileFormat = LogFileFormat.Both,
            MinimumLevel = LogLevel.Debug,
            MaxDisplayCount = 2000,
        });
        LogManager.Instance.Info("App", "IIoT Manager 시작");

        // ③ DI + 윈도우 표시
        _services = _ConfigureServices();
        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_services?.GetService<ProcessManager>() is { } mgr)
            await mgr.DisposeAsync();

        LogManager.Instance.Info("App", "IIoT Manager 종료");
        await LogManager.Instance.StopAsync();
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    // §3 ─ DI 구성 ────────────────────────────────────────────
    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ProcessManager>();
        services.AddSingleton<ManagerViewModel>();

        // ★ V3 Step1 수정: AddTransient → AddSingleton
        //   이전: services.AddTransient<MainWindow>(...)
        //   이유: Transient = 호출마다 새 Window 인스턴스 → 이중 창 버그
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(sp.GetRequiredService<ManagerViewModel>()));

        return services.BuildServiceProvider();
    }
}