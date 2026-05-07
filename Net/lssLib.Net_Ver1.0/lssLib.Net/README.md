# lssLib.Net

**통신 모듈** · .NET 8.0 · C# 12 · BCL only (+ lssLib.Log)

---

## 개요

임베디드·산업용·업무 앱에서 공통으로 사용할 수 있는  
**TCP / UDP / Serial / 공유 메모리 / HTTP / MQTT** 통신을  
`통신 형태 + 전송 계층 + 프로토콜` 3-레이어 조합 구조로 제공하는 통신 라이브러리입니다.

```
통신 형태 (NetChannelBase 파생)
  ├─ PassiveNetChannel         — 형태 1: 수동 수신 (장치가 먼저 보냄)
  └─ RequestResponseChannel    — 형태 2: 요청-응답 (우리가 요청, 장치가 응답)

전송 계층 (INetTransport 구현)
  ├─ TcpTransport
  ├─ UdpTransport
  ├─ SerialTransport
  └─ SharedMemoryTransport

프로토콜 계층 (INetProtocol 구현)
  ├─ RawProtocol               — pass-through (테스트 / UDP)
  └─ BinaryProtocol            — lssLib.Binary STX/LEN/DATA/CRC-32 프레임
```

---

## 솔루션 구성

```
lssLib.Net/
├── lssLib.Net.csproj
│
├── Core/                              ← 핵심 추상화 레이어
│   ├── NetPriority.cs                   우선순위 열거형 (Critical > Write > Read > Low)
│   ├── NetMode.cs                       통신 형태 / 연결 상태 / 내부 패킷 모드
│   ├── NetResult.cs                     결과 값 타입 (readonly record struct)
│   ├── NetPacket.cs                     내부 큐 패킷 (internal)
│   ├── NetConfig.cs                     채널 공통 설정 + 환경별 프리셋
│   ├── INetTransport.cs                 전송 계층 인터페이스
│   ├── INetProtocol.cs                  프로토콜 계층 인터페이스
│   └── NetChannelBase.cs                ★ 핵심 추상 베이스 (Channel<T> + PriorityQueue)
│
├── Transport/                         ← 전송 계층
│   ├── NetTransportBase.cs              공통 상태 관리 추상 베이스
│   ├── TcpTransport.cs                  TCP 클라이언트
│   ├── UdpTransport.cs                  UDP (예정)
│   ├── SerialTransport.cs               COM 포트
│   ├── SharedMemoryTransport.cs         공유 메모리 IPC (MemoryMappedFiles)
│   ├── HttpTransport.cs                 HTTP/REST HttpClient 래핑 (예정)
│   └── MqttTransport.cs                 MQTT (예정)
│
├── Protocol/                          ← 프로토콜 계층
│   ├── RawProtocol.cs                   pass-through
│   ├── BinaryProtocol.cs                lssLib.Binary 기반 표준 프레임
│   └── ModbusRtuProtocol.cs             Modbus RTU (예정)
│
└── Channel/                           ← 통신 채널 구현체
    ├── PassiveNetChannel.cs             수동 수신 채널 (형태 1)
    └── RequestResponseChannel.cs        요청-응답 채널 (형태 2)
```

---

## 아키텍처 원칙

| 원칙 | 내용 |
|---|---|
| **3-레이어 조합** | 통신형태 + 전송계층 + 프로토콜을 독립 교체 |
| **통신 우선** | Write 는 항상 주기 Read 보다 먼저 처리 (PriorityQueue) |
| **Channel<T> 생산자-소비자** | 외부 WriteAsync 블로킹 없음. 단일 소비자 루프 |
| **interface + abstract class** | 통신 모듈 — 교체 가능성·Mock 테스트를 위해 interface 허용 |
| **BCL only** | lssLib.Log 만 참조. lssLib.Binary/Extensions 는 선택적 참조 |
| **자동 재접속** | 실패 시 지수 백오프 재접속 (MaxReconnectAttempts) |
| **Write 재전송** | Write 실패 시 Critical 우선순위로 재투입 (MaxWriteRetries) |

---

## 내부 데이터 흐름

```
[외부] WriteAsync(data)
    │   → Protocol.Encode(data)
    │   → Channel<NetPacket>.WriteAsync(Write, Priority=Write)
    │
[외부] RequestAsync(data)
    │   → Channel<NetPacket>.WriteAsync(Request, Priority=Write, Tcs=new TCS)
    │
[AsyncScheduler] 주기 Read
    │   → BuildReadRequestAsync()
    │   → Channel<NetPacket>.WriteAsync(PeriodicRead, Priority=Read)
    │
    ▼
Channel<NetPacket> ─────────── 진입 채널 (언바운드, 블로킹 없음)
    │
    ▼
ProcessQueueAsync (소비자 단일 루프)
    │
    ├─ 1. Ingress Channel → PriorityQueue (재정렬)
    │       Critical(0) > Write(1) > Read(2) > Low(3)
    │
    └─ 2. DispatchPacketAsync
            ├─ INetTransport.WriteAsync(encoded)
            │
            ├─ [Request] INetTransport.ReadAsync() → Protocol.TryDecode → Tcs.SetResult
            │
            └─ [PeriodicRead] INetTransport.ReadAsync() → Protocol.TryDecode
                    → _receiveChannel.Writer.TryWrite
                    → OnFrameReceivedAsync → FrameReceived 이벤트

[INetTransport.DataReceived] (Passive 수신 이벤트)
    │   → Protocol.TryDecode
    └─ → _receiveChannel.Writer.TryWrite
       → OnFrameReceivedAsync → FrameReceived 이벤트

[Write 실패]
    │   → RetryCount < MaxWriteRetries
    └─ → packet.ToRetry() → Priority=Critical → 재투입

[연결 끊김]
    └─ ReconnectAsync (지수 백오프, MaxReconnectAttempts)
```

---

## 빠른 시작

### 형태 1 — Passive 수신 (시리얼 센서)

```csharp
using lssLib.Net;
using lssLib.Net.Transport;
using lssLib.Net.Protocol;
using lssLib.Net.Channel;

var transport = new SerialTransport("COM3", 115200);
var protocol  = new BinaryProtocol(stx: 0xAA);
var channel   = new PassiveNetChannel(transport, protocol, NetConfig.Serial);

// 이벤트 구독
channel.FrameReceived += frame =>
{
    // lssLib.Binary: var result = frame.ToParser().Parse(SensorSchema.Default);
    // float temp = result.GetFloat("Temperature");
};

channel.StateChanged += state =>
    LogManager.Instance.Info("App", $"통신 상태: {state}");

await channel.StartAsync();

// --- 앱 종료 시 ---
await channel.DisposeAsync();
```

### 형태 2 — 요청-응답 (Modbus RTU 파생)

```csharp
public class ModbusChannel : RequestResponseChannel
{
    public ModbusChannel()
        : base(new SerialTransport("COM3", 9600),
               new RawProtocol(),
               NetConfig.Serial with { PeriodicReadInterval = TimeSpan.FromMilliseconds(50) }) { }

    // 주기적으로 보낼 읽기 요청 정의
    protected override Task<byte[]?> BuildReadRequestAsync(CancellationToken ct)
    {
        // Modbus FC=0x03 (lssLib.Binary.BufferWriter 사용 예시)
        // byte[] req = BufferWriter.Create()
        //     .WriteUInt8(0x01).WriteUInt8(0x03)
        //     .WriteUInt16BE(0).WriteUInt16BE(10)
        //     .AppendCrc16Modbus().ToArray();
        return Task.FromResult<byte[]?>(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A });
    }
}

var channel = new ModbusChannel();
await channel.StartAsync();

// Write 즉시 우선 전송 (setpoint 변경 등)
await channel.WriteAsync(setpointFrame, NetPriority.Write);

// 단발 요청-응답 (3초 타임아웃)
NetResult r = await channel.RequestAsync(queryFrame, TimeSpan.FromSeconds(3));
if (r.IsOk)
    LogManager.Instance.Info("Modbus", $"응답: {r.Data!.Length}B");
```

### 공유 메모리 IPC

```csharp
// 프로세스 A (센서 데이터 공급자)
var tx = new SharedMemoryTransport("lssLib_Sensor", SharedMemoryRole.Writer);
await tx.ConnectAsync();
await tx.WriteAsync(sensorFrame);

// 프로세스 B (소비자)
var rx       = new SharedMemoryTransport("lssLib_Sensor", SharedMemoryRole.Reader);
var channel  = new PassiveNetChannel(rx, new RawProtocol(), NetConfig.SharedMemory);
channel.FrameReceived += frame => ProcessSensorData(frame);
await channel.StartAsync();
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
                                                    lssLib.Net           ← 이 모듈
                                                    (NetChannelBase
                                                     INetTransport
                                                     INetProtocol)
```

`lssLib.Net`은 `lssLib.Log`만 의존합니다.  
`lssLib.Binary` / `lssLib.Extensions`는 프로토콜 구현 시 선택적으로 참조합니다.

---

## 설정 항목 (NetConfig)

| 항목 | 기본값 | 설명 |
|---|---|---|
| `AutoReconnect` | `true` | 연결 끊김 시 자동 재접속 |
| `MaxReconnectAttempts` | `5` | 재접속 최대 횟수 (0=무제한) |
| `ReconnectDelay` | `2s` | 재접속 기준 대기 (지수 백오프) |
| `HeartbeatInterval` | `Zero` | Heartbeat 간격 (Zero=비활성) |
| `PeriodicReadInterval` | `100ms` | 주기 Read 간격 (RequestResponse) |
| `RequestTimeout` | `3s` | 단발 요청 타임아웃 |
| `MaxWriteRetries` | `3` | Write 실패 재전송 횟수 |
| `RetryDelay` | `50ms` | 재전송 대기 시간 |
| `ReceiveChannelCapacity` | `0` | 수신 Channel 용량 (0=무제한) |

### 환경별 프리셋

```csharp
NetConfig.Serial       // COM 포트 / 저속 산업 통신
NetConfig.Tcp          // TCP 클라이언트
NetConfig.Udp          // UDP 비연결형
NetConfig.SharedMemory // 공유 메모리 고속 IPC
```

---

## 주의 사항

| 항목 | 내용 |
|---|---|
| `FrameReceived` / `StateChanged` | 백그라운드 스레드 → WPF UI 접근 시 `Dispatcher.InvokeAsync` 필요 |
| `StopAsync()` | 앱 종료 시 반드시 호출. 큐 잔여 항목 처리 후 종료 |
| `DisposeAsync()` | `StopAsync` 포함. `await using` 패턴 권장 |
| `ReceiveChannelCapacity` | 0=무제한(안전), 양수=초과분 오래된 항목부터 삭제 |
| `SharedMemoryTransport` | 같은 머신 내 프로세스 간 전용. 네트워크 통신 불가 |

---

*lssLib.Net · .NET 8.0 · C# 12 · interface + abstract class 허용 (통신 모듈 원칙)*
