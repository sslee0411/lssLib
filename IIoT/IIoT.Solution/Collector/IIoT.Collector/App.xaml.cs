// ══════════════════════════════════════════════════════════
//  IIoT.Collector · App.xaml.cs
//  역할: 애플리케이션 진입점
//        ① 테마 복원 (창 표시 전 — 색상 준비)
//        ② LogManager.Instance.Start() (DI 빌드 전 필수)
//        ③ DI 서비스 구성 (_ConfigureServices)
//        ④ 창 생성 후 Loaded 이벤트에서 LoadPlugins() 호출
//           (LoadPlugins 로그가 LogPanelView.LogAdded 구독 완료 후 발생하도록)
//        ⑤ OnExit: LogManager.StopAsync() + ThemeSettingsService.Dispose()
//
//  Studio-P04 fix 적용:
//    LoadPlugins() 를 win.Loaded 안으로 이동
//    → LogPanelView 가 LogAdded 구독 완료 후 플러그인 로그가 패널에 표시됨
//
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Engine;
using IIoT.Collector.SignalR;
using IIoT.Collector.Storage.Query;
using IIoT.Collector.Views.Trend;
using IIoT.Collector.Storage;
using lssLib.Net;
using IIoT.Collector.Views.Alarm;
using IIoT.Collector.Views.Flow;
using IIoT.Collector.Core.Plugin;
using IIoT.Collector.ViewModels;
using IIoT.Collector.Views.Status;
using IIoT.Collector.Notification;
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.Collector;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private ThemeSettingsService? _themeSettings;
    private IServiceProvider?     _services;

    // §2 ─ 시작 ───────────────────────────────────────────────

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① 테마 (가장 먼저 — 창 표시 전 색상 준비)
        _themeSettings = new ThemeSettingsService();
        _themeSettings.LoadAndApply(this);

        // ② LogManager 시작 (반드시 DI 빌드 전에 호출)
        LogManager.Instance.Start(new LogConfig
        {
            LogRootPath         = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log"),
            ValidDays           = 30,
            FileFormat          = LogFileFormat.Both,
            MinimumLevel        = LogLevel.Debug,
            MinimumConsoleLevel = LogLevel.Info,
            MaxDisplayCount     = 2000
        });

        LogManager.Instance.Info("App", "IIoT.Collector 시작");

        // ③ DI 빌드
        _services = _ConfigureServices();

        // ④ 창 생성
        var win = _services.GetRequiredService<MainWindow>();

        win.Loaded += async (_, _) =>
        {
            // ★ Studio-P04 fix 동일 패턴:
            //   LoadPlugins() 를 Loaded 안으로 이동
            //   이유: LoadPlugins 로그가 LogPanelView.LogAdded 구독보다
            //         먼저 발생하면 패널에 표시되지 않음.
            //   Loaded 시점: LogPanelView 가 LogAdded 구독 완료.
            _services.GetRequiredService<CollectorPluginService>()
                     .LoadPlugins();

            // ★ C-01: device.json 로드는 플러그인 로드 이후에 수행
            //   (미등록 드라이버 경고가 정확히 동작하려면 플러그인 목록이 먼저 채워져야 함)
            await _services.GetRequiredService<CollectorConfigLoader>()
                            .LoadAsync();

            // ★ C-04: 설정 로드 완료 직후 LiveTags 초기 행 구성 + EventBus 구독 시작
            //   (FlowEngine.StartAsync() 보다 반드시 먼저 호출 — 폴링 결과를 놓치지 않도록)
            _services.GetRequiredService<StatusViewModel>()
                     .Initialize();

            // ★ C-06: 알람 감지기 초기화 + AlarmView 구독 시작 (FlowEngine 보다 먼저)
            _services.GetRequiredService<AlarmStateManager>()
                     .Initialize();
            _services.GetRequiredService<AlarmViewModel>()
                     .Initialize();

            // ★ C-14: 에스컬레이션 관리자 초기화 (AlarmStateManager 이후 — 동일 EventBus 이벤트 순서 보장)
            _services.GetRequiredService<EscalationManager>()
                     .Initialize();

            // ★ C-07: settings.json 로드 (SQLite/InfluxDB Provider 결정)
            await _services.GetRequiredService<CollectorSettingsLoader>()
                            .LoadAsync();

            // ★ C-07: 저장소 초기화 (DB 연결 / 테이블 생성)
            await _services.GetRequiredService<ITimeSeriesStore>()
                            .InitializeAsync();

            // ★ C-03: 설정 로드 완료 후 수집 시작
            await _services.GetRequiredService<FlowEngine>()
                            .StartAsync();

            // ★ C-07: SDT 필터 + 저장 서비스 초기화 (FlowEngine 이후)
            _services.GetRequiredService<DataCollectionService>()
                     .Initialize();

            // ★ C-09: 수집 흐름 뷰 초기화 (FlowEngine 시작 후)
            _services.GetRequiredService<FlowViewModel>()
                     .Initialize();

            // ★ C-13: 트렌드 뷰 초기화 (Tag 목록 구성)
            _services.GetRequiredService<TrendViewModel>()
                     .Initialize();

            // ★ C-10: MQTT 발행 서비스 초기화 (활성화 여부는 settings.json 에서 결정)
            await _services.GetRequiredService<MqttPublishService>()
                           .InitializeAsync();

            // ★ C-11: SignalR Hub 서버 시작 + Push 구독
            await _services.GetRequiredService<SignalRHostService>()
                           .StartAsync();
            _services.GetRequiredService<SignalRPushService>()
                     .Initialize();

            // ★ C-08: .signal 파일 감시 시작 (모든 서비스 준비 완료 후)
            var watchPath = _services.GetRequiredService<CollectorSettingsLoader>()
                                     .Settings.Storage.WatchPath;
            _services.GetRequiredService<ConfigReloadWatcher>()
                     .Start(string.IsNullOrWhiteSpace(watchPath) ? null : watchPath);
        };

        win.Show();
    }

    // §3 ─ 종료 ───────────────────────────────────────────────

    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.Instance.Info("App", "IIoT.Collector 종료");

        // ★ C-03: 수집 정지 (드라이버 연결 해제) — LogManager 정지보다 먼저
        if (_services is not null)
        {
            // C-11: SignalR Hub 종료
            await _services.GetRequiredService<SignalRHostService>().DisposeAsync();
            _services.GetRequiredService<SignalRPushService>().Dispose();
            // C-10: MQTT 서비스 종료
            await _services.GetRequiredService<MqttPublishService>().DisposeAsync();
            // C-08: FSW 감지 종료
            await _services.GetRequiredService<ConfigReloadWatcher>().DisposeAsync();
            // C-07: 저장 서비스 먼저 종료 (남은 배치 Flush)
            await _services.GetRequiredService<DataCollectionService>().DisposeAsync();
            await _services.GetRequiredService<FlowEngine>().StopAsync();
        }

        await LogManager.Instance.StopAsync();
        _themeSettings?.Dispose();
        base.OnExit(e);
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── 플러그인 레지스트리 (Col-Base-0 핵심)
        services.AddSingleton<CollectorPluginService>();

        // ── 설정 로더 (C-01)
        services.AddSingleton<CollectorConfigLoader>();

        // ── 스케일 변환 엔진 (C-05)
        services.AddSingleton<ScaleEngine>();

        // ── 알람 감지·관리 (C-06)
        services.AddSingleton<AlarmStateManager>();
        services.AddSingleton<AlarmViewModel>();
        services.AddSingleton<AlarmView>(sp =>
            new AlarmView(sp.GetRequiredService<AlarmViewModel>()));

        // ── 수집 흐름 엔진 (C-03)
        services.AddSingleton<FlowEngine>();

        // ── 설정 로더 (C-07)
        services.AddSingleton<CollectorSettingsLoader>();

        // ★ C-07: Provider 에 따라 SQLite 또는 InfluxDB 등록
        services.AddSingleton<ITimeSeriesStore>(sp =>
        {
            var sl = sp.GetRequiredService<CollectorSettingsLoader>();
            return sl.Settings.Storage.Provider.Equals("InfluxDB",
                StringComparison.OrdinalIgnoreCase)
                ? new InfluxDbTimeSeriesStore(sl)
                : new SqliteTimeSeriesStore(sl);
        });
        services.AddSingleton<DataCollectionService>();

        // ── MQTT 발행 서비스 (C-10)
        services.AddSingleton<MqttPublishService>();

        // ── 수집 이력 조회 (C-13)
        services.AddSingleton<TrendQueryService>();
        services.AddSingleton<TrendViewModel>();
        services.AddSingleton<TrendView>(sp =>
            new TrendView(sp.GetRequiredService<TrendViewModel>()));

        // ── SignalR Hub 서비스 (C-11)
        services.AddSingleton<SignalRHostService>();
        services.AddSingleton<SignalRPushService>();

        // ── 설정 변경 감지 (C-08)
        services.AddSingleton<ConfigReloadWatcher>();

        // ── 수집 흐름 시각화 (C-09)
        services.AddSingleton<FlowViewModel>();
        services.AddSingleton<FlowView>(sp =>
            new FlowView(sp.GetRequiredService<FlowViewModel>()));

        // ── 메인 ViewModel
        services.AddSingleton<MainViewModel>();

        // ── 수집 현황 ViewModel/View (C-04)
        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<StatusView>(sp =>
            new StatusView(sp.GetRequiredService<StatusViewModel>()));

        // ★ AddSingleton 필수 (Transient → 이중 창 버그)
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<MainViewModel>(),
                sp.GetRequiredService<StatusView>(),
                sp.GetRequiredService<AlarmView>(),
                sp.GetRequiredService<FlowView>(),
                sp.GetRequiredService<TrendView>()));

        services.AddSingleton<NotificationService>();
        services.AddSingleton<EscalationManager>();

        return services.BuildServiceProvider();
    }
}
