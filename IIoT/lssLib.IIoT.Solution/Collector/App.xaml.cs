// ══════════════════════════════════════════════════════════
//  IIoT.Collector · App.xaml.cs
//  역할: Collector 통합 앱 진입점
//        구 CollectorRuntime + Monitor 완전 통합
//  V3 Step4: 신규
//
//  핵심 변경:
//    CollectionEngine + MonitorEngine → 동일 프로세스
//    → EventBus.Subscribe<TagValueUpdatedEvent> 정상 동작
//    → cross-process EventBus 버그 근본 해결
//    → MQTT 브로커 의존성 제거
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core;
using IIoT.Collector.ViewModels;
using IIoT.UI.Themes;
using lssLib.Log;
using lssLib.Messaging;
using System.IO;
using System.Windows;

namespace IIoT.Collector;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private ThemeSettingsService?  _themeSettings;
    private CollectionEngine?      _collectionEngine;
    private MonitorEngine?         _monitorEngine;
    private ConfigReloadWatcher?   _watcher;
    private MainViewModel?         _vm;

    private static string ConfigDir =>
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
            LogRootPath  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
            ValidDays    = 30,
            FileFormat   = LogFileFormat.Both,
            MinimumLevel = LogLevel.Debug,
        });
        LogManager.Instance.Info("App", "IIoT Collector 시작 (수집+감지 통합)");

        Directory.CreateDirectory(ConfigDir);

        // ③ 엔진 생성 — 동일 프로세스
        _collectionEngine = new CollectionEngine(ConfigDir);
        _monitorEngine    = new MonitorEngine(ConfigDir);

        // ④ ★ 핵심: 동일 프로세스 내 EventBus → 정상 동작
        //    이전(V2): CollectorRuntime → EventBus → Monitor (cross-process, 수신 불가)
        //    현재(V3): 동일 App 내 EventBus.Publish/Subscribe (in-process, 정상)
        EventBus.Instance.Subscribe<TagValueUpdatedEvent>(async e =>
            await _monitorEngine.ProcessTagAsync(e));

        // ⑤ ViewModel (수집+알람 통합)
        _vm = new MainViewModel(_collectionEngine, _monitorEngine);

        // ⑥ FSW: config.json .signal 감지 → 자동 재시작
        _watcher = new ConfigReloadWatcher(ConfigDir);
        _watcher.ReloadRequested += async _ =>
        {
            LogManager.Instance.Info("App", "설정 변경 감지 → 재시작");
            await _collectionEngine.RestartAsync();
            await _monitorEngine.RestartAsync();
        };
        _watcher.Start();

        // ⑦ ★ Singleton 패턴 — Window 1개만 생성
        new MainWindow(_vm).Show();

        // ⑧ 엔진 시작
        _ = Task.Run(async () =>
        {
            await _collectionEngine.StartAsync();
            await _monitorEngine.StartAsync();
        });
    }

    // §3 ─ 종료 ───────────────────────────────────────────────
    protected override async void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();

        if (_collectionEngine is not null)
            await _collectionEngine.DisposeAsync();
        if (_monitorEngine is not null)
            await _monitorEngine.DisposeAsync();

        _vm?.Dispose();
        _themeSettings?.Dispose();

        LogManager.Instance.Info("App", "IIoT Collector 종료");
        await LogManager.Instance.StopAsync();

        base.OnExit(e);
    }
}
