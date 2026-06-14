// ══════════════════════════════════════════════════════════
//  IIoT.Studio · App.xaml.cs
//  Fix:
//    ① using 누락 추가
//       - IIoT.Studio.ViewModels.DeviceTree  (CS0234)
//       - IIoT.Studio.ViewModels.Library     (CS0234)
//       - IIoT.Studio.Core.Config            (CS0246: JsonWriteService 등)
//    ② ConfigInitializer.EnsureConfigFiles() resourcePrefix 파라미터 제거
//       실제 시그니처: EnsureConfigFiles(string configDirectory) — 파라미터 1개 (CS1739)
//    ③ MainWindow 생성자: (StudioMainViewModel, DeviceTreeViewModel) 2 파라미터 유지
// ══════════════════════════════════════════════════════════

using IIoT.Shared.Config;
using IIoT.UI.Themes;
using IIoT.Studio.Core.Config;                  // ★ Fix ①: JsonWriteService, JsonConfigLoader,
                                                 //          CollectConfigService, ConfigInitializer
using IIoT.Studio.ViewModels;                   // StudioMainViewModel
using IIoT.Studio.ViewModels.Canvas;            // CanvasViewModel
using IIoT.Studio.ViewModels.DeviceTree;        // ★ Fix ①: DeviceTreeViewModel
using IIoT.Studio.ViewModels.Library;           // ★ Fix ①: ScaleLibraryViewModel 등
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

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

        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        LogManager.Instance.Start(new LogConfig
        {
            LogRootPath     = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
            ValidDays       = 30,
            FileFormat      = LogFileFormat.Both,
            MinimumLevel    = LogLevel.Debug,
            MaxDisplayCount = 2000,
        });
        LogManager.Instance.Info("App", "IIoT Studio 시작 (V3)");

        // ★ Fix ②: resourcePrefix 파라미터 없음 — 1개 파라미터만
        ConfigInitializer.EnsureConfigFiles(ConfigDirectory);

        _services = _ConfigureServices();
        _services.GetRequiredService<MainWindow>().Show();
    }

    // §3 ─ 종료 ───────────────────────────────────────────────
    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.Instance.Info("App", "IIoT Studio 종료");
        await LogManager.Instance.StopAsync();
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────
    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── 기반 서비스 ──────────────────────────────────────
        services.AddSingleton<JsonWriteService>(
            _ => new JsonWriteService(ConfigDirectory));
        services.AddSingleton<JsonConfigLoader>(
            _ => new JsonConfigLoader(ConfigDirectory));
        services.AddSingleton<CollectConfigService>(
            _ => new CollectConfigService(ConfigDirectory));

        // ── 라이브러리 ViewModel ──────────────────────────────
        services.AddSingleton<ScaleLibraryViewModel>(sp =>
            new ScaleLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<AlarmLibraryViewModel>(sp =>
            new AlarmLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<CommLibraryViewModel>(sp =>
            new CommLibraryViewModel(sp.GetRequiredService<JsonWriteService>()));
        services.AddSingleton<CanvasViewModel>();

        // ── ConfigBundle (8→2 파라미터 번들) ─────────────────
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

        // ── DeviceTree + Main ViewModel ───────────────────────
        services.AddSingleton<DeviceTreeViewModel>();
        services.AddSingleton<StudioMainViewModel>(sp => new StudioMainViewModel(
            sp.GetRequiredService<DeviceTreeViewModel>(),
            sp.GetRequiredService<ConfigBundle>()
        ));

        // ── ★ Singleton 필수 (Transient 이중 창 버그 방지) ───
        services.AddSingleton<MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<StudioMainViewModel>(),
            sp.GetRequiredService<DeviceTreeViewModel>()
        ));

        return services.BuildServiceProvider();
    }
}
