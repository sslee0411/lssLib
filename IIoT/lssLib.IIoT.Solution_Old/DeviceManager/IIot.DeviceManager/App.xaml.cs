// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · App.xaml.cs
//  역할: 애플리케이션 진입점
//  Phase 5R: MainViewModel 생성자에 라이브러리 VM 직접 주입
//            MainWindow 생성자 단순화 (MainViewModel 하나만 주입)
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.ViewModels;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using IIoT.DeviceManager.ViewModels.Library;
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.DeviceManager;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private ThemeSettingsService? _themeSettings;
    private IServiceProvider? _services;

    private static string ConfigDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

    // §2 ─ 시작 ───────────────────────────────────────────────
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① 테마 초기화 — 반드시 첫 번째
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        // ② LogManager
        _InitLogManager();
        LogManager.Instance.Info("App", "IIoT DeviceManager 시작");

        // ③ 설정 파일 보장
        ConfigInitializer.EnsureConfigFiles(ConfigDirectory);

        // ④ DI + 윈도우 표시
        _services = _ConfigureServices();
        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.Instance.Info("App", "IIoT DeviceManager 종료");
        await LogManager.Instance.StopAsync();
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    // §3 ─ LogManager 초기화 ──────────────────────────────────
    private static void _InitLogManager()
    {
        var logConfig = new LogConfig
        {
            LogRootPath     = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
            ValidDays       = 30,
            FileFormat      = LogFileFormat.Both,
            MinimumLevel    = LogLevel.Debug,
            MaxDisplayCount = 2000,
        };
        LogManager.Instance.Start(logConfig);
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────
    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // Config 서비스
        services.AddSingleton<JsonWriteService>(
            _ => new JsonWriteService(ConfigDirectory));
        services.AddSingleton<JsonConfigLoader>(
            _ => new JsonConfigLoader(ConfigDirectory));

        // DeviceTree VM
        services.AddSingleton<DeviceTreeViewModel>();

        // 라이브러리 VM (MainViewModel 에 직접 주입)
        services.AddSingleton<ScaleLibraryViewModel>(sp =>
            new ScaleLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<AlarmLibraryViewModel>(sp =>
            new AlarmLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<CommLibraryViewModel>(sp =>
            new CommLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));

        // ★ Phase 5R: MainViewModel 에 라이브러리 VM 모두 주입
        services.AddSingleton<MainViewModel>(sp => new MainViewModel(
            sp.GetRequiredService<DeviceTreeViewModel>(),
            sp.GetRequiredService<JsonConfigLoader>(),
            sp.GetRequiredService<JsonWriteService>(),
            sp.GetRequiredService<ScaleLibraryViewModel>(),   // ← 신규
            sp.GetRequiredService<AlarmLibraryViewModel>(),   // ← 신규
            sp.GetRequiredService<CommLibraryViewModel>()     // ← 신규
        ));

        // ★ Phase 5R: MainWindow 생성자 단순화 (MainViewModel + DeviceTreeViewModel)
        services.AddTransient<MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<MainViewModel>(),
            sp.GetRequiredService<DeviceTreeViewModel>()
        ));

        return services.BuildServiceProvider();
    }
}
