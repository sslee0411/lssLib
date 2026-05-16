// ══════════════════════════════════════════════════════════════════════
//  lssLib.NetSequence.Demo · NetSequenceExamples.cs
//  역할: 시퀀스 제어 전체 예제 (S1~S7)
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;
using lssLib.NetSequence;
using lssLib.Sequence;

namespace lssLib.NetSequence.Demo;

// ══════════════════════════════════════════════════════════════════════
//  S1 — 단일 장비 순차 Write
// ══════════════════════════════════════════════════════════════════════
static class SeqEx01_SingleWrite
{
    public static async Task RunAsync(
        NetSequenceController ctrl, NetSequenceContext ctx, CancellationToken ct)
    {
        Console.WriteLine("=== S1: 단일 장비 순차 Write ===");

        var seq = NetSequence.For(deviceId: 1)
            .Write("초기화", [0x01, 0x06, 0x00, 0x00, 0x00, 0x00])
            .Delay(200, "초기화 대기")
            .Write("모터 기동", [0x01, 0x06, 0x00, 0x01, 0x00, 0x01])
            .Write("속도 100rpm", [0x01, 0x06, 0x00, 0x02, 0x00, 0x64])
            .Write("비상 정지 해제", [0x01, 0x06, 0x00, 0x03, 0x00, 0x00],
                priority: NetPriority.Critical)
            .Build("모터 기동 시퀀스",
                continueOnError: false,
                totalTimeout: TimeSpan.FromMilliseconds(10000));

        Console.WriteLine($"  {seq}");
        SequenceResult result = await ctrl.RunAsync(seq, ctx, ct);
        Console.WriteLine($"\n  결과: {result}");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  S2 — 단일 장비 Request + 응답 검증 + 재시도
// ══════════════════════════════════════════════════════════════════════
static class SeqEx02_Request
{
    public static async Task RunAsync(
        NetSequenceController ctrl, NetSequenceContext ctx, CancellationToken ct)
    {
        Console.WriteLine("=== S2: Request 응답 검증 ===");

        var seq = NetSequence.For(deviceId: 1)
            .Write("밸브 열기", [0x01, 0x06, 0x00, 0x10, 0x00, 0x01])
            .Delay(500, "밸브 동작 대기")
            .Request("밸브 상태", [0x01, 0x03, 0x00, 0x10, 0x00, 0x01],
                validator: r =>
                {
                    if (!r.IsOk || r.Data!.Length < 5) return false;
                    bool open = r.Data[4] == 0x01;
                    Console.WriteLine($"    밸브: {(open ? "열림 ✔" : "닫힘 ✘")}");
                    return open;
                },
                timeoutMs: 500, retries: 3, retryDelayMs: 200)
            .Write("펌프 기동", [0x01, 0x06, 0x00, 0x11, 0x00, 0x01])
            .Request("공정 확인", [0x01, 0x03, 0x00, 0x20, 0x00, 0x02],
                validator: r => r.IsOk,
                onCompleted: sr =>
                {
                    if (sr.OutputData is NetResult nr)
                        Console.WriteLine($"    최종 응답: {nr.Data?.Length}B");
                })
            .Build("밸브+펌프 시퀀스", totalTimeout: TimeSpan.FromMilliseconds(15000));

        Console.WriteLine($"  {seq}");
        SequenceResult result = await ctrl.RunAsync(seq, ctx, ct);
        Console.WriteLine($"\n  결과: {result}");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  S3 — 다중 장비 순차 연계
// ══════════════════════════════════════════════════════════════════════
static class SeqEx03_Sequential
{
    public static async Task RunAsync(
        NetSequenceController ctrl, NetSequenceContext ctx, CancellationToken ct)
    {
        Console.WriteLine("=== S3: 다중 장비 순차 연계 ===");

        var seq = NetSequence.Create("라인 기동 시퀀스")

            .Then(StepExecutionMode.Sequential, "Phase1: 장비 기동")
                .AddWrite(1, "컨베이어", [0x01, 0x06, 0x00, 0x01, 0x00, 0x01])
                .AddDelay(300, "안정화")
                .AddWrite(2, "이송 로봇", [0x02, 0x06, 0x00, 0x01, 0x00, 0x01])
                .AddDelay(300)
                .AddWrite(3, "용접기 예열", [0x03, 0x06, 0x00, 0x01, 0x00, 0x01])

            .Then(StepExecutionMode.Sequential, "Phase2: 예열 확인")
                .AddDelay(2000, "예열 대기")
                .AddRequest(3, "예열 완료",
                    data: [0x03, 0x03, 0x00, 0x10, 0x00, 0x01],
                    validator: r => r.IsOk && r.Data!.Length >= 5 && r.Data[4] >= 0x50,
                    timeoutMs: 500, retries: 5, retryDelayMs: 500)
                .AddWrite(1, "라인 시작", [0x01, 0x06, 0x00, 0x20, 0x00, 0x01])

            .Build(continueOnError: false,
                   totalTimeout: TimeSpan.FromSeconds(30));

        Console.WriteLine($"  {seq}");
        SequenceResult result = await ctrl.RunAsync(seq, ctx, ct);
        Console.WriteLine($"\n  결과: {result}");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  S4 — 다중 장비 병렬 연계
// ══════════════════════════════════════════════════════════════════════
static class SeqEx04_Parallel
{
    public static async Task RunAsync(
        NetSequenceController ctrl, NetSequenceContext ctx, CancellationToken ct)
    {
        Console.WriteLine("=== S4: 다중 장비 병렬 연계 ===");

        var seq = NetSequence.Create("병렬 기동 시퀀스")

            .Then(StepExecutionMode.Parallel, "펌프 동시 기동")
                .AddWrite(1, "펌프A 기동", [0x01, 0x06, 0x00, 0x01, 0x00, 0x01])
                .AddWrite(2, "펌프B 기동", [0x02, 0x06, 0x00, 0x01, 0x00, 0x01])

            .Then(StepExecutionMode.Parallel, "펌프 상태 동시 확인")
                .AddDelay(500, "기동 대기")
                .AddRequest(1, "펌프A 상태",
                    [0x01, 0x03, 0x00, 0x64, 0x00, 0x01],
                    r => r.IsOk, timeoutMs: 500)
                .AddRequest(2, "펌프B 상태",
                    [0x02, 0x03, 0x00, 0x64, 0x00, 0x01],
                    r => r.IsOk, timeoutMs: 500)

            .Then(StepExecutionMode.Sequential, "완료 신호")
                .AddWrite(3, "공정 시작", [0x03, 0x06, 0x00, 0x01, 0x00, 0x01])

            .Build(continueOnError: false,
                   totalTimeout: TimeSpan.FromSeconds(15));

        Console.WriteLine($"  {seq}");
        SequenceResult result = await ctrl.RunAsync(seq, ctx, ct);
        Console.WriteLine($"\n  결과: {result}");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  S5 — 혼합 그룹 (실제 공정 시나리오)
// ══════════════════════════════════════════════════════════════════════
static class SeqEx05_Mixed
{
    public static async Task RunAsync(
        NetSequenceController ctrl, NetSequenceContext ctx, CancellationToken ct)
    {
        Console.WriteLine("=== S5: 혼합 그룹 (도장 공정 시나리오) ===");

        var seq = NetSequence.Create("도장 공정")

            .Then(StepExecutionMode.Sequential, "Phase1: 환기")
                .AddWrite(1, "환기 시작", [0x01, 0x06, 0x00, 0x01, 0x00, 0x01])
                .AddDelay(1000)
                .AddRequest(1, "환기 확인", [0x01, 0x03, 0x00, 0x10, 0x00, 0x01],
                    r => r.IsOk, timeoutMs: 500)

            .Then(StepExecutionMode.Parallel, "Phase2: 동시 예열")
                .AddWrite(2, "도장건A 예열", [0x02, 0x06, 0x00, 0x01, 0x00, 0x01])
                .AddWrite(3, "도장건B 예열", [0x03, 0x06, 0x00, 0x01, 0x00, 0x01])
                .AddWrite(4, "로봇 홈", [0x04, 0x06, 0x00, 0x01, 0x00, 0x00])

            .Then(StepExecutionMode.Parallel, "Phase3: 준비 병렬 확인")
                .AddDelay(500)
                .AddRequest(2, "도장건A 준비",
                    [0x02, 0x03, 0x00, 0x10, 0x00, 0x01], r => r.IsOk, retries: 3)
                .AddRequest(3, "도장건B 준비",
                    [0x03, 0x03, 0x00, 0x10, 0x00, 0x01], r => r.IsOk, retries: 3)

            .Then(StepExecutionMode.Sequential, "Phase4: 도장 실행")
                .AddWrite(4, "워크 이송", [0x04, 0x06, 0x00, 0x02, 0x00, 0x01])
                .AddDelay(800)
                .AddWrite(2, "도장 시작A", [0x02, 0x06, 0x00, 0x02, 0x00, 0x01])
                .AddWrite(3, "도장 시작B", [0x03, 0x06, 0x00, 0x02, 0x00, 0x01])
                .AddDelay(3000, "도장 진행")
                .AddWrite(2, "도장 완료A", [0x02, 0x06, 0x00, 0x02, 0x00, 0x00])
                .AddWrite(3, "도장 완료B", [0x03, 0x06, 0x00, 0x02, 0x00, 0x00])
                .AddWrite(4, "워크 반출", [0x04, 0x06, 0x00, 0x02, 0x00, 0x00])
                .AddWrite(1, "공정 완료", [0x01, 0x06, 0x00, 0x20, 0x00, 0x01])

            .Build(continueOnError: false,
                   totalTimeout: TimeSpan.FromMinutes(2),
                   onCompleted: r => Console.WriteLine($"\n  [Callback] {r}"));

        Console.WriteLine($"  {seq}  (전체 스텝: {seq.AllSteps.Count})");
        SequenceResult result = await ctrl.RunAsync(seq, ctx, ct);
        Console.WriteLine($"\n  결과: {result}");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  S6 — 배치 실행 (RunAllAsync)
// ══════════════════════════════════════════════════════════════════════
static class SeqEx06_Batch
{
    public static async Task RunAsync(
        NetSequenceController ctrl, NetSequenceContext ctx, CancellationToken ct)
    {
        Console.WriteLine("=== S6: 배치 실행 (공정A → 공정B → 공정C) ===");

        var seqA = NetSequence.For(1)
            .Write("원재료 투입", [0x01, 0x06, 0x00, 0x01, 0x00, 0x01])
            .Delay(200)
            .Write("혼합 시작", [0x01, 0x06, 0x00, 0x02, 0x00, 0x01])
            .Build("공정A — 원료 준비");

        var seqB = NetSequence.Create("공정B — 가공")
            .Then(StepExecutionMode.Parallel)
                .AddWrite(2, "가열로 기동", [0x02, 0x06, 0x00, 0x01, 0x00, 0x01])
                .AddWrite(3, "프레스 대기", [0x03, 0x06, 0x00, 0x01, 0x00, 0x00])
            .Then()
                .AddDelay(500)
                .AddWrite(3, "프레스 기동", [0x03, 0x06, 0x00, 0x01, 0x00, 0x01])
            .Build();

        var seqC = NetSequence.For(4)
            .Write("냉각 시작", [0x04, 0x06, 0x00, 0x01, 0x00, 0x01])
            .Delay(300)
            .Write("검사 요청", [0x04, 0x06, 0x00, 0x02, 0x00, 0x01])
            .Write("완제품 반출", [0x04, 0x06, 0x00, 0x03, 0x00, 0x01])
            .Build("공정C — 검사 및 반출");

        Console.WriteLine($"  A={seqA.Name} / B={seqB.Name} / C={seqC.Name}");

        SequenceBatchResult batch = await ctrl.RunAllAsync(
            [seqA, seqB, seqC], ctx, continueOnError: false, ct);

        Console.WriteLine($"\n  배치: {batch}");
        foreach (var r in batch.Results)
            Console.WriteLine($"    [{r.SequenceName}] " +
                $"{(r.IsSuccess ? "✔" : $"✘ {r.ErrorMessage}")} " +
                $"성공={r.SuccessCount}/{r.StepResults.Count}");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  S7 — Virtual Transport 전체 시연 ★ 하드웨어 불필요
// ══════════════════════════════════════════════════════════════════════
static class SeqEx07_Virtual
{
    public static async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("=== S7: Virtual Transport 시연 ★ 하드웨어 불필요 ===");
        Console.WriteLine("  lssLib.Sequence + lssLib.Net + lssLib.NetSequence 전체 검증");

        // ── 채널 생성 ─────────────────────────────────────────────────
        var hub1 = VirtualTransportHub.Create("dev1");
        var hub2 = VirtualTransportHub.Create("dev2");

        var cfg1 = new VirtualDeviceConfig(1, "VDev1", hub1);
        var cfg2 = new VirtualDeviceConfig(2, "VDev2", hub2);

        await using var ch1 = new RequestResponseChannel(
            cfg1, VirtualTransport.FromConfig(cfg1),
            new BinaryProtocol(0xAA), autoRegister: true);
        await using var ch2 = new RequestResponseChannel(
            cfg2, VirtualTransport.FromConfig(cfg2),
            new BinaryProtocol(0xAA), autoRegister: true);

        await ch1.StartAsync(ct);
        await ch2.StartAsync(ct);

        // ── 시뮬레이터 응답 주입 ──────────────────────────────────────
        var sim1 = new VirtualTransport(hub1, isServer: true);
        var sim2 = new VirtualTransport(hub2, isServer: true);
        await sim1.ConnectAsync(ct);
        await sim2.ConnectAsync(ct);
        var proto = new BinaryProtocol(0xAA);

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                var r1 = new byte[5]; r1[4] = 0x01;
                var r2 = new byte[5]; r2[4] = 0x01;
                await sim1.InjectAsync(proto.Encode(r1), ct);
                await sim2.InjectAsync(proto.Encode(r2), ct);
                await Task.Delay(30, ct);
            }
        }, ct);

        // ── 컨텍스트 ─────────────────────────────────────────────────
        var context = new NetSequenceContext(
            logAction: msg => Console.WriteLine($"  [LOG] {msg}"),
            logErrorAction: msg => Console.WriteLine($"  [ERR] {msg}"));

        // ── 시퀀스 정의 ──────────────────────────────────────────────
        var seq = NetSequence.Create("Virtual 공정 검증")

            .Then(StepExecutionMode.Sequential, "Phase1: 초기화")
                .AddWrite(1, "장치1 초기화", [0x00, 0x06, 0x00, 0x00])
                .AddDelay(50)
                .AddWrite(2, "장치2 초기화", [0x00, 0x06, 0x00, 0x00])

            .Then(StepExecutionMode.Parallel, "Phase2: 상태 확인")
                .AddRequest(1, "장치1 상태",
                    [0x00, 0x03, 0x00, 0x00],
                    r => r.IsOk && r.Data!.Length >= 5 && r.Data[4] == 0x01,
                    timeoutMs: 200, retries: 2)
                .AddRequest(2, "장치2 상태",
                    [0x00, 0x03, 0x00, 0x00],
                    r => r.IsOk, timeoutMs: 200)

            .Then(StepExecutionMode.Sequential, "Phase3: 공정")
                .AddWrite(1, "공정 시작", [0x00, 0x06, 0x00, 0x01])
                .AddWrite(2, "공정 시작", [0x00, 0x06, 0x00, 0x01])
                .AddDelay(100)
                .AddWrite(1, "공정 완료", [0x00, 0x06, 0x00, 0x02])
                .AddWrite(2, "공정 완료", [0x00, 0x06, 0x00, 0x02])

            .Build(continueOnError: false,
                   totalTimeout: TimeSpan.FromSeconds(10));

        Console.WriteLine($"\n  {seq}  (스텝 수={seq.AllSteps.Count})");

        // ── 실행 ─────────────────────────────────────────────────────
        var controller = new NetSequenceController();

        controller.SequenceStarted += s => Console.WriteLine($"\n  ▶ {s.Name}");
        controller.SequenceCompleted += r => Console.WriteLine($"  ■ {r}");
        controller.StepCompleted += r =>
        {
            string icon = r.IsSuccess ? "✔" : "✘";
            string retry = r.RetryCount > 0 ? $" 재시도={r.RetryCount}" : "";
            Console.WriteLine($"  [{r.Step.StepIndex:D2}:{r.Step.StepName}] " +
                $"{icon} {r.Elapsed.TotalMilliseconds:F0}ms{retry}" +
                (!r.IsSuccess ? $" → {r.ErrorMessage}" : ""));
        };

        SequenceResult result = await controller.RunAsync(seq, context, ct);

        Console.WriteLine($"\n  ═══ 최종 결과 ═══");
        Console.WriteLine($"  {result}");
        Console.WriteLine($"  성공={result.SuccessCount}/{result.StepResults.Count}");

        // 정리
        await sim1.DisposeAsync();
        await sim2.DisposeAsync();
        NetDeviceRegistry.Instance.Clear();
    }
}