// ══════════════════════════════════════════════════════════
//  IIoT.Studio · App.xaml.cs
//  S-11: CanvasViewModel + CollectConfigService DI 등록
//  S-12B: CanvasViewModel 생성자에 DeviceTreeViewModel 주입
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Core.Config;
using IIoT.Studio.ViewModels;
using IIoT.UI.Themes;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace IIoT.Studio;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private ThemeSettingsService? _themeSettings;
    private IServiceProvider?     _services;

    // §2 ─ 시작 ───────────────────────────────────────────────

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);
        _services = _ConfigureServices();
        _services.GetRequiredService<MainWindow>().Show();
    }

    // §3 ─ 종료 ───────────────────────────────────────────────

    protected override void OnExit(ExitEventArgs e)
    {
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── 라이브러리 ViewModel ─────────────────────────────
        services.AddSingleton<ScaleLibraryViewModel>();
        services.AddSingleton<AlarmLibraryViewModel>();
        services.AddSingleton<CommLibraryViewModel>();

        // ── 장비 트리 ViewModel ──────────────────────────────
        services.AddSingleton<DeviceTreeViewModel>(sp =>
            new DeviceTreeViewModel(
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>()));

        // ── CanvasViewModel (★ S-12B: DeviceTreeViewModel 주입) ─
        services.AddSingleton<CanvasViewModel>(sp =>
            new CanvasViewModel(
                sp.GetRequiredService<DeviceTreeViewModel>()));

        // ── 서비스 ───────────────────────────────────────────
        services.AddSingleton<DeviceConfigService>(sp =>
            new DeviceConfigService(
                sp.GetRequiredService<DeviceTreeViewModel>(),
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>(),
                sp.GetRequiredService<CommLibraryViewModel>()));

        services.AddSingleton<CollectConfigService>(sp =>
            new CollectConfigService(
                sp.GetRequiredService<CanvasViewModel>()));

        // ── MainViewModel ────────────────────────────────────
        services.AddSingleton<MainViewModel>(sp =>
            new MainViewModel(
                sp.GetRequiredService<DeviceTreeViewModel>(),
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>(),
                sp.GetRequiredService<CommLibraryViewModel>(),
                sp.GetRequiredService<CanvasViewModel>(),
                sp.GetRequiredService<DeviceConfigService>(),
                sp.GetRequiredService<CollectConfigService>()));

        // ── MainWindow ───────────────────────────────────────
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(sp.GetRequiredService<MainViewModel>()));

        return services.BuildServiceProvider();
    }
}
