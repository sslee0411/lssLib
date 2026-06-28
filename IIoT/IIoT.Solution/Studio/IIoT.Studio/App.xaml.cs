// ══════════════════════════════════════════════════════════
//  IIoT.Studio · App.xaml.cs
//  S-14: DeviceTreeViewModel 생성자에 TagTemplateViewModel 추가
//  S-15: OnStartup에서 설정 로드 (DeviceConfigLoader + CollectConfigLoader)
//  Studio-P01: PluginRegistryService DI 등록 + LoadPlugins() 호출
//  Studio-P01 fix: LogManager.Instance.Start() 추가
//                  (미호출 시 IsRunning=false → 모든 로그 무시됨)
//  생성: 2026-06-15 / 수정: 2026-06-27
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Core.Config;
using IIoT.Studio.Core.Plugin;
using IIoT.Studio.ViewModels;
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.Studio;

public partial class App : Application
{
    private ThemeSettingsService? _themeSettings;
    private IServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① 테마 (가장 먼저 — 창 표시 전 색상 준비)
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        // ② LogManager 시작 (★ 반드시 DI 빌드 전에 호출)
        //    미호출 시 IsRunning = false → AddLog() 즉시 반환 → 로그 전체 무시
        LogManager.Instance.Start(new LogConfig
        {
            LogRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log"),
            ValidDays = 30,
            FileFormat = LogFileFormat.Both,
            MinimumLevel = LogLevel.Debug,
            MinimumConsoleLevel = LogLevel.Info,
            MaxDisplayCount = 2000
        });

        LogManager.Instance.Info("App", "IIoT.Studio 시작");

        // ③ DI 빌드
        _services = _ConfigureServices();

        // ④ 플러그인 로드 (DI 빌드 직후, 창 표시 전)
        _services.GetRequiredService<PluginRegistryService>()
                 .LoadPlugins();

        // ⑤ 창 생성 + 표시
        var win = _services.GetRequiredService<MainWindow>();

        win.Loaded += async (_, _) =>
        {
            // ── CanvasView 속성 주입 ─────────────────────────
            var canvasView = _FindCanvasView(win);
            if (canvasView is not null)
            {
                var deviceTreeVm = _services.GetRequiredService<DeviceTreeViewModel>();
                var tagTemplateVm = _services.GetRequiredService<TagTemplateViewModel>();

                canvasView.DeviceTreeVm = deviceTreeVm;
                canvasView.GetTemplates = () => tagTemplateVm.Templates;
            }

            // ★ S-15: 설정 로드 — CanvasView 주입 이후에 실행
            var loader = _services.GetRequiredService<DeviceConfigLoader>();
            var collectLoader = _services.GetRequiredService<CollectConfigLoader>();

            await loader.LoadAsync();
            await collectLoader.LoadAsync();

            _services.GetRequiredService<CanvasViewModel>().RefreshDevicePalette();
        };

        win.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.Instance.Info("App", "IIoT.Studio 종료");

        // LogManager 큐 잔여 로그 파일에 모두 기록 후 종료
        await LogManager.Instance.StopAsync();

        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ★ Studio-P01: 플러그인 레지스트리
        services.AddSingleton<PluginRegistryService>();

        // ── 라이브러리 ViewModel
        services.AddSingleton<ScaleLibraryViewModel>();
        services.AddSingleton<AlarmLibraryViewModel>();
        services.AddSingleton<CommLibraryViewModel>();

        // ── 태그 템플릿
        services.AddSingleton<TagTemplateService>();
        services.AddSingleton<TagTemplateViewModel>(sp =>
            new TagTemplateViewModel(
                sp.GetRequiredService<TagTemplateService>()));

        // ── 장비 트리
        services.AddSingleton<DeviceTreeViewModel>(sp =>
            new DeviceTreeViewModel(
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>(),
                sp.GetRequiredService<TagTemplateViewModel>()));

        // ── 캔버스
        services.AddSingleton<CanvasViewModel>(sp =>
            new CanvasViewModel(
                sp.GetRequiredService<DeviceTreeViewModel>()));

        // ── 저장 서비스
        services.AddSingleton<DeviceConfigService>(sp =>
            new DeviceConfigService(
                sp.GetRequiredService<DeviceTreeViewModel>(),
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>(),
                sp.GetRequiredService<CommLibraryViewModel>()));

        services.AddSingleton<CollectConfigService>(sp =>
            new CollectConfigService(
                sp.GetRequiredService<CanvasViewModel>()));

        // ── 로드 서비스
        services.AddSingleton<DeviceConfigLoader>(sp =>
            new DeviceConfigLoader(
                sp.GetRequiredService<DeviceTreeViewModel>(),
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>(),
                sp.GetRequiredService<CommLibraryViewModel>()));

        services.AddSingleton<CollectConfigLoader>(sp =>
            new CollectConfigLoader(
                sp.GetRequiredService<CanvasViewModel>()));

        // ── MainViewModel
        services.AddSingleton<MainViewModel>(sp =>
            new MainViewModel(
                sp.GetRequiredService<DeviceTreeViewModel>(),
                sp.GetRequiredService<ScaleLibraryViewModel>(),
                sp.GetRequiredService<AlarmLibraryViewModel>(),
                sp.GetRequiredService<CommLibraryViewModel>(),
                sp.GetRequiredService<CanvasViewModel>(),
                sp.GetRequiredService<DeviceConfigService>(),
                sp.GetRequiredService<CollectConfigService>(),
                sp.GetRequiredService<DeviceConfigLoader>(),
                sp.GetRequiredService<PluginRegistryService>()
                ));  // ← 추가
 
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(sp.GetRequiredService<MainViewModel>()));

        return services.BuildServiceProvider();
    }

    // ── CanvasView 탐색 (비주얼 트리)
    private static Views.Canvas.CanvasView? _FindCanvasView(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is Views.Canvas.CanvasView cv) return cv;
            var found = _FindCanvasView(child);
            if (found is not null) return found;
        }
        return null;
    }
}