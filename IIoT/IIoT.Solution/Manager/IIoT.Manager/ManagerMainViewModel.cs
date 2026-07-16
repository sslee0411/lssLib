// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ManagerMainViewModel.cs
//  역할: MainWindow DataContext — 프로세스 상태 카드 목록 + 주기 갱신 타이머
//  MG-01: 신규 — Studio·Collector·Monitor 3개 카드, 2초 주기 상태 갱신
//         (MN-EX-04 배지 타이머와 동일한 DispatcherTimer 패턴)
//  MG-02: 카드 구성을 manager.json(ManagerSettingsLoader) 기반으로 전환.
//         InitializeAsync() — MainWindow.Loaded 에서 호출 (Monitor 의
//         CollectorManageView.Loaded 패턴과 동일: 파일 I/O 는 창 표시 후).
//  MG-03: 헬스체크 통합 — Refresh 가 비동기(핑 포함)로 전환.
//         타이머 Tick 재진입 방지 가드(_refreshing) 추가.
//  이동(2026-07-09): ViewModels\ → 프로젝트 루트로 이동 + namespace IIoT.Manager
//         (규칙: 메인 ViewModel 은 루트 레벨 고정 — Studio·Collector 와 동일.
//          Monitor 만 ViewModels 하위로 예외였음)
//  MG-04: 탭 상태(ActiveTabIndex/SwitchTab) 추가 + LogTailService 시작 연동
//  MG-05: 대시보드 탭(인덱스 2) 추가 + EventHistoryService 카드 전달
//  MG-06: 배포 탭(인덱스 3) 추가 + DeployViewModel.Initialize() 연동
//  MG-07: 스케줄 탭(인덱스 4) 추가 + ScheduleViewModel.Initialize()
//         + ScheduleService.Start() 연동
//  MG-EX-03: Windows 시작 시 자동 실행 토글(RunAtWindowsStartup — 레지스트리)
//            + AutoStart 프로그램 순차 자동 기동 (배열 순서, 프로그램별 지연)
//  MG-EX-04: EventHistoryDbService.InitializeAsync() 호출 추가
//            (설정 로드 직후 — 이후 발생 이벤트부터 DB 기록)
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-EX-04)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Manager.Core;
using IIoT.Manager.Core.Config;
using IIoT.Manager.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;   // ★ 규칙: ImplicitUsings 미의존 (CS0246 재발 방지)
using System.Windows.Threading;

namespace IIoT.Manager;

/// <summary>
/// MainWindow 의 DataContext.
/// <para>
/// manager.json 에 등록된 프로그램들의 상태 카드 컬렉션을 보유하고,
/// DispatcherTimer(2초)로 각 카드의 RefreshAsync() 를 호출한다.
/// UI 스레드 타이머이므로 별도 마샬링 불필요 (Dispatcher.Invoke 교착 이슈 없음).
/// </para>
/// </summary>
public partial class ManagerMainViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly ManagerSettingsLoader _settingsLoader;
    private readonly ProcessManager        _processManager;
    private readonly HealthCheckService    _healthCheck;
    private readonly LogTailService        _logTail;
    private readonly EventHistoryService   _events;
    private readonly ViewModels.DeployViewModel   _deployVm;
    private readonly ViewModels.ScheduleViewModel _scheduleVm;
    private readonly ScheduleService              _scheduleService;
    private readonly StartupRegistrationService   _startupReg;
    private readonly Core.Storage.EventHistoryDbService _eventDb;
    private readonly DispatcherTimer       _refreshTimer;
    private bool                           _initialized;

    /// <summary>★ MG-03: Tick 재진입 방지 (핑 지연 시 갱신 중첩 금지)</summary>
    private bool _refreshing;

    // §2 ─ 컬렉션 ─────────────────────────────────────────────

    /// <summary>프로세스 상태 카드 목록 (manager.json Processes[] 기반)</summary>
    public ObservableCollection<ProcessCardViewModel> Processes { get; } = [];

    // §3 ─ 관찰 속성 ─────────────────────────────────────────

    /// <summary>하단 상태 문구 (마지막 갱신 시각)</summary>
    [ObservableProperty]
    private string _statusText = "초기화 중…";

    /// <summary>★ MG-04~07: 현재 선택된 탭 인덱스 (0=프로세스, 1=로그, 2=대시보드, 3=배포, 4=스케줄)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProcessTab))]
    [NotifyPropertyChangedFor(nameof(IsLogTab))]
    [NotifyPropertyChangedFor(nameof(IsDashboardTab))]
    [NotifyPropertyChangedFor(nameof(IsDeployTab))]
    [NotifyPropertyChangedFor(nameof(IsScheduleTab))]
    private int _activeTabIndex;

    public bool IsProcessTab   => ActiveTabIndex == 0;
    public bool IsLogTab       => ActiveTabIndex == 1;
    public bool IsDashboardTab => ActiveTabIndex == 2;
    public bool IsDeployTab    => ActiveTabIndex == 3;
    public bool IsScheduleTab  => ActiveTabIndex == 4;

    /// <summary>★ MG-EX-03: Windows 시작 시 Manager 자동 실행 (레지스트리 HKCU Run)</summary>
    [ObservableProperty]
    private bool _runAtWindowsStartup;

    // ★ MG-EX-03: 체크 토글 → 즉시 레지스트리 반영 (실패 시 상태바 노출)
    partial void OnRunAtWindowsStartupChanged(bool value)
    {
        var ok = value ? _startupReg.Register() : _startupReg.Unregister();
        if (ok)
        {
            _events.Record("IIoT.Manager",
                value ? "Windows 시작 시 자동 실행 등록" : "Windows 시작 시 자동 실행 해제");
        }
        else
        {
            StatusText = "Windows 자동 실행 설정 실패 — 로그 확인";
        }
    }

    // §4 ─ 생성자 ─────────────────────────────────────────────

    public ManagerMainViewModel(ManagerSettingsLoader settingsLoader,
                                ProcessManager        processManager,
                                HealthCheckService    healthCheck,
                                LogTailService        logTail,
                                EventHistoryService   events,
                                ViewModels.DeployViewModel   deployVm,
                                ViewModels.ScheduleViewModel scheduleVm,
                                ScheduleService              scheduleService,
                                StartupRegistrationService   startupReg,
                                Core.Storage.EventHistoryDbService eventDb)
    {
        _settingsLoader  = settingsLoader;
        _processManager  = processManager;
        _healthCheck     = healthCheck;
        _logTail         = logTail;
        _events          = events;
        _deployVm        = deployVm;
        _scheduleVm      = scheduleVm;
        _scheduleService = scheduleService;
        _startupReg      = startupReg;
        _eventDb         = eventDb;

        // ★ MG-EX-03: 현재 레지스트리 등록 상태로 초기화
        //   (백킹 필드 직접 대입 — OnChanged 핸들러의 불필요한 재등록 방지)
        _runAtWindowsStartup = startupReg.IsRegistered();

        // ★ 2초 주기 상태 갱신 (UI 스레드 — MN-EX-04 와 동일 패턴)
        //   카드가 채워지기 전(초기화 전)에는 빈 컬렉션 순회 — 무해
        //   ★ MG-03: async void Tick — 반드시 try/catch + 재진입 가드
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += async (_, _) =>
        {
            if (_refreshing) return;
            _refreshing = true;
            try     { await _RefreshAllAsync(); }
            catch (Exception ex)
            {
                lssLib.Log.LogManager.Instance.Warn("ManagerMain", $"상태 갱신 오류: {ex.Message}");
            }
            finally { _refreshing = false; }
        };
        _refreshTimer.Start();
    }

    // §5 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// manager.json 로드 후 카드를 구성한다.
    /// MainWindow.Loaded 에서 1회 호출 (재호출 시 무시).
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            // ★ MG-EX-04: 이벤트 이력 DB 초기화 (SQLite 오픈 — 파일 I/O 이므로
            //   창 표시 후 시점인 여기서 수행. 이후 발생 이벤트부터 DB 기록됨)
            await _eventDb.InitializeAsync();

            await _settingsLoader.LoadAsync();

            Processes.Clear();
            foreach (var info in _settingsLoader.Settings.Processes)
                Processes.Add(new ProcessCardViewModel(info, _processManager, _healthCheck, _events,
                                                       _settingsLoader.Settings.Resource));   // ★ MG-EX-05

            // ★ MG-04: 로그 테일링 시작 (manager.json 로드 후 — 대상 경로 확정)
            _logTail.Start();

            // ★ MG-06: 배포 탭 초기화 (소스·대상 구성 — 설정 로드 후)
            _deployVm.Initialize();

            // ★ MG-07: 스케줄 탭 초기화 + 스케줄 검사 시작 (설정 로드 후)
            _scheduleVm.Initialize();
            _scheduleService.Start();

            await _RefreshAllAsync();   // 첫 화면부터 상태가 보이도록 즉시 1회 갱신

            // ★ MG-EX-03: AutoStart 프로그램 순차 자동 기동
            //   (fire-and-forget — 지연 대기가 초기화 완료를 막지 않도록.
            //    내부에서 try/catch — 규칙: async void/미대기 Task 는 이중 방어)
            _ = _AutoStartProgramsAsync();
        }
        catch (Exception ex)
        {
            // ★ 규칙: 조용히 삼키지 않는다 — 로그 + 상태바 노출
            lssLib.Log.LogManager.Instance.Error("ManagerMain", $"초기화 실패: {ex.Message}");
            StatusText = $"초기화 실패: {ex.Message}";
        }
    }

    /// <summary>★ MG-04: 탭 전환 (규칙 ⑤: CommandParameter string → TryParse)</summary>
    [RelayCommand]
    private void SwitchTab(string index)
    {
        if (int.TryParse(index, out var i))
            ActiveTabIndex = i;
    }

    // §6 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>
    /// ★ MG-EX-03: AutoStart=true 프로그램을 배열 순서대로 자동 시작한다.
    /// 각 프로그램 시작 후 AutoStartDelaySec 만큼 대기 (기동 순서 보장 —
    /// 예: Collector 가 Hub 를 연 뒤 Monitor 접속).
    /// 이미 실행 중이면 건너뜀 (Manager 재시작 시 중복 실행 방지).
    /// </summary>
    private async Task _AutoStartProgramsAsync()
    {
        try
        {
            var targets = Processes.Where(c => c.Info.AutoStart).ToList();
            if (targets.Count == 0) return;

            _events.Record("자동 기동", $"대상 {targets.Count}개 프로그램 순차 시작");

            foreach (var card in targets)
            {
                if (card.IsRunning)
                {
                    _events.Record(card.Info.Name, "자동 기동 건너뜀 (이미 실행 중)");
                    continue;
                }

                var result = _processManager.Start(card.Info);
                _events.Record(card.Info.Name,
                    result.Ok ? "자동 기동 시작" : $"자동 기동 실패 — {result.Error}",
                    result.Ok ? EventSeverity.Info : EventSeverity.Warning);

                // 다음 프로그램 시작 전 대기 (음수 방어)
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, card.Info.AutoStartDelaySec)));
            }
        }
        catch (Exception ex)
        {
            // ★ 규칙: 미대기(fire-and-forget) Task 는 반드시 자체 try/catch
            lssLib.Log.LogManager.Instance.Error("ManagerMain", $"자동 기동 오류: {ex.Message}");
        }
    }

    /// <summary>모든 카드의 프로세스 상태를 갱신한다 (MG-03: 헬스체크 핑 포함 비동기).</summary>
    private async Task _RefreshAllAsync()
    {
        foreach (var card in Processes)
            await card.RefreshAsync();

        StatusText = $"마지막 갱신: {DateTime.Now:HH:mm:ss}  ·  설정: {ManagerSettingsLoader.SettingsPath}";
    }
}
