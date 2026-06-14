// ══════════════════════════════════════════════════════════
//  IIoT.Studio · App.xaml.cs
//  Fix 목록:
//    ① using IIot.Studio 제거 (잘못된 네임스페이스)
//    ② ConfigBundle 패턴 적용 (StudioMainViewModel 2 파라미터)
//    ③ AddSingleton<MainWindow> 유지 확인
//    ④ ConfigInitializer.ResourcePrefix = "IIoT.Studio.Config." 명시
// ══════════════════════════════════════════════════════════

using IIoT.Shared.Config;              // ★ ConfigBundle
using IIoT.Studio.Core.Config;
using IIoT.Studio.ViewModels;
using IIoT.Studio.ViewModels.Canvas;
using IIoT.Studio.ViewModels.DeviceTree;
using IIoT.Studio.ViewModels.Library;
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

// ★ Fix ①: using IIot.Studio 제거 (소문자 i — 잘못된 네임스페이스)

namespace IIoT.Studio;

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

        // ① 테마
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
        LogManager.Instance.Info("App", "IIoT Studio 시작 (V3 — DeviceManager+ConfigApp 통합)");

        // ★ Fix ④: ResourcePrefix 명시 (RootNamespace=IIoT.Studio 에 맞춤)
        ConfigInitializer.EnsureConfigFiles(
            ConfigDirectory,
            resourcePrefix: "IIoT.Studio.Config.");

        // ④ DI + 윈도우 표시
        _services = _ConfigureServices();
        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.Instance.Info("App", "IIoT Studio 종료");
        await LogManager.Instance.StopAsync();
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    // §3 ─ DI 구성 ────────────────────────────────────────────
    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── 기반 서비스 ──
        services.AddSingleton<JsonWriteService>(
            _ => new JsonWriteService(ConfigDirectory));
        services.AddSingleton<JsonConfigLoader>(
            _ => new JsonConfigLoader(ConfigDirectory));
        services.AddSingleton<CollectConfigService>(
            _ => new CollectConfigService(ConfigDirectory));

        // ── 라이브러리 ViewModel ──
        services.AddSingleton<ScaleLibraryViewModel>(sp =>
            new ScaleLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<AlarmLibraryViewModel>(sp =>
            new AlarmLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<CommLibraryViewModel>(sp =>
            new CommLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<CanvasViewModel>();

        // ── ★ Fix ②: ConfigBundle 번들 (8→1) ──
        services.AddSingleton<ConfigBundle>(sp => new ConfigBundle
        {
            Loader  = sp.GetRequiredService<JsonConfigLoader>(),
            Writer  = sp.GetRequiredService<JsonWriteService>(),
            Collect = sp.GetRequiredService<CollectConfigService>(),
            Scale   = sp.GetRequiredService<ScaleLibraryViewModel>(),
            Alarm   = sp.GetRequiredService<AlarmLibraryViewModel>(),
            Comm    = sp.GetRequiredService<CommLibraryViewModel>(),
            Canvas  = sp.GetRequiredService<CanvasViewModel>(),
        });

        // ── DeviceTree + 메인 ViewModel ──
        services.AddSingleton<DeviceTreeViewModel>();
        services.AddSingleton<StudioMainViewModel>(sp => new StudioMainViewModel(
            sp.GetRequiredService<DeviceTreeViewModel>(),
            sp.GetRequiredService<ConfigBundle>()  // ← 2 파라미터
        ));

        // ── ★ Fix ③: Singleton 필수 (Transient → 이중 창 버그) ──
        services.AddSingleton<MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<StudioMainViewModel>(),
            sp.GetRequiredService<DeviceTreeViewModel>()
        ));

        return services.BuildServiceProvider();
    }
}
