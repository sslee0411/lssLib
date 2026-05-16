# lssLib.NetSequence

**lssLib.Net 전용 시퀀스 제어 모듈** · .NET 8.0-windows · C# 12

---

## 솔루션 구성 (lssLib.NetSequence.sln)

```
lssLib.NetSequence.sln
│
├── lssLib.Sequence     ← 참조 (범용 엔진, BCL only)
├── lssLib.Net          ← 참조 (통신 모듈)
│
├── lssLib.NetSequence/ ── 이 모듈
│   ├── Steps/              NetWriteStep · NetRequestStep · NetDelayStep
│   │                       : NetSequenceStepBase : SequenceStepBase
│   ├── Context/            NetSequenceContext
│   │                       : SequenceContextBase → NetDeviceRegistry 브리지
│   ├── Builder/            NetSequence (진입점)
│   │                       NetSequenceBuilder    (단일 장비 Fluent)
│   │                       NetGroupSequenceBuilder (다중 장비 그룹 Fluent)
│   └── Controller/         NetSequenceController
│                           : SequenceControllerBase (추가 구현 없음)
│
└── lssLib.NetSequence.Demo/ ── 시퀀스 예제 (S1~S7)
    ├── Program.cs
    └── NetSequenceExamples.cs
```

---

## 3개 솔루션 전체 구조

```
┌─────────────────────────────────────────────────────────────────┐
│  lssLib.Sequence.sln                                            │
│    lssLib.Sequence    (범용 엔진, BCL only)                     │
│    lssLib.Sequence.Demo  (D1~D5)                                │
├─────────────────────────────────────────────────────────────────┤
│  lssLib.Net.sln                                                 │
│    lssLib.Net    (통신 전용, Sequence 참조 없음)                │
│    lssLib.Net.Demo  (Ex01~Ex11 Transport 예제)                  │
├─────────────────────────────────────────────────────────────────┤
│  lssLib.NetSequence.sln              ← 이 솔루션               │
│    lssLib.Sequence  ◄ 참조           (범용 엔진)                │
│    lssLib.Net       ◄ 참조           (통신 모듈)                │
│    lssLib.NetSequence                (Net 시퀀스 제어)          │
│    lssLib.NetSequence.Demo  (S1~S7)                             │
└─────────────────────────────────────────────────────────────────┘
```

---

## 빠른 시작

```csharp
using lssLib.Net;
using lssLib.NetSequence;
using lssLib.Sequence;

// 1. 채널 생성 (autoRegister: true 필수)
var cfg = new SerialDeviceConfig(1, "Modbus-PLC", "COM3", 9600);
await using var channel = new RequestResponseChannel(
    cfg, SerialTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true);
await channel.StartAsync();

// 2. 시퀀스 정의
var seq = NetSequence.For(deviceId: 1)
    .Write("초기화",       [0x01, 0x06, 0x00, 0x00, 0x00, 0x00])
    .Delay(200)
    .Write("모터 기동",    [0x01, 0x06, 0x00, 0x01, 0x00, 0x01])
    .Request("상태 확인",  [0x01, 0x03, 0x00, 0x64, 0x00, 0x01],
        validator: r => r.IsOk && r.Data![4] == 0x01,
        timeoutMs: 500, retries: 3)
    .Build("모터 시퀀스");

// 3. 실행
var controller = new NetSequenceController();
var context    = new NetSequenceContext();

controller.StepCompleted += r =>
    Console.WriteLine($"{(r.IsSuccess ? "✔" : "✘")} {r.Step.StepName}");

SequenceResult result = await controller.RunAsync(seq, context);
Console.WriteLine(result);
```

---

## 데모 예제 (S1~S7)

| 메뉴 | 예제 | 하드웨어 |
|---|---|---|
| S1 | 단일 장비 순차 Write | 필요 |
| S2 | Request 응답 검증 + 재시도 | 필요 |
| S3 | 다중 장비 순차 연계 | 필요 |
| S4 | 다중 장비 병렬 연계 | 필요 |
| S5 | 혼합 그룹 (도장 공정) | 필요 |
| S6 | 배치 실행 (RunAllAsync) | 필요 |
| **S7** | **Virtual 전체 시연 ★** | **불필요** |

---

*lssLib.NetSequence · .NET 8.0 · lssLib.Sequence + lssLib.Net 조합*
