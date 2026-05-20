# lssLib.Sequence

**범용 시퀀스 실행 엔진** · .NET 8.0 · C# 12 · BCL only

---

## 개요

통신·DB·HTTP·Node-RED 스타일 워크플로 등 어떤 도메인에서도 재사용 가능한  
**인터페이스 + 추상 클래스 기반 범용 시퀀스 실행 엔진**입니다.

```
외부 의존성 없음 (BCL only)
lssLib.Net / DB / HTTP 어떤 레이어에도 종속되지 않음
```

---

## 솔루션 구성

```
lssLib.Sequence/
│
├── Core/
│   ├── SequenceEnums.cs          StepExecutionMode · SequenceStepStatus · SequenceStatus
│   └── SequenceResults.cs        SequenceStepResult · SequenceResult · SequenceBatchResult
│
├── Contracts/                    ── 계약 (인터페이스)
│   ├── ISequenceStep.cs          ExecuteAsync(context, ct) → SequenceStepResult
│   ├── ISequenceContext.cs       GetDevice / SetVariable / GetVariable / Log
│   └── ISequenceExecutor.cs      RunAsync / RunAllAsync + ISequenceDefinition
│
├── Abstractions/                 ── 공통 구현 (추상 클래스)
│   ├── SequenceStepBase.cs       Delay + 재시도 공통, ExecuteCoreAsync 추상
│   ├── SequenceContextBase.cs    변수저장소(ConcurrentDictionary) + 로그 공통
│   ├── SequenceBase.cs           그룹 목록 + ISequenceDefinition 구현
│   └── SequenceControllerBase.cs 그룹 순차/병렬 실행 엔진, Before/AfterStep 훅
│
└── Builder/
    └── SequenceBuilderBase.cs    단일그룹 + 다중그룹 Fluent 빌더 추상 + DelayStep
```

---

## 상속 계층 구조

```
ISequenceStep
  └─► SequenceStepBase         공통: Delay 대기, MaxRetries 재시도, OnCompleted 콜백
        └─► (파생 구현체)       예) NetWriteStep / NetRequestStep / DbQueryStep / HttpCallStep

ISequenceContext
  └─► SequenceContextBase      공통: ConcurrentDictionary 변수저장소, 이벤트 훅
        └─► (파생 구현체)       예) NetSequenceContext / DbSequenceContext

ISequenceDefinition
ISequenceExecutor
  └─► SequenceControllerBase   공통: 그룹 순차/병렬 처리, 이벤트, Before/AfterStep 훅
        └─► (파생 구현체)       예) NetSequenceController / 커스텀Controller

SequenceBuilderBase<TStep,TBuilder>
  └─► (파생 빌더)              예) NetSequenceBuilder

GroupSequenceBuilderBase<TStep,TBuilder>
  └─► (파생 빌더)              예) NetGroupSequenceBuilder
```

---

## 도메인별 구현 확장 방법

### 1. 새 스텝 타입 추가

```csharp
// HTTP 호출 스텝
public sealed class HttpCallStep : SequenceStepBase
{
    public override string StepName { get; }
    public string Url    { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";

    public HttpCallStep(string name) => StepName = name;

    protected override async Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
    {
        using var http = new HttpClient();
        var resp = await http.GetAsync(Url, ct);
        if (!resp.IsSuccessStatusCode)
            return SequenceStepResult.Fail(this, $"HTTP {(int)resp.StatusCode}");
        var body = await resp.Content.ReadAsByteArrayAsync(ct);
        return SequenceStepResult.Ok(this, outputData: body);
    }
}

// DB 쿼리 스텝
public sealed class DbQueryStep : SequenceStepBase
{
    public override string StepName { get; }
    public string Sql { get; init; } = string.Empty;

    public DbQueryStep(string name) => StepName = name;

    protected override async Task<SequenceStepResult> ExecuteCoreAsync(
        ISequenceContext context, CancellationToken ct)
    {
        var conn = context.GetDevice(0) as DbConnection;
        if (conn is null) return SequenceStepResult.Fail(this, "DB 연결 없음");
        // ... 쿼리 실행
        return SequenceStepResult.Ok(this);
    }
}
```

### 2. 새 컨텍스트 구현

```csharp
public sealed class DbSequenceContext : SequenceContextBase
{
    private readonly Dictionary<int, DbConnection> _conns;

    public DbSequenceContext(Dictionary<int, DbConnection> conns)
        => _conns = conns;

    protected override object? GetDeviceCore(int deviceId)
        => _conns.TryGetValue(deviceId, out var c) ? c : null;

    protected override bool IsDeviceConnectedCore(int deviceId)
        => _conns.TryGetValue(deviceId, out var c) &&
           c.State == System.Data.ConnectionState.Open;

    // lssLib.Log 연동
    protected override void LogCore(string msg)
        => LogManager.Instance.Info("Sequence", msg);
}
```

### 3. 컨트롤러 훅 활용 (WPF 진행률)

```csharp
public class WpfSequenceController : SequenceControllerBase
{
    protected override Task OnBeforeStepAsync(
        ISequenceStep step, ISequenceContext ctx, CancellationToken ct)
    {
        // WPF Dispatcher 로 UI 업데이트
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            LblCurrentStep.Content = step.StepName;
            PbProgress.Value       = step.StepIndex;
        });
        return Task.CompletedTask;
    }
}
```

---

## 실행 흐름

```
RunAsync(sequence, context, ct)
  │
  ├─ foreach Group in sequence.Groups
  │    ├─ Sequential: 스텝 1개씩 순서대로
  │    │    └─ step.ExecuteAsync(context, ct)
  │    │         └─ Delay → ExecuteCoreAsync → 재시도 → OnCompleted
  │    │
  │    └─ Parallel: Task.WhenAll(모든 스텝)
  │         └─ 각 step.ExecuteAsync(context, ct) 동시 실행
  │
  └─ 결과 집계 → SequenceResult 반환
```

---

*lssLib.Sequence · .NET 8.0 · BCL only · Node-RED 스타일 확장 가능*
