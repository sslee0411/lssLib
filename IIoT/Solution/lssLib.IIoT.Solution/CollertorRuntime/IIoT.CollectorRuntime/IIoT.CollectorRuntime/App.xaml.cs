// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · App.xaml.cs
// ══════════════════════════════════════════════════════════

using IIoT.CollectorRuntime.Core;
using IIoT.CollectorRuntime.ViewModels;
using IIoT.UI.Themes;
using lssLib.Log;
using System.IO;
using System.Windows;

namespace IIoT.CollectorRuntime;

public partial class App : Application
{
    private ThemeSettingsService?  _themeSettings;
    private MainViewModel?         _vm;
    private ConfigReloadWatcher?   _watcher;

    private static string ConfigDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

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
        LogManager.Instance.Info("App", "IIoT CollectorRuntime 시작");

        Directory.CreateDirectory(ConfigDir);

        // ③ 수집 엔진 + ViewModel
        var engine = new CollectionEngine(ConfigDir);
        _vm        = new MainViewModel(engine);

        // ④ .signal 감시 (DeviceManager 설정 변경 자동 감지)
        _watcher = new ConfigReloadWatcher(ConfigDir);
        _watcher.ReloadRequested += async _ => await engine.RestartAsync();
        _watcher.Start();

        new MainWindow(_vm).Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_vm?.Engine.IsRunning == true)
            await _vm.Engine.StopAsync();

        _watcher?.Dispose();
        _vm?.Dispose();
        _themeSettings?.Dispose();

        LogManager.Instance.Info("App", "IIoT CollectorRuntime 종료");
        await LogManager.Instance.StopAsync();

        base.OnExit(e);
    }
}
