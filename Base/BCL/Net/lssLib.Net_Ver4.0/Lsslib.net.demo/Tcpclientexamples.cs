// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Demo/TcpClientExamples.cs
//  역할: TcpTestServer 와 연동하는 TCP 클라이언트 테스트 예제
//
//  ┌─ 사전 준비 ──────────────────────────────────────────────────────┐
//  │  1. 4-lssLib.Net.TcpTestServer 빌드 및 실행                     │
//  │  2. Example4  ← 서버: Push 모드  / 포트: 9000                   │
//  │     Example5A ← 서버: Echo 모드  / 포트: 9000                   │
//  │     Example5B ← 서버: Echo 모드  / 포트: 9000                   │
//  │     Example5C ← 서버: Echo 모드  / 포트: 9000                   │
//  └──────────────────────────────────────────────────────────────────┘
//
//  ┌─ Passive vs RequestResponse 핵심 차이 ───────────────────────────┐
//  │                                                                   │
//  │  PassiveNetChannel (Passive 모드)                                 │
//  │    → 서버가 먼저 데이터를 보내옴 (Push 서버 연동)                │
//  │    → TcpTransport.FromConfig(cfg, enablePassiveReceive: true)    │
//  │       ← 반드시 true 지정: 백그라운드 수신 루프 활성화           │
//  │    → PeriodicInterval = TimeSpan.Zero (주기 Read 없음)           │
//  │    → 수신: PassiveReceiveLoopAsync → PushReceived → 이벤트      │
//  │                                                                   │
//  │  RequestResponseChannel (RequestResponse 모드)                    │
//  │    → 클라이언트가 요청 → 서버가 응답 (Echo 서버 연동)            │
//  │    → TcpTransport.FromConfig(cfg)  ← enablePassiveReceive 기본값 false │
//  │    → PeriodicInterval 주기로 ReadCommand 자동 전송               │
//  │    → 수신: DispatchAsync WriteAsync → ReadAsync → 이벤트        │
//  └───────────────────────────────────────────────────────────────────┘
//
//  ┌─ 페이로드 구조 ──────────────────────────────────────────────────┐
//  │  [Push 수신 payload 12B]                                          │
//  │    [FrameId:uint 4B LE][Temp:float 4B LE][Hum:float 4B LE]       │
//  │                                                                   │
//  │  [Echo 요청 payload 3B]                                           │
//  │    [FC:0x03][AddrHi:0x00][AddrLo:0x00]                           │
//  │                                                                   │
//  │  [Echo 응답 payload 14B]                                          │
//  │    [FC:0x03][DataLen:0x0C]                                        │
//  │    [FrameId:uint 4B LE][Temp:float 4B LE][Hum:float 4B LE]       │
//  └──────────────────────────────────────────────────────────────────┘
//  READ_CMD / WRITE_CMD / Heartbeat 개념:
//
//  WRITE_CMD : WriteAsync(cmd) → _stream.WriteAsync   [응답 없음]
//              Write(1) 우선순위 → READ_CMD 보다 항상 먼저
//
//  READ_CMD  : cfg.AddReadCommand(cmd) + PeriodicInterval 설정
//              → 100ms마다 _stream.WriteAsync(cmd) + _stream.ReadAsync()
//              → DeviceFrameReceived 이벤트 (응답 있음, 타임아웃 적용)
//              ★ Echo 서버 전용 — Push 서버와 혼용 불가
//
//  Heartbeat : HeartbeatInterval=30s
//              → 30초 통신 공백 시 _stream.WriteAsync(hb)  [응답 없음]
//              Low(3) 최저 우선순위 → Write/Read 중에는 전송 안 함
//
//  재접속    : 서버 강제 종료 → 2초 내 자동 재접속 시작
//              DeviceErrorOccurred 이벤트: "재접속 1/∞ — 즉시 시도 중..."
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;
using lssLib.Net.Implementation;

namespace lssLib.Net.Demo;

// ══════════════════════════════════════════════════════════════════════
//  예시 4 — TCP Passive 클라이언트 (Push 서버)
//  서버: TcpTestServer → Push 모드 → 포트 9000
//  ★ enablePassiveReceive: true 필수
//  ★ PeriodicInterval = Zero (READ_CMD 없음)
//  ★ 서버 재시작 후 자동 재접속됨
// ══════════════════════════════════════════════════════════════════════
static class Example4_TcpPassiveClient
{
    private const int PAYLOAD_SIZE = 12;

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Example4: TCP Passive (Push 서버) ===");
        Console.WriteLine("서버: TcpTestServer → Push 모드 → 포트 9000\n");

        var cfg = new TcpDeviceConfig(4, "PushServer", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect,
            MaxRetries = 0,                    // 무제한 재시도
            RetryDelay = TimeSpan.FromSeconds(2),
            ReconnectBackoff = true,                 // 2→4→8→...→60초
            PeriodicInterval = TimeSpan.Zero,        // READ_CMD 없음
            HeartbeatInterval = TimeSpan.Zero,
        };

        await using var channel = new PassiveNetChannel(
            cfg,
            TcpTransport.FromConfig(cfg, enablePassiveReceive: true),  // ← 필수
            new BinaryProtocol(stx: 0xAA, fc: 0x01),
            autoRegister: true);

        channel.DeviceFrameReceived += OnFrameReceived;  // ← 데이터 받는 함수
        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example4] 상태: {state}");
        channel.DeviceErrorOccurred += (id, ex) =>       // ← 재접속 진행 상황 포함
            Console.WriteLine($"[Example4] {ex.Message}");

        Console.WriteLine("[Example4] 접속 중...");
        await channel.StartAsync(ct);
        Console.WriteLine("[Example4] 연결됨 — Push 수신 대기 중");
        Console.WriteLine("[Example4] 서버 강제 종료 후 재시작하면 자동 재접속됩니다\n");

        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }
    }

    // ★ 서버 Push 데이터를 실제로 받는 함수
    // ⚠ 백그라운드 스레드 — WPF: Dispatcher.InvokeAsync 필수
    private static void OnFrameReceived(int id, byte[] frame)
    {
        if (frame.Length < PAYLOAD_SIZE) return;
        uint frameId = BitConverter.ToUInt32(frame, 0);
        float temp = BitConverter.ToSingle(frame, 4);
        float hum = BitConverter.ToSingle(frame, 8);
        Console.WriteLine(
            $"[Example4] Frame#{frameId:D5} | " +
            $"온도: {temp,5:F1}°C | 습도: {hum,5:F1}%  ({DateTime.Now:HH:mm:ss.fff})");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 5A — TCP RequestResponse / 주기 READ_CMD
//  서버: TcpTestServer → Echo 모드 → 포트 9000
//
//  ★ READ_CMD 동작:
//    cfg.AddReadCommand(READ_CMD) + PeriodicInterval=100ms
//    → 100ms마다: WriteAsync(READ_CMD) → ReadAsync() → DeviceFrameReceived
//
//  ★ READ_CMD 가 동작하지 않을 때 체크 항목:
//    1. cfg.AddReadCommand(READ_CMD) 호출 여부
//    2. PeriodicInterval 이 Zero 가 아닌지 확인
//    3. Echo 서버인지 확인 (Push 서버는 응답 안 함 → ReadAsync 블로킹)
// ══════════════════════════════════════════════════════════════════════
static class Example5A_TcpRequestResponse_Periodic
{
    private const int RESPONSE_SIZE = 14;
    // READ_CMD: [FC=0x03][AddrHi=0x00][AddrLo=0x00]
    private static readonly byte[] READ_CMD = [0x03, 0x00, 0x00];

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Example5A: TCP RequestResponse (주기 READ_CMD) ===");
        Console.WriteLine("서버: TcpTestServer → Echo 모드 → 포트 9000\n");

        var cfg = new TcpDeviceConfig(5, "EchoServer", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 0,
            RetryDelay = TimeSpan.FromSeconds(2),
            ReconnectBackoff = true,
            IsSequential = true,
            // ★ READ_CMD 주기 — 0 이면 READ_CMD 전송 안 됨
            PeriodicInterval = TimeSpan.FromMilliseconds(100),
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            HeartbeatInterval = TimeSpan.Zero,
            // HeartbeatInterval = TimeSpan.FromSeconds(30),  // 활성화 예시
        };
        // ★ READ_CMD 등록 — PeriodicInterval 마다 자동 전송
        cfg.AddReadCommand(READ_CMD);

        await using var channel = new RequestResponseChannel(
            cfg,
            TcpTransport.FromConfig(cfg),  // enablePassiveReceive 기본 false
            new BinaryProtocol(stx: 0xAA, fc: 0x01),
            autoRegister: true);

        channel.DeviceFrameReceived += OnFrameReceived;  // ← READ_CMD 응답 받는 함수
        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example5A] 상태: {state}");
        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example5A] {ex.Message}");

        await channel.StartAsync(ct);
        Console.WriteLine($"[Example5A] 연결됨 — {cfg.PeriodicInterval.TotalMilliseconds}ms 주기 READ_CMD 시작\n");

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(5000, ct); } catch { break; }
                var s = channel.Statistics;
                Console.WriteLine(
                    $"[Example5A][통계] 전송={s.TotalSent} 수신={s.TotalReceived} " +
                    $"오류={s.TotalErrors} 재접속={s.TotalReconnects} " +
                    $"평균응답={s.AvgResponseMs:F1}ms");
            }
        }, ct);

        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }
    }

    private static void OnFrameReceived(int id, byte[] frame)
    {
        if (frame.Length < RESPONSE_SIZE) return;
        uint frameId = BitConverter.ToUInt32(frame, 2);
        float temp = BitConverter.ToSingle(frame, 6);
        float hum = BitConverter.ToSingle(frame, 10);
        Console.WriteLine(
            $"[Example5A] Frame#{frameId:D5} | " +
            $"온도: {temp,5:F1}°C | 습도: {hum,5:F1}%  ({DateTime.Now:HH:mm:ss.fff})");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 5B — TCP RequestResponse / 단발 RequestAsync
//  서버: TcpTestServer → Echo 모드 → 포트 9000
// ══════════════════════════════════════════════════════════════════════
static class Example5B_TcpRequestResponse_OneShot
{
    private static readonly byte[] QUERY = [0x03, 0x00, 0x00];

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Example5B: TCP RequestResponse (단발 RequestAsync) ===");
        Console.WriteLine("서버: TcpTestServer → Echo 모드 → 포트 9000\n");

        var cfg = new TcpDeviceConfig(6, "EchoServer-OneShot", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 0,
            ReconnectBackoff = true,
            PeriodicInterval = TimeSpan.Zero,   // READ_CMD 없음
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            HeartbeatInterval = TimeSpan.Zero,
        };

        await using var channel = new RequestResponseChannel(
            cfg, TcpTransport.FromConfig(cfg),
            new BinaryProtocol(stx: 0xAA, fc: 0x01), autoRegister: true);

        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example5B] 상태: {state}");
        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example5B] {ex.Message}");

        await channel.StartAsync(ct);
        Console.WriteLine("[Example5B] 연결됨 — 단발 RequestAsync 10회 테스트\n");

        for (int i = 1; i <= 30 && !ct.IsCancellationRequested; i++)
        {
            NetResult r = await channel.RequestAsync(QUERY,
                timeout: TimeSpan.FromMilliseconds(500), ct: ct);

            if (r.IsOk && r.Data!.Length >= 14)
            {
                uint frameId = BitConverter.ToUInt32(r.Data, 2);
                float temp = BitConverter.ToSingle(r.Data, 6);
                float hum = BitConverter.ToSingle(r.Data, 10);
                Console.WriteLine(
                    $"[Example5B][{i:D2}] Frame#{frameId:D5} 온도: {temp:F1}°C 습도: {hum:F1}%");
            }
            else
                Console.WriteLine($"[Example5B][{i:D2}] 실패: {r.Error?.Message}");

            await Task.Delay(200, ct);
        }

        var s = channel.Statistics;
        Console.WriteLine(
            $"[Example5B] 완료 | 전송={s.TotalSent} 수신={s.TotalReceived} " +
            $"평균응답={s.AvgResponseMs:F1}ms");


    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 5C — WRITE_CMD + READ_CMD + Heartbeat 혼용
//  서버: TcpTestServer → Echo 모드 → 포트 9000
//
//  우선순위: Critical(0) > WRITE_CMD Write(1) > READ_CMD Read(2) > Heartbeat Low(3)
// ══════════════════════════════════════════════════════════════════════
static class Example5C_TcpWriteAndRead
{
    private static readonly byte[] WRITE_CMD = [0x06, 0x00, 0x01, 0x00, 0x64];
    private static readonly byte[] READ_CMD = [0x03, 0x00, 0x00];

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Example5C: WRITE_CMD + READ_CMD + Heartbeat ===");
        Console.WriteLine("서버: TcpTestServer → Echo 모드 → 포트 9000\n");

        var cfg = new TcpDeviceConfig(7, "EchoServer-RW", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 0,
            ReconnectBackoff = true,
            IsSequential = true,
            PeriodicInterval = TimeSpan.FromMilliseconds(200),
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            // ★ Heartbeat: READ_CMD 진행 중에는 Low(3) 우선순위로 전송 안 됨
            //   PeriodicInterval=Zero 로 테스트하면 30초 후 Heartbeat 확인 가능
            HeartbeatInterval = TimeSpan.FromSeconds(30),
        };
        cfg.AddReadCommand(READ_CMD);

        await using var channel = new RequestResponseChannel(
            cfg, TcpTransport.FromConfig(cfg),
            new BinaryProtocol(stx: 0xAA, fc: 0x01), autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) =>
        {
            if (frame.Length >= 14)
            {
                uint frameId = BitConverter.ToUInt32(frame, 2);
                float temp = BitConverter.ToSingle(frame, 6);
                Console.WriteLine($"[Example5C] READ 응답 Frame#{frameId:D5} 온도: {temp:F1}°C");
            }
        };
        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example5C] {ex.Message}");
        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example5C] 상태: {state}");

        await channel.StartAsync(ct);
        Console.WriteLine("[Example5C] 연결됨\n");

        // WRITE_CMD: Write(1) 우선순위 → READ_CMD(2) 보다 먼저 처리
        await channel.WriteAsync(WRITE_CMD, NetPriority.Write, false, ct);
        Console.WriteLine("[Example5C] WRITE_CMD 전송");

        // Critical: 최우선
        await channel.WriteAsync([0xFF, 0x00], NetPriority.Critical, false, ct);
        Console.WriteLine("[Example5C] Emergency Stop (Critical)\n");

        // 단발 RequestAsync
        await Task.Delay(1000, ct);
        NetResult r = await channel.RequestAsync(READ_CMD,
            timeout: TimeSpan.FromMilliseconds(500), ct: ct);
        Console.WriteLine(r.IsOk
            ? $"[Example5C] 단발 읽기: {r.Data!.Length}B"
            : $"[Example5C] 단발 읽기 실패: {r.Error!.Message}");

        await Task.Delay(5000, ct);
        var s = channel.Statistics;
        Console.WriteLine(
            $"\n[Example5C] 완료 | 전송={s.TotalSent} 수신={s.TotalReceived} " +
            $"재접속={s.TotalReconnects} 평균응답={s.AvgResponseMs:F1}ms");


        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 6 — Registry 다중 채널 (Push 서버 + Echo 서버 동시 운용)
//
//  Push 서버(9000) — PassiveNetChannel
//  Echo 서버(9001) — RequestResponseChannel  ← 별도 TcpTestServer 인스턴스 필요
// ══════════════════════════════════════════════════════════════════════
static class Example6_MultiChannel_Registry
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine(" Example6: Registry 다중 채널");
        Console.WriteLine(" Push 서버: 포트 9000 / Echo 서버: 포트 9001");
        Console.WriteLine("=================================================");

        // Push 서버(9000) — Passive 채널
        var pushCfg = new TcpDeviceConfig(10, "Push-9000", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect,
            MaxRetries = 0,                               // 0 = 무제한 재시도
            RetryDelay = TimeSpan.FromSeconds(2),
            ReconnectBackoff = true,                            // 2s → 4s → 8s ... 최대 60s

            PeriodicInterval = TimeSpan.Zero,
            HeartbeatInterval = TimeSpan.Zero,
        };

        // Echo 서버(9001) — RequestResponse 채널
        var echoCfg = new TcpDeviceConfig(11, "Echo-9001", "127.0.0.1", 9001)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect,
            MaxRetries = 0,                               // 0 = 무제한 재시도
            RetryDelay = TimeSpan.FromSeconds(2),
            ReconnectBackoff = true,                            // 2s → 4s → 8s ... 최대 60s

            PeriodicInterval = TimeSpan.FromMilliseconds(200),
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            HeartbeatInterval = TimeSpan.FromSeconds(30),   // 30초 Heartbeat
        };
        echoCfg.AddReadCommand([0x03, 0x00, 0x00]);

        // autoRegister=true → Registry 자동 등록
        var passiveCh = new PassiveNetChannel(
            pushCfg,
            TcpTransport.FromConfig(pushCfg, enablePassiveReceive: true),  // ← 필수
            new BinaryProtocol(stx: 0xAA),
            autoRegister: true);

        var rrCh = new RequestResponseChannel(
            echoCfg,
            TcpTransport.FromConfig(echoCfg),
            new BinaryProtocol(stx: 0xAA),
            autoRegister: true);

        // 이벤트 구독
        passiveCh.DeviceFrameReceived += (id, frame) =>
        {
            if (frame.Length >= 12)
            {
                uint frameId = BitConverter.ToUInt32(frame, 0);
                float temp = BitConverter.ToSingle(frame, 4);
                Console.WriteLine($"[Registry][{id}:Push] Frame#{frameId:D5} 온도: {temp:F1}°C");
            }
        };

        rrCh.DeviceFrameReceived += (id, frame) =>
            Console.WriteLine($"[Registry][{id}:Echo] 응답: {frame.Length}B");

        // Registry 일괄 시작
        await NetDeviceRegistry.Instance.StartAllAsync(ct);
        Console.WriteLine($"[Registry] 전체 채널 시작 | " +
                          $"등록: {NetDeviceRegistry.Instance.Count}개 / " +
                          $"연결: {NetDeviceRegistry.Instance.ConnectedCount}개\n");

        await Task.Delay(5000, ct);

        // DeviceId 로 개별 접근
        var echo = NetDeviceRegistry.Instance.Get(11);
        if (echo?.IsConnected == true)
        {
            await echo.WriteAsync([0x06, 0x00, 0x01, 0x00, 0x64], NetPriority.Write, false, ct);
            Console.WriteLine("[Registry] Echo 채널 Write 전송");
        }

        // 전체 상태 조회
        Console.WriteLine("\n[Registry] 전체 상태:");
        foreach (var status in NetDeviceRegistry.Instance.GetStatusAll())
        {
            Console.WriteLine(
                $"  [{status.DeviceId}:{status.DeviceName}] " +
                $"상태={status.State} " +
                $"전송={status.TotalSent} 수신={status.TotalReceived} " +
                $"오류={status.TotalErrors} 응답={status.AvgResponseMs:F1}ms");
        }

        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }

        Console.WriteLine("[Example6] 종료");

        // 전체 정지
        await NetDeviceRegistry.Instance.StopAllAsync();
        Console.WriteLine("\n[Registry] 전체 채널 정지 완료");
    }
}