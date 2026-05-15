# lssLib.Messaging

**이벤트/메시지 처리 라이브러리** · .NET 8.0 · C# 12 · BCL only

---

## 개요

임베디드·산업용·업무 앱에서 공통으로 사용할 수 있는  
**Pub/Sub 이벤트 버스 / 우선순위 커맨드 큐 / 비동기 스케줄러** 기능을  
lssLib 아키텍처 원칙에 따라 제공하는 이벤트·메시지 라이브러리입니다.

```
EventBus       — 타입 안전 발행·구독 (Action / Func<Task> / IEventHandler<T>)
CommandQueue   — 우선순위 기반 비동기 커맨드 큐 (PriorityQueue + SemaphoreSlim)
AsyncScheduler — 지연·반복·일별 작업 스케줄러 (독립 Task 루프)
```

---

## 솔루션 구성

```
lssLib.Messaging/
│
├── lssLib.Messaging.csproj
│
├── EventBus/
│   ├── EventMessage.cs          이벤트 메시지 기반 추상 레코드
│   ├── IEventHandler.cs         클래스 기반 핸들러 인터페이스
│   └── EventBus.cs              타입 안전 Pub/Sub 싱글톤
│
├── CommandQueue/
│   ├── ICommand.cs              ICommand / CommandBase / LambdaCommand / CommandPriority
│   ├── CommandResult.cs         실행 결과 readonly record struct
│   └── CommandQueue.cs          우선순위 비동기 큐 싱글톤
│
└── Scheduler/
    ├── ScheduleOptions.cs       스케줄 설정 (Once / Recurring / DailyAt 팩토리)
    ├── ScheduledTask.cs         작업 상태 및 Pause/Resume/Cancel 핸들
    └── AsyncScheduler.cs        비동기 스케줄러 싱글톤
```

---

## 개발 환경

| 항목 | 버전 |
|---|---|
| .NET | 8.0-windows |
| C# | 12 (latest) |
| 외부 패키지 | 없음 (BCL only) |
| 의존 모듈 | `lssLib.Log` (로그 기록) |

---

## lssLib 생태계 내 위치

```
lssLib.Binary ──► lssLib.Extensions ──► lssLib.Utils ──► lssLib.Retry
                                                              │
                                                              ▼
                                                    lssLib.Messaging      ← 이 모듈
                                                    (EventBus
                                                     CommandQueue
                                                     AsyncScheduler)
                                                              │
                                                              ▼
                                                    lssLib.XXX.WpfDemo
```

`lssLib.Messaging`은 `lssLib.Log`만 참조합니다.  
Binary / Extensions / Utils / Retry 와 독립적으로 참조 가능하며,  
각 모듈과 파이프라인에서 자유롭게 조합해 사용합니다.

---

## 빠른 시작

### EventBus

```csharp
using lssLib.Messaging;

// 1. 이벤트 메시지 선언
public record SensorDataEvent(int DeviceId, float Temperature) : EventMessage;

// 2. 구독
var sub = EventBus.Instance.Subscribe<SensorDataEvent>(e =>
    Console.WriteLine($"[{e.Timestamp:HH:mm:ss}] 온도: {e.Temperature}°C"));

// 3. 비동기 구독
var sub2 = EventBus.Instance.Subscribe<SensorDataEvent>(async e =>
{
    await SaveToDbAsync(e);
});

// 4. IEventHandler 구독
var sub3 = EventBus.Instance.Subscribe<SensorDataEvent>(new SensorAlertHandler());

// 5. 발행
EventBus.Instance.Publish(new SensorDataEvent(DeviceId: 1, Temperature: 42.5f));

// 6. 비동기 발행 (핸들러 전체 완료 대기)
await EventBus.Instance.PublishAsync(new SensorDataEvent(1, 85f));

// 7. 구독 해제
sub.Dispose();
```

### CommandQueue

```csharp
// 1. 시작
CommandQueue.Instance.Start();

// 2. 클래스 커맨드
public class SaveFrameCommand : CommandBase
{
    private readonly byte[] _frame;
    private readonly string _path;

    public override CommandPriority Priority => CommandPriority.High;

    public SaveFrameCommand(byte[] frame, string path) { _frame = frame; _path = path; }

    public override async Task ExecuteAsync(CancellationToken ct)
        => await File.WriteAllBytesAsync(_path, _frame, ct);
}

CommandQueue.Instance.Enqueue(new SaveFrameCommand(frame, "out/snap.bin"));

// 3. 람다 인라인 커맨드
CommandQueue.Instance.Enqueue(LambdaCommand.Create(async ct =>
{
    await Task.Delay(500, ct);
    Console.WriteLine("백그라운드 작업 완료");
}, CommandPriority.Low));

// 4. 완료 이벤트
CommandQueue.Instance.CommandCompleted += r => Console.WriteLine(r.ToString());

// 5. 앱 종료 시
await CommandQueue.Instance.StopAsync();
```

### AsyncScheduler

```csharp
// 1. 5초마다 반복
var poll = AsyncScheduler.Instance.ScheduleRecurring(
    TimeSpan.FromSeconds(5),
    async ct =>
    {
        var data = await sensor.ReadAsync(ct);
        LogManager.Instance.Info("Sensor", $"온도: {data.Temp}°C");
    },
    name: "SensorPoll");

// 2. 3초 뒤 1회
AsyncScheduler.Instance.ScheduleOnce(TimeSpan.FromSeconds(3), async ct =>
{
    await InitializeAsync(ct);
}, "DeviceInit");

// 3. 매일 오전 2시
AsyncScheduler.Instance.ScheduleDailyAt(TimeSpan.FromHours(2), async ct =>
{
    await CleanupOldLogsAsync(ct);
}, "NightlyCleanup");

// 4. 세밀 설정
AsyncScheduler.Instance.Schedule(async ct =>
{
    await CheckNetworkAsync(ct);
}, new ScheduleOptions
{
    Name            = "NetworkCheck",
    InitialDelay    = TimeSpan.FromSeconds(5),
    Interval        = TimeSpan.FromSeconds(30),
    MaxRuns         = 100,
    ContinueOnError = true,
    Category        = "Network"
});

// 5. 일시 정지 / 재개 / 취소
poll.Pause();
poll.Resume();
poll.Cancel();

// 6. 앱 종료 시
await AsyncScheduler.Instance.StopAsync();
```

---

## 컴포넌트 분류 기준

### 핵심 질문 세 가지

```
"지금 하려는 것이..."

발생한 사실을 알려주는 것인가?      → EventBus
작업을 실행시키는 것인가?           → CommandQueue
주기적·시각적으로 반복하는 것인가?  → AsyncScheduler
```

---

### EventBus — "이런 일이 일어났다"

**선택 기준**

```
✅ 발행자가 수신자를 몰라도 된다
✅ 수신자가 0명이어도 괜찮다
✅ 같은 이벤트를 여러 곳에서 동시에 받아야 한다
✅ 컴포넌트 간 결합을 끊고 싶다
```

**실제 예시**

```
센서 데이터 수신됨 → UI도 갱신, DB도 저장, 알람도 체크 (3곳이 독립 수신)
네트워크 연결 끊김 → 스케줄러 일시 정지, 로그 기록, 화면 표시 (각자 처리)
사용자가 로그인함  → 메뉴 갱신, 세션 시작, 로그 기록
```

**이것이라면 EventBus가 아니다**

```
❌ "이 작업이 반드시 완료되어야 한다"      → CommandQueue
❌ "실행 결과(성공/실패)를 알아야 한다"   → CommandQueue
❌ "5초마다 자동으로 실행해야 한다"        → AsyncScheduler
```

---

### CommandQueue — "이것을 실행해라"

**선택 기준**

```
✅ 작업이 반드시 실행되어야 한다
✅ 실행 순서 또는 우선순위가 중요하다
✅ 실행 결과(성공/실패, 소요 시간)를 알아야 한다
✅ 같은 작업이 동시에 여러 번 실행되면 안 된다
✅ 우선순위가 다른 작업들이 섞인다
```

**실제 예시**

```
버튼 클릭 → DB 저장     (여러 번 클릭해도 순서대로 처리)
CRC 불일치 → 재수신 요청 (Critical — 다른 작업보다 먼저)
프레임 수신 → 파싱 처리  (Normal — 순서 보장)
세션 종료  → 파일 저장   (Low — 나중에 해도 됨)
```

**이것이라면 CommandQueue가 아니다**

```
❌ "완료를 기다릴 필요 없이 알리기만 하면 된다" → EventBus
❌ "매 5초마다 자동으로 확인한다"               → AsyncScheduler
```

---

### AsyncScheduler — "언제 자동으로 실행해라"

**선택 기준**

```
✅ 시간이 트리거다 (주기, 지연, 특정 시각)
✅ 사람이 시작하지 않아도 자동 실행된다
✅ 반복 실행, 일시 정지, 재개가 필요하다
✅ 여러 반복 작업을 독립적으로 관리해야 한다
```

**실제 예시**

```
5초마다      → 센서 폴링
30초마다     → 하트비트 체크
매일 오전 2시 → 로그 파일 정리
앱 시작 3초 뒤 → 디바이스 초기화
10회 후 종료  → 초기 연결 재시도
```

**이것이라면 AsyncScheduler가 아니다**

```
❌ "버튼 클릭처럼 외부 이벤트가 트리거다"  → EventBus 또는 CommandQueue
❌ "1회만 즉시 실행한다"                  → 그냥 Task.Run 또는 LambdaCommand
```

---

### 한눈에 비교

| | EventBus | CommandQueue | AsyncScheduler |
|---|---|---|---|
| **트리거** | 이벤트 발생 | 코드가 명시적으로 Enqueue | 시간 (주기·지연·시각) |
| **수신자** | 0~N명 (불특정) | Worker 1개 | 독립 Task 루프 |
| **실행 순서** | 병렬 (PublishAsync) | 우선순위 순서 보장 | 작업별 독립 |
| **결과 추적** | 없음 | `CommandCompleted` 이벤트 | `RunCount`, `LastError` |
| **취소 단위** | 구독 해제 (`Dispose`) | `Clear()` | `task.Cancel()` |
| **주요 관심사** | 결합 제거 | 순서·우선순위 | 시간 관리 |

---

### 조합 패턴 — 세 개는 함께 쓰인다

실제로는 단독 사용보다 조합이 더 많습니다.

```
AsyncScheduler           →  "5초마다 센서 읽기"
    └─ EventBus.Publish  →  "센서 데이터 수신됨" 알림
           ├─ UI 핸들러              →  화면 갱신 (즉시)
           └─ CommandQueue.Enqueue  →  "DB 저장" 명령 (순서 보장)
```

세 컴포넌트를 혼동하지 않으려면 **"트리거가 시간인가, 코드인가, 외부 사건인가"** 를 먼저 확인합니다.  
**스케줄러**는 "언제"를, **이벤트버스**는 "누구에게 알릴지"를, **커맨드큐**는 "어떤 순서로 처리할지"를 담당합니다.

---

## 설계 원칙

| 원칙 | 내용 |
|---|---|
| **싱글톤** | `Lazy<T>` 기반 thread-safe 싱글톤 |
| **No external dependencies** | BCL + `lssLib.Log`만 참조 |
| **타입 안전** | EventBus 채널 = 메시지 타입. 문자열 토픽 없음 |
| **우선순위 큐** | `PriorityQueue<ICommand, int>` + `SemaphoreSlim` 조합 |
| **독립 루프** | 스케줄 작업마다 독립 Task → 상호 영향 없음 |
| **IDisposable 구독 해제** | Subscribe 반환값 Dispose → 자동 해제 |
| **LogManager 연동** | 발행·실행·예외 모두 `lssLib.Log`에 기록 |

---

## 주의 사항

| 항목 | 내용 |
|---|---|
| `EventBus.Publish` (동기) | UI 스레드 호출 시 비동기 핸들러 블로킹 위험 → `PublishAsync` 권장 |
| `LogAdded` / `CommandCompleted` | 백그라운드 스레드 → UI 접근 시 `Dispatcher.InvokeAsync` 필요 |
| `CommandQueue.Start()` 필수 | Enqueue 전에 반드시 Start() 호출 |
| `StopAsync()` 필수 | 앱 종료 시 미호출 시 실행 중 작업 강제 종료 |
| 구독 해제 | `UserControl.Unloaded` / `Dispose` 시점에 반드시 `sub.Dispose()` |

---

*lssLib.Messaging · .NET 8.0 · BCL only · lssLib v5 아키텍처*
