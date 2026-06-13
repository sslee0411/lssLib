// ══════════════════════════════════════════════════════════
//  IIoT.UI.Themes.Demo · App.xaml.cs
//  역할: 테마 데모 앱 진입점
//  생성: 2025-05-16
// ══════════════════════════════════════════════════════════
using System.Windows;
using IIoT.UI.Themes;
namespace IIoT.UI.Themes.Demo;

public partial class App : Application
{
    private ThemeSettingsService? _themeSettings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 저장된 테마 복원 (기본: DarkNavy)
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeSettings?.Dispose();
        base.OnExit(e);
    }
}
