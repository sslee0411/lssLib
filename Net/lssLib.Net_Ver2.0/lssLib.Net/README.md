# lssLib.Net — v4

**통신 모듈** · .NET 8.0-windows · C# 12 · BCL only (+ lssLib.Log)

---

## 개요

임베디드·산업용·업무 앱에서 공통으로 사용할 수 있는  
**TCP / UDP / Serial / 공유 메모리 / HTTP / MQTT** 통신을  
`통신 형태 × 전송 계층 × 프로토콜` 3-레이어 Lego 조립 구조로 제공합니다.

---

## 솔루션 구성

```
lssLib.Net/
│
├── Core/                              ← 핵심 추상화 + 전문 클래스
│   ├── NetChannelBase.cs              ★ 얇은 오케스트레이터 (3클래스 조합)
│   ├── NetConnectionManager.cs        연결·재접속·상태 머신 전담
│   ├── NetDispatchPipeline.cs         4채널 우선순위 파이프라인 (lock 없음)
│   ├── NetScheduler.cs                주기 Read + Heartbeat (Pause/Resume)
│   ├── NetStatistics.cs               통신 통계 수집 (Interlocked 스레드 안전)
│   ├── NetDeviceRegistry.cs           Lazy<T> 싱글톤 전체 장비 관리
│   ├── INet.cs                        INetTransport / INetProtocol 인터페이스
│   ├── NetMode.cs                     통신 형태 / 연결 상태 / 내부 패킷 모드  [고정]
│   ├── NetPacket.cs                   내부 큐 패킷 (internal)                   [고정]
│   ├── NetPriority.cs                 우선순위 열거형 (Critical > Write > Read > Low) [고정]
│   └── NetResult.cs                   결과 값 타입 (readonly record struct)     [고정]
│
├── Config/
│   ├── Interfaces.cs                  IDeviceConfig / IRetryConfig / ISequenceConfig / ICommandConfig
│   ├── NetDeviceConfigBase.cs         4개 인터페이스 조립 추상 베이스
│   └── DeviceConfigs.cs               TcpDeviceConfig / SerialDeviceConfig / UdpDeviceConfig / SharedMemoryDeviceConfig
│
├── Transport/
│   ├── NetTransportBase.cs            상태 관리 공통 추상 베이스
│   └── Transports.cs                  TcpTransport / SerialTransport / SharedMemoryTransport
│
├── Protocol/
│   └── Protocols.cs                   RawProtocol / BinaryProtocol
│
├── Channel/
│   └── Channels.cs                    PassiveNetChannel / RequestResponseChannel
│
└── Examples/
    └── UsageExamples.cs               조립 패턴 예시 + v3→v4 마이그레이션 가이드
```

---

## lssLib 생태계 내 위치

```
lssLib.Binary ──► lssLib.Extensions ──► lssLib.Utils ──► lssLib.Retry
                                                              │
                                                              ▼
                                                    lssLib.Messaging
                                                              │
                                                              ▼
                                                    lssLib.Net  ← 이 모듈
```

---

## 아키텍처 — v3 vs v4

### v3: 단일 클래스에 모든 책임 집중 (SRP 위반)
```
NetChannelBase
  ├─ 연결·재접속 로직 (인라인)
  ├─ lock + PriorityQueue (이중 동기화)
  ├─ 주기 Read + Heartbeat 루프 (인라인)
  └─ 수신 처리
```

### v4: 관심사 분리 (SRP 준수)
```
NetChannelBase (얇은 오케스트레이터)
  ├─ NetConnectionManager  — 연결·재접속·상태 머신 전담
  ├─ NetDispatchPipeline   — Channel[4] 우선순위 파이프라인 (lock 제거)
  ├─ NetScheduler          — 주기 Read + Heartbeat (Pause/Resume 제어)
  └─ NetStatistics         — 통신 통계 수집
```

---

## Lego 조립 원칙

### 설정 브릭 계층

```
[인터페이스 브릭]
  IDeviceConfig     — DeviceId, DeviceName (LogManager Source 자동)
  IRetryConfig      — IsRetryEnabled, RetryTarget[Flags], MaxRetries, RetryDelay
  ISequenceConfig   — IsSequential (순차 / 병렬)
  ICommandConfig    — ReadCommands, WriteCommands 목록
         │
         ▼ (조립)
[NetDeviceConfigBase]  ← 4개 인터페이스 구현 + 공통 채널 설정
         │
         ▼ (확장)
  TcpDeviceConfig         — Host, Port, ConnectTimeout
  SerialDeviceConfig      — PortName, BaudRate, Parity, StopBits
  UdpDeviceConfig         — RemoteHost, RemotePort
  SharedMemoryDeviceConfig — MapName, Role, MapSize
```

### RetryTarget 플래그

```csharp
[Flags]
public enum RetryTarget
{
    None    = 0,
    Connect = 1 << 0,   // 접속 실패 시 재시도
    Read    = 1 << 1,   // 읽기 실패 (CircuitBreaker 로 처리)
    Write   = 1 << 2,   // 쓰기 실패 → 재접속 후 Critical 재투입
    All     = Connect | Read | Write
}
```

---

## 빠른 시작

### 형태 1 — Passive 수신 (시리얼 센서)

```csharp
var cfg = new SerialDeviceConfig(3, "Sensor-03", "COM3", 115200)
{
    IsRetryEnabled   = true,
    RetryTarget      = RetryTarget.All,
    MaxRetries       = 10,
    ReconnectBackoff = false,
    IsSequential     = true
};

// autoRegister=true → NetDeviceRegistry 에 자동 등록
await using var channel = new PassiveNetChannel(
    cfg,
    SerialTransport.FromConfig(cfg),
    new BinaryProtocol(stx: 0xAA),
    autoRegister: true);

channel.DeviceFrameReceived += (id, frame) =>
    Dispatcher.InvokeAsync(() => UpdateUI(id, frame));

channel.DeviceStateChanged += (id, state) =>
    Dispatcher.InvokeAsync(() => LblState.Content = state.ToString());

// v4: 오류는 이벤트로 통보 (WriteAsync 실패 포함)
channel.DeviceErrorOccurred += (id, ex) =>
    LogManager.Instance.Error("Sensor-03", ex.Message);

await channel.StartAsync();
```

### 형태 2 — RequestResponse (Modbus RTU)

```csharp
var cfg = new SerialDeviceConfig(1, "Modbus-PLC", "COM3", 9600)
{
    IsSequential     = true,
    PeriodicInterval = TimeSpan.FromMilliseconds(50)
};
cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]);

await using var channel = new RequestResponseChannel(
    cfg,
    SerialTransport.FromConfig(cfg),
    new RawProtocol(),
    autoRegister: true);

channel.DeviceFrameReceived += (id, frame) => ProcessModbus(id, frame);
await channel.StartAsync();

// v4: WriteAsync → Task (실패는 DeviceErrorOccurred 이벤트)
await channel.WriteAsync(setpointFrame, NetPriority.Write);

// RequestAsync 는 여전히 NetResult 반환
NetResult r = await channel.RequestAsync(queryFrame);
if (r.IsOk) ProcessResponse(r.Data!);
```

---

## NetDeviceRegistry — 전역 장비 관리 (v4 신규)

```csharp
// DeviceId 로 어디서든 접근
var plc = NetDeviceRegistry.Instance.Get(1);
if (plc?.IsConnected == true)
    await plc.WriteAsync(frame);

// 연결된 장비만 필터
foreach (var ch in NetDeviceRegistry.Instance.GetConnected())
    LogManager.Instance.Info(ch.DeviceName, "정상 운영 중");

// 전체 브로드캐스트 (비상 정지 등)
await NetDeviceRegistry.Instance.BroadcastAsync(
    emergencyStopFrame, NetPriority.Critical);

// 전체 상태 로그
NetDeviceRegistry.Instance.LogStatus();

// 앱 종료 시 일괄 정지
await NetDeviceRegistry.Instance.StopAllAsync();
```

---

## NetStatistics — 통신 통계 (v4 신규)

```csharp
var stats = channel.Statistics;

// 실시간 조회
LogManager.Instance.Info(channel.DeviceName,
    $"전송={stats.TotalSent} 수신={stats.TotalReceived} " +
    $"오류={stats.TotalErrors} 평균응답={stats.AvgResponseMs:F1}ms");

// WPF DataGrid 바인딩용 불변 스냅샷
var snap = stats.Snapshot();
// snap.TotalSent / TotalReceived / TotalErrors / AvgResponseMs
// snap.MaxResponseMs / TotalReconnects / LastError / LastErrorTime

// 전체 장비 상태 일괄 조회
var allStatus = NetDeviceRegistry.Instance.GetStatusAll();
DgDevices.ItemsSource = allStatus.ToList();

// 리셋
stats.Reset();
```

---

## 연결 상태 확인 방법

### 방법 1 — `IsConnected` 직접 조회 (폴링)

```csharp
if (channel.IsConnected)
    await channel.WriteAsync(frame);
else
    LogManager.Instance.Warn(channel.DeviceName, $"연결 없음 ({channel.State})");
```

### 방법 2 — `DeviceStateChanged` 이벤트 (push, 권장)

```csharp
channel.DeviceStateChanged += (id, state) =>
{
    Dispatcher.InvokeAsync(() =>
    {
        TxtState.Text      = state.ToString();
        BtnSend.IsEnabled  = (state == NetState.Connected);
        // Reconnecting 시 재접속 중 UI 표시
    });
};
```

### 방법 3 — `NetResult` (RequestAsync 결과)

```csharp
NetResult r = await channel.RequestAsync(frame);
if (r.IsOk)   ProcessResponse(r.Data!);
else           LogManager.Instance.Error(channel.DeviceName, r.Error!.Message);
```

---

## 내부 데이터 흐름 (v4)

```
WriteAsync / RequestAsync
        │ IsConnected 가드
        ▼
NetDispatchPipeline.EnqueueAsync
        │
        ▼ (우선순위별 채널 투입)
Channel[0] Critical ──┐
Channel[1] Write    ──┤→ ProcessLoopAsync (소비자 단일 루프)
Channel[2] Read     ──┤   ├─ 0번 채널부터 TryRead 순차 시도
Channel[3] Low      ──┘   └─ DispatchAsync → Transport.WriteAsync
                                    │
                           성공 → 결과 전달
                           실패 → NetConnectionManager.HandleErrorAsync
                                    │
                               1. DeviceErrorOccurred 이벤트
                               2. NetScheduler.Pause()
                               3. DisconnectAsync
                               4. ReconnectAsync (지수 백오프)
                               5. 성공 → NetScheduler.Resume()
                               6. 보존 Write → Critical 재투입

NetScheduler (독립 루프)
        ├─ PeriodicReadLoopAsync (IsConnected && !Paused)
        └─ HeartbeatLoopAsync    (IsConnected && !Paused)

Transport.DataReceived (Passive 수신)
        → Pipeline.PushReceived
        → Protocol.TryDecode
        → ReceiveChannel + DeviceFrameReceived
```

---

## lssLib.Retry 패턴 연동

| 동작 | 패턴 | 비고 |
|---|---|---|
| 접속 | `RetryAsync` | 지수 백오프, MaxRetries 설정 |
| 주기 Read | `CircuitBreaker` + `RateLimiter` | Read/Write 별도 CB 인스턴스 |
| Write | `CircuitBreaker` + `ExecuteWithRetryAsync` | 차단 우선, 해제 후 재시도 |
| RequestAsync | `RetryWithTimeout` | 1회 + 전체 타임아웃 이중 보호 |
| Heartbeat | `TryExecuteAsync` | 실패해도 루프 유지 |

```csharp
// NetDeviceConfigBase 에 Retry 정책 추가 (선택)
// using lssLib.Retry;

var cfg = new SerialDeviceConfig(1, "PLC", "COM3", 9600)
{
    RetryPolicy          = new RetryPolicy(MaxAttempts: 5, ...),
    CircuitBreakerPolicy = new CircuitBreakerPolicy(FailureThreshold: 3, ...),
    RateLimiterPolicy    = RateLimiterPolicy.PerSecond(20)
};
```

---

## 설정 항목 (NetDeviceConfigBase)

| 항목 | 기본값 | 설명 |
|---|---|---|
| `DeviceId` | — | 장비 고유 ID |
| `DeviceName` | — | LogManager Source 자동 적용 |
| `IsRetryEnabled` | `true` | 재시도 활성화 |
| `RetryTarget` | `All` | Connect / Read / Write 플래그 |
| `MaxRetries` | `3` | 최대 재시도 횟수 (0=무제한) |
| `RetryDelay` | `200ms` | 재시도 기준 대기 시간 |
| `ReconnectBackoff` | `true` | 지수 백오프 (true) / 고정 (false) |
| `IsSequential` | `true` | 커맨드 순차 / 병렬 |
| `PeriodicInterval` | `100ms` | 주기 Read 간격 |
| `RequestTimeout` | `3s` | 단발 요청 타임아웃 |
| `HeartbeatInterval` | `Zero` | Heartbeat 간격 (Zero=비활성) |
| `ReceiveChannelCapacity` | `0` | 수신 채널 용량 (0=무제한) |

---

## v3 → v4 마이그레이션

### WriteAsync 반환값

```csharp
// v3
var r = await channel.WriteAsync(frame);
if (r.IsError) LogError(r.Error!.Message);

// v4 — Task 반환, 실패는 이벤트
await channel.WriteAsync(frame);
channel.DeviceErrorOccurred += (id, ex) => LogError(ex.Message);  // 등록 필요
```

### 채널 생성 — autoRegister 옵션

```csharp
// v3
var ch = new RequestResponseChannel(cfg, transport, protocol);

// v4 — autoRegister=true 추가 시 Registry 자동 등록
var ch = new RequestResponseChannel(cfg, transport, protocol, autoRegister: true);
```

### 앱 종료

```csharp
// v3
await Task.WhenAll(channels.Select(c => c.DisposeAsync().AsTask()));

// v4 — Registry 사용 시
await NetDeviceRegistry.Instance.StopAllAsync();
```

---

## 주의 사항

| 항목 | 내용 |
|---|---|
| `DeviceFrameReceived` / `DeviceStateChanged` | 백그라운드 스레드 → WPF UI 접근 시 `Dispatcher.InvokeAsync` 필수 |
| `StopAsync()` / `DisposeAsync()` | 앱 종료 시 반드시 호출. `await using` 패턴 권장 |
| `WriteAsync` v4 | `Task` 반환. 실패는 `DeviceErrorOccurred` 이벤트로 처리 |
| `autoRegister` | 동일 `DeviceId` 중복 등록 시 `InvalidOperationException` 발생 |
| Read / Write `CircuitBreaker` | 별도 인스턴스로 독립 관리 필수 |
| `NetScheduler` Pause/Resume | 재접속 중 자동 처리됨. 별도 코드 불필요 |

---

*lssLib.Net v4 · .NET 8.0-windows · C# 12 · 관심사 분리 (SRP) 아키텍처*
