// ══════════════════════════════════════════════════════════
//  IIoT.Studio · App.xaml.cs
//  S-14: DeviceTreeViewModel 생성자에 TagTemplateViewModel 추가
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
    private ThemeSettingsService? _themeSettings;
    private IServiceProvider?     _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);
        _services = _ConfigureServices();

        var win = _services.GetRequiredService<MainWindow>();

        win.Loaded += (_, _) =>
        {
            var canvasView = _FindCanvasView(win);
            if (canvasView is not null)
            {
                var deviceTreeVm  = _services.GetRequiredService<DeviceTreeViewModel>();
                var tagTemplateVm = _services.GetRequiredService<TagTemplateViewModel>();

                canvasView.DeviceTreeVm = deviceTreeVm;
                canvasView.GetTemplates = () => tagTemplateVm.Templates;
            }
        };

        win.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── 라이브러리 ViewModel ─────────────────────────────
        services.AddSingleton<ScaleLibraryViewModel>();
        services.AddSingleton<AlarmLibraryViewModel>();
        services.AddSingleton<CommLibraryViewModel>();

        // ── 태그 템플릿 (★ S-14: DeviceTreeViewModel 주입 전 먼저 등록)
        services.AddSingleton<TagTemplateService>();
        services.AddSingleton<TagTemplateViewModel>(sp =>
            new TagTemplateViewModel(
                sp.GetRequiredService<TagTemplateService>()));

        // ── 장비 트리 (★ S-14: TagTemplateViewModel 추가)
        services.AddSingleton<DeviceTreeViewModel>(sp =>
            new DeviceTreeViewModel(
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>(),
                sp.GetRequiredService<TagTemplateViewModel>()));   // ★ S-14

        // ── 캔버스 ───────────────────────────────────────────
        services.AddSingleton<CanvasViewModel>(sp =>
            new CanvasViewModel(
                sp.GetRequiredService<DeviceTreeViewModel>()));

        // ── 저장 서비스 ──────────────────────────────────────
        services.AddSingleton<DeviceConfigService>(sp =>
            new DeviceConfigService(
                sp.GetRequiredService<DeviceTreeViewModel>(),
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>(),
                sp.GetRequiredService<CommLibraryViewModel>()));

        services.AddSingleton<CollectConfigService>(sp =>
            new CollectConfigService(
                sp.GetRequiredService<CanvasViewModel>()));

        // ── 메인 ViewModel ───────────────────────────────────
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

    private static Views.Canvas.CanvasView? _FindCanvasView(
        System.Windows.DependencyObject parent)
    {
        for (int i = 0;
             i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is Views.Canvas.CanvasView cv) return cv;
            var found = _FindCanvasView(child);
            if (found is not null) return found;
        }
        return null;
    }
}
