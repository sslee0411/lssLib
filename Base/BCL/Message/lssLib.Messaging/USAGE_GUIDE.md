# lssLib.Messaging — 사용 가이드

> **이 문서의 목적**: 각 컴포넌트가 *왜* 필요한지, *어떤 상황*에서 쓰는지,  
> *어떻게* 조합하는지를 단계별로 설명합니다.

---

## 목차

1. [모듈 개요 — 세 가지 문제를 해결한다](#1-모듈-개요)
2. [EventBus — 발행·구독 패턴](#2-eventbus)
3. [CommandQueue — 우선순위 작업 큐](#3-commandqueue)
4. [AsyncScheduler — 비동기 스케줄러](#4-asyncscheduler)
5. [세 컴포넌트 조합 — 실전 파이프라인](#5-세-컴포넌트-조합)
6. [WPF 앱 연동 패턴](#6-wpf-앱-연동-패턴)
7. [lssLib 생태계 연동](#7-lsslib-생태계-연동)
8. [자주 묻는 질문 (FAQ)](#8-faq)

---

## 1. 모듈 개요

### "왜 이 모듈이 필요한가?"

임베디드·산업 앱을 만들다 보면 세 가지 반복 문제가 나타납니다.

```
문제 1 — 컴포넌트 간 직접 호출
  SerialService → WeatherService.Update()
  SerialService → UIService.Refresh()
  SerialService → DatabaseService.Save()
  → SerialService가 모든 서비스를 알아야 함 (강한 결합)

문제 2 — 작업 실행 순서/우선순위 관리
  "비상 정지"와 "통계 저장"이 동시에 들어오면?
  "긴급 알람"과 "로그 파일 정리"의 실행 순서는?
  → 우선순위 없는 큐는 늦게 들어온 긴급 작업이 대기해야 함

문제 3 — 반복 작업 관리
  5초마다 센서 폴링, 매일 새벽 2시 로그 정리, 앱 시작 3초 후 초기화...
  → Thread.Sleep 루프, Timer 관리, 취소 처리 코드가 흩어짐
```

`lssLib.Messaging`은 이 세 문제를 각각 해결합니다.

```
EventBus      → 문제 1 해결: 컴포넌트 간 직접 의존성 제거
CommandQueue  → 문제 2 해결: 우선순위 기반 순차 실행 보장
AsyncScheduler→ 문제 3 해결: 반복·지연·일별 작업 일관 관리
```

### 세 컴포넌트 관계

```
┌──────────────────────────────────────────────────────────────┐
│                     lssLib.Messaging                         │
│                                                              │
│  [EventBus]          [CommandQueue]      [AsyncScheduler]    │
│  발행 → 구독         우선순위 큐         주기·일별 반복       │
│  느슨한 결합         순차 실행 보장      독립 Task 루프       │
│                                                              │
│  ← 모두 싱글톤, lssLib.Log 자동 연동 →                      │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. EventBus

### 2.1 개념 이해

**이벤트 버스는 "방송국"입니다.**

```
기존 방식 (직접 호출):
  SensorService.OnDataReceived → UIService.Update()   ← 직접 호출
                               → DbService.Save()     ← 직접 호출
                               → AlertService.Check() ← 직접 호출
  문제: SensorService가 UI/DB/Alert 모두 알아야 함

EventBus 방식 (간접 호출):
  SensorService  →  EventBus.Publish(new SensorDataEvent(...))
                          │
                 ┌────────┼────────┐
                 ▼        ▼        ▼
              UIService DbService AlertService
              (각자 Subscribe해서 독립적으로 처리)
  
  SensorService는 "이벤트를 발행했다"는 사실만 알면 됨
```

### 2.2 이벤트 메시지 선언

모든 이벤트 메시지는 `EventMessage`를 상속한 `record`로 선언합니다.

```csharp
// ── 센서 데이터 이벤트
public record SensorDataEvent(
    int   DeviceId,
    float Temperature,
    float Humidity) : EventMessage;

// ── 네트워크 상태 이벤트
public record NetworkStatusEvent(
    bool   IsConnected,
    string Host,
    string Reason = "") : EventMessage;

// ── 프레임 수신 이벤트 (lssLib.Binary 연동)
public record FrameReceivedEvent(
    byte[] RawFrame,
    int    DeviceId,
    uint   Crc) : EventMessage;

// ── 에러 이벤트
public record ErrorOccurredEvent(
    string Source,
    string Message,
    Exception? Exception = null) : EventMessage;
```

> **왜 record인가?**  
> `record`는 불변(immutable) + 구조적 동등성 + 분해(deconstruct)가 자동으로 제공됩니다.  
> 이벤트 메시지는 "발행된 순간의 스냅샷"이므로 불변이어야 안전합니다.

`EventMessage` 기반 클래스가 자동으로 부여하는 값:

```csharp
var evt = new SensorDataEvent(1, 42.5f, 65f);
Console.WriteLine(evt.MessageId);  // "A3F2B1C0"  — 8자리 고유 ID (추적용)
Console.WriteLine(evt.Timestamp);  // 2025-04-01 14:30:25.123  — 생성 시각
```

### 2.3 구독 방법 세 가지

```csharp
// ─────────────────────────────────────────────────
// 방법 1 — 동기 람다 (가장 간단)
// ─────────────────────────────────────────────────
var sub1 = EventBus.Instance.Subscribe<SensorDataEvent>(e =>
{
    Console.WriteLine($"[{e.Timestamp:HH:mm:ss}] 장치 {e.DeviceId}: {e.Temperature:F1}°C");
});


// ─────────────────────────────────────────────────
// 방법 2 — 비동기 람다 (DB 저장, HTTP 요청 등)
// ─────────────────────────────────────────────────
var sub2 = EventBus.Instance.Subscribe<SensorDataEvent>(async e =>
{
    await database.InsertAsync(new SensorRecord
    {
        DeviceId    = e.DeviceId,
        Temperature = e.Temperature,
        RecordedAt  = e.Timestamp
    });
});


// ─────────────────────────────────────────────────
// 방법 3 — IEventHandler 클래스 (복잡한 로직, 재사용)
// ─────────────────────────────────────────────────
public class OverheatAlertHandler : IEventHandler<SensorDataEvent>
{
    private const float DANGER_TEMP = 80f;

    public async Task HandleAsync(SensorDataEvent e, CancellationToken ct)
    {
        if (e.Temperature >= DANGER_TEMP)
        {
            await alertService.SendAsync(
                $"⚠️ 장치 {e.DeviceId} 과열 감지: {e.Temperature:F1}°C", ct);

            LogManager.Instance.Fatal("Alert",
                $"장치 {e.DeviceId} 과열 — {e.Temperature:F1}°C ≥ {DANGER_TEMP}°C");
        }
    }
}

var sub3 = EventBus.Instance.Subscribe<SensorDataEvent>(new OverheatAlertHandler());
```

### 2.4 발행 방법 두 가지

```csharp
// ─────────────────────────────────────────────────
// 동기 발행 — 핸들러를 순서대로 블로킹 실행
// ─────────────────────────────────────────────────
// ※ 백그라운드 스레드에서 사용. UI 스레드에서는 데드락 위험.
EventBus.Instance.Publish(new SensorDataEvent(1, 42.5f, 65f));


// ─────────────────────────────────────────────────
// 비동기 발행 — 핸들러를 병렬 실행, 전체 완료 대기
// ─────────────────────────────────────────────────
// ※ UI 스레드·async 메서드에서 권장
await EventBus.Instance.PublishAsync(new SensorDataEvent(1, 85f, 70f));
//   └─ 모든 핸들러(sub1, sub2, sub3)가 병렬로 실행됨
//      한 핸들러의 예외가 다른 핸들러에 영향을 주지 않음
```

### 2.5 구독 해제 — 반드시 해제하세요

```csharp
// ─────────────────────────────────────────────────
// WPF UserControl 패턴 — Loaded/Unloaded 쌍으로 관리
// ─────────────────────────────────────────────────
public partial class SensorPanel : UserControl
{
    private IDisposable? _sub;

    public SensorPanel()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _sub = EventBus.Instance.Subscribe<SensorDataEvent>(OnSensorData);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _sub?.Dispose();   // 구독 해제 — 메모리 누수 방지
    }

    private void OnSensorData(SensorDataEvent e)
    {
        // 백그라운드 스레드 → UI 접근 시 Dispatcher 필요
        Dispatcher.InvokeAsync(() =>
        {
            TxtTemp.Text = $"{e.Temperature:F1}°C";
        });
    }
}
```

### 2.6 구독자 수 확인 및 전체 해제

```csharp
// 현재 SensorDataEvent 구독자 수 확인
int count = EventBus.Instance.GetSubscriberCount<SensorDataEvent>();
Console.WriteLine($"구독자: {count}명");

// 특정 타입 전체 해제
EventBus.Instance.UnsubscribeAll<SensorDataEvent>();

// 모든 구독 초기화 (앱 종료 시)
EventBus.Instance.Clear();
```

---

## 3. CommandQueue

### 3.1 개념 이해

**커맨드 큐는 "작업 접수창구 + 우선순위 처리 직원"입니다.**

```
기존 방식 (직접 실행):
  버튼 클릭 → Task.Run(() => HeavyWork())    ← 여러 번 클릭 시 동시 실행
  시리얼 수신 → Task.Run(() => ParseFrame()) ← 프레임 순서 보장 안 됨
  에러 발생 → Task.Run(() => SaveDump())     ← 우선순위 없음

CommandQueue 방식:
  버튼 클릭 → CommandQueue.Enqueue(new SaveCommand())
  시리얼 수신 → CommandQueue.Enqueue(new ParseFrameCommand())
  에러 발생 → CommandQueue.Enqueue(new SaveDumpCommand() { Priority = Critical })
                     │
              [CommandQueue 내부]
              ┌──────────────────────┐
              │ Critical: SaveDump   │ ← 가장 먼저 처리
              │ Normal:  ParseFrame  │
              │ Normal:  SaveData    │
              │ Low:     WriteLog    │ ← 나중에 처리
              └──────────────────────┘
                     │ Worker Task가 순서대로 꺼내서 실행
                     ▼
              단일 소비자 → 실행 순서 보장, 동시 충돌 없음
```

### 3.2 커맨드 선언 방법

#### 방법 A — 클래스 커맨드 (재사용, 상태 포함)

```csharp
// ── 기본 커맨드
public class ParseFrameCommand : CommandBase
{
    private readonly byte[] _frame;

    public ParseFrameCommand(byte[] frame) => _frame = frame;

    // Priority 재정의 (기본값: Normal)
    public override CommandPriority Priority => CommandPriority.High;

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        // lssLib.Binary 파싱 로직
        var parser = _frame.ToParser();
        var result = parser.Parse(SensorSchema.Default);

        if (!result.IsAllOk)
            throw new InvalidDataException($"파싱 실패: {string.Join(", ", result.ErrorFields)}");

        var temperature = result.GetFloat("Temp");

        // 파싱 결과를 EventBus로 발행 → 다른 컴포넌트에 알림
        await EventBus.Instance.PublishAsync(
            new SensorDataEvent(DeviceId: 1, Temperature: temperature, Humidity: 0f), ct);
    }
}


// ── 비상 정지 커맨드 (최우선)
public class EmergencyStopCommand : CommandBase
{
    public override CommandPriority Priority => CommandPriority.Critical;

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        await device.SendStopSignalAsync(ct);
        LogManager.Instance.Fatal("Device", "비상 정지 신호 전송 완료");

        await EventBus.Instance.PublishAsync(
            new NetworkStatusEvent(IsConnected: false, Host: "Device", Reason: "EmergencyStop"), ct);
    }
}


// ── 파일 저장 커맨드
public class SaveSnapshotCommand : CommandBase
{
    private readonly byte[] _data;
    private readonly string _path;

    // 우선순위 낮음 — 나중에 처리해도 됨
    public override CommandPriority Priority => CommandPriority.Low;

    public SaveSnapshotCommand(byte[] data, string path)
    {
        _data = data;
        _path = path;
    }

    public override async Task ExecuteAsync(CancellationToken ct)
        => await File.WriteAllBytesAsync(_path, _data, ct);
}
```

#### 방법 B — 람다 커맨드 (간단한 1회성 작업)

```csharp
// 비동기 람다
CommandQueue.Instance.Enqueue(LambdaCommand.Create(async ct =>
{
    await Task.Delay(500, ct);
    LogManager.Instance.Info("Task", "백그라운드 초기화 완료");
}));

// 동기 람다 + 우선순위 지정
CommandQueue.Instance.Enqueue(LambdaCommand.Create(
    () => Console.WriteLine("UI 갱신"),
    CommandPriority.High));

// 긴급 람다
CommandQueue.Instance.Enqueue(LambdaCommand.Create(
    async ct => await SendAlertAsync(ct),
    CommandPriority.Critical));
```

### 3.3 우선순위 가이드

```
CommandPriority.Critical (3) — 비상 정지, 알람, 즉각 처리 필수
CommandPriority.High     (2) — 사용자 입력 응답, 즉시 피드백
CommandPriority.Normal   (1) — 일반 데이터 처리, 파싱 (기본값)
CommandPriority.Low      (0) — 로그 저장, 통계, 정리 작업

실행 순서 예시:
  입력 순서: Low → Normal → Critical → High
  실행 순서: Critical → High → Normal → Low
```

### 3.4 큐 시작과 종료

```csharp
// App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    // LogManager 먼저 시작
    LogManager.Instance.Start(new LogConfig { ValidDays = 30 });

    // CommandQueue 시작 (기본: 단일 소비자)
    CommandQueue.Instance.Start();

    // 병렬 처리가 필요하면 (순서 보장 불필요한 경우에만)
    // CommandQueue.Instance.MaxConcurrency = 2;
    // CommandQueue.Instance.Start();
}

// WPF 종료 시
private async void Window_Closing(object sender, CancelEventArgs e)
{
    e.Cancel = true;  // 창 닫기 잠시 중단

    await CommandQueue.Instance.StopAsync();
    await LogManager.Instance.StopAsync();

    Application.Current.Shutdown();
}
```

### 3.5 완료 이벤트 활용

```csharp
// 커맨드 실행 결과를 UI에 표시
CommandQueue.Instance.CommandCompleted += result =>
{
    // 백그라운드 스레드 → Dispatcher 필요
    Dispatcher.InvokeAsync(() =>
    {
        if (result.IsSuccess)
        {
            TxtStatus.Text = $"✅ {result.CommandType} 완료 ({result.ElapsedMs}ms)";
        }
        else
        {
            TxtStatus.Text = $"❌ {result.CommandType} 실패: {result.Error?.Message}";
            LogManager.Instance.Error("UI", result.ToString());
        }
    });
};
```

### 3.6 큐 상태 모니터링

```csharp
// 대기 중인 커맨드 수
Console.WriteLine($"대기: {CommandQueue.Instance.PendingCount}");

// 누적 처리/실패 수
Console.WriteLine($"처리: {CommandQueue.Instance.ProcessedCount}");
Console.WriteLine($"실패: {CommandQueue.Instance.FailedCount}");

// 큐 비우기 (긴급 상황)
CommandQueue.Instance.Clear();
```

---

## 4. AsyncScheduler

### 4.1 개념 이해

**스케줄러는 "자동 알람 시계 + 반복 타이머 모음"입니다.**

```
기존 방식 (Timer / Thread.Sleep):
  var timer = new System.Timers.Timer(5000);
  timer.Elapsed += OnTimerElapsed;
  timer.Start();
  // 문제: 취소 처리, 일시 정지, 예외 처리, 앱 종료 시 정리가 모두 수동

AsyncScheduler 방식:
  var task = AsyncScheduler.Instance.ScheduleRecurring(
      TimeSpan.FromSeconds(5),
      async ct => await PollSensorAsync(ct),
      "SensorPoll");
  
  task.Pause();   // 일시 정지
  task.Resume();  // 재개
  task.Cancel();  // 종료
  // 앱 종료: await AsyncScheduler.Instance.StopAsync() — 한 번에 전체 정리
```

### 4.2 작업 등록 방법

#### 방법 A — 편의 메서드 (권장)

```csharp
// ─────────────────────────────────────────────────
// 즉시 시작, 5초마다 반복 (무한)
// ─────────────────────────────────────────────────
var sensorPoll = AsyncScheduler.Instance.ScheduleRecurring(
    interval: TimeSpan.FromSeconds(5),
    action:   async ct =>
    {
        var data = await sensor.ReadAsync(ct);
        await EventBus.Instance.PublishAsync(
            new SensorDataEvent(1, data.Temp, data.Humidity), ct);
    },
    name: "SensorPoll");


// ─────────────────────────────────────────────────
// 3초 뒤 1회 실행 (초기화 지연)
// ─────────────────────────────────────────────────
AsyncScheduler.Instance.ScheduleOnce(
    delay:  TimeSpan.FromSeconds(3),
    action: async ct =>
    {
        await device.InitializeAsync(ct);
        LogManager.Instance.Info("Device", "초기화 완료");
    },
    name: "DeviceInit");


// ─────────────────────────────────────────────────
// 매일 오전 2시 자동 정리
// ─────────────────────────────────────────────────
AsyncScheduler.Instance.ScheduleDailyAt(
    timeOfDay: TimeSpan.FromHours(2),
    action:    async ct =>
    {
        // 7일 이상 된 로그 파일 삭제
        await CleanupOldLogsAsync(ct);
        LogManager.Instance.Info("Maintenance", "야간 정리 완료");
    },
    name: "NightlyCleanup");
```

#### 방법 B — ScheduleOptions 세밀 설정

```csharp
// 시작 5초 뒤부터 10초마다, 최대 100회, 오류 발생 시 계속
AsyncScheduler.Instance.Schedule(async ct =>
{
    await CheckNetworkConnectionAsync(ct);

}, new ScheduleOptions
{
    Name            = "NetworkHealthCheck",
    InitialDelay    = TimeSpan.FromSeconds(5),    // 5초 뒤 첫 실행
    Interval        = TimeSpan.FromSeconds(10),   // 10초마다
    MaxRuns         = 100,                         // 100회 후 자동 종료
    ContinueOnError = true,                        // 예외 발생해도 계속
    Category        = "Network"                    // 로그 카테고리
});
```

### 4.3 작업 제어

```csharp
var task = AsyncScheduler.Instance.ScheduleRecurring(
    TimeSpan.FromSeconds(5),
    async ct => await PollAsync(ct),
    "SensorPoll");

// 일시 정지 (현재 실행 중인 반복이 끝난 후 다음 실행 건너뜀)
task.Pause();
Console.WriteLine($"정지됨: {task.IsPaused}");   // true

// 재개
task.Resume();
Console.WriteLine($"활성: {task.IsActive}");     // true

// 완전 종료 (재개 불가)
task.Cancel();
Console.WriteLine($"취소됨: {task.IsCancelled}"); // true

// 상태 조회
Console.WriteLine(task.ToString());
// [A3F2B1C0] SensorPoll  runs=42  paused=False  cancelled=False  next=14:30:45
```

### 4.4 전체 작업 관리

```csharp
// 모든 작업 일시 정지 (예: 디바이스 연결 끊김)
AsyncScheduler.Instance.PauseAll();
LogManager.Instance.Warn("Scheduler", "전체 작업 일시 정지");

// 재개 (연결 복구 후)
AsyncScheduler.Instance.ResumeAll();

// 등록된 모든 작업 목록 출력
foreach (var t in AsyncScheduler.Instance.GetTasks())
    Console.WriteLine(t.ToString());

// 특정 작업 ID로 취소
AsyncScheduler.Instance.Cancel(taskId: "A3F2B1C0");

// 앱 종료 시 (5초 타임아웃으로 전체 정리)
await AsyncScheduler.Instance.StopAsync(TimeSpan.FromSeconds(5));
```

---

## 5. 세 컴포넌트 조합

### 5.1 조합의 핵심 아이디어

```
┌─────────────────────────────────────────────────────────────┐
│                   전형적인 조합 패턴                          │
│                                                             │
│  AsyncScheduler — "주기적으로 데이터 수집"                   │
│         │                                                   │
│         │ (데이터 수집 완료 시)                               │
│         ▼                                                   │
│    EventBus.Publish — "누군가 이 데이터를 처리해"             │
│         │                                                   │
│         │ (무거운 처리 작업은)                                │
│         ▼                                                   │
│  CommandQueue.Enqueue — "우선순위대로 처리해"                 │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 실전 예제 — 산업 센서 수집 시스템

```csharp
// ══════════════════════════════════════════════════════════════
//  시나리오: 시리얼 포트로 센서 데이터를 수신하고
//           정상 데이터는 DB에 저장, 이상 데이터는 즉시 알람
//           오래된 파일은 매일 새벽 자동 정리
// ══════════════════════════════════════════════════════════════

// ── Step 1: 이벤트 메시지 선언 ────────────────────────────────
public record SensorDataEvent(int DeviceId, float Temp, float Hum) : EventMessage;
public record SensorAlarmEvent(int DeviceId, string Reason) : EventMessage;

// ── Step 2: 커맨드 선언 ───────────────────────────────────────
public class SaveSensorDataCommand : CommandBase
{
    private readonly SensorDataEvent _data;
    public override CommandPriority Priority => CommandPriority.Normal;

    public SaveSensorDataCommand(SensorDataEvent data) => _data = data;

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        await database.InsertAsync(new SensorRecord
        {
            DeviceId = _data.DeviceId,
            Temp     = _data.Temp,
            Hum      = _data.Hum,
            At       = _data.Timestamp
        }, ct);
    }
}

public class SendAlarmCommand : CommandBase
{
    private readonly SensorAlarmEvent _alarm;
    public override CommandPriority Priority => CommandPriority.Critical; // 최우선

    public SendAlarmCommand(SensorAlarmEvent alarm) => _alarm = alarm;

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        await smsService.SendAsync($"[긴급] 장치 {_alarm.DeviceId}: {_alarm.Reason}", ct);
        await emailService.SendAsync("admin@company.com",
            $"센서 알람 — {_alarm.Reason}", ct);
    }
}

// ── Step 3: 애플리케이션 조립 ─────────────────────────────────
public class SensorApp
{
    public void Start()
    {
        // EventBus 구독 설정
        // "SensorDataEvent 수신 → DB 저장 커맨드를 큐에 넣는다"
        EventBus.Instance.Subscribe<SensorDataEvent>(e =>
            CommandQueue.Instance.Enqueue(new SaveSensorDataCommand(e)));

        // "SensorAlarmEvent 수신 → 알람 커맨드를 큐에 넣는다 (Critical 우선순위)"
        EventBus.Instance.Subscribe<SensorAlarmEvent>(e =>
            CommandQueue.Instance.Enqueue(new SendAlarmCommand(e)));

        // CommandQueue 시작
        CommandQueue.Instance.Start();

        // 5초마다 센서 폴링 → 이벤트 발행
        AsyncScheduler.Instance.ScheduleRecurring(
            interval: TimeSpan.FromSeconds(5),
            action:   async ct =>
            {
                var frame = await serialPort.ReadFrameAsync(ct);

                // lssLib.Binary 파싱
                var result = frame.ToParser().Parse(SensorSchema.Default);
                var temp   = result.GetFloat("Temp");
                var hum    = result.GetFloat("Humidity");
                var id     = result.GetInt("DeviceId");

                // 이벤트 발행 → 구독자들이 각자 처리
                await EventBus.Instance.PublishAsync(
                    new SensorDataEvent(id, temp, hum), ct);

                // 이상 온도 감지 → 알람 이벤트 발행
                if (temp > 85f)
                    await EventBus.Instance.PublishAsync(
                        new SensorAlarmEvent(id, $"과열 {temp:F1}°C"), ct);
            },
            name: "SensorPoll");

        // 매일 새벽 3시 파일 정리
        AsyncScheduler.Instance.ScheduleDailyAt(
            timeOfDay: TimeSpan.FromHours(3),
            action:    async ct =>
            {
                await CleanupOldDataFilesAsync(ct);
                LogManager.Instance.Info("Maintenance", "정기 정리 완료");
            },
            name: "NightlyCleanup");
    }

    public async Task StopAsync()
    {
        await AsyncScheduler.Instance.StopAsync();
        await CommandQueue.Instance.StopAsync();
    }
}
```

### 5.3 실전 예제 — 연결 복구 자동화

```csharp
// ── 연결 끊김 이벤트 선언
public record DeviceDisconnectedEvent(int DeviceId, string Reason) : EventMessage;
public record DeviceReconnectedEvent(int DeviceId) : EventMessage;

// ── 재연결 커맨드
public class ReconnectCommand : CommandBase
{
    private readonly int _deviceId;
    public override CommandPriority Priority => CommandPriority.High;

    public ReconnectCommand(int deviceId) => _deviceId = deviceId;

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                await device.ConnectAsync(ct);
                LogManager.Instance.Info("Device", $"장치 {_deviceId} 재연결 성공 ({attempt}회 시도)");

                // 재연결 성공 → 스케줄러 재개 → 이벤트 발행
                AsyncScheduler.Instance.ResumeAll();
                await EventBus.Instance.PublishAsync(
                    new DeviceReconnectedEvent(_deviceId), ct);
                return;
            }
            catch
            {
                LogManager.Instance.Warn("Device",
                    $"장치 {_deviceId} 재연결 실패 ({attempt}/5)");
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct); // 지수 대기
            }
        }

        LogManager.Instance.Fatal("Device", $"장치 {_deviceId} 재연결 최종 실패");
    }
}

// ── 연결 끊김 감지 시 자동 대응
EventBus.Instance.Subscribe<DeviceDisconnectedEvent>(e =>
{
    // 1. 스케줄러 일시 정지 (데이터 없으니 폴링 중단)
    AsyncScheduler.Instance.PauseAll();

    // 2. 재연결 커맨드를 큐에 등록 (High 우선순위)
    CommandQueue.Instance.Enqueue(new ReconnectCommand(e.DeviceId));
});
```

---

## 6. WPF 앱 연동 패턴

### 6.1 전체 초기화 순서 (App.xaml.cs)

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① LogManager 가장 먼저 (다른 모듈이 Log를 쓰므로)
        LogManager.Instance.Start(new LogConfig
        {
            LogRootPath  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log"),
            ValidDays    = 30,
            FileFormat   = LogFileFormat.Both,
            MinimumLevel = LogLevel.Info
        });

        // ② CommandQueue 시작
        CommandQueue.Instance.Start();

        // ③ AsyncScheduler는 별도 시작 불필요 (Schedule 호출 시 즉시 시작)

        LogManager.Instance.Info("App", "애플리케이션 시작");
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.Instance.Info("App", "애플리케이션 종료 중...");

        // ① 스케줄러 먼저 정지 (새 이벤트 발행 중단)
        await AsyncScheduler.Instance.StopAsync();

        // ② 큐 소진 후 정지 (처리 중인 커맨드 완료 대기)
        await CommandQueue.Instance.StopAsync();

        // ③ 로그 마지막 (큐 소진 후 로그 파일 flush)
        await LogManager.Instance.StopAsync();

        base.OnExit(e);
    }
}
```

### 6.2 WPF UserControl 구독 패턴

```csharp
public partial class DashboardControl : UserControl
{
    // 구독 핸들 목록으로 관리 (여러 개 구독 시 편리)
    private readonly List<IDisposable> _subscriptions = [];

    public DashboardControl()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 여러 이벤트 구독
        _subscriptions.Add(
            EventBus.Instance.Subscribe<SensorDataEvent>(OnSensorData));

        _subscriptions.Add(
            EventBus.Instance.Subscribe<NetworkStatusEvent>(OnNetworkStatus));

        _subscriptions.Add(
            EventBus.Instance.Subscribe<ErrorOccurredEvent>(OnError));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 한 번에 전체 해제
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
    }

    private void OnSensorData(SensorDataEvent e)
    {
        // 백그라운드 → UI 스레드 전환 필수
        Dispatcher.InvokeAsync(() =>
        {
            TxtTemp.Text     = $"{e.Temperature:F1}°C";
            TxtHumidity.Text = $"{e.Humidity:F1}%";
            TxtLastUpdate.Text = $"마지막 수신: {e.Timestamp:HH:mm:ss}";

            // 과열 표시
            TxtTemp.Foreground = e.Temperature > 80
                ? Brushes.Red
                : Brushes.Black;
        });
    }

    private void OnNetworkStatus(NetworkStatusEvent e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            LedStatus.Fill = e.IsConnected ? Brushes.LimeGreen : Brushes.Red;
            TxtHost.Text   = e.Host;
        });
    }

    private void OnError(ErrorOccurredEvent e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            LstErrors.Items.Insert(0,
                $"[{DateTime.Now:HH:mm:ss}] {e.Source}: {e.Message}");
        });
    }
}
```

### 6.3 버튼 클릭 → 커맨드 큐 패턴

```csharp
// 버튼 클릭에서 직접 무거운 작업 실행 ❌
private void BtnSave_Click(object sender, RoutedEventArgs e)
{
    // 이렇게 하면 UI 스레드 블로킹 or 순서 보장 안 됨
    Task.Run(() => HeavySaveWork());
}

// CommandQueue 사용 ✅
private void BtnSave_Click(object sender, RoutedEventArgs e)
{
    var data = GetCurrentData();

    CommandQueue.Instance.Enqueue(LambdaCommand.Create(async ct =>
    {
        await SaveDataAsync(data, ct);
    }, CommandPriority.Normal));

    TxtStatus.Text = "저장 중...";  // 즉시 반응
}

private void BtnEmergencyStop_Click(object sender, RoutedEventArgs e)
{
    // Critical 우선순위 — 다른 모든 커맨드보다 먼저 처리
    CommandQueue.Instance.Enqueue(new EmergencyStopCommand());

    BtnEmergencyStop.IsEnabled = false;
    TxtStatus.Text = "비상 정지 신호 전송 중...";
}
```

---

## 7. lssLib 생태계 연동

### 7.1 lssLib.Binary + EventBus

```csharp
// 시리얼 수신 → 파싱 → 이벤트 발행
void OnSerialDataReceived(byte[] rawBytes)
{
    LogManager.Execute(tryAction: () =>
    {
        var ring = new RingBuffer(4096);
        ring.Write(rawBytes, 0, rawBytes.Length);

        while (ring.TryReadFrame(stx: 0xAA, length: 32, out byte[] frame))
        {
            if (!frame.VerifyCrc32()) continue;

            var result = frame[..^4].ToParser().Parse(SensorSchema.Default);

            // 파싱 결과 → EventBus 발행
            EventBus.Instance.Publish(new SensorDataEvent(
                DeviceId:    result.GetInt("DeviceId"),
                Temperature: result.GetFloat("Temp"),
                Humidity:    result.GetFloat("Humidity")));
        }
    }, category: "Serial");
}
```

### 7.2 lssLib.Log + CommandQueue 완료 이벤트

```csharp
// 커맨드 결과를 LogManager로 기록
CommandQueue.Instance.CommandCompleted += result =>
{
    if (result.IsSuccess)
        LogManager.Instance.Debug("CommandQueue", result.ToString());
    else
        LogManager.Instance.Error("CommandQueue", result.ToString());
};
```

### 7.3 lssLib.Utils.Guard + CommandBase

```csharp
public class ParseFrameCommand : CommandBase
{
    private readonly byte[]    _frame;
    private readonly BufSchema _schema;

    public ParseFrameCommand(byte[] frame, BufSchema schema)
    {
        // Guard로 진입 전 검증
        _frame  = Guard.NotEmpty(frame);
        _schema = Guard.NotNull(schema);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        Guard.That(_frame[0] == 0xAA, "STX 헤더 없음");

        var result = _frame.ToParser().Parse(_schema);
        Guard.That(result.IsAllOk, $"파싱 오류: {string.Join(", ", result.ErrorFields)}");

        await EventBus.Instance.PublishAsync(
            new FrameReceivedEvent(_frame, result.GetInt("DeviceId"), 0), ct);
    }
}
```

---

## 8. FAQ

### Q1. EventBus vs CommandQueue — 언제 어느 것을 써야 하나요?

```
EventBus 사용:
  ✅ "이 일이 발생했다"는 사실을 여러 곳에 알릴 때
  ✅ 누가 구독하는지 발행자가 알 필요 없을 때
  ✅ 구독자가 0명이어도 괜찮을 때
  예) 센서 데이터 수신, 연결 상태 변경, 에러 발생

CommandQueue 사용:
  ✅ "이 작업을 반드시 실행해"라는 명령을 내릴 때
  ✅ 실행 순서 또는 우선순위가 중요할 때
  ✅ 실행 결과(성공/실패, 소요 시간)를 알아야 할 때
  예) 파일 저장, DB 삽입, 외부 API 호출
```

### Q2. 비동기 핸들러에서 UI를 업데이트하려면?

```csharp
// ❌ 잘못된 방법 — 크로스 스레드 예외 발생
EventBus.Instance.Subscribe<SensorDataEvent>(e =>
{
    TxtTemp.Text = $"{e.Temperature}°C";  // 오류!
});

// ✅ 올바른 방법
EventBus.Instance.Subscribe<SensorDataEvent>(e =>
{
    Dispatcher.InvokeAsync(() =>
    {
        TxtTemp.Text = $"{e.Temperature}°C";
    });
});
```

### Q3. Publish와 PublishAsync의 차이는?

```
Publish      — 핸들러를 순서대로 블로킹 실행
               백그라운드 스레드에서 사용 권장
               UI 스레드 사용 시 async 핸들러에서 데드락 가능

PublishAsync — 핸들러를 병렬 실행 + Task.WhenAll 대기
               async/await 컨텍스트에서 권장
               핸들러 하나가 느려도 다른 핸들러에 영향 없음
```

### Q4. 스케줄러 작업이 실행 중에 예외가 발생하면?

```
ContinueOnError = true  (기본값)
  → 예외를 LogManager.Error에 기록하고 다음 실행을 계속합니다.

ContinueOnError = false
  → 예외 발생 즉시 해당 작업이 종료됩니다. (다른 작업에는 영향 없음)

OperationCanceledException은 ContinueOnError 무관하게 항상 루프 종료.
```

### Q5. 앱 종료 시 올바른 정리 순서는?

```csharp
// 1. AsyncScheduler — 새 이벤트/커맨드 생성 중단
await AsyncScheduler.Instance.StopAsync();

// 2. CommandQueue — 대기 중인 커맨드 처리 포기 (혹은 드레인)
await CommandQueue.Instance.StopAsync();

// 3. LogManager — 남은 로그 파일에 flush
await LogManager.Instance.StopAsync();
```

---

*lssLib.Messaging 사용 가이드 · .NET 8.0 · lssLib v5*
