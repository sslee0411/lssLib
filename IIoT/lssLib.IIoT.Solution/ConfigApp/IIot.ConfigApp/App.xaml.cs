// ══════════════════════════════════════════════════════════
//  IIoT.ConfigApp · App.xaml.cs
//  역할: ConfigApp 통합 진입점
//        구 DeviceManager + CollectConfig 통합 앱
//  Phase 11: 신규 (구 DeviceManager App.xaml.cs 기반 확장)
//
//  변경점 (DeviceManager → ConfigApp):
//    · 네임스페이스: IIoT.DeviceManager → IIoT.ConfigApp
//    · CollectConfigService DI 추가
//    · CanvasViewModel DI 추가
//    · ConfigInitializer.ResourcePrefix 변경 필요:
//        "IIoT.DeviceManager.Config." → "IIoT.ConfigApp.Config."
// ══════════════════════════════════════════════════════════

using IIoT.ConfigApp.Core.Config;
using IIoT.ConfigApp.ViewModels;
using IIoT.ConfigApp.ViewModels.Canvas;
using IIoT.ConfigApp.ViewModels.DeviceTree;
using IIoT.ConfigApp.ViewModels.Library;
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.ConfigApp;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private ThemeSettingsService? _themeSettings;
    private IServiceProvider?     _services;

    private static string ConfigDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

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
            LogRootPath     = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
            ValidDays       = 30,
            FileFormat      = LogFileFormat.Both,
            MinimumLevel    = LogLevel.Debug,
            MaxDisplayCount = 2000,
        });
        LogManager.Instance.Info("App", "IIoT ConfigApp 시작 (구 DeviceManager + CollectConfig 통합)");

        // ③ 설정 파일 초기화
        //    ★ ConfigInitializer.ResourcePrefix 를
        //      "IIoT.ConfigApp.Config." 으로 변경 후 사용
        ConfigInitializer.EnsureConfigFiles(ConfigDirectory);

        // ④ DI + 윈도우 표시
        _services = _ConfigureServices();
        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.Instance.Info("App", "IIoT ConfigApp 종료");
        await LogManager.Instance.StopAsync();
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    // §3 ─ DI 구성 ────────────────────────────────────────────
    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── 설정 서비스 ──
        services.AddSingleton<JsonWriteService>(
            _ => new JsonWriteService(ConfigDirectory));
        services.AddSingleton<JsonConfigLoader>(
            _ => new JsonConfigLoader(ConfigDirectory));
        services.AddSingleton<CollectConfigService>(
            _ => new CollectConfigService(ConfigDirectory));

        // ── DeviceTree VM ──
        services.AddSingleton<DeviceTreeViewModel>();

        // ── 라이브러리 VM ──
        services.AddSingleton<ScaleLibraryViewModel>(sp =>
            new ScaleLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<AlarmLibraryViewModel>(sp =>
            new AlarmLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<CommLibraryViewModel>(sp =>
            new CommLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));

        // ── Canvas VM (Phase 11 신규) ──
        services.AddSingleton<CanvasViewModel>();

        // ── ConfigApp 통합 MainViewModel ──
        services.AddSingleton<ConfigAppMainViewModel>(sp => new ConfigAppMainViewModel(
            sp.GetRequiredService<DeviceTreeViewModel>(),
            sp.GetRequiredService<JsonConfigLoader>(),
            sp.GetRequiredService<JsonWriteService>(),
            sp.GetRequiredService<ScaleLibraryViewModel>(),
            sp.GetRequiredService<AlarmLibraryViewModel>(),
            sp.GetRequiredService<CommLibraryViewModel>(),
            sp.GetRequiredService<CanvasViewModel>(),
            sp.GetRequiredService<CollectConfigService>()
        ));

        // ── MainWindow ──
        services.AddTransient<MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<ConfigAppMainViewModel>(),
            sp.GetRequiredService<DeviceTreeViewModel>()
        ));

        return services.BuildServiceProvider();
    }
}
