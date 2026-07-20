// ══════════════════════════════════════════════════════════
//  IIoT.Studio · App.xaml.cs
//  S-14: DeviceTreeViewModel 생성자에 TagTemplateViewModel 추가
//  S-15: OnStartup에서 설정 로드 (DeviceConfigLoader + CollectConfigLoader)
//  Studio-P01: PluginRegistryService DI 등록 + LoadPlugins() 호출
//  Studio-P01 fix: LogManager.Instance.Start() 추가
//  Studio-P04 fix: LoadPlugins()를 win.Loaded 안으로 이동
//                  → LogPanelView.LogAdded 구독 완료 후 실행되어야
//                    플러그인 로드 로그가 패널에 표시됨
//  MG-03 (2026-07-09): HealthPipeServer 추가 — Manager 헬스체크(NamedPipe
//                      핑/퐁) 응답. 파이프명 "IIoT.Health.IIoT.Studio".
//                      OnExit 에서 DisposeAsync 정리.
//  생성: 2026-06-15 / 수정: 2026-07-09 (MG-03)
// ══════════════════════════════════════════════════════════

using IIoT.Contracts.Health;
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
    private IServiceProvider?     _services;

    // ★ MG-03: Manager 헬스체크 응답 서버 (NamedPipe 핑/퐁)
    private HealthPipeServer?     _healthServer;

    // ★ C-SET-01 후속: studio-settings.json — LogManager.Start() 및 DI 그래프
    //   구성(DeviceTreeViewModel/MainViewModel 생성자)보다 반드시 먼저 동기 로드
    private StudioSettingsLoader? _studioSettings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① 테마 (가장 먼저 — 창 표시 전 색상 준비)
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        // ★ C-SET-01 후속: studio-settings.json 동기 로드 (LogManager.Start() 보다 먼저 —
        //   아래 LogConfig 가 이 값을 사용하고, DeviceTreeViewModel/MainViewModel 생성자도
        //   DI 그래프 구성 시점에 이 값을 필요로 함)
        _studioSettings = new StudioSettingsLoader();
        _studioSettings.LoadSync();
        var logCfg = _studioSettings.Settings.Log;

        // ② LogManager 시작 (반드시 DI 빌드 전에 호출)
        LogManager.Instance.Start(new LogConfig
        {
            LogRootPath         = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log"),
            ValidDays           = logCfg.ValidDays,
            FileFormat          = LogFileFormat.Both,
            MinimumLevel        = logCfg.MinimumLevel,
            MinimumConsoleLevel = logCfg.MinimumConsoleLevel,
            MaxDisplayCount     = logCfg.MaxDisplayCount
        });

        LogManager.Instance.Info("App", "IIoT.Studio 시작");

        // ★ MG-03: 헬스체크 응답 서버 시작 (Manager 가 핑을 보냄)
        // ★ HM-22: 원격 설정 조회/저장 콜백 추가 — studio-settings.json 원문을
        //   그대로 읽고 쓴다(로더의 인메모리 Settings 는 건드리지 않음 — 다른
        //   프로그램의 원격 저장과 동일하게 "재시작해야 반영" 원칙 유지).
        _healthServer = new HealthPipeServer(
            "IIoT.Studio",
            statusProvider: () => "설정 편집기 정상",
            onLog: m => LogManager.Instance.Debug("Health", m),
            settingsProvider: () => File.Exists(StudioSettingsLoader.SettingsPath)
                ? File.ReadAllText(StudioSettingsLoader.SettingsPath, System.Text.Encoding.UTF8)
                : "{}",
            settingsSaver: json =>
            {
                try
                {
                    File.WriteAllText(StudioSettingsLoader.SettingsPath, json, System.Text.Encoding.UTF8);
                    return "";
                }
                catch (Exception ex) { return ex.Message; }
            });
        _healthServer.Start();

        // ③ DI 빌드
        _services = _ConfigureServices(_studioSettings);

        // ④ 창 생성
        var win = _services.GetRequiredService<MainWindow>();

        win.Loaded += async (_, _) =>
        {
            // ── CanvasView 속성 주입 ─────────────────────────
            var canvasView = _FindCanvasView(win);
            if (canvasView is not null)
            {
                var deviceTreeVm  = _services.GetRequiredService<DeviceTreeViewModel>();
                var tagTemplateVm = _services.GetRequiredService<TagTemplateViewModel>();

                canvasView.DeviceTreeVm = deviceTreeVm;
                canvasView.GetTemplates = () => tagTemplateVm.Templates;
            }

            // ★ Studio-P04 fix: LoadPlugins()를 Loaded 안으로 이동
            //   이유: LoadPlugins() 로그가 LogPanelView.LogAdded 구독보다
            //         먼저 발생하면 패널에 표시되지 않음.
            //   Loaded 시점에는 LogPanelView가 이미 LogAdded를 구독 완료.
            _services.GetRequiredService<PluginRegistryService>()
                     .LoadPlugins();

            // ★ S-15: 설정 로드 — CanvasView 주입 이후에 실행
            var loader        = _services.GetRequiredService<DeviceConfigLoader>();
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

        // ★ MG-03: 헬스체크 파이프 정리 (내부 2초 타임아웃 — 무한 대기 없음)
        if (_healthServer is not null)
            await _healthServer.DisposeAsync();

        await LogManager.Instance.StopAsync();
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    private static IServiceProvider _ConfigureServices(StudioSettingsLoader studioSettings)
    {
        var services = new ServiceCollection();

        // ★ C-SET-01 후속: OnStartup 에서 이미 LoadSync() 완료된 인스턴스를 그대로 등록
        //   (다른 프로그램의 AddSingleton<XxxSettingsLoader>() 후 나중에 LoadAsync() 하는
        //   방식과 달리, Studio 는 DI 그래프 구성 시점부터 값이 필요해 미리 로드해둔다)
        services.AddSingleton(studioSettings);

        // ★ C-SET-01 후속: 환경설정 화면 ViewModel (DeviceTreeViewModel/MainViewModel 보다 먼저 등록)
        services.AddSingleton<SettingsViewModel>();

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
                sp.GetRequiredService<TagTemplateViewModel>(),
                sp.GetRequiredService<StudioSettingsLoader>()));   // ★ C-SET-01 후속

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
                sp.GetRequiredService<PluginRegistryService>(),
                sp.GetRequiredService<StudioSettingsLoader>(),     // ★ C-SET-01 후속
                sp.GetRequiredService<SettingsViewModel>()));      // ★ C-SET-01 후속

        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(sp.GetRequiredService<MainViewModel>()));

        return services.BuildServiceProvider();
    }

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
