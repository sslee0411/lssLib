// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · App.xaml.cs
//  역할: 모니터 애플리케이션 진입점
//  Phase 10: 신규
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core;
using IIoT.Monitor.ViewModels;
using IIoT.UI.Themes;
using lssLib.Log;
using System.IO;
using System.Windows;

namespace IIoT.Monitor;

public partial class App : Application
{
    private ThemeSettingsService? _themeSettings;
    private MainViewModel?        _vm;
    private MonitorEngine?        _engine;

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
        LogManager.Instance.Info("App", "IIoT Monitor 시작");

        Directory.CreateDirectory(ConfigDir);

        // ③ MonitorEngine + ViewModel
        _engine = new MonitorEngine(ConfigDir);
        _vm     = new MainViewModel(_engine);

        new MainWindow(_vm).Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_engine is not null)
            await _engine.DisposeAsync();

        _vm?.Dispose();
        _themeSettings?.Dispose();

        LogManager.Instance.Info("App", "IIoT Monitor 종료");
        await LogManager.Instance.StopAsync();

        base.OnExit(e);
    }
}
