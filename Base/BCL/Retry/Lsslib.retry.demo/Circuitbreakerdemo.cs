using lssLib.Retry;

namespace lssLib.Retry.Demo;

// ═══════════════════════════════════════════════════════════════════
//  CircuitBreakerDemo — CircuitBreaker 예제
//  §1 프리셋  §2 상태전이  §3 ExecuteAsync  §4 CB+Retry
//  §5 TryExecuteAsync  §6 수동제어
// ═══════════════════════════════════════════════════════════════════

internal static class CircuitBreakerDemo
{
    public static void Run()
    {
        DemoHelper.Header("CircuitBreaker — 회로 차단기");
        PresetsDemo();
        StateTransitionDemo();
        ExecuteAsyncDemo();
        ExecuteWithRetryDemo();
        TryExecuteDemo();
        ManualControlDemo();
    }

    // ─────────────────────────────────────────────
    // §1  프리셋
    // ─────────────────────────────────────────────
    static void PresetsDemo()
    {
        DemoHelper.Section("§1  프리셋 · 상태 전이 규칙");
        var presets = new (string name, CircuitBreakerPolicy p)[]
        {
            ("Default", CircuitBreakerPolicy.Default),
            ("Strict",  CircuitBreakerPolicy.Strict),
            ("Lenient", CircuitBreakerPolicy.Lenient),
        };
        foreach (var (name, p) in presets)
        {
            DemoHelper.Show($"{name} FailureThreshold", p.FailureThreshold);
            DemoHelper.Show($"{name} OpenDuration", p.OpenDuration?.ToString() ?? "30 s (기본값)");
            DemoHelper.Show($"{name} HalfOpenSuccessThreshold", p.HalfOpenSuccessThreshold);
            Console.WriteLine();
        }
        DemoHelper.Info("Closed ─(실패≥Threshold)──► Open ─(경과)──► HalfOpen ─(성공)──► Closed");
    }

    // ─────────────────────────────────────────────
    // §2  상태 전이 시연
    // ─────────────────────────────────────────────
    static void StateTransitionDemo()
    {
        DemoHelper.Section("§2  상태 전이 시연 (빠른 OpenDuration)");

        var state = new CircuitBreakerState(
            new CircuitBreakerPolicy(
                FailureThreshold: 3,
                OpenDuration: TimeSpan.FromMilliseconds(300),
                HalfOpenSuccessThreshold: 1,
                OnStateChanged: (prev, next) =>
                {
                    Console.ForegroundColor = next switch
                    {
                        CircuitState.Open => ConsoleColor.Red,
                        CircuitState.HalfOpen => ConsoleColor.Yellow,
                        _ => ConsoleColor.Green
                    };
                    Console.WriteLine($"  [CB] {prev} → {next}");
                    Console.ResetColor();
                }));

        ShowState(state, "초기");

        DemoHelper.Info("── 3회 연속 실패 ──");
        DemoHelper.RunAsync(async () =>
        {
            for (int i = 1; i <= 3; i++)
            {
                Func<CancellationToken, Task<string>> failFunc =
                    ct => Task.FromException<string>(new IOException($"연결 오류 #{i}"));
                await DemoHelper.TryCatchAsync($"실패 {i}",
                    () => failFunc.ExecuteAsync(state));
                ShowState(state, $"실패 {i}회 후");
            }
        });

        DemoHelper.Info("── Open: 요청 즉시 차단 ──");
        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task<string>> func = ct => Task.FromResult("ok");
            await DemoHelper.TryCatchAsync("Open 차단", () => func.ExecuteAsync(state));
            DemoHelper.Show("잔여 차단 시간", state.RemainingOpenDuration.ToString(@"mm\:ss\.fff"));
        });

        DemoHelper.Info("── 300ms 대기 → HalfOpen ──");
        Thread.Sleep(350);
        ShowState(state, "대기 후");

        DemoHelper.Info("── HalfOpen 성공 → Closed ──");
        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task<string>> func = ct => Task.FromResult("복구 성공");
            var r = await func.ExecuteAsync(state);
            DemoHelper.Ok($"  결과: {r}");
            ShowState(state, "복구 후");
        });
    }

    // ─────────────────────────────────────────────
    // §3  ExecuteAsync
    // ─────────────────────────────────────────────
    static void ExecuteAsyncDemo()
    {
        DemoHelper.Section("§3  ExecuteAsync");

        var state = new CircuitBreakerState(CircuitBreakerPolicy.Default);

        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task<byte[]>> func =
                ct => Task.FromResult(new byte[] { 0x02, 0xAA, 0x03 });
            var result = await func.ExecuteAsync(state);
            DemoHelper.Ok($"Func<byte[]> 성공: {result.Length}B");
        });

        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task> action = ct => Task.CompletedTask;
            await action.ExecuteAsync(state);
            DemoHelper.Ok("Action 성공");
        });

        DemoHelper.Show("FailureCount", state.FailureCount);
        DemoHelper.Show("Current State", state.Current);
    }

    // ─────────────────────────────────────────────
    // §4  ExecuteWithRetryAsync
    // ─────────────────────────────────────────────
    static void ExecuteWithRetryDemo()
    {
        DemoHelper.Section("§4  ExecuteWithRetryAsync — CB + Retry");

        var state = new CircuitBreakerState(
            new CircuitBreakerPolicy(FailureThreshold: 10, OpenDuration: TimeSpan.FromSeconds(60)));

        DemoHelper.Info("Closed: 실패 → Retry 적용");
        DemoHelper.RunAsync(async () =>
        {
            int cnt = 0;
            Func<CancellationToken, Task<string>> func = ct =>
            {
                cnt++;
                if (cnt < 3) throw new HttpRequestException($"503 (시도 {cnt})");
                return Task.FromResult("OK");
            };
            var result = await func.ExecuteWithRetryAsync(state,
                new RetryPolicy(MaxAttempts: 5, Delay: TimeSpan.FromMilliseconds(10),
                    OnRetry: (ex, n) => DemoHelper.Warn($"  재시도 {n}: {ex.Message}")));
            DemoHelper.Ok($"  최종: {result}  (총 {cnt}회)");
        });

        DemoHelper.Info("Open 상태: 재시도 없이 즉시 차단");
        state.Trip();
        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task<string>> func = ct => Task.FromResult("ok");
            await DemoHelper.TryCatchAsync("Open 즉시 차단",
                () => func.ExecuteWithRetryAsync(state));
        });
        state.Reset();
    }

    // ─────────────────────────────────────────────
    // §5  TryExecuteAsync
    // ─────────────────────────────────────────────
    static void TryExecuteDemo()
    {
        DemoHelper.Section("§5  TryExecuteAsync — 예외 없는 CB 실행");

        var state = new CircuitBreakerState(
            new CircuitBreakerPolicy(FailureThreshold: 3, OpenDuration: TimeSpan.FromMilliseconds(200)));

        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task<int>> func = ct => Task.FromResult(42);
            var r = await func.TryExecuteAsync(state);
            DemoHelper.Show("성공 IsOk", r.IsOk);
            DemoHelper.Show("성공 Value", r.Value);
        });

        // 실패 3회 → Open
        for (int i = 0; i < 3; i++)
        {
            DemoHelper.RunAsync(async () =>
            {
                Func<CancellationToken, Task<int>> func =
                    ct => Task.FromException<int>(new Exception("오류"));
                await func.TryExecuteAsync(state);
            });
        }

        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task<int>> func = ct => Task.FromResult(99);
            var r = await func.TryExecuteAsync(state);
            switch (r)
            {
                case { IsOk: true }:
                    DemoHelper.Ok($"성공: {r.Value}");
                    break;
                case { Error: CircuitBreakerOpenException cbEx }:
                    DemoHelper.Warn($"CB 차단: {cbEx.RemainingDuration.TotalMilliseconds:0} ms 후 재시도");
                    break;
                default:
                    DemoHelper.Err($"오류: {r.Error!.Message}");
                    break;
            }
        });
    }

    // ─────────────────────────────────────────────
    // §6  수동 제어
    // ─────────────────────────────────────────────
    static void ManualControlDemo()
    {
        DemoHelper.Section("§6  수동 제어 — Reset · Trip");

        var state = new CircuitBreakerState(CircuitBreakerPolicy.Default);
        ShowState(state, "초기");

        state.Trip();
        ShowState(state, "Trip() 후 (강제 Open)");
        DemoHelper.Show("잔여 시간", state.RemainingOpenDuration.TotalSeconds.ToString("F1") + " s");

        state.Reset();
        ShowState(state, "Reset() 후 (강제 Closed)");
        DemoHelper.Show("FailureCount", state.FailureCount);
    }

    static void ShowState(CircuitBreakerState s, string label)
    {
        Console.ForegroundColor = s.Current switch
        {
            CircuitState.Closed => ConsoleColor.Green,
            CircuitState.Open => ConsoleColor.Red,
            CircuitState.HalfOpen => ConsoleColor.Yellow,
            _ => ConsoleColor.White
        };
        Console.WriteLine($"  [{label}] State={s.Current}  Failures={s.FailureCount}");
        Console.ResetColor();
    }
}