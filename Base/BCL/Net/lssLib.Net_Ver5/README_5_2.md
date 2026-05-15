# lssLib.Net v5.1

**통신 모듈** · .NET 8.0-windows · C# 12 · BCL only (Serial 제외)

---

## 솔루션 구성

```
lssLib.Net.v5.sln
│
├── lssLib.Sequence/              ── 범용 시퀀스 엔진 (BCL only, Net 미참조)
│   ├── Core/                        SequenceEnums · SequenceResults
│   ├── Contracts/                   ISequenceStep · ISequenceContext · ISequenceExecutor
│   ├── Abstractions/                SequenceStepBase · SequenceContextBase
│   │                                SequenceBase · SequenceControllerBase
│   └── Builder/                     SequenceBuilderBase (단일/그룹 Fluent 빌더)
│
├── lssLib.Net/                   ── 통신 모듈 (lssLib.Sequence 참조)
│   ├── Core/        (6)          NetMode · NetState · NetPriority · NetResult
│   │                             NetPacket · PacketMode
│   ├── Config/      (7)          NetDeviceConfig★ · NetTransportType · RetryTarget
│   │                             TcpDeviceConfig★ · SerialDeviceConfig★
│   │                             UdpDeviceConfig★ · SharedMemDeviceConfig★
│   ├── Contracts/   (2)          INetTransport · INetProtocol
│   ├── Transport/  (10)          NetTransportBase + 9가지
│   │                             (NamedPipe/Http/WebSocket/Mqtt/Virtual Config 포함)
│   ├── Protocol/    (2)          BinaryProtocol · RawProtocol
│   ├── Channels/    (3)          NetChannelBase · PassiveNetChannel
│   │                             RequestResponseChannel
│   ├── Infrastructure/(5)        NetConnectionManager · NetDispatchPipeline
│   │                             NetScheduler★ · NetStatistics · NetDeviceRegistry
│   └── Sequence/    (4)          NetSequenceStep · NetSequenceContext
│                                 NetSequenceBuilder (NetSequence 진입점 포함)
│                                 NetSequenceController
│
└── lssLib.Net.Demo/              ── 콘솔 데모
    ├── Program.cs                   Transport(1~11) + Sequence(S1~S7) 메뉴
    ├── AllTransportExamples.cs      Ex01~Ex11
    └── SequenceExamples.cs          SeqEx01~SeqEx07
```

★ = v5.1 변경 파일

---

## 프로젝트 의존성

```
lssLib.Sequence  ←── BCL only, 독립
      ▲
      │ ProjectReference
lssLib.Net       ←── System.IO.Ports 추가
      ▲
      │ ProjectReference
lssLib.Net.Demo
```

---

## 시퀀스 제어 — 상속 구조

```
lssLib.Sequence (범용)          lssLib.Net (구현체)
─────────────────────────────   ──────────────────────────────
SequenceStepBase            ←── NetSequenceStepBase
                                  ├─ NetWriteStep
                                  ├─ NetRequestStep
                                  └─ NetDelayStep

SequenceContextBase         ←── NetSequenceContext
                                  → NetDeviceRegistry 브리지

SequenceControllerBase      ←── NetSequenceController
                                  (추가 구현 없음, 훅만 override)

SequenceBuilderBase         ←── NetSequenceBuilder
GroupSequenceBuilderBase    ←── NetGroupSequenceBuilder

[진입점]  NetSequence.For(id)    → NetSequenceBuilder
          NetSequence.Create()   → NetGroupSequenceBuilder
```

---

## 빠른 시작 — 시퀀스 제어

### 단일 장비

```csharp
using lssLib.Net;
using lssLib.Sequence;

// 채널 생성 (autoRegister: true 필수)
var cfg = new SerialDeviceConfig(1, "Modbus-PLC", "COM3", 9600);
await using var channel = new RequestResponseChannel(
    cfg, SerialTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true);
await channel.StartAsync();

// 시퀀스 정의
var seq = NetSequence.For(deviceId: 1)
    .Write("초기화",       [0x01, 0x06, 0x00, 0x00, 0x00, 0x00])
    .Delay(200,            "안정화 대기")
    .Write("모터 기동",    [0x01, 0x06, 0x00, 0x01, 0x00, 0x01])
    .Request("상태 확인",  [0x01, 0x03, 0x00, 0x64, 0x00, 0x01],
        validator: r => r.IsOk && r.Data![4] == 0x01,
        timeoutMs: 500, retries: 3)
    .Build("모터 기동 시퀀스", totalTimeoutMs: 10_000);

// 실행
var controller = new NetSequenceController();
var context    = new NetSequenceContext(
    logAction: msg => LogManager.Instance.Info("Seq", msg));

controller.StepCompleted += r =>
    Console.WriteLine($"  {(r.IsSuccess ? "✔" : "✘")} {r}");

SequenceResult result = await controller.RunAsync(seq, context);
Console.WriteLine(result);
```

### 다중 장비 연계

```csharp
var seq = NetSequence.Create("공정 시퀀스")

    // 그룹1: 순차 — 장비 1→2 순서 보장
    .Then(StepExecutionMode.Sequential, "초기화")
        .AddWrite(1, "밸브 열기",  [0x01, 0x06, 0x00, 0x01, 0x00, 0x01])
        .AddDelay(300)
        .AddWrite(2, "펌프 기동",  [0x02, 0x06, 0x00, 0x01, 0x00, 0x01])

    // 그룹2: 병렬 — 장비 3, 4 동시 확인
    .Then(StepExecutionMode.Parallel, "상태 확인")
        .AddRequest(3, "압력 확인", [0x03, 0x03, ...], r => r.IsOk)
        .AddRequest(4, "온도 확인", [0x04, 0x03, ...], r => r.IsOk)

    .Build(totalTimeout: TimeSpan.FromSeconds(30));

// 배치 실행 (여러 시퀀스 순서대로)
SequenceBatchResult batch = await controller.RunAllAsync(
    [seqA, seqB, seqC], context, continueOnError: false);
```

---

## SequenceMode (v5.1)

| 값 | 상수 | 동작 | 기본 환경 |
|---|---|---|---|
| 0 | `Parallel` | 모든 ReadCommands 동시 (Task.WhenAll) | TCP / UDP / HTTP / WS / MQTT |
| 1 | `Sequential` | 1개씩 순서대로 | Serial / NamedPipe |
| N≥2 | — | 슬라이딩 윈도우 N개 동시 | 멀티드롭 최적화 |

---

## Transport 목록

| # | Transport | 기본 SequenceMode | 용도 |
|---|---|---|---|
| 1 | TCP | 0 Parallel | Modbus TCP, 산업 제어 |
| 2 | Serial | **1 Sequential** | RS-485, Modbus RTU |
| 3 | UDP | 0 Parallel | 브로드캐스트, 센서 |
| 4 | SharedMemory | 0 Parallel | 동일 PC IPC |
| 5 | NamedPipe | **1 Sequential** | 프로세스 간 IPC |
| 6 | HTTP | 0 Parallel | REST API |
| 7 | WebSocket | 0 Parallel | 실시간 양방향 |
| 8 | MQTT | 0 Parallel | IoT 메시징 |
| 9 | Virtual ★ | 0 Parallel | 테스트·시뮬레이터 |

---

## 데모 예제

| 메뉴 | 예제 | 하드웨어 |
|---|---|---|
| 1~11 | Transport 예제 (Ex01~Ex11) | 각 환경 필요 |
| S1 | 단일 장비 순차 Write | 필요 |
| S2 | Request 응답 검증 + 재시도 | 필요 |
| S3 | 다중 장비 순차 연계 | 필요 |
| S4 | 다중 장비 병렬 연계 | 필요 |
| S5 | 혼합 그룹 (공정 시나리오) | 필요 |
| S6 | 배치 실행 (공정A→B→C) | 필요 |
| **S7** | **Virtual 전체 시연 ★** | **불필요** |

---

## Ver History

| 버전 | 내용 |
|---|---|
| v4 | 기본 TCP/Serial/UDP/SharedMemory 4종 Transport |
| v5 | 9종 Transport 통합, NetDeviceConfig 단일화, Channel 4채널 파이프라인 |
| v5.1 | IsSequential(bool) → **SequenceMode(int)** 변경 (Parallel/Sequential/Window(N)), lssLib.Sequence 분리·통합 |

---

*lssLib.Net v5.1 · lssLib.Sequence v1.0 · .NET 8.0*
