// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · App.xaml.cs
//  역할: 애플리케이션 진입점 — 테마 초기화 (반드시 첫 번째) + DI 구성
//  Phase 0: 테마 통합
// ══════════════════════════════════════════════════════════

using IIoT.UI.Themes;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace IIoT.DeviceManager;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private ThemeSettingsService? _themeSettings;
    private IServiceProvider? _services;

    // §2 ─ 시작 / 종료 ─────────────────────────────────────────
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ★ Phase 0 규칙: 테마 초기화는 반드시 첫 번째
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        // DI 컨테이너 구성
        _services = ConfigureServices();

        // 메인 윈도우 생성 (DI에서 가져옴)
        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // ★ Phase 0 규칙: ThemeChanged 정적 이벤트 해제 — 반드시 Dispose 호출
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    // §3 ─ DI 구성 ───────────────────────────────────────────
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Views
        services.AddTransient<MainWindow>();

        // TODO Phase 1~: ViewModels, Services 등록 예정
        // services.AddSingleton<DeviceTreeViewModel>();
        // services.AddSingleton<XmlWriteService>();

        return services.BuildServiceProvider();
    }
}