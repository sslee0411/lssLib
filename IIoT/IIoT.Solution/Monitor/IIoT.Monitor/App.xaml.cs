// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · App.xaml.cs
//  역할: 애플리케이션 진입점
//        ① 테마 복원 (창 표시 전 — 색상 준비)
//        ② LogManager.Instance.Start() (DI 빌드 전 필수)
//        ③ DI 서비스 구성 (_ConfigureServices)
//        ④ 창 생성 (AddSingleton — Transient 사용 시 이중 창 버그)
//
//  MN-Base-0: 최초 생성 — 빈 창 + 테마만 적용.
//  MN-Base-1: MonitorMainViewModel DI 등록 추가.
//  MN-01: MonitorSettingsLoader / CollectorManageViewModel / CollectorManageView
//         DI 등록 추가. monitor.json 로드는 CollectorManageView.Loaded 에서 수행되므로
//         여기서는 등록만 한다 (Studio-P04와 동일하게 "요구하는 곳"+"등록하는 곳" 세트 확인).
//         CommandQueue.Instance.Start() 는 EventBus 사용 시점(MN-01B)에 추가 예정.
//  MN-01B: CollectorConnectionManager DI 등록 추가 (CollectorId↔HubConnection 관리자)
//  MN-02: LiveTagAggregator / LiveTagViewModel / LiveTagView DI 등록 추가
//  MN-02B: DashboardViewModel / DashboardView DI 등록 추가 (대시보드 탭)
//  MN-03: AlarmAggregator / AlarmViewModel / AlarmView DI 등록 추가
//  MN-04: DetectorHost DI 등록 + 예시 Detector/Responder 부트스트랩 등록
//  MN-05: MonitorHostService DI 등록 (자체 SignalR Hub — 웹 브라우저 연동).
//         실제 시작(StartAsync)은 MainWindow.Loaded 에서 수행 (Collector의
//         win.Loaded 패턴과 동일 — 창이 뜬 뒤 웹 서버 기동)
//  MN-06: ChartViewModel / ChartView DI 등록 추가 (실시간 차트 탭)
//  FIX(2026-07-08): 앱 종료 시 CollectorConnectionManager(N개 HubConnection/
//                   HttpClient)가 정리되지 않아 프로세스가 정상 종료되지
//                   않던 문제 수정 — OnExit에서 함께 Dispose하도록 추가
//  FIX(2, 2026-07-08): 위 종료 정리가 UI 스레드를 블로킹하는 동안 Aggregator의
//                      Dispatcher.Invoke(동기)와 맞물려 교착상태 발생 —
//                      Aggregator를 BeginInvoke로 수정 + 5초 타임아웃 안전장치 추가
//  MN-EX-01: TrayNotificationService DI 등록 + 초기화 + AlarmAggregator.
//            NewAlarmCreated 구독(신규 알람 시 사운드+트레이 풍선 알림)
//  MN-EX-02: AlarmHistoryService DI 등록 + AlarmAggregator.AlarmRecorded 구독
//            (알람 생성/상태전이마다 SQLite 이력 저장). 초기화는 MainWindow.
//            Loaded 에서 수행(파일 I/O이므로 창 표시 전 블로킹 방지)
//  신규(2026-07-08): LogPanelView DI 등록 — [로그] 탭 실제 구현 (기존 미구현 확인)
//  MN-EX-03: 트레이 상주 + 최소화 — TrayNotificationService 에 RestoreRequested/
//            ExitRequested 이벤트 추가, MainWindow 에서 최소화 시 트레이로 숨김
//  MN-EX-05: FavoriteTagService DI 등록 (즐겨찾기/핀 고정)
//  MN-EX-07: SnapshotCsvExportService DI 등록 (현재값 스냅샷 CSV 내보내기)
//  MG-03 (2026-07-09): HealthPipeServer 추가 — Manager 헬스체크(NamedPipe
//            핑/퐁) 응답. 파이프명 "IIoT.Health.IIoT.Monitor".
//            csproj 에 IIoT.Contracts 참조 신규 추가. OnExit 정리 세트 추가.
//  생성: 2026-07-07 / 수정: 2026-07-09 (MG-03)
// ══════════════════════════════════════════════════════════

using IIoT.Contracts.Health;
using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Core.Connection;
using IIoT.Monitor.Core.Detection;
using IIoT.Monitor.Core.Detection.Detectors;
using IIoT.Monitor.Core.Detection.Responders;
using IIoT.Monitor.Core.Export;
using IIoT.Monitor.Core.Favorites;
using IIoT.Monitor.Core.Notification;
using IIoT.Monitor.Core.Storage;
using IIoT.Monitor.SignalR;
using IIoT.Monitor.ViewModels;
using IIoT.Monitor.Views.Alarm;
using IIoT.Monitor.Views.Chart;
using IIoT.Monitor.Views.CollectorManage;
using IIoT.Monitor.Views.Dashboard;
using IIoT.Monitor.Views.LiveTag;
using IIoT.Monitor.Views.Log;
using IIoT.Monitor.Views.Settings;   // ★ C-SET-01 후속
using IIoT.UI.Themes;
using lssLib.Log;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace IIoT.Monitor;

public partial class App : Application
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private ThemeSettingsService? _themeSettings;
    private IServiceProvider?     _services;

    // ★ MG-03: Manager 헬스체크 응답 서버 (NamedPipe 핑/퐁)
    private HealthPipeServer?     _healthServer;

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

        LogManager.Instance.Info("App", "IIoT.Monitor 시작");

        // ★ MG-03: 헬스체크 응답 서버 시작 (Manager 가 핑을 보냄)
        // ★ HM-22: 원격 설정 조회/저장 — settings.json 원문을 그대로 읽고 쓴다
        _healthServer = new HealthPipeServer(
            "IIoT.Monitor",
            statusProvider: () => "모니터링 정상",
            onLog: m => LogManager.Instance.Debug("Health", m),
            settingsProvider: () => File.Exists(MonitorSettingsLoader.SettingsPath)
                ? File.ReadAllText(MonitorSettingsLoader.SettingsPath, System.Text.Encoding.UTF8)
                : "{}",
            settingsSaver: json =>
            {
                try
                {
                    File.WriteAllText(MonitorSettingsLoader.SettingsPath, json, System.Text.Encoding.UTF8);
                    return "";
                }
                catch (Exception ex) { return ex.Message; }
            });
        _healthServer.Start();

        // ③ DI 빌드
        _services = _ConfigureServices();

        // ★ MN-04: 예시 Detector/Responder 등록
        //   실제 운영 시에는 여기에 프로젝트에 맞는 Detector/Responder 를 자유롭게
        //   추가하면 된다 (AbstractDetector 상속 + IDetectionResponder 구현).
        //   TagId "T001"은 예시값 — 실제 감시할 Tag ID로 교체해서 사용할 것.
        var detectorHost = _services.GetRequiredService<DetectorHost>();
        detectorHost.RegisterResponder(new LogResponder());
        detectorHost.RegisterDetector(new RateOfChangeDetector(tagId: "T001", maxRatePerSec: 5.0));

        // ★ MN-EX-01: 트레이 알림 초기화 + 신규 알람 발생 시 사운드/풍선 알림 연결
        var tray = _services.GetRequiredService<TrayNotificationService>();
        tray.Initialize();

        var alarmAggregator = _services.GetRequiredService<AlarmAggregator>();
        alarmAggregator.NewAlarmCreated += row =>
            tray.NotifyNewAlarm(
                row.Level,
                $"[{row.Level}] 알람 발생 — {row.CollectorName}",
                $"{row.TagId} · {row.Message}");

        // ★ MN-EX-02: 알람 생성/상태전이 시마다 SQLite 이력 저장 (fire-and-forget)
        var alarmHistory = _services.GetRequiredService<AlarmHistoryService>();
        alarmAggregator.AlarmRecorded += row => _ = alarmHistory.RecordAsync(row);

        // ④ 창 생성 및 표시
        _services.GetRequiredService<MainWindow>().Show();
    }

    // §3 ─ 종료 ───────────────────────────────────────────────

    protected override void OnExit(ExitEventArgs e)
    {
        // ★ FIX(2026-07-08): CollectorConnectionManager 가 종료 시 전혀 정리되지
        //   않아 N개 Collector에 대한 SignalR HubConnection/HttpClient가 살아있는
        //   채로 남아 프로세스가 정상 종료되지 않던 문제 수정.
        //   ★ 근본 원인은 LiveTagAggregator/AlarmAggregator의 Dispatcher.Invoke
        //   (동기·블로킹)가 이 블로킹 대기와 맞물려 교착상태를 일으켰던 것 —
        //   BeginInvoke로 수정 완료. 아래 5초 타임아웃은 그 외의 사유(네트워크
        //   지연 등)로 인한 무한 대기까지 방어하는 안전장치.
        _WaitWithTimeout(_services?.GetService<CollectorConnectionManager>()?.DisposeAsync().AsTask());
        _WaitWithTimeout(_services?.GetService<MonitorHostService>()?.DisposeAsync().AsTask());

        // ★ MN-EX-01: 트레이 아이콘 정리 (남아있으면 작업표시줄에 유령 아이콘으로 남음)
        _services?.GetService<TrayNotificationService>()?.Dispose();

        // ★ MN-EX-02: 알람 이력 DB 연결 정리
        _WaitWithTimeout(_services?.GetService<AlarmHistoryService>()?.DisposeAsync().AsTask());

        // ★ MG-03: 헬스체크 파이프 정리 (내부 2초 타임아웃 + 외부 5초 이중 방어)
        _WaitWithTimeout(_healthServer?.DisposeAsync().AsTask());

        _themeSettings?.Dispose();   // 이벤트 구독 해제 필수
        LogManager.Instance.Info("App", "IIoT.Monitor 종료");
        base.OnExit(e);
    }

    /// <summary>
    /// 종료 정리 Task 를 최대 5초까지만 기다린다. 그 안에 끝나지 않으면
    /// 로그만 남기고 포기한다 — 앱 종료 자체가 무한정 멈추는 것을 방지하는 안전장치.
    /// </summary>
    private static void _WaitWithTimeout(Task? task)
    {
        if (task is null) return;

        try
        {
            if (!task.Wait(TimeSpan.FromSeconds(5)))
                LogManager.Instance.Warn("App", "종료 정리 작업이 5초 내 완료되지 않아 건너뜁니다.");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("App", $"종료 정리 중 예외(무시하고 계속 종료): {ex.Message}");
        }
    }

    // §4 ─ DI 구성 ────────────────────────────────────────────

    private static IServiceProvider _ConfigureServices()
    {
        var services = new ServiceCollection();

        // ★ MN-EX-05 신규: Tag 즐겨찾기 서비스 (LiveTagAggregator 의존성이므로 먼저 등록)
        services.AddSingleton<FavoriteTagService>();

        // ★ MN-EX-07 신규: 현재값 스냅샷 CSV 내보내기 (LiveTagViewModel 의존성이므로 먼저 등록)
        services.AddSingleton<SnapshotCsvExportService>();

        // ★ MN-02 신규: 전체 Collector 통합 실시간 Tag 집계기 + 화면
        //   (CollectorConnectionManager 보다 먼저 등록 — 생성자 의존성)
        services.AddSingleton<LiveTagAggregator>();
        services.AddSingleton<LiveTagViewModel>();
        services.AddSingleton<LiveTagView>();

        // ★ MN-03 신규: 전체 Collector 통합 실시간 알람 집계기 + 화면
        services.AddSingleton<AlarmAggregator>();
        services.AddSingleton<AlarmViewModel>();
        services.AddSingleton<AlarmView>();

        // ★ MN-04 신규: AbstractDetector 커스텀 확장 호스트
        //   (LiveTagAggregator 의존 — 위에서 먼저 등록됨)
        services.AddSingleton<DetectorHost>();

        // ★ MN-EX-01 신규: 알람 사운드 + 트레이 알림
        services.AddSingleton<TrayNotificationService>();

        // ★ MN-EX-02 신규: 알람 이력 SQLite 저장
        services.AddSingleton<AlarmHistoryService>();

        // ★ MN-01B 신규: Collector 연결 관리자 (CollectorId ↔ HubConnection)
        services.AddSingleton<CollectorConnectionManager>();

        // ★ MN-01 신규: monitor.json 설정 + Collector 관리 화면
        services.AddSingleton<MonitorSettingsLoader>();
        services.AddSingleton<CollectorManageViewModel>();
        services.AddSingleton<CollectorManageView>();

        // ★ C-SET-01 후속 신규: 환경설정 화면 (MonitorSettingsLoader 의존 — 위에서 먼저 등록됨)
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SettingsView>();

        // ★ MN-Base-1: MainWindow DataContext
        services.AddSingleton<MonitorMainViewModel>();

        // ★ MN-02B 신규: 대시보드 탭 (CollectorManageViewModel/LiveTagAggregator 의존)
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DashboardView>();

        // ★ MN-05 신규: Monitor 자체 SignalR Hub (웹 브라우저 연동)
        //   (LiveTagAggregator/AlarmAggregator/MonitorSettingsLoader 의존 — 위에서 먼저 등록됨)
        services.AddSingleton<MonitorHostService>();

        // ★ MN-06 신규: 실시간 차트 (CollectorManageViewModel/LiveTagAggregator 의존)
        services.AddSingleton<ChartViewModel>();
        services.AddSingleton<ChartView>();

        // ★ 신규: [로그] 탭 (LogManager 싱글턴 이벤트 구독 — DI 의존성 없음)
        services.AddSingleton<LogPanelView>();

        // ★ 반드시 AddSingleton (Transient → 이중 창 버그)
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<MonitorMainViewModel>(),
                sp.GetRequiredService<CollectorManageView>(),
                sp.GetRequiredService<LiveTagView>(),
                sp.GetRequiredService<AlarmView>(),
                sp.GetRequiredService<DashboardView>(),
                sp.GetRequiredService<ChartView>(),
                sp.GetRequiredService<LogPanelView>(),
                sp.GetRequiredService<MonitorHostService>(),
                sp.GetRequiredService<AlarmHistoryService>(),
                sp.GetRequiredService<TrayNotificationService>(),
                sp.GetRequiredService<SettingsView>()));   // ★ C-SET-01 후속

        return services.BuildServiceProvider();
    }
}
