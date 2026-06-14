// ══════════════════════════════════════════════════════════
//  IIoT.Studio · App.xaml.cs
//  역할: Config 통합 진입점
//        구 DeviceManager + CollectConfig 통합 앱
//  Phase 11: 신규
//  V3 Step1: AddTransient<MainWindow> → AddSingleton<MainWindow> 수정
//            (이중 창 버그 해결)
// ══════════════════════════════════════════════════════════

using IIot.Studio;
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

namespace IIoT.Studio;

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
        LogManager.Instance.Info("App", "IIoT Config 시작 (구 DeviceManager + CollectConfig 통합)");

        // ③ 설정 파일 초기화
        ConfigInitializer.EnsureConfigFiles(ConfigDirectory);

        // ④ DI + 윈도우 표시
        _services = _ConfigureServices();
        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.Instance.Info("App", "IIoT Config 종료");
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

        // ── Canvas VM ──
        services.AddSingleton<CanvasViewModel>();

        // ── Config 통합 MainViewModel ──
        services.AddSingleton<ConfigMainViewModel>(sp => new ConfigMainViewModel(
            sp.GetRequiredService<DeviceTreeViewModel>(),
            sp.GetRequiredService<JsonConfigLoader>(),
            sp.GetRequiredService<JsonWriteService>(),
            sp.GetRequiredService<ScaleLibraryViewModel>(),
            sp.GetRequiredService<AlarmLibraryViewModel>(),
            sp.GetRequiredService<CommLibraryViewModel>(),
            sp.GetRequiredService<CanvasViewModel>(),
            sp.GetRequiredService<CollectConfigService>()
        ));

        // ★ V3 Step1 수정: AddTransient → AddSingleton
        //   이전: services.AddTransient<MainWindow>(...)
        //   이유: Transient = 호출마다 새 Window 인스턴스 → 이중 창 버그
        services.AddSingleton<MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<ConfigMainViewModel>(),
            sp.GetRequiredService<DeviceTreeViewModel>()
        ));

        return services.BuildServiceProvider();
    }
}