using lssLib.Retry;

namespace lssLib.Retry.Demo;

// ═══════════════════════════════════════════════════════════════════
//  PipelineDemo — CB + RL + Retry 3중 보호 통합 시나리오
//
//  시나리오 1: HTTP API 클라이언트 (RL 분당10 + CB 3회 + Retry)
//  시나리오 2: STX 프레임 수신 파이프라인 (RL 초당30 + CB CRC오류)
//  시나리오 3: 앱 종료 안전 정리 (TryExecuteAsync 실패 무시)
//
//  ★ CB+RL 조합 TryExecuteAsync 오버로드 없음
//    → try/catch 패턴으로 UtilResult를 직접 구성합니다.
// ═══════════════════════════════════════════════════════════════════

internal static class PipelineDemo
{
    public static void Run()
    {
        DemoHelper.Header("Pipeline — CB + RL + Retry 3중 보호 시나리오");
        Scenario1_HttpClient();
        Scenario2_FramePipeline();
        Scenario3_SafeShutdown();
    }

    // ═══════════════════════════════════════════════════════════════
    //  시나리오 1 — HTTP API 클라이언트
    //  RL(분당 10회) + CB(3회 실패 → 300ms Open)
    // ═══════════════════════════════════════════════════════════════
    static void Scenario1_HttpClient()
    {
        DemoHelper.Section("시나리오 1 — HTTP API 클라이언트 (RL + CB)");

        var limiter = new RateLimiterState(RateLimiterPolicy.PerMinute(10));
        var breaker = new CircuitBreakerState(
            new CircuitBreakerPolicy(
                FailureThreshold: 3,
                OpenDuration: TimeSpan.FromMilliseconds(300),
                OnStateChanged: (prev, next) =>
                    DemoHelper.Warn($"  [CB] {prev} → {next}")));

        // CB+RL 조합 안전 실행 헬퍼
        async Task<UtilResult<string>> CallApi(string url, int failUntil = 0)
        {
            int attempt = 0;
            Func<CancellationToken, Task<string>> func = ct =>
            {
                attempt++;
                if (attempt <= failUntil)
                    throw new HttpRequestException($"503 (시도 {attempt})");
                return Task.FromResult($"{{\"url\":\"{url}\"}}");
            };
            try { return UtilResults.Ok(await func.ExecuteAsync(limiter, breaker)); }
            catch (Exception ex) { return UtilResults.Fail<string>(ex); }
        }

        // ① 정상 호출 3회
        DemoHelper.Info("── 정상 호출 3회 ──");
        DemoHelper.RunAsync(async () =>
        {
            for (int i = 1; i <= 3; i++)
            {
                var r = await CallApi($"/api/sensor/{i}");
                if (r.IsOk) DemoHelper.Ok($"  호출 {i}: {r.Unwrap()}");
                else DemoHelper.Err($"  호출 {i}: {r.Error!.Message}");
            }
            DemoHelper.Show("RL 남은 슬롯", limiter.Available);
        });

        // ② CB 실패 3회 누적
        DemoHelper.Info("── CB 실패 3회 누적 ──");
        DemoHelper.RunAsync(async () =>
        {
            for (int i = 1; i <= 3; i++)
            {
                var r = await CallApi("/api/fail", failUntil: 10);
                if (r.IsError)
                    DemoHelper.Err($"  실패 {i}: {r.Error!.Message}  (Failures={breaker.FailureCount})");
            }
        });

        // ③ CB Open — 즉시 차단
        DemoHelper.Info("── CB Open: 즉시 차단 ──");
        DemoHelper.RunAsync(async () =>
        {
            var r = await CallApi("/api/sensor/4");
            if (r is { Error: CircuitBreakerOpenException cbEx })
                DemoHelper.Warn($"  CB 차단: {cbEx.RemainingDuration.TotalMilliseconds:0} ms 후 재시도");
        });

        // ④ 대기 후 복구
        DemoHelper.Info("── 300ms 대기 후 복구 ──");
        Thread.Sleep(400);
        DemoHelper.RunAsync(async () =>
        {
            var r = await CallApi("/api/sensor/5");
            if (r.IsOk) DemoHelper.Ok($"  복구 성공: {r.Unwrap()}");
            DemoHelper.Show("CB 최종 상태", breaker.Current);
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  시나리오 2 — STX 프레임 수신 파이프라인
    //  lssLib.Serialization.WpfDemo 탭8 모사
    //  RL(초당 30프레임) + CB(CRC 연속 오류 차단) + UtilResult 분기
    // ═══════════════════════════════════════════════════════════════
    static void Scenario2_FramePipeline()
    {
        DemoHelper.Section("시나리오 2 — STX 프레임 수신 파이프라인 (RL + CB)");

        var frameLimiter = new RateLimiterState(RateLimiterPolicy.PerSecond(30));
        var crcBreaker = new CircuitBreakerState(
            new CircuitBreakerPolicy(
                FailureThreshold: 3,
                OpenDuration: TimeSpan.FromMilliseconds(200),
                OnStateChanged: (prev, next) =>
                {
                    if (next == CircuitState.Open)
                        DemoHelper.Warn("  [CRC-CB] 연속 CRC 오류 → 200ms 수신 차단");
                }));

        var rng = new Random(42);
        int frameOk = 0, frameFail = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        DemoHelper.Info("20 프레임 처리 (5~7번 CRC 오류 주입):");
        DemoHelper.RunAsync(async () =>
        {
            for (int i = 1; i <= 20; i++)
            {
                bool crcError = i is 5 or 6 or 7;
                int capturedI = i;

                Func<CancellationToken, Task<(uint id, float temp)>> func = ct =>
                {
                    uint id = (uint)capturedI;
                    float temp = 20f + (float)(rng.NextDouble() * 10);
                    if (crcError)
                        throw new InvalidDataException($"CRC 불일치 (프레임 #{capturedI})");
                    return Task.FromResult((id, temp));
                };

                UtilResult<(uint id, float temp)> r;
                try { r = UtilResults.Ok(await func.ExecuteAsync(frameLimiter, crcBreaker)); }
                catch (Exception ex) { r = UtilResults.Fail<(uint, float)>(ex); }

                switch (r)
                {
                    case { IsOk: true }:
                        frameOk++;
                        if (i <= 4 || i >= 10)
                            DemoHelper.Ok($"  Frame #{i:D2}: ID={r.Value.id} Temp={r.Value.temp:F1}°C");
                        break;
                    case { Error: CircuitBreakerOpenException cbEx }:
                        frameFail++;
                        DemoHelper.Warn($"  Frame #{i:D2}: CB 차단 ({cbEx.RemainingDuration.TotalMilliseconds:0}ms)");
                        Thread.Sleep(210);
                        break;
                    case { Error: RateLimitExceededException }:
                        DemoHelper.Warn($"  Frame #{i:D2}: RL 초과");
                        break;
                    default:
                        frameFail++;
                        DemoHelper.Err($"  Frame #{i:D2}: {r.Error!.Message}");
                        break;
                }
            }
        });

        sw.Stop();
        Console.WriteLine();
        DemoHelper.Info("── 처리 결과 ──");
        DemoHelper.Show("성공 프레임", frameOk);
        DemoHelper.Show("실패 프레임", frameFail);
        DemoHelper.Show("총 소요 시간", $"{sw.ElapsedMilliseconds} ms");
        DemoHelper.Show("CB 최종 상태", crcBreaker.Current);
        DemoHelper.Show("RL 남은 슬롯", frameLimiter.Available);
    }

    // ═══════════════════════════════════════════════════════════════
    //  시나리오 3 — 앱 종료 안전 정리
    //  TryExecuteAsync(Func<Task>) — 실패 개별 처리, 계속 진행
    // ═══════════════════════════════════════════════════════════════
    static void Scenario3_SafeShutdown()
    {
        DemoHelper.Section("시나리오 3 — 앱 종료 안전 정리 (TryExecuteAsync)");

        var cleanups = new (string name, bool fail, int delayMs)[]
        {
            ("RingBuffer 플러시",  false, 20),
            ("프레임 캐시 저장",   false, 15),
            ("DB 연결 해제",       true,  10),
            ("MQ 연결 해제",       false, 10),
            ("텔레메트리 플러시",  true,   5),
            ("로그 파일 닫기",     false,  5),
        };

        int success = 0, failed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        DemoHelper.Info("정리 작업 순차 실행 (실패 시 계속 진행):");
        DemoHelper.RunAsync(async () =>
        {
            foreach (var (name, shouldFail, delay) in cleanups)
            {
                string capturedName = name;
                bool capturedFail = shouldFail;

                Func<Task> action = async () =>
                {
                    await Task.Delay(delay);
                    if (capturedFail)
                        throw new Exception($"{capturedName} 오류: 리소스 이미 해제됨");
                };
                UtilResult r = await action.TryExecuteAsync();

                if (r.IsOk) { success++; DemoHelper.Ok($"  {name}"); }
                else { failed++; DemoHelper.Warn($"  {name}: {r.Error!.Message}"); }
            }
        });

        sw.Stop();
        Console.WriteLine();
        DemoHelper.Info("── 종료 결과 ──");
        DemoHelper.Show("성공", success);
        DemoHelper.Show("실패(무시)", failed);
        DemoHelper.Show("총 소요", $"{sw.ElapsedMilliseconds} ms");
        DemoHelper.Ok("앱 정상 종료 완료 (모든 정리 시도됨)");
    }
}