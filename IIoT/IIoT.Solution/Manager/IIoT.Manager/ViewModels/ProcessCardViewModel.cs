// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/ProcessCardViewModel.cs
//  역할: 상태 카드 1장의 ViewModel — 프로세스 존재 여부 + 헬스체크로 상태 갱신
//  MG-01: 신규 (상태 표시만)
//  MG-02: Start/Stop/Restart RelayCommand 추가 (ProcessManager 위임)
//         ★ 규칙 ⑬: CanExecute 트리거 프로퍼티(_state/_isBusy)에
//           [NotifyCanExecuteChangedFor] 필수 — 없으면 버튼이 항상 비활성 유지
//  MG-03: NamedPipe 헬스체크 통합 — Refresh() → RefreshAsync() 전환.
//         실행 중 + 핑 실패 → 🟡 응답 없음. 응답시간(ms)·상태문구 표시.
//         AutoRestart=true 인 프로그램은 연속 3회 실패 시 자동 재시작.
//  MG-05: EventHistoryService 연동 — 수동 시작/정지/재시작, 자동 재시작,
//         상태 변경(감지)을 대시보드 이벤트 이력에 기록.
//  MG-EX-02: 상태 변경 심각도 판정 (비정상 종료·행 → Warning 트레이 알림)
//  MG-EX-05: CPU/메모리 리소스 샘플링 + 임계 초과 경고 (5분 쿨다운).
//         CPU% = TotalProcessorTime 증분 / (경과시간 × 코어수) — 2초 주기 샘플
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-EX-05)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Manager.Core;
using IIoT.Manager.Core.Config;
using IIoT.Manager.Models;
using System.Diagnostics;

namespace IIoT.Manager.ViewModels;

/// <summary>
/// 관리 대상 프로그램 1개의 상태 카드 ViewModel.
/// <para>
/// RefreshAsync() 가 호출될 때마다 ① 프로세스 존재 검사
/// ② 실행 중이면 NamedPipe 핑(왕복 ms 측정) 순으로 상태를 판정한다.
/// (호출 주기는 ManagerMainViewModel 의 DispatcherTimer 가 관리)
/// </para>
/// </summary>
public partial class ProcessCardViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly ProcessManager      _processManager;
    private readonly HealthCheckService  _healthCheck;
    private readonly EventHistoryService _events;
    private readonly ResourceSettings    _resource;

    /// <summary>★ MG-03: 헬스체크 연속 실패 횟수 (자동복구 판정용, 성공 시 리셋)</summary>
    private int _healthFailCount;

    /// <summary>★ MG-03: 자동 재시작 발동 임계 (연속 실패 3회 = 약 6초)</summary>
    private const int _autoRestartThreshold = 3;

    // ★ MG-EX-05: CPU 샘플링 상태 (이전 샘플과의 증분으로 % 계산)
    private TimeSpan _prevCpuTime;
    private DateTime _prevSampleAt;
    private int      _prevSamplePid = -1;

    /// <summary>★ MG-EX-05: 리소스 경고 쿨다운 (항목별 마지막 경고 시각 — 5분)</summary>
    private DateTime _lastCpuWarnAt, _lastMemWarnAt;
    private static readonly TimeSpan _resourceWarnCooldown = TimeSpan.FromMinutes(5);

    /// <summary>이 카드가 표시하는 프로그램 정의 (읽기 전용)</summary>
    public ManagedProcessInfo Info { get; }

    // §2 ─ 관찰 속성 ─────────────────────────────────────────

    /// <summary>현재 프로세스 상태 (카드 상태 점 색상 트리거)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    private ProcessState _state = ProcessState.Stopped;

    /// <summary>실행 중일 때의 PID (정지 시 null)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PidText))]
    private int? _pid;

    /// <summary>★ MG-02: 제어 작업 진행 중 여부 (진행 중엔 모든 버튼 비활성)</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    private bool _isBusy;

    /// <summary>★ MG-02: 마지막 제어 오류 메시지 (성공 시 빈 문자열 — 카드 하단 표시)</summary>
    [ObservableProperty]
    private string _lastError = "";

    /// <summary>★ MG-03: 헬스체크 왕복 시간 ms (실패·정지 시 null)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingText))]
    private long? _pingMs;

    /// <summary>★ MG-03: pong 에 실려온 내부 상태 문구 (없으면 빈 문자열)</summary>
    [ObservableProperty]
    private string _healthStatus = "";

    /// <summary>★ MG-EX-05: CPU 사용률 % (첫 샘플·정지 시 null)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResourceText))]
    private double? _cpuPercent;

    /// <summary>★ MG-EX-05: 메모리(WorkingSet) MB (정지 시 null)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResourceText))]
    private double? _memoryMb;

    // §3 ─ 파생 속성 ─────────────────────────────────────────

    // ★ MG-EX-05: 리소스 샘플링 + 임계 초과 경고 (Refresh ① 단계에서 호출)
    private void _SampleResources(Process p)
    {
        try
        {
            var now     = DateTime.Now;
            var cpuTime = p.TotalProcessorTime;
            var memMb   = p.WorkingSet64 / 1024.0 / 1024.0;

            // CPU%: 이전 샘플과의 증분 (같은 PID 일 때만 — 재시작 시 리셋)
            double? cpuPct = null;
            if (_prevSamplePid == p.Id)
            {
                var elapsed = (now - _prevSampleAt).TotalSeconds;
                if (elapsed > 0.5)
                    cpuPct = (cpuTime - _prevCpuTime).TotalSeconds
                             / (elapsed * Environment.ProcessorCount) * 100.0;
            }

            _prevCpuTime   = cpuTime;
            _prevSampleAt  = now;
            _prevSamplePid = p.Id;

            CpuPercent = cpuPct is double c ? Math.Clamp(c, 0, 100) : null;
            MemoryMb   = memMb;

            // ── 임계 초과 경고 (항목별 5분 쿨다운 — 반복 알림 방지) ──
            if (_resource.CpuWarnPercent > 0 && CpuPercent is double cpu &&
                cpu >= _resource.CpuWarnPercent &&
                now - _lastCpuWarnAt >= _resourceWarnCooldown)
            {
                _lastCpuWarnAt = now;
                _events.Record(Info.Name,
                    $"CPU 사용률 임계 초과: {cpu:F1}% (임계 {_resource.CpuWarnPercent}%)",
                    EventSeverity.Warning);
            }

            if (_resource.MemoryWarnMb > 0 && memMb >= _resource.MemoryWarnMb &&
                now - _lastMemWarnAt >= _resourceWarnCooldown)
            {
                _lastMemWarnAt = now;
                _events.Record(Info.Name,
                    $"메모리 사용량 임계 초과: {memMb:F0} MB (임계 {_resource.MemoryWarnMb} MB)",
                    EventSeverity.Warning);
            }
        }
        catch (Exception ex)
        {
            // 접근 거부(권한) 등 — 리소스 표시만 생략, 상태 감지는 계속
            lssLib.Log.LogManager.Instance.Debug("ProcessCard",
                $"{Info.Name} 리소스 샘플 실패: {ex.Message}");
            CpuPercent = null;
            MemoryMb   = null;
        }
    }

    /// <summary>카드에 표시할 상태 문구</summary>
    public string StatusText => State switch
    {
        ProcessState.Running => "실행 중",
        ProcessState.Error   => "응답 없음",
        _                    => "정지",
    };

    /// <summary>실행 여부 (XAML 트리거용)</summary>
    public bool IsRunning => State == ProcessState.Running;

    /// <summary>PID 표시 문구 (정지 시 "—")</summary>
    public string PidText => Pid is int p ? $"PID {p}" : "—";

    /// <summary>★ MG-03: 응답시간 표시 문구</summary>
    public string PingText => PingMs is long ms ? $"{ms} ms" : "—";

    /// <summary>★ MG-EX-05: 리소스 표시 문구 (예: "CPU 3.2%  ·  메모리 145 MB")</summary>
    public string ResourceText
    {
        get
        {
            if (MemoryMb is not double mem) return "";
            var cpu = CpuPercent is double c ? $"CPU {c:F1}%" : "CPU —";
            return $"{cpu}  ·  메모리 {mem:F0} MB";
        }
    }

    // §4 ─ 생성자 ─────────────────────────────────────────────

    public ProcessCardViewModel(ManagedProcessInfo  info,
                                ProcessManager      processManager,
                                HealthCheckService  healthCheck,
                                EventHistoryService events,
                                ResourceSettings    resource)
    {
        Info            = info;
        _processManager = processManager;
        _healthCheck    = healthCheck;
        _events         = events;
        _resource       = resource;
    }

    // ★ MG-05: 상태 변경 감지 → 이벤트 이력 기록
    //   (ObservableProperty partial 메서드 — 수동/외부 종료 모두 포착)
    // ★ MG-EX-02: 심각도 판정 — "실행 중 → 정지/응답없음" 이 수동 조작(IsBusy)
    //   없이 발생하면 비정상 종료/행 의심 → Warning (트레이 알림 대상)
    partial void OnStateChanged(ProcessState oldValue, ProcessState newValue)
    {
        if (oldValue == newValue) return;

        var severity = oldValue == ProcessState.Running && !IsBusy
            ? EventSeverity.Warning
            : EventSeverity.Info;

        _events.Record(Info.Name,
            $"상태 변경: {_StateText(oldValue)} → {_StateText(newValue)}", severity);
    }

    private static string _StateText(ProcessState s) => s switch
    {
        ProcessState.Running => "실행 중",
        ProcessState.Error   => "응답 없음",
        _                    => "정지",
    };

    // §5 ─ 커맨드 (MG-02) ─────────────────────────────────────

    private bool _CanStart() => !IsRunning && !IsBusy;

    // ★ MG-03: 응답 없음(Error) 상태에서도 정지/재시작 가능해야 함 (행 걸린 프로세스 회수)
    private bool _CanStop()  => State != ProcessState.Stopped && !IsBusy;

    /// <summary>▶ 시작</summary>
    [RelayCommand(CanExecute = nameof(_CanStart))]
    private async Task StartAsync()
    {
        // ★ 규칙: try/catch + 오류를 로그·카드에 노출 (조용히 삼키기 금지)
        //   Start 내부에서 예외를 이미 처리하므로 여기선 결과만 반영
        IsBusy = true;
        try
        {
            var result = _processManager.Start(Info);
            LastError = result.Ok ? "" : result.Error ?? "알 수 없는 오류";

            // ★ MG-05: 이벤트 기록
            _events.Record(Info.Name, result.Ok ? "수동 시작" : $"수동 시작 실패: {LastError}");
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    /// <summary>⏹ 정지 (정상 종료 → 5초 후 강제)</summary>
    [RelayCommand(CanExecute = nameof(_CanStop))]
    private async Task StopAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _processManager.StopAsync(Info);
            LastError = result.Ok ? "" : result.Error ?? "알 수 없는 오류";

            // ★ MG-05: 이벤트 기록
            _events.Record(Info.Name, result.Ok ? "수동 정지" : $"수동 정지 실패: {LastError}");
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    /// <summary>🔄 재시작 (정지 → 0.5초 → 시작)</summary>
    [RelayCommand(CanExecute = nameof(_CanStop))]
    private async Task RestartAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _processManager.RestartAsync(Info);
            LastError = result.Ok ? "" : result.Error ?? "알 수 없는 오류";

            // ★ MG-05: 이벤트 기록
            _events.Record(Info.Name, result.Ok ? "수동 재시작" : $"수동 재시작 실패: {LastError}");
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    // §6 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// ① 프로세스 존재 검사 → ② 실행 중이면 NamedPipe 핑 순으로 상태를 갱신한다.
    /// UI 스레드(DispatcherTimer)에서 호출 — 핑은 비동기라 UI 를 막지 않는다.
    /// </summary>
    public async Task RefreshAsync()
    {
        // ── ① 프로세스 존재 검사 + 리소스 샘플 채집 (MG-EX-05) ──
        bool running;
        Process[] found = [];
        try
        {
            found = Process.GetProcessesByName(Info.ProcessName);
            running = found.Length > 0;
            Pid     = running ? found[0].Id : null;

            // ★ MG-EX-05: 핸들이 살아있는 여기서 리소스 값 채집 (Dispose 전)
            if (running)
                _SampleResources(found[0]);
        }
        catch (Exception ex)
        {
            // ★ 규칙: 조용히 삼키지 않는다 — 로그 + 카드에 오류 상태 노출
            lssLib.Log.LogManager.Instance.Warn("ProcessCard",
                $"{Info.Name} 상태 검사 실패: {ex.Message}");
            Pid   = null;
            State = ProcessState.Error;
            return;
        }
        finally
        {
            // ★ Process 객체는 네이티브 핸들 보유 — 반드시 Dispose
            foreach (var p in found) p.Dispose();
        }

        if (!running)
        {
            State            = ProcessState.Stopped;
            PingMs           = null;
            HealthStatus     = "";
            _healthFailCount = 0;

            // ★ MG-EX-05: 리소스 표시 초기화
            CpuPercent     = null;
            MemoryMb       = null;
            _prevSamplePid = -1;
            return;
        }

        // ── ② 헬스체크 핑 (MG-03) ────────────────────────────
        var health = await _healthCheck.PingAsync(Info.ProcessName);

        if (health.Ok)
        {
            PingMs           = health.ElapsedMs;
            HealthStatus     = health.Status;
            State            = ProcessState.Running;
            _healthFailCount = 0;
            return;
        }

        // 실행 중인데 핑 실패 → 응답 없음 (행 또는 헬스채널 없는 구버전 빌드)
        PingMs       = null;
        HealthStatus = "";
        State        = ProcessState.Error;
        _healthFailCount++;

        // ── ③ 자동복구 (manager.json AutoRestart=true 일 때만) ──
        if (Info.AutoRestart && _healthFailCount >= _autoRestartThreshold && !IsBusy)
        {
            _healthFailCount = 0;
            LastError = $"응답 없음 {_autoRestartThreshold}회 연속 — 자동 재시작 ({DateTime.Now:HH:mm:ss})";
            lssLib.Log.LogManager.Instance.Warn("ProcessCard",
                $"{Info.Name} 헬스체크 연속 {_autoRestartThreshold}회 실패 — 자동 재시작 시도");

            // ★ MG-05: 이벤트 기록 (자동복구 발동) — MG-EX-02: Warning (트레이 알림)
            _events.Record(Info.Name,
                $"자동 재시작 발동 (헬스체크 {_autoRestartThreshold}회 연속 실패)",
                EventSeverity.Warning);

            IsBusy = true;
            try
            {
                var result = await _processManager.RestartAsync(Info);
                if (!result.Ok)
                {
                    LastError = $"자동 재시작 실패: {result.Error}";
                    // ★ MG-EX-02: Warning (트레이 알림)
                    _events.Record(Info.Name, LastError, EventSeverity.Warning);
                }
            }
            finally { IsBusy = false; }
        }
    }
}
