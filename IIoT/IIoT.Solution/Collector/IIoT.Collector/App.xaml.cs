// ══════════════════════════════════════════════════════════
//  IIoT.Collector · App.xaml.cs
//  역할: 애플리케이션 진입점
//        ① 테마 복원 (창 표시 전 — 색상 준비)
//        ② LogManager.Instance.Start() (DI 빌드 전 필수)
//        ③ CommandQueue.Instance.Start() (★ C-15 버그 수정 — EventBus.Publish 의존)
//        ④ DI 서비스 구성 (_ConfigureServices)
//        ⑤ 창 생성 후 Loaded 이벤트에서 LoadPlugins() 호출
//        ⑥ OnExit: 역순 종료
//
//  누적 반영 이력:
//    C-14 알람 에스컬레이션 / C-15 강제쓰기 / C-16 이상값 필터 /
//    C-17 집계 엔진 / C-18 가상 Tag / C-19 일시정지 /
//    C-EX-01 DeviceInstance 통합 조회 + [장비] 탭 /
//    C-EX-02~08 보안·보존·억제·백업·CSV·자체진단 /
//    C-EX-10 CollectorId 반영 — settings.json 을 DeviceInstanceService.Initialize()
//            보다 먼저 로드하도록 순서 변경 (CollectorId 가 DeviceInstance 에 포함되어야 함)
//    MG-03 (2026-07-09): HealthPipeServer 추가 — Manager 헬스체크(NamedPipe
//            핑/퐁) 응답. 파이프명 "IIoT.Health.IIoT.Collector".
//            pong 상태문구에 FlowEngine 실행 여부 포함. OnExit 정리 세트 추가.
//
//  생성: 2026-06-29 / 수정: 2026-07-09 (MG-03)
// ══════════════════════════════════════════════════════════

using IIoT.Contracts.Health;
using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Engine;
using IIoT.Collector.SignalR;
using IIoT.Collector.Storage.Query;
using IIoT.Collector.Views.Trend;
using IIoT.Collector.Views.Device;
using IIoT.Collector.Storage;
using lssLib.Net;
using IIoT.Collector.Views.Alarm;
using IIoT.Collector.Views.Flow;
using IIoT.Collector.Core.Plugin;
using IIoT.Collector.ViewModels;
using IIoT.Collector.Views.Status;
using IIoT.Collector.Notification;
using IIoT.UI.Themes;
using lssLib.Messaging;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.Collector;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private ThemeSettingsService? _themeSettings;
    private IServiceProvider? _services;

    // ★ MG-03: Manager 헬스체크 응답 서버 (NamedPipe 핑/퐁)
    private HealthPipeServer? _healthServer;

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
            LogRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log"),
            ValidDays = 30,
            FileFormat = LogFileFormat.Both,
            MinimumLevel = LogLevel.Debug,
            MinimumConsoleLevel = LogLevel.Info,
            MaxDisplayCount = 2000
        });

        LogManager.Instance.Info("App", "IIoT.Collector 시작");

        // ★ C-15 버그 수정: CommandQueue 시작 누락
        //   EventBus.Publish() 가 내부적으로 CommandQueue 를 통해 디스패치하므로
        //   반드시 DI 빌드 및 FlowEngine 시작 전에 호출해야 함
        //   (미호출 시 EventBus.Publish 호출 시점에 InvalidOperationException 발생)
        CommandQueue.Instance.Start();

        // ③ DI 빌드
        _services = _ConfigureServices();

        // ★ MG-03: 헬스체크 응답 서버 시작 (Manager 가 핑을 보냄)
        //   상태문구에 FlowEngine 실행 여부 포함 — "UI 만 살아있는 좀비" 감지용
        _healthServer = new HealthPipeServer(
            "IIoT.Collector",
            statusProvider: () =>
            {
                try
                {
                    var flow = _services?.GetService<FlowEngine>();
                    return flow is null ? "초기화 중"
                                        : $"수집 엔진 {(flow.IsRunning ? "실행 중" : "정지")}";
                }
                catch { return "상태 조회 불가"; }
            },
            onLog: m => LogManager.Instance.Debug("Health", m));
        _healthServer.Start();

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

            // ★ C-07 / C-EX-10: settings.json 로드 — CollectorId 를 포함해야 하므로
            //   DeviceInstanceService.Initialize() 보다 반드시 먼저 호출
            await _services.GetRequiredService<CollectorSettingsLoader>()
                            .LoadAsync();

            // ★ C-EX-01: DeviceInstance 트리 조립 (ConfigLoader + SettingsLoader 로드 직후)
            _services.GetRequiredService<DeviceInstanceService>()
                     .Initialize();

            // ★ C-EX-01-6: 장비 트리 화면 스냅샷 갱신 시작
            _services.GetRequiredService<DeviceTreeViewModel>()
                     .Initialize();

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

            // ★ C-07: 저장소 초기화 (DB 연결 / 테이블 생성)
            await _services.GetRequiredService<ITimeSeriesStore>()
                            .InitializeAsync();

            // ★ C-EX-03: 감사 로그 초기화 (저장소 초기화 이후 — 같은 DB 파일 사용)
            await _services.GetRequiredService<AuditLogService>()
                           .InitializeAsync();

            // ★ C-EX-04: 데이터 보존 정책 초기화
            await _services.GetRequiredService<DataRetentionService>()
                           .InitializeAsync();

            // ★ C-EX-06: DB 자동 백업 초기화
            _services.GetRequiredService<DbBackupService>()
                     .Initialize();

            // ★ C-EX-08: 자체 진단 시작
            _services.GetRequiredService<SelfHealthService>()
                     .Initialize();

            // ★ C-17: 집계 서비스 초기화 (SQLite Provider 일 때만 활성화됨)
            await _services.GetRequiredService<TagAggregationService>()
                            .InitializeAsync();

            // ★ C-16: 이상값 필터 초기화 (FlowEngine 시작 전 — 첫 폴링부터 적용되도록)
            _services.GetRequiredService<AnomalyFilterService>()
                     .Initialize();

            // ★ C-03: 설정 로드 완료 후 수집 시작
            await _services.GetRequiredService<FlowEngine>()
                            .StartAsync();

            // ★ C-18: 가상 Tag 엔진 초기화 (FlowEngine 시작 이후)
            _services.GetRequiredService<VirtualTagEngine>()
                     .Initialize();

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

        // ★ MG-03: 헬스체크 파이프 정리 (내부 2초 타임아웃 — 무한 대기 없음)
        if (_healthServer is not null)
            await _healthServer.DisposeAsync();

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
            // C-17: 집계 서비스 종료
            await _services.GetRequiredService<TagAggregationService>().DisposeAsync();
            // C-18: 가상 Tag 엔진 종료
            _services.GetRequiredService<VirtualTagEngine>().Dispose();

            // ★ C-EX-03/04/06/08: 신규 서비스 종료
            await _services.GetRequiredService<AuditLogService>().DisposeAsync();
            await _services.GetRequiredService<DataRetentionService>().DisposeAsync();
            _services.GetRequiredService<DbBackupService>().Dispose();
            _services.GetRequiredService<SelfHealthService>().Dispose();

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

        // ── DeviceInstance 통합 조회 서비스 (C-EX-01 신규)
        services.AddSingleton<DeviceInstanceService>();

        // ── 스케일 변환 엔진 (C-05)
        services.AddSingleton<ScaleEngine>();

        // ── 알람 감지·관리 (C-06)
        services.AddSingleton<AlarmStateManager>();
        services.AddSingleton<AlarmShelvingService>();   // ★ C-EX-05 신규 (AlarmViewModel 보다 먼저 등록)
        services.AddSingleton<AlarmViewModel>(sp =>
            new AlarmViewModel(
                sp.GetRequiredService<AlarmStateManager>(),
                sp.GetRequiredService<AlarmShelvingService>()));
        services.AddSingleton<AlarmView>(sp =>
            new AlarmView(sp.GetRequiredService<AlarmViewModel>()));

        // ── DeviceInstance 통합 조회 View (C-EX-01-6 신규)
        services.AddSingleton<DeviceTreeViewModel>(sp =>
            new DeviceTreeViewModel(
                sp.GetRequiredService<DeviceInstanceService>(),
                sp.GetRequiredService<CollectorSettingsLoader>()));
        services.AddSingleton<DeviceTreeView>(sp =>
            new DeviceTreeView(sp.GetRequiredService<DeviceTreeViewModel>()));

        // ── 이상값 필터 (C-16)
        services.AddSingleton<AnomalyFilterService>();

        // ── 수집 흐름 엔진 (C-03)
        services.AddSingleton<FlowEngine>();

        // ── 가상 Tag 엔진 (C-18 신규)
        services.AddSingleton<VirtualTagEngine>();

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
        services.AddSingleton<TrendViewModel>(sp =>
            new TrendViewModel(
                sp.GetRequiredService<TrendQueryService>(),
                sp.GetRequiredService<CollectorConfigLoader>(),
                sp.GetRequiredService<CsvExportService>()));
        services.AddSingleton<TrendView>(sp =>
            new TrendView(sp.GetRequiredService<TrendViewModel>()));

        // ── 이력 집계 (C-17 신규)
        services.AddSingleton<TagAggregationService>();

        // ── CSV 내보내기 (C-EX-07 신규)
        services.AddSingleton<CsvExportService>();

        // ── SignalR Hub 서비스 (C-11, C-EX-01-7 신규: DeviceInstanceService 주입)
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
        //    ★ C-15: ForceWriteService 추가 주입 (강제쓰기 버튼용)
        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<StatusView>(sp =>
            new StatusView(
                sp.GetRequiredService<StatusViewModel>(),
                sp.GetRequiredService<ForceWriteService>()));

        // ★ AddSingleton 필수 (Transient → 이중 창 버그)
        //   ★ C-EX-01-6: DeviceTreeView 인자 추가
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<MainViewModel>(),
                sp.GetRequiredService<StatusView>(),
                sp.GetRequiredService<AlarmView>(),
                sp.GetRequiredService<FlowView>(),
                sp.GetRequiredService<TrendView>(),
                sp.GetRequiredService<DeviceTreeView>()));

        // ── C-14 알림/에스컬레이션
        services.AddSingleton<NotificationService>();
        services.AddSingleton<EscalationManager>();

        // ★ C-15 버그 수정: 등록 누락 — StatusView 팩토리가 요구하는데 등록이 없었음
        //   ★ C-EX-03: AuditLogService 추가 주입
        services.AddSingleton<ForceWriteService>();

        // ── C-EX-02~08: 신규 실무 기능 서비스 일괄 등록
        services.AddSingleton<AuditLogService>();
        services.AddSingleton<DataRetentionService>();
        services.AddSingleton<DbBackupService>();
        services.AddSingleton<SelfHealthService>();

        return services.BuildServiceProvider();
    }
}