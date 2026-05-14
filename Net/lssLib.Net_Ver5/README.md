# lssLib.Net — v5

**통신 모듈** · .NET 8.0 · C# 12 · BCL only

---

## v4 → v5 주요 변경사항

| 항목 | v4 | v5 |
|---|---|---|
| **프로젝트 수** | 2개 (Base + Implementation) | **1개** (lssLib.Net) |
| **네임스페이스** | `lssLib.Net` + `lssLib.Net.Implementation` | **`lssLib.Net` 단일** |
| **using** | `using lssLib.Net;` + `using lssLib.Net.Implementation;` | **`using lssLib.Net;` 1줄** |
| **Config 베이스** | `NetDeviceConfigBase` + 4개 인터페이스 | **`NetDeviceConfig` 단일 추상 클래스** |
| **RetryTarget** | `Connect / Read / Write / All` | **`Connect / Write / ConnectAndWrite / All`** (Read 제거) |
| **NetTransportType** | 15개 (미구현 포함) | **4개** (구현체만: Tcp/Serial/Udp/SharedMemory) |
| **WriteCommands** | `AddWriteCommand()` 존재 | **제거** (인프라에서 미사용) |
| **버그** | `_connMgr.Reconnected` 이중 구독 | **수정** |
| **Interface 폴더** | `Interface/` | **`Contracts/`** |

---

## 솔루션 구성

```
lssLib.Net.sln
│
├── lssLib.Net/                      ← 단일 라이브러리 프로젝트 (v4: Base + Implementation 통합)
│   ├── lssLib.Net.csproj
│   │
│   ├── Contracts/                   ← 계약 인터페이스 (v4: Interface/)
│   │   ├── INetTransport.cs
│   │   └── INetProtocol.cs
│   │
│   ├── Core/                        ← 열거형 · 값 타입 (★ 고정)
│   │   ├── NetMode.cs
│   │   ├── NetState.cs
│   │   ├── NetPriority.cs           ★
│   │   ├── NetResult.cs             ★
│   │   ├── NetPacket.cs             (internal)
│   │   └── PacketMode.cs            (internal)
│   │
│   ├── Config/                      ← 설정 클래스 (v4: Interface 4개 + ConfigBase → 단일 추상 클래스)
│   │   ├── NetDeviceConfig.cs       ← 핵심 변경: 추상 베이스 (4 인터페이스 통합)
│   │   ├── RetryTarget.cs           ← 단순화 (Read 제거, ConnectAndWrite 추가)
│   │   ├── NetTransportType.cs      ← 단순화 (4가지만 유지)
│   │   ├── TcpDeviceConfig.cs
│   │   ├── SerialDeviceConfig.cs
│   │   ├── UdpDeviceConfig.cs
│   │   └── SharedMemDeviceConfig.cs
│   │
│   ├── Transport/                   ← NetTransportBase 파생 (v4: Implementation/Transport)
│   │   ├── NetTransportBase.cs
│   │   ├── TcpTransport.cs
│   │   ├── SerialTransport.cs
│   │   ├── UdpTransport.cs
│   │   └── SharedMemoryTransport.cs
│   │
│   ├── Protocol/                    ← INetProtocol 구현
│   │   ├── RawProtocol.cs           ★
│   │   └── BinaryProtocol.cs        ★
│   │
│   ├── Channels/                    ← NetChannelBase 파생 (v4: Implementation/Channels)
│   │   ├── NetChannelBase.cs        ← 버그 수정 + 단순화
│   │   ├── PassiveNetChannel.cs
│   │   └── RequestResponseChannel.cs
│   │
│   └── Infrastructure/              ← internal (동일 기능 유지)
│       ├── NetConnectionManager.cs
│       ├── NetDispatchPipeline.cs
│       ├── NetScheduler.cs
│       ├── NetStatistics.cs         ★
│       └── NetDeviceRegistry.cs
│
├── lssLib.Net.Demo/                 ← 조립 예시
│   ├── lssLib.Net.Demo.csproj       ← 단일 ProjectReference
│   └── Program.cs
│
└── lssLib.Net.TcpTestServer/        ← WPF 테스트 서버 (변경 없음)
```

---

## 빠른 시작

```csharp
// v5: using 1줄
using lssLib.Net;

// Passive 수신 (TCP Push 서버)
var cfg = new TcpDeviceConfig(1, "PushSensor", "192.168.1.50", 5000)
{
    MaxRetries        = 0,    // 무제한 재시도
    HeartbeatInterval = TimeSpan.FromSeconds(30)
};

await using var channel = new PassiveNetChannel(
    cfg,
    TcpTransport.FromConfig(cfg, enablePassiveReceive: true),
    new BinaryProtocol(stx: 0xAA),
    autoRegister: true);

channel.DeviceFrameReceived += (id, frame) =>
    Dispatcher.InvokeAsync(() => UpdateUI(id, frame));  // ⚠ WPF: Dispatcher 필수

channel.DeviceErrorOccurred += (id, ex) =>
    LogManager.Instance.Error(cfg.DeviceName, ex.Message);

await channel.StartAsync();
```

```csharp
// RequestResponse (Modbus RTU)
var cfg = new SerialDeviceConfig(2, "Modbus-PLC", "COM3", 9600);
cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]);

await using var channel = new RequestResponseChannel(
    cfg, SerialTransport.FromConfig(cfg),
    new RawProtocol(), autoRegister: true);

channel.DeviceFrameReceived += (id, frame) => ProcessModbus(id, frame);
await channel.StartAsync();

await channel.WriteAsync(setpointFrame);
NetResult r = await channel.RequestAsync(queryFrame, TimeSpan.FromMilliseconds(500));
if (r.IsOk) ProcessResponse(r.Data!);
```

---

## v4 → v5 마이그레이션

### 1. using 정리

```csharp
// v4
using lssLib.Net;
using lssLib.Net.Implementation;  // ← 제거

// v5
using lssLib.Net;  // 1줄로 충분
```

### 2. 프로젝트 참조 정리

```xml
<!-- v4 -->
<ProjectReference Include="..\lssLib.Net.Base\lssLib.Net.Base.csproj" />
<ProjectReference Include="..\lssLib.Net.Implementation\lssLib.Net.Implementation.csproj" />

<!-- v5 -->
<ProjectReference Include="..\lssLib.Net\lssLib.Net.csproj" />
```

### 3. Config 베이스 클래스 이름 변경

```csharp
// v4
public class MyConfig : NetDeviceConfigBase { ... }

// v5
public class MyConfig : NetDeviceConfig { ... }
```

### 4. RetryTarget 변경

```csharp
// v4
cfg.RetryTarget = RetryTarget.Connect | RetryTarget.Write;
cfg.RetryTarget = RetryTarget.All;

// v5 (ConnectAndWrite = Connect | Write)
cfg.RetryTarget = RetryTarget.ConnectAndWrite;  // 권장
cfg.RetryTarget = RetryTarget.All;              // 동일
```

### 5. WriteCommands 제거

```csharp
// v4
cfg.AddWriteCommand(frame);  // 실제로 인프라에서 사용 안 됨

// v5: 제거됨. WriteAsync 로 직접 전송하세요.
await channel.WriteAsync(frame);
```

---

## 설정 항목

| 항목 | 기본값 | 설명 |
|---|---|---|
| `IsRetryEnabled` | `true` | 재시도 활성화 |
| `RetryTarget` | `ConnectAndWrite` | Connect / Write 플래그 |
| `MaxRetries` | `3` | 0=무제한 |
| `RetryDelay` | `200ms` | Backoff 기준값 |
| `ReconnectBackoff` | `true` | 지수 증가 (최대 60s) |
| `IsSequential` | `true` | 순차(RS-485) / 병렬(TCP) |
| `PeriodicInterval` | `100ms` | 주기 Read 간격. Zero=비활성 |
| `RequestTimeout` | `3s` | 단발 요청 타임아웃 |
| `HeartbeatInterval` | `Zero` | Zero=비활성 |
| `ReceiveChannelCapacity` | `0` | 0=무제한 |

### 환경별 기본값

| 환경 | Backoff | Sequential | Heartbeat |
|---|---|---|---|
| TCP | true | false | 30s |
| Serial | false | true | Zero |
| UDP | — | false | Zero |
| SharedMemory | — | false | Zero |

---

## 버그 수정 내역 (v4 → v5)

### `_connMgr.Reconnected` 이중 구독 버그

```csharp
// v4 NetChannelBase 생성자 (버그)
_connMgr.Reconnected += () => _scheduler.Resume();
// ...
_connMgr.Reconnected += () => _scheduler.Resume();  // 동일 핸들러 이중 등록!

// v5 (수정)
_connMgr.Reconnected += () => _scheduler.Resume();  // 단일 구독
```

재접속 성공 시 `_scheduler.Resume()` 이 2회 호출되어 내부 `_paused` 플래그가
의도치 않게 두 번 `false` 로 설정되던 문제를 수정했습니다.

---

*lssLib.Net v5 · .NET 8.0 · C# 12 · 단일 프로젝트 아키텍처*
