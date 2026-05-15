# lssLib.Net — v5.1

**통신 모듈** · .NET 8.0-windows · C# 12 · BCL only (Serial 제외)

---

## v5 → v5.1 변경사항 (IsSequential → SequenceMode)

| 항목 | v5 | v5.1 |
|---|---|---|
| **타입** | `bool IsSequential` | **`int SequenceMode`** |
| **false (병렬)** | `IsSequential = false` | `SequenceMode = 0` (Parallel) |
| **true (순차)** | `IsSequential = true` | `SequenceMode = 1` (Sequential) |
| **신규: 슬라이딩 윈도우** | 없음 | `SequenceMode = N` (N개 동시) |

### SequenceMode 값 상세

| 값 | 상수 | 동작 | 내부 구현 | 용도 |
|---|---|---|---|---|
| **0** | `SequenceModes.Parallel` | 모든 커맨드 동시 투입 | `Task.WhenAll` | TCP / UDP / HTTP / WS / MQTT / Virtual |
| **1** | `SequenceModes.Sequential` | 1개씩 순서대로 투입 | `foreach + await` | Serial RS-485 / NamedPipe (버스 충돌 방지) |
| **N ≥ 2** | — | N개씩 동시 허용, 전체 순서 유지 | `SemaphoreSlim(N)` | 멀티드롭 버스, 대용량 폴링 최적화 |

```csharp
// 상수 사용 (권장)
cfg.SequenceMode = NetDeviceConfig.SequenceModes.Parallel;    // 0
cfg.SequenceMode = NetDeviceConfig.SequenceModes.Sequential;  // 1

// 슬라이딩 윈도우 직접 지정
cfg.SequenceMode = 3;   // 최대 3개 동시, 순서 유지

// ToString() 출력 예시
// [PLC-01#1] Transport=Tcp ReadCmd=2 SeqMode=Parallel(0) Retry=True(ConnectAndWrite)
// [Modbus#2] Transport=Serial ReadCmd=2 SeqMode=Sequential(1) Retry=True(ConnectAndWrite)
// [Multi#3]  Transport=Tcp ReadCmd=6 SeqMode=Window(3) Retry=True(ConnectAndWrite)
```

### 슬라이딩 윈도우 동작 예시

```
ReadCommands 6개 + SequenceMode = 3:

시간 →
그룹1: [CMD1, CMD2, CMD3] 동시 투입 → 완료
그룹2: [CMD4, CMD5, CMD6] 동시 투입 → 완료
(그룹 간 순서 유지, 그룹 내 병렬)

ReadCommands 6개 + SequenceMode = 1 (Sequential):
CMD1 → CMD2 → CMD3 → CMD4 → CMD5 → CMD6 (순서 완전 보장)

ReadCommands 6개 + SequenceMode = 0 (Parallel):
[CMD1, CMD2, CMD3, CMD4, CMD5, CMD6] 모두 동시
```

### v5 → v5.1 마이그레이션

```csharp
// v5
cfg.IsSequential = false;   // 병렬
cfg.IsSequential = true;    // 단일 순차

// v5.1
cfg.SequenceMode = 0;  // 또는 NetDeviceConfig.SequenceModes.Parallel
cfg.SequenceMode = 1;  // 또는 NetDeviceConfig.SequenceModes.Sequential
cfg.SequenceMode = 3;  // 슬라이딩 윈도우 3개 (신규)
```

---

## 개요

임베디드·산업용·IoT·엔터프라이즈 앱에서 공통으로 사용할 수 있는  
**TCP / Serial / UDP / SharedMemory / NamedPipe / HTTP / WebSocket / MQTT / Virtual**  
9가지 전송 계층을 단일 라이브러리로 제공하는 통신 모듈입니다.

```
using lssLib.Net;   ← 이 한 줄로 모든 Transport 사용 가능
```

---

## 솔루션 구성

```
lssLib.Net.v5.sln
│
├── lssLib.Net/
│   ├── Core/        (6)  NetMode · NetState · NetPriority · NetResult · NetPacket · PacketMode
│   ├── Config/      (7)  NetDeviceConfig ★ · NetTransportType · RetryTarget
│   │                     TcpDeviceConfig · SerialDeviceConfig · UdpDeviceConfig · SharedMemDeviceConfig
│   ├── Contracts/   (2)  INetTransport · INetProtocol
│   ├── Transport/  (10)  NetTransportBase + 9가지
│   │                     (NamedPipe/Http/WebSocket/Mqtt/Virtual Config 파일 내 포함)
│   ├── Protocol/    (2)  BinaryProtocol · RawProtocol
│   ├── Channels/    (3)  NetChannelBase · PassiveNetChannel · RequestResponseChannel
│   └── Infrastructure/(5)  NetConnectionManager · NetDispatchPipeline
│                            NetScheduler ★ · NetStatistics · NetDeviceRegistry
│
└── lssLib.Net.Demo/
    ├── Program.cs
    └── AllTransportExamples.cs  (Ex01~Ex11)
```

★ = v5.1 변경 파일

---

## 빠른 시작

### Passive 수신 (서버가 먼저 보내오는 환경)

```csharp
using lssLib.Net;

var cfg = new TcpDeviceConfig(1, "PushSensor", "192.168.1.50", 5000)
{
    MaxRetries        = 0,
    HeartbeatInterval = TimeSpan.FromSeconds(30)
    // SequenceMode = 0 (Parallel) ← TcpDeviceConfig 기본값
};

await using var channel = new PassiveNetChannel(
    cfg,
    TcpTransport.FromConfig(cfg, enablePassiveReceive: true),
    new BinaryProtocol(stx: 0xAA),
    autoRegister: true);

channel.DeviceFrameReceived += (id, frame) =>
    Dispatcher.InvokeAsync(() => UpdateUI(id, frame));

await channel.StartAsync();
```

### RequestResponse — Modbus RTU (단일 순차)

```csharp
var cfg = new SerialDeviceConfig(2, "Modbus-PLC", "COM3", 9600);
// cfg.SequenceMode == 1 (Sequential) ← 기본값, RS-485 버스 충돌 방지

cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]); // 슬레이브 1
cfg.AddReadCommand([0x02, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]); // 슬레이브 2
// → 슬레이브 1 완료 후 슬레이브 2 순서 보장

await using var channel = new RequestResponseChannel(
    cfg, SerialTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true);
```

### RequestResponse — TCP 슬라이딩 윈도우

```csharp
var cfg = new TcpDeviceConfig(3, "Multi-PLC", "192.168.1.10", 502)
{
    SequenceMode     = 3,   // 최대 3개 동시, 전체 순서 유지
    PeriodicInterval = TimeSpan.FromMilliseconds(100)
};

// ReadCommands 6개 → 3개씩 묶어 병렬 처리
cfg.AddReadCommand(cmd1); cfg.AddReadCommand(cmd2); cfg.AddReadCommand(cmd3);
cfg.AddReadCommand(cmd4); cfg.AddReadCommand(cmd5); cfg.AddReadCommand(cmd6);
// 투입 순서: [cmd1,cmd2,cmd3] → [cmd4,cmd5,cmd6]
```

---

## 전송 계층 전체 현황

| # | Transport | Config | 기본 SequenceMode | 용도 |
|---|---|---|---|---|
| 1 | `TcpTransport` | `TcpDeviceConfig` | **0** (Parallel) | 산업용 통신, Modbus TCP |
| 2 | `SerialTransport` | `SerialDeviceConfig` | **1** (Sequential) | RS-485, Modbus RTU |
| 3 | `UdpTransport` | `UdpDeviceConfig` | **0** (Parallel) | 브로드캐스트, 센서 |
| 4 | `SharedMemoryTransport` | `SharedMemDeviceConfig` | **0** (Parallel) | 동일 PC IPC |
| 5 | `NamedPipeTransport` | `NamedPipeDeviceConfig` | **1** (Sequential) | 프로세스 간 IPC |
| 6 | `HttpTransport` | `HttpDeviceConfig` | **0** (Parallel) | REST API, 클라우드 |
| 7 | `WebSocketTransport` | `WebSocketDeviceConfig` | **0** (Parallel) | 실시간 양방향 |
| 8 | `MqttTransport` | `MqttDeviceConfig` | **0** (Parallel) | IoT 메시징 |
| 9 | `VirtualTransport` | `VirtualDeviceConfig` | **0** (Parallel) | 테스트·시뮬레이터 |

---

## NetScheduler 내부 흐름 (SequenceMode)

```
PeriodicInterval 타이머 → ReadCommands 순회

SequenceMode == 0 (Parallel)
  → Task.WhenAll([CMD1, CMD2, CMD3, ...])
     모두 동시 투입, 순서 보장 없음

SequenceMode == 1 (Sequential)
  → await EnqueueAsync(CMD1)
  → await EnqueueAsync(CMD2)
  → await EnqueueAsync(CMD3)
     완전 순서 보장

SequenceMode == N (Window, N≥2)
  → SemaphoreSlim(N)
  → [CMD1, CMD2, CMD3] 동시 (세마포어 N=3 기준)
  → [CMD4, CMD5, CMD6] 동시
     N개씩 묶어 처리, 그룹 간 순서 유지
```

---

## 설정 항목 전체

### 공통 (NetDeviceConfig)

| 항목 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `IsRetryEnabled` | bool | true | 재시도 활성화 |
| `RetryTarget` | RetryTarget | ConnectAndWrite | Connect / Write 플래그 |
| `MaxRetries` | int | 3 | 0=무제한 |
| `RetryDelay` | TimeSpan | 200ms | Backoff 기준값 |
| `ReconnectBackoff` | bool | true | 지수 증가 최대 60s |
| **`SequenceMode`** | **int** | **1** | **0=병렬, 1=순차, N=윈도우(N)** |
| `PeriodicInterval` | TimeSpan | 100ms | 주기 Read 간격. Zero=비활성 |
| `RequestTimeout` | TimeSpan | 3s | 단발 요청 타임아웃 |
| `HeartbeatInterval` | TimeSpan | Zero | Zero=비활성 |
| `IsHeartbeatAcknowledged` | bool | false | Heartbeat 응답 수신 |
| `ReceiveChannelCapacity` | int | 0 | 0=무제한 |

### 환경별 기본값

| 환경 | Backoff | SequenceMode | Heartbeat | RetryDelay |
|---|---|---|---|---|
| TCP | true | **0 (Parallel)** | 30s | 2s |
| Serial | false | **1 (Sequential)** | Zero | 100ms |
| UDP | — | **0 (Parallel)** | Zero | — |
| SharedMemory | — | **0 (Parallel)** | Zero | — |
| NamedPipe | false | **1 (Sequential)** | Zero | 1s |
| HTTP | true | **0 (Parallel)** | Zero | 2s |
| WebSocket | true | **0 (Parallel)** | 30s | 2s |
| MQTT | true | **0 (Parallel)** | 30s | 3s |
| Virtual | — | **0 (Parallel)** | Zero | — |

---

## 전체 v4 → v5.1 마이그레이션

### 1. 프로젝트 참조

```xml
<!-- v4 -->
<ProjectReference Include="..\lssLib.Net.Base\..." />
<ProjectReference Include="..\lssLib.Net.Implementation\..." />

<!-- v5.1 -->
<ProjectReference Include="..\lssLib.Net\lssLib.Net.csproj" />
```

### 2. using

```csharp
// v4
using lssLib.Net;
using lssLib.Net.Implementation;

// v5.1
using lssLib.Net;
```

### 3. Config 베이스

```csharp
// v4
public class MyConfig : NetDeviceConfigBase { ... }

// v5.1
public class MyConfig : NetDeviceConfig { ... }
```

### 4. IsSequential → SequenceMode

```csharp
// v4
cfg.IsSequential = false;
cfg.IsSequential = true;

// v5.1
cfg.SequenceMode = 0;   // Parallel
cfg.SequenceMode = 1;   // Sequential
cfg.SequenceMode = 3;   // Window(3) — 신규
```

---

## 주의 사항

| 항목 | 내용 |
|---|---|
| `SequenceMode=1` Serial | RS-485 멀티드롭 버스에서 반드시 1로 유지 (버스 충돌 방지) |
| `SequenceMode=N` 윈도우 | N은 채널 수용량 및 장비 응답 속도에 맞게 조정 |
| `DeviceFrameReceived` | 백그라운드 스레드 → WPF: `Dispatcher.InvokeAsync` 필수 |
| `await using` | `DisposeAsync()` 가 `StopAsync()` 포함 |
| `autoRegister` | 동일 DeviceId 중복 시 `InvalidOperationException` |

---

## 데모 예제 목록

| 예제 | SequenceMode | 필요 환경 |
|---|---|---|
| Ex01 TCP Passive | 0 (Parallel) | TcpTestServer Push |
| Ex02 TCP RequestResponse | **0/1/3 시연** | TcpTestServer Echo |
| Ex03 Serial Modbus RTU | **1 (Sequential)** | COM3 |
| Ex04 UDP Passive | 0 (Parallel) | UDP 발신자 |
| Ex05 NamedPipe IPC | **1 (Sequential)** | 파이프 서버 |
| Ex06 HTTP REST | 0 (Parallel) | HTTP 서버 |
| Ex07 WebSocket | 0 (Parallel) | WS 서버 |
| Ex08 MQTT | 0 (Parallel) | 브로커 |
| **Ex09 Virtual ★** | **0/1/3 전체 시연** | **하드웨어 불필요** |
| Ex10 Multi Registry | 0 (Parallel) | Virtual 포함 |
| Ex11 SharedMemory | 0 (Parallel) | 동일 PC |

---

*lssLib.Net v5.1 · .NET 8.0-windows · C# 12 · SequenceMode: 0=Parallel / 1=Sequential / N=Window(N)*
