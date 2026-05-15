// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence.Demo · SequenceDemoExamples.cs
//  범용 시퀀스 엔진 데모 — 도메인 독립 예제 (D1~D5)
//
//  lssLib.Net / DB / HTTP 어떤 것도 참조하지 않습니다.
//  직접 SequenceStepBase / SequenceContextBase 를 상속하여
//  커스텀 스텝을 만드는 패턴을 보여줍니다.
// ══════════════════════════════════════════════════════════════════════

using lssLib.Sequence;

namespace lssLib.Sequence.Demo;

// ══════════════════════════════════════════════════════════════════════
//  커스텀 스텝 정의 — 데모 전용
// ══════════════════════════════════════════════════════════════════════

/// <summary>간단한 에코 스텝 — 메시지를 컨텍스트에 기록합니다.</summary>
sealed class EchoStep : SequenceStepBase
{
    public override string StepName { get; }
    private readonly string _message;

    public EchoStep(string name, string message, int delayMs = 0)
    {
        StepName = name;
        _message = message;
        Delay = TimeSpan.FromMilliseconds(delayMs);
    }

    protected override Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
    {
        context.Log($"[Echo] {_message}");
        return Task.FromResult(SequenceStepResult.Ok(this, outputData: _message));
    }
}

/// <summary>성공/실패 여부를 파라미터로 받는 테스트 스텝.</summary>
sealed class MockStep : SequenceStepBase
{
    public override string StepName { get; }
    private readonly bool _succeed;

    public MockStep(string name, bool succeed = true, int delayMs = 50)
    {
        StepName = name;
        _succeed = succeed;
        Delay = TimeSpan.FromMilliseconds(delayMs);
    }

    protected override async Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
    {
        await Task.Delay(50, ct);   // 작업 시뮬레이션
        if (!_succeed)
            return SequenceStepResult.Fail(this, $"{StepName} 실패 (Mock)");
        return SequenceStepResult.Ok(this);
    }
}

/// <summary>변수를 저장하고 읽는 스텝.</summary>
sealed class SetVariableStep : SequenceStepBase
{
    public override string StepName { get; }
    private readonly string _key;
    private readonly object _value;

    public SetVariableStep(string name, string key, object value)
    {
        StepName = name;
        _key = key;
        _value = value;
    }

    protected override Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
    {
        context.SetVariable(_key, _value);
        context.Log($"[SetVar] {_key} = {_value}");
        return Task.FromResult(SequenceStepResult.Ok(this));
    }
}

/// <summary>변수를 읽어 검증하는 스텝.</summary>
sealed class AssertVariableStep : SequenceStepBase
{
    public override string StepName { get; }
    private readonly string _key;
    private readonly Func<object?, bool> _predicate;
    private readonly string _failMessage;

    public AssertVariableStep(string name, string key,
        Func<object?, bool> predicate, string failMessage = "검증 실패")
    {
        StepName = name;
        _key = key;
        _predicate = predicate;
        _failMessage = failMessage;
    }

    protected override Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
    {
        var val = context.GetVariable<object>(_key);
        bool ok = _predicate(val);
        if (!ok)
        {
            context.LogError($"[Assert] {_key}={val} → {_failMessage}");
            return Task.FromResult(SequenceStepResult.Fail(this, _failMessage));
        }
        context.Log($"[Assert] {_key}={val} ✔");
        return Task.FromResult(SequenceStepResult.Ok(this));
    }
}

// ══════════════════════════════════════════════════════════════════════
//  커스텀 컨텍스트 — 인메모리 "장치" 딕셔너리
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 데모용 인메모리 컨텍스트.
/// Dictionary 로 장치(Mock)를 등록합니다.
/// </summary>
sealed class DemoContext : SequenceContextBase
{
    private readonly Dictionary<int, object> _devices;

    public DemoContext(Dictionary<int, object>? devices = null)
        => _devices = devices ?? [];

    protected override object? GetDeviceCore(int deviceId)
        => _devices.TryGetValue(deviceId, out var d) ? d : null;

    protected override bool IsDeviceConnectedCore(int deviceId)
        => _devices.ContainsKey(deviceId);

    protected override void LogCore(string message)
        => Console.WriteLine($"  [LOG] {message}");

    protected override void LogErrorCore(string message)
        => Console.WriteLine($"  [ERR] {message}");
}

// ══════════════════════════════════════════════════════════════════════
//  DemoEx01 — 커스텀 스텝 단일 순차
// ══════════════════════════════════════════════════════════════════════
static class DemoEx01_CustomStep
{
    public static async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("\n=== D1: 커스텀 스텝 단일 순차 ===");
        Console.WriteLine("  SequenceBuilderBase 없이 직접 SequenceDefinition 구성");

        // ── 스텝 직접 구성 ───────────────────────────────────────────
        var group = new SequenceGroup { ExecutionMode = StepExecutionMode.Sequential };
        group.Steps.Add(new EchoStep("Step1", "공정 시작", delayMs: 0));
        group.Steps.Add(new MockStep("Step2", succeed: true, delayMs: 100));
        group.Steps.Add(new EchoStep("Step3", "중간 처리", delayMs: 50));
        group.Steps.Add(new MockStep("Step4", succeed: true, delayMs: 80));
        group.Steps.Add(new EchoStep("Step5", "공정 완료"));

        // StepIndex 자동 부여
        for (int i = 0; i < group.Steps.Count; i++)
            group.Steps[i].StepIndex = i;

        var sequence = new SequenceDefinition("Demo 순차 시퀀스")
        { ContinueOnError = false };
        sequence.AddGroup(group);

        Console.WriteLine($"  시퀀스: {sequence}");

        // ── 실행 ────────────────────────────────────────────────────
        var controller = new DemoController();
        var context = new DemoContext();

        controller.StepCompleted += r =>
            Console.WriteLine($"  {(r.IsSuccess ? "✔" : "✘")} [{r.Step.StepIndex}:{r.Step.StepName}] " +
                $"{r.Elapsed.TotalMilliseconds:F0}ms");

        SequenceResult result = await controller.RunAsync(sequence, context, ct);
        Console.WriteLine($"  결과: {result}");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  DemoEx02 — Sequential + Parallel 혼합
// ══════════════════════════════════════════════════════════════════════
static class DemoEx02_MixedGroups
{
    public static async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("\n=== D2: Sequential + Parallel 혼합 ===");

        // 그룹1: 순차
        var g1 = new SequenceGroup
        {
            Name = "순차 초기화",
            ExecutionMode = StepExecutionMode.Sequential
        };
        g1.Steps.Add(new EchoStep("Init1", "초기화 A"));
        g1.Steps.Add(new EchoStep("Init2", "초기화 B", delayMs: 100));

        // 그룹2: 병렬
        var g2 = new SequenceGroup
        {
            Name = "병렬 작업",
            ExecutionMode = StepExecutionMode.Parallel
        };
        g2.Steps.Add(new MockStep("Parallel-A", succeed: true, delayMs: 200));
        g2.Steps.Add(new MockStep("Parallel-B", succeed: true, delayMs: 150));
        g2.Steps.Add(new MockStep("Parallel-C", succeed: true, delayMs: 180));

        // 그룹3: 순차 완료
        var g3 = new SequenceGroup
        {
            Name = "순차 완료",
            ExecutionMode = StepExecutionMode.Sequential
        };
        g3.Steps.Add(new EchoStep("Done", "모든 작업 완료"));

        // StepIndex 부여
        int idx = 0;
        foreach (var g in new[] { g1, g2, g3 })
            foreach (var s in g.Steps)
                s.StepIndex = idx++;

        var sequence = new SequenceDefinition("혼합 그룹 시퀀스");
        sequence.AddGroup(g1).AddGroup(g2).AddGroup(g3);

        Console.WriteLine($"  {sequence}");

        var controller = new DemoController();
        var context = new DemoContext();

        controller.StepCompleted += r =>
            Console.WriteLine($"  {(r.IsSuccess ? "✔" : "✘")} {r.Step.StepName} " +
                $"({r.Elapsed.TotalMilliseconds:F0}ms)");

        SequenceResult result = await controller.RunAsync(sequence, context, ct);
        Console.WriteLine($"  결과: {result}");
        Console.WriteLine($"  병렬 그룹은 {g2.Steps.Count}개 스텝이 동시 실행됩니다.");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  DemoEx03 — 컨텍스트 변수 스텝 간 공유
// ══════════════════════════════════════════════════════════════════════
static class DemoEx03_ContextVariable
{
    public static async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("\n=== D3: 컨텍스트 변수 스텝 간 공유 ===");

        var group = new SequenceGroup { ExecutionMode = StepExecutionMode.Sequential };
        group.Steps.Add(new SetVariableStep("변수 설정", "target_temp", 85.5));
        group.Steps.Add(new SetVariableStep("플래그 설정", "heating", true));
        group.Steps.Add(new AssertVariableStep("온도 검증", "target_temp",
            v => v is double d && d > 80.0, "목표 온도 미달"));
        group.Steps.Add(new AssertVariableStep("플래그 검증", "heating",
            v => v is bool b && b, "가열 미시작"));
        group.Steps.Add(new EchoStep("완료", "모든 변수 검증 통과"));

        for (int i = 0; i < group.Steps.Count; i++)
            group.Steps[i].StepIndex = i;

        var sequence = new SequenceDefinition("변수 공유 시퀀스");
        sequence.AddGroup(group);

        var controller = new DemoController();
        var context = new DemoContext();

        controller.StepCompleted += r =>
            Console.WriteLine($"  {(r.IsSuccess ? "✔" : "✘")} {r.Step.StepName}");

        SequenceResult result = await controller.RunAsync(sequence, context, ct);
        Console.WriteLine($"  결과: {result}");

        // 컨텍스트 변수 최종 값 출력
        Console.WriteLine($"  target_temp = {context.GetVariable<double>("target_temp")}");
        Console.WriteLine($"  heating     = {context.GetVariable<bool>("heating")}");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  DemoEx04 — ContinueOnError 오류 무시 모드
// ══════════════════════════════════════════════════════════════════════
static class DemoEx04_ContinueOnError
{
    public static async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("\n=== D4: ContinueOnError 오류 무시 모드 ===");

        var group = new SequenceGroup { ExecutionMode = StepExecutionMode.Sequential };
        group.Steps.Add(new MockStep("Step1 성공", succeed: true));
        group.Steps.Add(new MockStep("Step2 실패", succeed: false));   // 실패
        group.Steps.Add(new MockStep("Step3 성공", succeed: true));    // 계속 실행
        group.Steps.Add(new MockStep("Step4 실패", succeed: false));   // 실패
        group.Steps.Add(new EchoStep("Step5", "ContinueOnError=true → 끝까지 실행"));

        for (int i = 0; i < group.Steps.Count; i++)
            group.Steps[i].StepIndex = i;

        var sequence = new SequenceDefinition("ContinueOnError 시퀀스")
        { ContinueOnError = true };   // ← 오류 발생해도 계속
        sequence.AddGroup(group);

        var controller = new DemoController();
        var context = new DemoContext();

        controller.StepCompleted += r =>
            Console.WriteLine($"  {(r.IsSuccess ? "✔" : "✘")} {r.Step.StepName}" +
                (r.ErrorMessage is not null ? $" → {r.ErrorMessage}" : ""));

        SequenceResult result = await controller.RunAsync(sequence, context, ct);
        Console.WriteLine($"\n  결과: {result}");
        Console.WriteLine($"  성공={result.SuccessCount} 실패={result.FailCount} (총 {result.StepResults.Count})");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  DemoEx05 — 배치 실행 RunAllAsync
// ══════════════════════════════════════════════════════════════════════
static class DemoEx05_BatchRun
{
    public static async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("\n=== D5: 배치 실행 (RunAllAsync) ===");

        SequenceDefinition MakeSeq(string name, bool allPass)
        {
            var g = new SequenceGroup { ExecutionMode = StepExecutionMode.Sequential };
            g.Steps.Add(new EchoStep($"{name}-Start", $"{name} 시작"));
            g.Steps.Add(new MockStep($"{name}-Work", succeed: allPass));
            g.Steps.Add(new EchoStep($"{name}-End", $"{name} 완료"));
            for (int i = 0; i < g.Steps.Count; i++) g.Steps[i].StepIndex = i;
            var s = new SequenceDefinition(name);
            s.AddGroup(g);
            return s;
        }

        var seqA = MakeSeq("공정A", allPass: true);
        var seqB = MakeSeq("공정B", allPass: true);
        var seqC = MakeSeq("공정C", allPass: false);  // 실패 시퀀스
        var seqD = MakeSeq("공정D", allPass: true);   // C 실패로 건너뜀

        var controller = new DemoController();
        var context = new DemoContext();

        controller.SequenceStarted += s => Console.WriteLine($"\n  ▶ [{s.Name}] 시작");
        controller.SequenceCompleted += r => Console.WriteLine($"  ■ [{r.SequenceName}] " +
            $"{(r.IsSuccess ? "성공" : $"실패({r.ErrorMessage})")}");

        // continueOnError=false → 공정C 실패 시 공정D 실행 안 됨
        SequenceBatchResult batch = await controller.RunAllAsync(
            [seqA, seqB, seqC, seqD], context,
            continueOnError: false, ct: ct);

        Console.WriteLine($"\n  배치 결과: {batch}");
        Console.WriteLine($"  실행된 시퀀스: {batch.Results.Count}/{4}");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  DemoController — SequenceControllerBase 최소 구현
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 데모용 컨트롤러 — SequenceControllerBase 상속, 추가 구현 없음.
/// </summary>
sealed class DemoController : SequenceControllerBase { }