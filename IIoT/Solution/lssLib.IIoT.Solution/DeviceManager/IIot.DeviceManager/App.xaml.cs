// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · App.xaml.cs
//  역할: 애플리케이션 진입점
//        ① 테마 초기화 (Phase 0)
//        ② LogManager 시작 (Phase 1)
//        ③ ConfigInitializer — JSON 파일 존재 보장 (Phase 1 Update)
//        ④ DI 컨테이너 — Phase 1 서비스 등록
//  Phase 1 Update: ConfigInitializer 호출 추가
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.Core.Config;
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.DeviceManager;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private ThemeSettingsService? _themeSettings;
    private IServiceProvider? _services;

    // §2 ─ 설정 파일 디렉터리 ─────────────────────────────────
    /// <summary>
    /// 설정 JSON 파일 저장 경로:
    ///   실행파일과 동일 디렉터리 / config
    ///   예) C:\IIoT\DeviceManager\config\
    /// </summary>
    private static string ConfigDirectory =>
        Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Config");

    // §3 ─ 시작 ───────────────────────────────────────────────
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① 테마 초기화 — 반드시 첫 번째 (Phase 0 규칙)
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        // ② LogManager 시작 (Phase 1)
        _InitLogManager();

        LogManager.Instance.Info("App", "IIoT DeviceManager 시작");

        // ③ JSON 설정 파일 존재 보장 (Phase 1 Update)
        //    config/*.json 없으면 *.json.sample 복사, sample도 없으면 기본값 생성
        ConfigInitializer.EnsureConfigFiles(ConfigDirectory);

        // ④ DI 구성 + 메인 윈도우 표시
        _services = _ConfigureServices();
        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    // §4 ─ 종료 ───────────────────────────────────────────────
    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.Instance.Info("App", "IIoT DeviceManager 종료");

        // ① LogManager 비동기 플러시 후 중단
        await LogManager.Instance.StopAsync();

        // ② 테마 이벤트 해제 (Phase 0 규칙)
        _themeSettings?.Dispose();

        base.OnExit(e);
    }

    // §5 ─ LogManager 초기화 ──────────────────────────────────
    private static void _InitLogManager()
    {
        var logConfig = new LogConfig
        {
            LogRootPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "logs"),
            ValidDays = 30,
            FileFormat = LogFileFormat.Both,   // .txt + .csv 동시 저장
            MinimumLevel = LogLevel.Debug,
            MaxDisplayCount = 2000,
        };
        LogManager.Instance.Start(logConfig);
    }

    // §6 ─ DI 구성 ────────────────────────────────────────────
    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── Phase 1: Config 서비스 ───────────────────────────
        // 경로를 생성자에 주입하는 팩토리 방식 — ConfigDirectory 는 정적 계산
        services.AddSingleton<JsonWriteService>(
            _ => new JsonWriteService(ConfigDirectory));

        services.AddSingleton<JsonConfigLoader>(
            _ => new JsonConfigLoader(ConfigDirectory));

        // ── Views ────────────────────────────────────────────
        services.AddTransient<MainWindow>();

        // TODO Phase 2~: ViewModels, DeviceTreeViewModel 등 추가 예정

        return services.BuildServiceProvider();
    }
}