using lssLib.Retry;

namespace lssLib.Retry.Demo;

// ═══════════════════════════════════════════════════════════════════
//  RetryDemo — RetryExtensions 예제
//  §1 동기Retry  §2 비동기Retry  §3 조건부  §4 Timeout
//  §5 RetryWithTimeout  §6 TryExecute
// ═══════════════════════════════════════════════════════════════════

internal static class RetryDemo
{
    public static void Run()
    {
        DemoHelper.Header("RetryExtensions — Retry · Timeout · TryExecute");
        SyncRetryDemo();
        AsyncRetryDemo();
        ConditionalRetryDemo();
        TimeoutDemo();
        RetryWithTimeoutDemo();
        TryExecuteDemo();
    }

    // ─────────────────────────────────────────────
    // §1  동기 Retry
    // ─────────────────────────────────────────────
    static void SyncRetryDemo()
    {
        DemoHelper.Section("§1  동기 Retry");

        // Action — 2회 실패 후 성공
        int attempt = 0;
        DemoHelper.Info("Action: 2회 실패 → 3회째 성공");
        Action openPort = () =>
        {
            attempt++;
            if (attempt < 3) throw new IOException($"포트 응답 없음 (시도 {attempt})");
            DemoHelper.Ok($"  포트 오픈 성공 (총 {attempt}회 시도)");
        };
        openPort.Retry(new RetryPolicy(
            MaxAttempts: 5, Delay: TimeSpan.FromMilliseconds(10),
            OnRetry: (ex, n) => DemoHelper.Warn($"  재시도 {n}: {ex.Message}")));

        // Func<T>
        attempt = 0;
        DemoHelper.Info("Func<string>: 1회 실패 → 2회째 성공");
        Func<string> readPort = () =>
        {
            attempt++;
            if (attempt < 2) throw new Exception("레지스트리 잠금");
            return $"COM{attempt}";
        };
        DemoHelper.Ok($"  결과: \"{readPort.Retry(RetryPolicy.Default)}\"");

        // 전부 실패
        DemoHelper.Info("모든 시도 실패 → 마지막 예외 throw:");
        Action alwaysFail = () => throw new IOException("하드웨어 오류");
        DemoHelper.TryCatch("전부 실패",
            () => alwaysFail.Retry(new RetryPolicy(MaxAttempts: 2, Delay: TimeSpan.Zero)));
    }

    // ─────────────────────────────────────────────
    // §2  비동기 Retry
    // ─────────────────────────────────────────────
    static void AsyncRetryDemo()
    {
        DemoHelper.Section("§2  비동기 Retry");

        int n = 0;
        DemoHelper.Info("RetryAsync(Func<Task>): 3회 실패 → 4회째 성공");
        DemoHelper.RunAsync(async () =>
        {
            Func<Task> connectApi = async () =>
            {
                n++;
                await Task.Delay(5);
                if (n < 4) throw new HttpRequestException($"HTTP 503 (시도 {n})");
                DemoHelper.Ok($"  API 연결 성공 (총 {n}회)");
            };
            await connectApi.RetryAsync(RetryPolicy.Http);
        });

        DemoHelper.Info("RetryAsync<byte[]>: 2회 실패 → 3회째 성공");
        DemoHelper.RunAsync(async () =>
        {
            int cnt = 0;
            Func<Task<byte[]>> recvFrame = () =>
            {
                cnt++;
                if (cnt < 3) throw new TimeoutException($"수신 타임아웃 (시도 {cnt})");
                return Task.FromResult(new byte[] { 0x02, 0xAA, 0xBB, 0x03 });
            };
            byte[] frame = await recvFrame.RetryAsync(new RetryPolicy(
                MaxAttempts: 5, Delay: TimeSpan.FromMilliseconds(10),
                OnRetry: (ex, n2) => DemoHelper.Warn($"  재시도 {n2}: {ex.Message}")));
            DemoHelper.Ok($"  프레임: {frame.Length}B  HEX={BitConverter.ToString(frame)}");
        });

        DemoHelper.Info("CancellationToken 취소 → 즉시 전파:");
        DemoHelper.RunAsync(async () =>
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50);
            await DemoHelper.TryCatchAsync("취소 전파", async () =>
            {
                Func<Task> wait = () => Task.Delay(500, cts.Token);
                await wait.RetryAsync(RetryPolicy.Default, cts.Token);
            });
        });
    }

    // ─────────────────────────────────────────────
    // §3  조건부 Retry
    // ─────────────────────────────────────────────
    static void ConditionalRetryDemo()
    {
        DemoHelper.Section("§3  RetryOnAsync<T, TEx> — 특정 예외만 재시도");

        DemoHelper.Info("HttpRequestException만 재시도 → 3회째 성공:");
        DemoHelper.RunAsync(async () =>
        {
            int cnt = 0;
            Func<Task<string>> callApi = () =>
            {
                cnt++;
                if (cnt < 3) throw new HttpRequestException($"503 (시도 {cnt})");
                return Task.FromResult("{\"status\":\"ok\"}");
            };
            string body = await callApi.RetryOnAsync<string, HttpRequestException>(
                new RetryPolicy(MaxAttempts: 5, Delay: TimeSpan.FromMilliseconds(10),
                    OnRetry: (ex, n) => DemoHelper.Warn($"  재시도 {n}: {ex.Message}")));
            DemoHelper.Ok($"  응답: {body}");
        });

        DemoHelper.Info("IOException 외 예외 → 즉시 throw:");
        DemoHelper.RunAsync(async () =>
        {
            Func<Task<byte[]>> func = () =>
                Task.FromException<byte[]>(new InvalidOperationException("로직 오류"));
            await DemoHelper.TryCatchAsync("즉시 throw",
                () => func.RetryOnAsync<byte[], IOException>(
                    new RetryPolicy(MaxAttempts: 3, Delay: TimeSpan.Zero)));
        });
    }

    // ─────────────────────────────────────────────
    // §4  Timeout 래퍼
    // ─────────────────────────────────────────────
    static void TimeoutDemo()
    {
        DemoHelper.Section("§4  WithTimeout — 타임아웃 래퍼");

        DemoHelper.Info("200 ms 타임아웃 내 완료:");
        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task<byte[]>> func =
                ct => Task.FromResult(new byte[] { 0x01, 0x02, 0x03 });
            var result = await func.WithTimeout(TimeSpan.FromMilliseconds(200), "프레임 수신");
            DemoHelper.Ok($"  성공: {result.Length}B");
        });

        DemoHelper.Info("100 ms 타임아웃 → 초과:");
        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task> action = ct => Task.Delay(500, ct);
            await DemoHelper.TryCatchAsync("타임아웃 초과",
                () => action.WithTimeout(TimeSpan.FromMilliseconds(100), "센서 초기화"));
        });
    }

    // ─────────────────────────────────────────────
    // §5  RetryWithTimeout
    // ─────────────────────────────────────────────
    static void RetryWithTimeoutDemo()
    {
        DemoHelper.Section("§5  RetryWithTimeout — 재시도 + 이중 타임아웃");

        DemoHelper.Info("perAttempt=200ms, total=1s → 성공:");
        DemoHelper.RunAsync(async () =>
        {
            int cnt = 0;
            Func<CancellationToken, Task<string>> func =
                ct => Task.FromResult($"응답#{++cnt}");
            var result = await func.RetryWithTimeout(
                policy: new RetryPolicy(MaxAttempts: 3, Delay: TimeSpan.Zero),
                perAttemptTimeout: TimeSpan.FromMilliseconds(200),
                totalTimeout: TimeSpan.FromSeconds(1));
            DemoHelper.Ok($"  결과: {result}");
        });

        DemoHelper.Info("각 시도 200ms 지연 → totalTimeout=300ms 소진:");
        DemoHelper.RunAsync(async () =>
        {
            Func<CancellationToken, Task<string>> slow =
                async ct => { await Task.Delay(200, ct); return "ok"; };
            await DemoHelper.TryCatchAsync("전체 타임아웃",
                () => slow.RetryWithTimeout(
                    policy: new RetryPolicy(MaxAttempts: 5, Delay: TimeSpan.Zero,
                        OnRetry: (_, n) => DemoHelper.Warn($"  재시도 {n}")),
                    totalTimeout: TimeSpan.FromMilliseconds(300)));
        });
    }

    // ─────────────────────────────────────────────
    // §6  TryExecute 동기
    // ─────────────────────────────────────────────
    static void TryExecuteDemo()
    {
        DemoHelper.Section("§6  TryExecute — 동기 안전 실행");

        Action ok = () => { };
        DemoHelper.Show("Action 성공 — bool", ok.TryExecute(out var e1));
        DemoHelper.Show("Action 성공 — error", e1?.Message ?? "<null>");

        Action fail = () => throw new IOException("포트 오류");
        bool ok2 = fail.TryExecute(out var e2);
        DemoHelper.Show("Action 실패 — bool", ok2);
        DemoHelper.Show("Action 실패 — error", e2!.Message);

        Func<string> getPort = () => "COM3";
        DemoHelper.Show("Func<string> 성공", getPort.TryExecute<string>(out _));

        Func<int> badFunc = () => throw new Exception("레지스트리 오류");
        int? num = badFunc.TryExecute<int>(out var e4);
        DemoHelper.Show("Func<int> 실패 값", num?.ToString() ?? "<null(default)>");
        DemoHelper.Show("Func<int> 실패 error", e4!.Message);

        DemoHelper.Info("── 정리 작업 실패 무시 패턴 ──");
        var cleanups = new (string label, Action act)[]
        {
            ("포트 닫기",   () => { }),
            ("버퍼 플러시", () => throw new Exception("버퍼 오류")),
            ("로그 닫기",   () => { }),
        };
        foreach (var (label, act) in cleanups)
        {
            bool s = act.TryExecute(out var e);
            if (s) DemoHelper.Ok($"  {label}");
            else DemoHelper.Warn($"  {label} 실패 (무시): {e!.Message}");
        }
    }
}