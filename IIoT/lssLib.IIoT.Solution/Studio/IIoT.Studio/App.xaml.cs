// ══════════════════════════════════════════════════════════
//  IIoT.Studio · App.xaml.cs
//  역할: 애플리케이션 시작·종료 진입점
//        - 테마 복원
//        - DI 구성
//        - MainWindow 표시
//  Base-0: 최소 구조 (서비스 없음, MainWindow 만 등록)
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using IIoT.Studio.ViewModels;
using IIoT.UI.Themes;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace IIoT.Studio;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private ThemeSettingsService? _themeSettings;
    private IServiceProvider? _services;

    // §2 ─ 시작 ───────────────────────────────────────────────
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① 저장된 테마 복원 (파일 없으면 DarkNavy 기본값 적용)
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        // ② DI 구성
        _services = _ConfigureServices();

        // ③ MainWindow 표시
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

        // ── 메인 ViewModel (DeviceTreeViewModel 주입) ────────
        services.AddSingleton<MainViewModel>(sp =>
            new MainViewModel(sp.GetRequiredService<DeviceTreeViewModel>()));

        // ── MainWindow ───────────────────────────────────────
        // ★ 반드시 AddSingleton (AddTransient → 이중 창 버그)
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(sp.GetRequiredService<MainViewModel>()));

        return services.BuildServiceProvider();
    }
}