// ══════════════════════════════════════════════════════════
//  IIoT.Collector · App.xaml.cs
//  Fix: EventBus.Subscribe 반환값(IDisposable) → 필드 저장
//       OnExit 에서 Dispose() 호출 (메모리 누수 방지)
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core;
using IIoT.Collector.ViewModels;
using IIoT.Shared.Models;
using IIoT.UI.Themes;
using lssLib.Log;
using lssLib.Messaging;
using System.IO;
using System.Windows;

namespace IIoT.Collector;

public partial class App : Application
{
    private ThemeSettingsService? _themeSettings;
    private CollectionEngine?     _collectionEngine;
    private MonitorEngine?        _monitorEngine;
    private ConfigReloadWatcher?  _watcher;
    private MainViewModel?        _vm;
    // ★ Fix: IDisposable 필드 저장
    private IDisposable?          _tagEventSub;

    private static string ConfigDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        LogManager.Instance.Start(new LogConfig
        {
            LogRootPath  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
            ValidDays    = 30,
            FileFormat   = LogFileFormat.Both,
            MinimumLevel = LogLevel.Debug,
        });
        LogManager.Instance.Info("App", "IIoT Collector 시작 (수집+감지 통합 — V3)");

        Directory.CreateDirectory(ConfigDir);

        // ③ 엔진 생성 — 동일 프로세스
        _collectionEngine = new CollectionEngine(ConfigDir);
        _monitorEngine    = new MonitorEngine(ConfigDir);

        // ④ ★ EventBus 연결 (in-process 정상) — Fix: 반환값 저장
        _tagEventSub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(async ev =>
            await _monitorEngine.ProcessTagAsync(ev.Value));

        // ⑤ ViewModel
        _vm = new MainViewModel(_collectionEngine, _monitorEngine);

        // ⑥ FSW: *.signal 감지 → 자동 재시작
        _watcher = new ConfigReloadWatcher(ConfigDir);
        _watcher.ReloadRequested += async signalFile =>
        {
            LogManager.Instance.Info("App", $"설정 변경 → 재시작 ({signalFile})");
            await _collectionEngine.RestartAsync();
            await _monitorEngine.RestartAsync();
        };
        _watcher.Start();

        // ⑦ Singleton — Window 1개만 생성
        new MainWindow(_vm).Show();

        // ⑧ 엔진 시작 (비동기)
        _ = Task.Run(async () =>
        {
            await _collectionEngine.StartAsync();
            await _monitorEngine.StartAsync();
        });
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        // ★ Fix: EventBus 구독 해제
        _tagEventSub?.Dispose();

        if (_collectionEngine is not null) await _collectionEngine.DisposeAsync();
        if (_monitorEngine    is not null) await _monitorEngine.DisposeAsync();

        _vm?.Dispose();
        _themeSettings?.Dispose();

        LogManager.Instance.Info("App", "IIoT Collector 종료");
        await LogManager.Instance.StopAsync();
        base.OnExit(e);
    }
}
