// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/ProcessManager.cs
//  역할: 관리 대상 프로그램의 시작/정지/재시작 실행자
//        정지 순서: CloseMainWindow(정상 종료 요청) → 5초 대기 → Kill(강제)
//  MG-02: 신규
//  설계 메모:
//    - Process 핸들을 보관하지 않는다 (매 호출 시 이름으로 조회 → 즉시 Dispose).
//      따라서 App.OnExit 정리 대상 아님. Manager 가 시작한 프로세스는
//      Manager 종료와 무관하게 계속 실행된다 (오케스트레이터 역할상 의도된 동작).
//    - WPF 앱은 CloseMainWindow 로 정상 종료 시 각자의 OnExit 정리 루틴
//      (Collector/Monitor 의 DisposeAsync 세트)이 실행되므로 Kill 보다 우선한다.
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Models;
using lssLib.Log;
using System.Diagnostics;
using System.IO;

namespace IIoT.Manager.Core;

/// <summary>프로세스 제어 결과 (성공 여부 + 실패 사유).</summary>
public readonly record struct ProcessOpResult(bool Ok, string? Error)
{
    public static ProcessOpResult Success        => new(true, null);
    public static ProcessOpResult Fail(string e) => new(false, e);
}

/// <summary>
/// 관리 대상 프로그램의 시작/정지/재시작 실행자 (DI 싱글턴).
/// </summary>
public sealed class ProcessManager
{
    // §1 ─ 상수 ──────────────────────────────────────────────

    /// <summary>정상 종료(CloseMainWindow) 대기 한도 — 초과 시 Kill 로 전환</summary>
    private static readonly TimeSpan _gracefulTimeout = TimeSpan.FromSeconds(5);

    // §2 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// 프로그램을 시작한다. ExePath 는 절대 경로 또는 Manager 실행 폴더 기준
    /// 상대 경로. 이미 실행 중이면 실패로 처리한다 (중복 실행 방지).
    /// </summary>
    public ProcessOpResult Start(ManagedProcessInfo info)
    {
        try
        {
            // ① 중복 실행 방지
            if (_IsRunning(info.ProcessName))
                return ProcessOpResult.Fail("이미 실행 중입니다.");

            // ② 경로 해석 (상대 → Manager 실행 폴더 기준 절대)
            var exePath = _ResolvePath(info.ExePath);
            if (!File.Exists(exePath))
                return ProcessOpResult.Fail($"실행 파일 없음: {exePath}");

            // ③ 시작 — WorkingDirectory 를 exe 폴더로 지정
            //   (각 프로그램이 BaseDirectory 기준으로 Config/Log 를 만들기 때문)
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName         = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute  = true
            });

            LogManager.Instance.Info("ProcessManager",
                $"{info.Name} 시작 (PID {p?.Id.ToString() ?? "?"})");
            return ProcessOpResult.Success;
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("ProcessManager", $"{info.Name} 시작 실패: {ex.Message}");
            return ProcessOpResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 프로그램을 정지한다. CloseMainWindow(정상 종료) → 5초 대기 →
    /// 미종료 시 Kill(프로세스 트리 포함) 순으로 진행한다.
    /// </summary>
    public async Task<ProcessOpResult> StopAsync(ManagedProcessInfo info)
    {
        Process[] found = [];
        try
        {
            found = Process.GetProcessesByName(info.ProcessName);
            if (found.Length == 0)
                return ProcessOpResult.Fail("실행 중이 아닙니다.");

            foreach (var p in found)
            {
                // ① 정상 종료 요청 (WPF OnExit 정리 루틴이 실행되도록)
                var closed = p.CloseMainWindow();

                // ② 5초 대기 — UI 스레드 블로킹 없이 비동기 대기
                using var cts = new CancellationTokenSource(_gracefulTimeout);
                try
                {
                    await p.WaitForExitAsync(cts.Token);
                    LogManager.Instance.Info("ProcessManager",
                        $"{info.Name} 정상 종료 (PID {p.Id})");
                }
                catch (OperationCanceledException)
                {
                    // ③ 타임아웃 → 강제 종료 (자식 프로세스 포함)
                    LogManager.Instance.Warn("ProcessManager",
                        $"{info.Name} 정상 종료 {_gracefulTimeout.TotalSeconds}초 초과" +
                        $"{(closed ? "" : " (창 닫기 요청 실패)")} — 강제 종료 (PID {p.Id})");
                    p.Kill(entireProcessTree: true);
                    await p.WaitForExitAsync();
                }
            }

            return ProcessOpResult.Success;
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("ProcessManager", $"{info.Name} 정지 실패: {ex.Message}");
            return ProcessOpResult.Fail(ex.Message);
        }
        finally
        {
            // ★ Process 객체는 네이티브 핸들 보유 — 반드시 Dispose
            foreach (var p in found) p.Dispose();
        }
    }

    /// <summary>프로그램을 재시작한다 (정지 → 0.5초 대기 → 시작).</summary>
    public async Task<ProcessOpResult> RestartAsync(ManagedProcessInfo info)
    {
        var stop = await StopAsync(info);
        if (!stop.Ok) return stop;

        // 파일 잠금/포트 해제 여유
        await Task.Delay(500);

        return Start(info);
    }

    // §3 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>상대 경로를 Manager 실행 폴더 기준 절대 경로로 해석한다.</summary>
    private static string _ResolvePath(string exePath) =>
        Path.IsPathRooted(exePath)
            ? exePath
            : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exePath));

    /// <summary>해당 이름의 프로세스가 실행 중인지 검사한다.</summary>
    private static bool _IsRunning(string processName)
    {
        var found = Process.GetProcessesByName(processName);
        try   { return found.Length > 0; }
        finally { foreach (var p in found) p.Dispose(); }
    }
}
