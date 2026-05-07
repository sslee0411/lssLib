using lssLib.Retry;

namespace lssLib.Retry.Demo;

// ═══════════════════════════════════════════════════════════════════
//  RateLimiterDemo — RateLimiter 예제 (슬라이딩 윈도우)
//  §1 팩토리·프리셋  §2 ExecuteAsync  §3 ExecuteWithWaitAsync
//  §4 ThrowOnExceeded:false  §5 TryExecuteAsync  §6 CB+RL 조합
// ═══════════════════════════════════════════════════════════════════

internal static class RateLimiterDemo
{
    public static void Run()
    {
        DemoHelper.Header("RateLimiter — 속도 제한 (슬라이딩 윈도우)");
        PresetsDemo();
        ExecuteAsyncDemo();
        WaitDemo();
        SoftLimitDemo();
        TryExecuteDemo();
        CombineDemo();
    }

    // ─────────────────────────────────────────────
    // §1  팩토리 · 프리셋
    // ─────────────────────────────────────────────
    static void PresetsDemo()
    {
        DemoHelper.Section("§1  팩토리 · 프리셋");

        var factories = new (string label, RateLimiterPolicy p)[]
        {
            ("PerSecond(10)",  RateLimiterPolicy.PerSecond(10)),
            ("PerMinute(60)",  RateLimiterPolicy.PerMinute(60)),
            ("PerHour(1000)",  RateLimiterPolicy.PerHour(1_000)),
            ("PerDay(10000)",  RateLimiterPolicy.PerDay(10_000)),
        };
        foreach (var (label, p) in factories)
            DemoHelper.Show(label, $"Max={p.MaxRequests}  Window={p.Window}");

        Console.WriteLine();
        var presets = new (string name, RateLimiterPolicy p)[]
        {
            ("ApiDefault",   RateLimiterPolicy.ApiDefault),
            ("Strict",       RateLimiterPolicy.Strict),
            ("Lenient",      RateLimiterPolicy.Lenient),
            ("LoginAttempt", RateLimiterPolicy.LoginAttempt),
        };
        foreach (var (name, p) in presets)
            DemoHelper.Show(name, $"Max={p.MaxRequests}  Window={p.Window}");
    }

    // ─────────────────────────────────────────────
    // §2  ExecuteAsync — 초과 시 throw
    // ─────────────────────────────────────────────
    static void ExecuteAsyncDemo()
    {
        DemoHelper.Section("§2  ExecuteAsync — 초과 시 RateLimitExceededException");

        var limiter = new RateLimiterState(RateLimiterPolicy.PerSecond(5));

        DemoHelper.Info("초당 5회 제한 — 8회 연속 시도:");
        DemoHelper.RunAsync(async () =>
        {
            for (int i = 1; i <= 8; i++)
            {
                try
                {
                    Func<CancellationToken, Task<string>> func =
                        ct => Task.FromResult($"응답#{i}");
                    string r = await func.ExecuteAsync(limiter);
                    DemoHelper.Ok($"  요청 {i}: {r}  (남은 슬롯={limiter.Available})");
                }
                catch (RateLimitExceededException ex)
                {
                    DemoHelper.Warn($"  요청 {i}: 한도 초과 — {ex.NextAvailableAt:HH:mm:ss.fff} 재시도 가능");
                }
            }
        });
    }

    // ─────────────────────────────────────────────
    // §3  ExecuteWithWaitAsync — 슬롯 대기
    // ─────────────────────────────────────────────
    static void WaitDemo()
    {
        DemoHelper.Section("§3  ExecuteWithWaitAsync — 슬롯 대기 후 실행");

        var limiter = new RateLimiterState(RateLimiterPolicy.PerSecond(3));

        DemoHelper.Info("초당 3회 제한 — 5회 요청, 대기 허용(maxWait=2s):");
        DemoHelper.RunAsync(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 1; i <= 5; i++)
            {
                Func<CancellationToken, Task<string>> func =
                    ct => Task.FromResult($"응답#{i}");
                string r = await func.ExecuteWithWaitAsync(
                    limiter, maxWait: TimeSpan.FromSeconds(2));
                DemoHelper.Ok($"  요청 {i}: {r}  [{sw.ElapsedMilliseconds} ms]");
            }
            DemoHelper.Show("총 소요 시간", $"{sw.ElapsedMilliseconds} ms");
        });

        DemoHelper.Info("maxWait=50ms 초과 → TimeoutException:");
        DemoHelper.RunAsync(async () =>
        {
            var tight = new RateLimiterState(RateLimiterPolicy.PerSecond(1));
            Func<CancellationToken, Task<string>> fill = ct => Task.FromResult("fill");
            await fill.ExecuteAsync(tight);     // 슬롯 1개 소진

            await DemoHelper.TryCatchAsync("maxWait 초과", async () =>
            {
                Func<CancellationToken, Task<string>> func = ct => Task.FromResult("ok");
                await func.ExecuteWithWaitAsync(tight, maxWait: TimeSpan.FromMilliseconds(50));
            });
        });
    }

    // ─────────────────────────────────────────────
    // §4  ThrowOnExceeded: false — 조용히 건너뜀
    // ─────────────────────────────────────────────
    static void SoftLimitDemo()
    {
        DemoHelper.Section("§4  ThrowOnExceeded:false — 초과 시 default 반환");

        var softLimiter = new RateLimiterState(
            RateLimiterPolicy.PerSecond(2) with { ThrowOnExceeded = false });

        DemoHelper.Info("초당 2회 제한 (ThrowOnExceeded=false) — 5회 시도:");
        DemoHelper.RunAsync(async () =>
        {
            for (int i = 1; i <= 5; i++)
            {
                Func<CancellationToken, Task<string?>> func =
                    ct => Task.FromResult<string?>($"결과#{i}");
                string? r = await func.ExecuteAsync(softLimiter);

                if (r != null) DemoHelper.Ok($"  요청 {i}: {r}");
                else DemoHelper.Warn($"  요청 {i}: 건너뜀 (예외 없음)");
            }
        });
    }

    // ─────────────────────────────────────────────
    // §5  TryExecuteAsync — UtilResult 반환
    // ─────────────────────────────────────────────
    static void TryExecuteDemo()
    {
        DemoHelper.Section("§5  TryExecuteAsync — UtilResult 반환");

        var limiter = new RateLimiterState(RateLimiterPolicy.PerSecond(2));

        DemoHelper.Info("초당 2회 제한 — 4회 TryExecuteAsync:");
        DemoHelper.RunAsync(async () =>
        {
            for (int i = 1; i <= 4; i++)
            {
                Func<CancellationToken, Task<int>> func =
                    ct => Task.FromResult(i * 10);
                var r = await func.TryExecuteAsync(limiter);

                switch (r)
                {
                    case { IsOk: true }:
                        DemoHelper.Ok($"  요청 {i}: {r.Value}");
                        break;
                    case { Error: RateLimitExceededException rlEx }:
                        DemoHelper.Warn($"  요청 {i}: RL 초과 — {rlEx.NextAvailableAt:HH:mm:ss.fff}");
                        break;
                    default:
                        DemoHelper.Err($"  요청 {i}: {r.Error!.Message}");
                        break;
                }
            }
        });
    }

    // ─────────────────────────────────────────────
    // §6  CB + RL 동시 적용
    // ─────────────────────────────────────────────
    static void CombineDemo()
    {
        DemoHelper.Section("§6  CB + RL 동시 적용");

        var limiter = new RateLimiterState(RateLimiterPolicy.PerSecond(5));
        var breaker = new CircuitBreakerState(
            new CircuitBreakerPolicy(FailureThreshold: 3,
                OpenDuration: TimeSpan.FromMilliseconds(200)));

        DemoHelper.Info("초당 5회 제한 + CB(3회 실패 → Open) — 8회 요청:");
        DemoHelper.RunAsync(async () =>
        {
            int failCount = 0;
            for (int i = 1; i <= 8; i++)
            {
                try
                {
                    Func<CancellationToken, Task<string>> func = ct =>
                    {
                        if (++failCount <= 3)
                            throw new IOException($"서버 오류 #{failCount}");
                        return Task.FromResult($"OK#{i}");
                    };
                    string r = await func.ExecuteAsync(limiter, breaker);
                    DemoHelper.Ok($"  요청 {i}: {r}");
                }
                catch (RateLimitExceededException)
                {
                    DemoHelper.Warn($"  요청 {i}: 속도 한도 초과");
                }
                catch (CircuitBreakerOpenException ex)
                {
                    DemoHelper.Warn($"  요청 {i}: CB 차단 ({ex.RemainingDuration.TotalMilliseconds:0} ms 후 재시도)");
                }
                catch (Exception ex)
                {
                    DemoHelper.Err($"  요청 {i}: {ex.Message}");
                }
            }
        });

        DemoHelper.Show("최종 CB 상태", breaker.Current);
        DemoHelper.Show("CB FailureCount", breaker.FailureCount);
        DemoHelper.Show("RL Available", limiter.Available);
    }
}