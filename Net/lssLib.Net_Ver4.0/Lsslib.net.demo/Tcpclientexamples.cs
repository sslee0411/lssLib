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
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;
using lssLib.Net.Implementation;

namespace lssLib.Net.Demo;

// ══════════════════════════════════════════════════════════════════════
//  예시 4 — TCP Passive 클라이언트 (Push 서버 연동)
//
//  [TcpTestServer] Push 모드 / 포트 9000
//  [이 클라이언트] PassiveNetChannel — 서버 Push 프레임 수신만 담당
//
//  ★ 데이터가 오는 경로:
//    서버 stream.WriteAsync(frame)
//      → [TcpTransport] PassiveReceiveLoopAsync: stream.ReadAsync
//        → RaiseDataReceived(bytes)
//          → [NetChannelBase] OnDataReceived
//            → [NetDispatchPipeline] PushReceived
//              → BinaryProtocol.TryDecode
//                → FrameReceived 이벤트
//                  → DeviceFrameReceived 이벤트  ← 여기서 받음
//
//  ★ 데이터 받는 함수: channel.DeviceFrameReceived 이벤트 핸들러
//    또는: await foreach (var frame in channel.ReadAllAsync(ct))
// ══════════════════════════════════════════════════════════════════════
static class Example4_TcpPassiveClient
{
    private const int OFFSET_FRAME_ID = 0;   // uint  4B LE
    private const int OFFSET_TEMPERATURE = 4;   // float 4B LE
    private const int OFFSET_HUMIDITY = 8;    // float 4B LE
    private const int PAYLOAD_SIZE = 12;

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine(" Example4: TCP Passive 클라이언트");
        Console.WriteLine(" 서버: TcpTestServer → Push 모드 → 포트 9000");
        Console.WriteLine("=================================================");

        // §1. 설정
        var cfg = new TcpDeviceConfig(4, "PushServer", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect,
            MaxRetries = 0,                               // 0 = 무제한 재시도
            RetryDelay = TimeSpan.FromSeconds(2),
            ReconnectBackoff = true,                            // 2s → 4s → 8s ... 최대 60s

            // ★ Passive 모드 필수 설정
            PeriodicInterval = TimeSpan.Zero,                  // 주기 Read 없음
            HeartbeatInterval = TimeSpan.Zero,                  // Heartbeat 없음
        };

        // §2. 채널 조립
        // ★ 핵심: enablePassiveReceive: true
        //   이 옵션이 없으면 서버가 Push 해도 클라이언트에서 수신 안 됨
        //   TCP 는 Serial 과 달리 DataReceived 이벤트가 없으므로
        //   별도 백그라운드 수신 루프(PassiveReceiveLoopAsync)가 필요함
        await using var channel = new PassiveNetChannel(
            cfg,
            TcpTransport.FromConfig(cfg, enablePassiveReceive: true),   // ← 필수
            new BinaryProtocol(stx: 0xAA, fc: 0x01),
            autoRegister: true);

        // §3. 이벤트 구독
        // ★ 서버 Push 프레임을 받는 함수: DeviceFrameReceived 이벤트 핸들러
        // ⚠ 백그라운드 스레드 → WPF: Dispatcher.InvokeAsync(() => ...) 필수
        channel.DeviceFrameReceived += OnFrameReceived;

        channel.DeviceStateChanged += (id, state) =>
        {
            Console.WriteLine($"[Example4] 상태 변경: {state}");
            // WPF 예시:
            // Dispatcher.InvokeAsync(() => LblState.Content = state.ToString());
        };

        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example4] 오류 (재접속 시도 중): {ex.Message}");

        // §4. 시작 (내부에서 ConnectAsync → PassiveReceiveLoopAsync 시작)
        await channel.StartAsync(ct);
        Console.WriteLine("[Example4] 연결됨 — 서버 Push 프레임 대기 중...\n");

        // §5. 비동기 열거 방법 (이벤트 대신 사용 가능 — 둘 중 하나만 사용)
        //
        //   방법 A: DeviceFrameReceived 이벤트 (§3에서 등록, 현재 방식)
        //   방법 B: ReadAllAsync 비동기 열거 (아래 주석 해제)
        //
        // await foreach (var frame in channel.ReadAllAsync(ct))
        // {
        //     ParseAndPrint(frame, "ReadAllAsync");
        // }

        // Ctrl+C 또는 CancellationToken 취소 대기
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }

        Console.WriteLine("[Example4] 종료");
    }

    // ★ 서버 Push 데이터를 실제로 받는 함수
    // ⚠ 백그라운드 스레드에서 호출됨 — WPF UI 직접 접근 금지
    private static void OnFrameReceived(int deviceId, byte[] frame)
    {
        if (frame.Length < PAYLOAD_SIZE)
        {
            Console.WriteLine($"[Example4] 프레임 크기 오류: {frame.Length}B (최소 {PAYLOAD_SIZE}B 필요)");
            return;
        }

        ParseAndPrint(frame, "이벤트");
    }

    private static void ParseAndPrint(byte[] frame, string source)
    {
        // BinaryProtocol 디코딩 완료 — frame = 순수 페이로드 (헤더/CRC 제거됨)
        uint frameId = BitConverter.ToUInt32(frame, OFFSET_FRAME_ID);
        float temp = BitConverter.ToSingle(frame, OFFSET_TEMPERATURE);
        float hum = BitConverter.ToSingle(frame, OFFSET_HUMIDITY);

        Console.WriteLine(
            $"[Example4][{source}] " +
            $"Frame#{frameId:D5} | " +
            $"온도: {temp,5:F1}°C | " +
            $"습도: {hum,5:F1}%  " +
            $"({DateTime.Now:HH:mm:ss.fff})");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 5A — TCP RequestResponse / 주기 폴링 (Echo 서버 연동)
//
//  [TcpTestServer] Echo 모드 / 포트 9000
//  [이 클라이언트] RequestResponseChannel — PeriodicRead 자동 전송
//
//  ★ 데이터 흐름:
//    [NetScheduler] RunPeriodicReadAsync
//      → 100ms 마다 EnqueueAsync(PeriodicRead 패킷)
//        → [NetDispatchPipeline] DispatchAsync
//          → Transport.WriteAsync(READ_COMMAND)   ← 서버로 요청 전송
//            → Transport.ReadAsync()               ← 응답 수신 대기
//              → BinaryProtocol.TryDecode()
//                → DeviceFrameReceived 이벤트      ← 여기서 받음
//
//  ★ Heartbeat 사용법:
//    HeartbeatInterval = TimeSpan.FromSeconds(30) 설정 시
//    30초 동안 Write/Read 가 없으면 Keep-Alive 프레임 자동 전송
// ══════════════════════════════════════════════════════════════════════
static class Example5A_TcpRequestResponse_Periodic
{
    private const int OFFSET_FC = 0;
    private const int OFFSET_DATA_LEN = 1;
    private const int OFFSET_FRAME_ID = 2;
    private const int OFFSET_TEMP = 6;
    private const int OFFSET_HUM = 10;
    private const int RESPONSE_SIZE = 14;

    // 서버에게 보낼 읽기 명령 [FC=0x03][AddrHi=0x00][AddrLo=0x00]
    private static readonly byte[] READ_COMMAND = [0x03, 0x00, 0x00];

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine(" Example5A: TCP RequestResponse — 주기 폴링");
        Console.WriteLine(" 서버: TcpTestServer → Echo 모드 → 포트 9000");
        Console.WriteLine("=================================================");

        // §1. 설정
        var cfg = new TcpDeviceConfig(5, "EchoServer", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 0,                               // 0 = 무제한
            RetryDelay = TimeSpan.FromSeconds(2),
            ReconnectBackoff = true,

            IsSequential = true,                           // 순차 처리
            PeriodicInterval = TimeSpan.FromMilliseconds(100), // 100ms 주기 Read
            RequestTimeout = TimeSpan.FromMilliseconds(500),

            // ★ Heartbeat 설정 예시
            // HeartbeatInterval = TimeSpan.FromSeconds(30),
            // → 30초 동안 통신이 없으면 BinaryProtocol.BuildHeartbeat() 전송
            // → 기본값 Zero = 비활성 (현재 비활성)
            HeartbeatInterval = TimeSpan.Zero,
        };

        // ReadCommand 등록 — PeriodicInterval 마다 자동 전송됨
        cfg.AddReadCommand(READ_COMMAND);

        // §2. 채널 조립
        // RequestResponse 모드: enablePassiveReceive 기본값 false (생략 가능)
        await using var channel = new RequestResponseChannel(
            cfg,
            TcpTransport.FromConfig(cfg),   // enablePassiveReceive: false (기본)
            new BinaryProtocol(stx: 0xAA, fc: 0x01),
            autoRegister: true);

        // §3. 이벤트 구독
        // ★ PeriodicRead 응답을 받는 함수: DeviceFrameReceived 이벤트 핸들러
        channel.DeviceFrameReceived += OnFrameReceived;

        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example5A] 상태 변경: {state}");

        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example5A] 오류: {ex.Message}");

        // §4. 시작
        await channel.StartAsync(ct);
        Console.WriteLine($"[Example5A] 연결됨 — {cfg.PeriodicInterval.TotalMilliseconds}ms 주기 폴링 시작\n");

        // §5. 5초마다 통계 출력
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(5000, ct); }
                catch { break; }
                var s = channel.Statistics;
                Console.WriteLine(
                    $"[Example5A][통계] 전송={s.TotalSent} 수신={s.TotalReceived} " +
                    $"오류={s.TotalErrors} 재접속={s.TotalReconnects} " +
                    $"평균응답={s.AvgResponseMs:F1}ms 최대={s.MaxResponseMs}ms");
            }
        }, ct);

        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }

        Console.WriteLine("[Example5A] 종료");
    }

    // ★ PeriodicRead 응답을 실제로 받는 함수
    private static void OnFrameReceived(int deviceId, byte[] frame)
    {
        if (frame.Length < RESPONSE_SIZE)
        {
            Console.WriteLine($"[Example5A] 응답 크기 오류: {frame.Length}B");
            return;
        }

        byte fc = frame[OFFSET_FC];
        byte dataLen = frame[OFFSET_DATA_LEN];
        uint frameId = BitConverter.ToUInt32(frame, OFFSET_FRAME_ID);
        float temp = BitConverter.ToSingle(frame, OFFSET_TEMP);
        float hum = BitConverter.ToSingle(frame, OFFSET_HUM);

        Console.WriteLine(
            $"[Example5A][이벤트] " +
            $"FC=0x{fc:X2} DataLen={dataLen} " +
            $"Frame#{frameId:D5} | " +
            $"온도: {temp,5:F1}°C | " +
            $"습도: {hum,5:F1}%  " +
            $"({DateTime.Now:HH:mm:ss.fff})");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 5B — TCP RequestResponse / 단발 RequestAsync (Echo 서버 연동)
//
//  [TcpTestServer] Echo 모드 / 포트 9000
//  [이 클라이언트] channel.RequestAsync() 단발 호출
//
//  ★ 데이터 흐름:
//    channel.RequestAsync(QUERY)
//      → TCS 생성 → EnqueueAsync(Request 패킷)
//        → [NetDispatchPipeline] DispatchAsync
//          → Transport.WriteAsync(QUERY)  ← 서버로 전송
//            → Transport.ReadAsync()       ← 응답 대기
//              → TryDecode → tcs.SetResult(NetResult.Ok(decoded))
//                → RequestAsync await 완료  ← 여기서 결과 받음
//
//  NetResult 사용 패턴 3가지 예시 포함
// ══════════════════════════════════════════════════════════════════════
static class Example5B_TcpRequestResponse_OneShot
{
    private static readonly byte[] QUERY = [0x03, 0x00, 0x00];

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine(" Example5B: TCP RequestResponse — 단발 RequestAsync");
        Console.WriteLine(" 서버: TcpTestServer → Echo 모드 → 포트 9000");
        Console.WriteLine("=================================================");

        var cfg = new TcpDeviceConfig(6, "EchoServer-OneShot", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 0,
            ReconnectBackoff = true,
            PeriodicInterval = TimeSpan.Zero,     // 주기 Read 없음
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            HeartbeatInterval = TimeSpan.Zero,
        };

        await using var channel = new RequestResponseChannel(
            cfg,
            TcpTransport.FromConfig(cfg),
            new BinaryProtocol(stx: 0xAA, fc: 0x01),
            autoRegister: true);

        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example5B] 상태: {state}");
        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example5B] 오류: {ex.Message}");

        await channel.StartAsync(ct);
        Console.WriteLine("[Example5B] 연결됨 — 단발 RequestAsync 테스트 (10회)\n");

        for (int i = 1; i <= 10 && !ct.IsCancellationRequested; i++)
        {
            // ── 패턴 A: IsOk / IsError 분기 ──────────────────────────
            NetResult r = await channel.RequestAsync(QUERY,
                timeout: TimeSpan.FromMilliseconds(500), ct: ct);

            if (r.IsOk && r.Data!.Length >= 14)
            {
                uint frameId = BitConverter.ToUInt32(r.Data, 2);
                float temp = BitConverter.ToSingle(r.Data, 6);
                float hum = BitConverter.ToSingle(r.Data, 10);
                Console.WriteLine(
                    $"[Example5B][A #{i:D2}] Frame#{frameId:D5} " +
                    $"온도: {temp:F1}°C 습도: {hum:F1}%");
            }
            else if (r.IsError)
            {
                Console.WriteLine($"[Example5B][A #{i:D2}] 실패: {r.Error!.Message}");
                continue;
            }

            // ── 패턴 B: Map + ValueOr ─────────────────────────────────
            var parsed = r.Map(frame =>
            {
                if (frame.Length < 14) throw new InvalidOperationException("응답 크기 부족");
                return (
                    Id: BitConverter.ToUInt32(frame, 2),
                    Temp: BitConverter.ToSingle(frame, 6),
                    Hum: BitConverter.ToSingle(frame, 10));
            });

            var (id, t, h) = parsed.ValueOr((0u, 0f, 0f));
            Console.WriteLine(
                $"[Example5B][B #{i:D2}] Map → Frame#{id:D5} " +
                $"온도: {t:F1}°C 습도: {h:F1}%");

            // ── 패턴 C: DataOr (실패 시 기본값) ──────────────────────
            byte[] safeData = r.DataOr(Array.Empty<byte>());
            Console.WriteLine(
                $"[Example5B][C #{i:D2}] DataOr → {safeData.Length}B 수신\n");

            await Task.Delay(200, ct);
        }

        var s = channel.Statistics;
        Console.WriteLine(
            $"[Example5B] 완료 | 전송={s.TotalSent} 수신={s.TotalReceived} " +
            $"오류={s.TotalErrors} 평균응답={s.AvgResponseMs:F1}ms 최대={s.MaxResponseMs}ms");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 5C — TCP RequestResponse + WriteAsync + Heartbeat (Echo 서버)
//
//  [TcpTestServer] Echo 모드 / 포트 9000
//  [이 클라이언트] WriteAsync (설정값 쓰기) + PeriodicRead (주기 읽기)
//                 + Heartbeat (30초 Keep-Alive)
//
//  ★ Heartbeat 동작:
//    HeartbeatInterval=30s 설정 → 30초 동안 Write/Read 없으면
//    BinaryProtocol.BuildHeartbeat() → Encode(Array.Empty<byte>()) 자동 전송
//    Low(3) 우선순위 → Write/Read 있으면 Heartbeat 는 건너뜀
// ══════════════════════════════════════════════════════════════════════
static class Example5C_TcpWriteAndRead
{
    private static readonly byte[] READ_CMD = [0x03, 0x00, 0x00];
    private static readonly byte[] WRITE_CMD = [0x06, 0x00, 0x01, 0x00, 0x64];  // 쓰기 FC=06

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine(" Example5C: TCP WriteAsync + PeriodicRead + Heartbeat");
        Console.WriteLine(" 서버: TcpTestServer → Echo 모드 → 포트 9000");
        Console.WriteLine("=================================================");

        var cfg = new TcpDeviceConfig(7, "EchoServer-RW", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 0,
            ReconnectBackoff = true,

            IsSequential = true,
            PeriodicInterval = TimeSpan.FromMilliseconds(200),

            // ★ Heartbeat 활성화
            // 200ms 주기 Read 가 진행되는 동안은 Heartbeat 전송 안 함
            // PeriodicRead 가 멈추는 순간(재접속 Pause 등)부터 카운트 시작
            // 테스트: PeriodicInterval=Zero 로 바꾸면 30초 후 Heartbeat 전송 확인 가능
            HeartbeatInterval = TimeSpan.FromSeconds(30),

            RequestTimeout = TimeSpan.FromMilliseconds(500),
        };
        cfg.AddReadCommand(READ_CMD);

        await using var channel = new RequestResponseChannel(
            cfg,
            TcpTransport.FromConfig(cfg),
            new BinaryProtocol(stx: 0xAA, fc: 0x01),
            autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) =>
        {
            if (frame.Length >= 14)
            {
                uint frameId = BitConverter.ToUInt32(frame, 2);
                float temp = BitConverter.ToSingle(frame, 6);
                Console.WriteLine(
                    $"[Example5C] 주기응답 Frame#{frameId:D5} 온도: {temp:F1}°C");
            }
        };

        // ★ WriteAsync 실패 → DeviceErrorOccurred 이벤트 (v4 설계)
        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example5C] WriteAsync 오류: {ex.Message}");

        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example5C] 상태: {state}");

        await channel.StartAsync(ct);
        Console.WriteLine("[Example5C] 연결됨\n");

        // ── WriteAsync: Write(1) 우선순위 → PeriodicRead(2) 보다 먼저 처리 ──
        await channel.WriteAsync(WRITE_CMD, NetPriority.Write, ct);
        Console.WriteLine("[Example5C] WriteAsync 전송 (큐 투입 완료)");

        // ── 긴급 WriteAsync: Critical(0) 최우선 ──────────────────────────
        byte[] emergencyStop = [0xFF, 0x00];
        await channel.WriteAsync(emergencyStop, NetPriority.Critical, ct);
        Console.WriteLine("[Example5C] Emergency Stop 전송 (Critical)\n");

        // ── 단발 RequestAsync (주기 Read 와 혼용 가능) ──────────────────
        await Task.Delay(1000, ct);
        NetResult r = await channel.RequestAsync(READ_CMD,
            timeout: TimeSpan.FromMilliseconds(500), ct: ct);

        Console.WriteLine(r.IsOk
            ? $"[Example5C] 단발 읽기 성공: {r.Data!.Length}B"
            : $"[Example5C] 단발 읽기 실패: {r.Error!.Message}");

        await Task.Delay(5000, ct);

        var s = channel.Statistics;
        Console.WriteLine(
            $"\n[Example5C] 완료 | 전송={s.TotalSent} 수신={s.TotalReceived} " +
            $"오류={s.TotalErrors} 재접속={s.TotalReconnects} " +
            $"평균응답={s.AvgResponseMs:F1}ms");

        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }

        Console.WriteLine("[Example5C] 종료");
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
            await echo.WriteAsync([0x06, 0x00, 0x01, 0x00, 0x64], NetPriority.Write, ct);
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