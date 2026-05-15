# lssLib.Net — v4

**통신 모듈** · .NET 8.0-windows · C# 12 · BCL only (+ lssLib.Log)

---

## 솔루션 구성

```
0-lssLib.Net.sln
│
├── 1-lssLib.Net.Base/                   ← 인터페이스 · 추상 클래스 · 인프라
│   ├── lssLib.Net.Base.csproj
│   │
│   ├── Interface/                       ← 계약 인터페이스 (고정)
│   │   ├── ICommandConfig.cs
│   │   ├── IDeviceConfig.cs
│   │   ├── INetProtocol.cs
│   │   ├── INetTransport.cs
│   │   ├── IRetryConfig.cs              + RetryTarget [Flags]
│   │   └── ISequenceConfig.cs
│   │
│   ├── Core/                            ← 열거형 · 값 타입 (고정 ★)
│   │   ├── NetMode.cs                   ★
│   │   ├── NetPacket.cs                 ★ internal
│   │   ├── NetPriority.cs               ★
│   │   ├── NetResult.cs                 ★ + NetResult<T>
│   │   ├── NetState.cs
│   │   └── PacketMode.cs                internal
│   │
│   ├── Abstractions/                    ← *Base.cs 추상 클래스 모음
│   │   ├── NetChannelBase.cs            ★ 오케스트레이터 (3클래스 조합)
│   │   ├── NetDeviceConfigBase.cs       ★ 4인터페이스 조립 + NetTransportType
│   │   └── NetTransportBase.cs          ★ INetTransport 공통 구현
│   │
│   └── Infrastructure/                  ← 내부 파이프라인 (internal)
│       ├── NetConnectionManager.cs      연결 · 재접속 · 상태 머신
│       ├── NetDeviceRegistry.cs         Lazy<T> 싱글톤 전체 장비 관리
│       ├── NetDispatchPipeline.cs       Channel[4] 우선순위 (lock 없음)
│       ├── NetScheduler.cs              주기 Read + Heartbeat (Pause/Resume)
│       └── NetStatistics.cs             통신 통계 (Interlocked 스레드 안전)
│
├── 2-lssLib.Net.Implementation/         ← 구현체 (Base 참조)
│   ├── lssLib.Net.Implementation.csproj
│   │
│   ├── Transport/                       ← NetTransportBase 파생
│   │   ├── TcpTransport.cs
│   │   ├── SerialTransport.cs
│   │   ├── UdpTransport.cs
│   │   └── SharedMemoryTransport.cs     + SharedMemoryRole
│   │
│   ├── Protocol/                        ← INetProtocol 구현
│   │   ├── RawProtocol.cs               pass-through
│   │   └── BinaryProtocol.cs            STX/FC/LEN/DATA/CRC-32
│   │
│   ├── Config/                          ← NetDeviceConfigBase 파생
│   │   ├── TcpDeviceConfig.cs
│   │   ├── SerialDeviceConfig.cs
│   │   ├── UdpDeviceConfig.cs
│   │   └── SharedMemDeviceConfig.cs
│   │
│   └── Channels/                        ← NetChannelBase 파생
│       ├── PassiveNetChannel.cs
│       └── RequestResponseChannel.cs
│
└── 3-lssLib.Net.Demo/                   ← 조립 예시 + 마이그레이션 가이드
    ├── lssLib.Net.Demo.csproj
    └── UsageExamples.cs
```

---

## 클래스 관계도

```
┌──────────────────────────────────────────────────────────────────┐
│  Interface/  (계약)                                               │
│                                                                  │
│  IDeviceConfig ─┐                                                │
│  IRetryConfig  ─┤──► NetDeviceConfigBase (Abstractions/)         │
│  ISequenceConfig┤         ▲                                      │
│  ICommandConfig ┘         │ 파생                                  │
│                           │                                      │
│  INetTransport ──► NetTransportBase (Abstractions/)              │
│                       ▲                                          │
│                       │ 파생                                      │
│  INetProtocol ─────────────────────────────────────┐             │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│  Abstractions/                                                   │
│                                                                  │
│  NetChannelBase (오케스트레이터) ◄── 파생 ── PassiveNetChannel    │
│      │                                       RequestResponseChannel│
│      │ 조합 (Composition)                                         │
│      ├─ NetConnectionManager  연결·재접속·상태 머신               │
│      ├─ NetDispatchPipeline   Channel[4] 우선순위 파이프라인       │
│      ├─ NetScheduler          주기 Read + Heartbeat               │
│      └─ NetStatistics         통신 통계 (Interlocked)             │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│  Implementation/  (구현체)                                        │
│                                                                  │
│  NetTransportBase ◄── TcpTransport                               │
│                   ◄── SerialTransport                            │
│                   ◄── UdpTransport                               │
│                   ◄── SharedMemoryTransport                      │
│                                                                  │
│  INetProtocol     ◄── RawProtocol                                │
│                   ◄── BinaryProtocol                             │
│                                                                  │
│  NetDeviceConfigBase ◄── TcpDeviceConfig                         │
│                      ◄── SerialDeviceConfig                      │
│                      ◄── UdpDeviceConfig                         │
│                      ◄── SharedMemDeviceConfig                   │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│  Infrastructure/  (내부 싱글톤)                                   │
│                                                                  │
│  NetDeviceRegistry.Instance                                      │
│      └─ ConcurrentDictionary<int, NetChannelBase>               │
│         ├─ Register / Unregister / Get / GetByName              │
│         ├─ GetAll / GetConnected / GetDisconnected              │
│         ├─ StartAllAsync / StopAllAsync                          │
│         ├─ BroadcastAsync (연결된 전체 장비)                      │
│         └─ GetStatusAll → NetDeviceStatus[]                      │
└──────────────────────────────────────────────────────────────────┘
```

---

## 내부 데이터 흐름

```
WriteAsync / RequestAsync
    │ IsConnected 가드
    ▼
NetDispatchPipeline.EnqueueAsync(packet)
    │
    ├─ Channel[0] Critical ─┐
    ├─ Channel[1] Write     ├─► ProcessLoopAsync (소비자)
    ├─ Channel[2] Read      │       TryRead: 0→1→2→3 순서
    └─ Channel[3] Low/HB   ─┘       ↓ DispatchAsync
                                    ├─ Transport.WriteAsync
                                    ├─ Request: ReadAsync → TCS
                                    ├─ PeriodicRead: ReadAsync → 이벤트
                                    └─ 실패: HandleErrorAsync
                                              │
                                         Scheduler.Pause
                                         Transport.Disconnect
                                         ReconnectLoopAsync (지수 백오프)
                                              │ 성공
                                         Scheduler.Resume
                                         보존 Write → Critical 재투입

NetScheduler (독립 루프)
    ├─ PeriodicReadLoopAsync  → Enqueue(Read)      [IsConnected && !Paused]
    └─ HeartbeatLoopAsync     → Enqueue(Low)        [IsConnected && !Paused]

Transport.DataReceived (Passive 수신)
    → Pipeline.PushReceived
    → Protocol.TryDecode
    → ReceiveChannel + DeviceFrameReceived 이벤트
```

---

## 빠른 시작

### 형태 1 — Passive 수신 (시리얼 센서)

```csharp
var cfg = new SerialDeviceConfig(3, "Sensor-03", "COM3", 115200)
{
    IsRetryEnabled = true, RetryTarget = RetryTarget.All,
    MaxRetries = 10, IsSequential = true
};

await using var channel = new PassiveNetChannel(
    cfg, SerialTransport.FromConfig(cfg),
    new BinaryProtocol(stx: 0xAA), autoRegister: true);

channel.DeviceFrameReceived += (id, frame) =>
    Dispatcher.InvokeAsync(() => UpdateUI(id, frame));  // ⚠ UI 스레드

channel.DeviceStateChanged  += (id, state) =>
    Dispatcher.InvokeAsync(() => LblState.Content = state.ToString());

channel.DeviceErrorOccurred += (id, ex) =>
    LogManager.Instance.Error(cfg.DeviceName, ex.Message);

await channel.StartAsync();
```

### 형태 2 — RequestResponse (Modbus RTU)

```csharp
var cfg = new SerialDeviceConfig(1, "Modbus-PLC", "COM3", 9600)
{
    IsSequential = true, PeriodicInterval = TimeSpan.FromMilliseconds(50)
};
cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]);

await using var channel = new RequestResponseChannel(
    cfg, SerialTransport.FromConfig(cfg),
    new RawProtocol(), autoRegister: true);

channel.DeviceFrameReceived += (id, frame) => ProcessModbus(id, frame);
await channel.StartAsync();

// Write (Read 보다 항상 우선)
await channel.WriteAsync(setpointFrame);

// 단발 요청-응답
NetResult r = await channel.RequestAsync(queryFrame, TimeSpan.FromMilliseconds(500));
if (r.IsOk) ProcessResponse(r.Data!);
```

### NetDeviceRegistry — 전역 장비 관리

```csharp
// DeviceId 로 어디서든 접근
NetDeviceRegistry.Instance.Get(1)?.WriteAsync(frame);

// 전체 브로드캐스트 (비상 정지)
await NetDeviceRegistry.Instance.BroadcastAsync(emergencyStop, NetPriority.Critical);

// 통계 + 상태 일괄 조회 → WPF DataGrid
DgDevices.ItemsSource = NetDeviceRegistry.Instance.GetStatusAll().ToList();

// 앱 종료
await NetDeviceRegistry.Instance.StopAllAsync();
```

---

## v3 → v4 마이그레이션

| 항목 | v3 | v4 |
|---|---|---|
| `WriteAsync` 반환 | `Task<NetResult>` | `Task` (실패 → `DeviceErrorOccurred`) |
| 채널 생성 | `new XxxChannel(cfg, t, p)` | `new XxxChannel(cfg, t, p, autoRegister: true)` |
| 장비 관리 | `List<NetChannelBase>` 직접 | `NetDeviceRegistry.Instance` |
| 앱 종료 | `Task.WhenAll(channels.Select(...))` | `StopAllAsync()` |
| 통계 | 없음 | `channel.Statistics` |
| 스케줄러 제어 | 없음 | Pause/Resume 자동 (재접속 연동) |

---

## 설정 항목

| 항목 | 기본값 | 설명 |
|---|---|---|
| `IsRetryEnabled` | `true` | 재시도 활성화 |
| `RetryTarget` | `All` | Connect / Read / Write 플래그 |
| `MaxRetries` | `3` | 0=무제한 |
| `RetryDelay` | `200ms` | Backoff 기준값 |
| `ReconnectBackoff` | `true` | 지수 증가 (최대 60s) |
| `IsSequential` | `true` | 순차(RS-485) / 병렬(TCP) |
| `PeriodicInterval` | `100ms` | 주기 Read 간격 |
| `RequestTimeout` | `3s` | 단발 요청 타임아웃 |
| `HeartbeatInterval` | `Zero` | Zero=비활성 |
| `ReceiveChannelCapacity` | `0` | 0=무제한 |

### 환경별 기본값 (Config 생성자 자동 적용)

| 환경 | Backoff | Sequential | HB |
|---|---|---|---|
| TCP | true | false | 30s |
| Serial | false | true | Zero |
| UDP | — | false | Zero |
| SharedMemory | — | false | Zero |

---

## 주의 사항

| 항목 | 내용 |
|---|---|
| `DeviceFrameReceived` / `DeviceStateChanged` | 백그라운드 스레드 → WPF: `Dispatcher.InvokeAsync` 필수 |
| `DisposeAsync()` | `StopAsync()` 포함. `await using` 패턴 권장 |
| `WriteAsync` | 연결 없으면 스킵. 실패는 `DeviceErrorOccurred` 이벤트 |
| `autoRegister` | 동일 DeviceId 중복 시 `InvalidOperationException` |
| Read / Write CircuitBreaker | 별도 인스턴스 독립 관리 필수 |

---

## 새 대화에서 이어가기

새 대화 시작 시 아래 내용을 첫 메시지에 붙여넣기:

```
lssLib.Net v4 솔루션 작업을 이어서 진행합니다.

[출력 경로]
/mnt/user-data/outputs/lssLib.Net.Solution/

[솔루션 구성]
0-lssLib.Net.sln
1-lssLib.Net.Base/     (Interface 6개, Core 6개, Abstractions 3개, Infrastructure 5개)
2-lssLib.Net.Implementation/  (Transport 4개, Protocol 2개, Config 4개, Channels 2개)
3-lssLib.Net.Demo/     (UsageExamples.cs)
README.md

[고정 파일 - 수정 시만 표시]
Core/NetPacket.cs / NetPriority.cs / NetResult.cs (업로드 파일 그대로)
lssLib.Net.Base.csproj (업로드 파일 그대로)

[완료 파일]
1-Base: Interface 6개 ✅ / Core 6개 ✅ / Abstractions 3개 ✅ / Infrastructure 5개 ✅
2-Impl: Transport 4개 ✅ / Protocol 2개 ✅ / Config 4개 ✅ / Channels 2개 ✅
3-Demo: UsageExamples.cs ✅
README.md ✅

[네임스페이스]
lssLib.Net (Base + Implementation 공통)
lssLib.Net.Demo (Demo)

[핵심 원칙]
- 1파일 1클래스/인터페이스/열거형
- autoRegister=true → NetDeviceRegistry 자동 등록
- WriteAsync → Task 반환 (실패는 DeviceErrorOccurred 이벤트)
- NetDispatchPipeline: Channel[4] (lock 없음, Priority=채널 인덱스)
- NetScheduler: Pause=재접속 시작, Resume=재접속 성공 후 자동
```

---

*lssLib.Net v4 · .NET 8.0-windows · C# 12 · 관심사 분리 (SRP) 아키텍처*
