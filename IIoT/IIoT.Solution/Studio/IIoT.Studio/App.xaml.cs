// ══════════════════════════════════════════════════════════
//  IIoT.Studio · App.xaml.cs
//  역할: 애플리케이션 시작·종료 진입점
//  Base-0: 최소 구조
//  S-10: DeviceConfigService DI 등록 추가
//  S-11: CanvasViewModel + CollectConfigService DI 등록 추가
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

        // ── 서브 ViewModel ───────────────────────────────────
        services.AddSingleton<DeviceTreeViewModel>();
        services.AddSingleton<ScaleLibraryViewModel>();
        services.AddSingleton<AlarmLibraryViewModel>();
        services.AddSingleton<CommLibraryViewModel>();
        services.AddSingleton<CanvasViewModel>();          // ★ S-11

        // ── 서비스 ───────────────────────────────────────────
        services.AddSingleton<DeviceConfigService>(sp =>
            new DeviceConfigService(
                sp.GetRequiredService<DeviceTreeViewModel>(),
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>(),
                sp.GetRequiredService<CommLibraryViewModel>()));

        // ★ S-11: CollectConfigService
        services.AddSingleton<CollectConfigService>(sp =>
            new CollectConfigService(
                sp.GetRequiredService<CanvasViewModel>()));

        // ── 메인 ViewModel ───────────────────────────────────
        // ★ S-11: CanvasViewModel + CollectConfigService 파라미터 추가
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
