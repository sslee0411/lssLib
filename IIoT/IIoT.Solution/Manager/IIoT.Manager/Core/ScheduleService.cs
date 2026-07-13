// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/ScheduleService.cs
//  역할: 스케줄 실행자 (요구사항 4-2-8)
//        manager.json Schedules[] 를 30초 주기로 검사, 도래 시
//        ProcessManager 로 시작/정지/재시작 실행 + 이벤트 이력 기록
//  MG-07: 신규
//  설계 메모:
//    - 중복 실행 방지: 스케줄별 "yyyyMMdd HH:mm" 실행 키 기억 —
//      같은 분(minute) 안에서 재검사되어도 1회만 실행
//    - DispatcherTimer(UI 스레드) — ProcessManager/EventHistory 호출 안전
//    - 검사 주기 30초: 분 단위 스케줄이므로 충분 (최대 30초 오차)
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Core.Config;
using IIoT.Manager.Models;
using lssLib.Log;
using System.Linq;   // ★ 규칙: ImplicitUsings 미의존 (CS0246 재발 방지)
using System.Windows.Threading;

namespace IIoT.Manager.Core;

/// <summary>
/// 스케줄 실행 서비스 (DI 싱글턴).
/// Start() 는 manager.json 로드 이후(ManagerMainViewModel.InitializeAsync)에 호출된다.
/// </summary>
public sealed class ScheduleService : IDisposable
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly ManagerSettingsLoader _settingsLoader;
    private readonly ProcessManager        _processManager;
    private readonly EventHistoryService   _events;
    private readonly DispatcherTimer       _timer;

    /// <summary>스케줄별 마지막 실행 키 ("yyyyMMdd HH:mm") — 같은 분 중복 실행 방지</summary>
    private readonly Dictionary<string, string> _lastRun = new();

    private bool _started;
    private bool _running;   // 재진입 가드 (StopAsync 대기 중 다음 틱 방지)

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public ScheduleService(ManagerSettingsLoader settingsLoader,
                           ProcessManager        processManager,
                           EventHistoryService   events)
    {
        _settingsLoader = settingsLoader;
        _processManager = processManager;
        _events         = events;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += async (_, _) =>
        {
            // ★ 규칙: async void Tick — try/catch + 재진입 가드
            if (_running) return;
            _running = true;
            try     { await _CheckDueAsync(); }
            catch (Exception ex)
            {
                LogManager.Instance.Warn("Schedule", $"스케줄 검사 오류: {ex.Message}");
            }
            finally { _running = false; }
        };
    }

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>스케줄 검사를 시작한다 (manager.json 로드 후 호출, 재호출 무시).</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _timer.Start();
        LogManager.Instance.Info("Schedule",
            $"스케줄 서비스 시작 — 등록 {_settingsLoader.Settings.Schedules.Count}건");
    }

    public void Dispose() => _timer.Stop();

    // §4 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>도래한 스케줄을 찾아 실행한다.</summary>
    private async Task _CheckDueAsync()
    {
        var now     = DateTime.Now;
        var nowText = now.ToString("HH:mm");
        var runKey  = now.ToString("yyyyMMdd ") + nowText;
        var day     = (int)now.DayOfWeek;

        // ToList(): 실행 중 UI 에서 목록이 변경되어도 안전하게 순회
        foreach (var s in _settingsLoader.Settings.Schedules.ToList())
        {
            if (!s.Enabled)                      continue;
            if (!s.Days.Contains(day))           continue;
            if (s.Time != nowText)               continue;
            if (_lastRun.GetValueOrDefault(s.Id) == runKey) continue;   // 이미 실행됨

            _lastRun[s.Id] = runKey;
            await _ExecuteAsync(s);
        }
    }

    /// <summary>스케줄 1건을 실행한다 (대상 검색 → 동작 수행 → 이벤트 기록).</summary>
    private async Task _ExecuteAsync(ScheduleEntry s)
    {
        var target = _settingsLoader.Settings.Processes
                                    .FirstOrDefault(p => p.Id == s.ProcessId);
        if (target is null)
        {
            _events.Record("스케줄", $"[{s.Time}] 대상 프로그램 없음: {s.ProcessId} (건너뜀)");
            return;
        }

        var result = s.Action switch
        {
            ScheduleAction.Start   => _processManager.Start(target),
            ScheduleAction.Stop    => await _processManager.StopAsync(target),
            _                      => await _processManager.RestartAsync(target),
        };

        _events.Record(target.Name,
            result.Ok
                ? $"스케줄 {s.ActionText} 실행 ({s.Time})"
                : $"스케줄 {s.ActionText} 실패 ({s.Time}) — {result.Error}");
    }
}
