// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Demo/TcpClientExamples.cs
//  역할: TcpTestServer 와 연동하는 TCP 클라이언트 테스트 예제
//
//  ┌─ 사전 준비 ──────────────────────────┐
//  │  1. 4-lssLib.Net.TcpTestServer 빌드 및 실행                     │
//  │  2. 포트: 9000                                                  │
//  │  3. Example4 ← Push 모드 선택 후 시작                          │
//  │     Example5 ← Echo 모드 선택 후 시작                          │
//  └─────────────────────────────────┘
//
//  ┌─ 페이로드 구조 (ServerProtocolHelper 와 동일) ─────────┐
//  │  [Push 수신 payload 12B]                                         │
//  │    [FrameId:uint 4B LE][Temp:float 4B LE][Hum:float 4B LE]       │
//  │                                                                  │
//  │  [Echo 요청 payload 3B]                                          │
//  │    [FC:0x03][Addr Hi:0x00][Addr Lo:0x00]                         │
//  │                                                                  │
//  │  [Echo 응답 payload 14B]                                         │
//  │    [FC:0x03][DataLen:0x0C][FrameId:uint 4B LE]                   │
//  │    [Temp:float 4B LE][Hum:float 4B LE]                           │
//  └─────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;
using lssLib.Net.Implementation;

namespace lssLib.Net.Demo;

// ══════════════════════════════════════════════════════════════════════
//  예시 4 — TCP Passive 클라이언트 (Push 서버 연동)
//
//  [TcpTestServer]  Push 모드 → 포트 9000
//  [이 클라이언트]  PassiveNetChannel → DeviceFrameReceived 이벤트 수신
//
//  서버가 500ms 마다 센서 프레임을 push
//  클라이언트는 데이터를 받기만 함 (능동 요청 없음)
// ══════════════════════════════════════════════════════════════════════
static class Example4_TcpPassiveClient
{
    // 서버 Push 페이로드 파싱 상수
    private const int OFFSET_FRAME_ID = 0;   // uint  4B LE
    private const int OFFSET_TEMPERATURE = 4;  // float 4B LE
    private const int OFFSET_HUMIDITY = 8;   // float 4B LE
    private const int PAYLOAD_SIZE = 12;

    public static async Task RunAsync(CancellationToken ct = default)
    {
        // §1. 설정
        // ──────────────────────────────────────────────────────────────
        // TcpTestServer 와 동일한 호스트:포트 지정
        // BinaryProtocol(stx:0xAA) — 서버의 ServerProtocolHelper 와 STX 일치
        var cfg = new TcpDeviceConfig(4, "PushServer", "127.0.0.1", 9000)
        {
            // 재접속 설정
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect,
            MaxRetries = 5,
            RetryDelay = TimeSpan.FromSeconds(2),
            ReconnectBackoff = true,

            // Passive 모드 — PeriodicInterval 불필요 (서버가 push)
            PeriodicInterval = TimeSpan.Zero,    // 주기 Read 비활성
            HeartbeatInterval = TimeSpan.Zero,    // Heartbeat 비활성
        };

        // §2. 채널 조립
        // ──────────────────────────────────────────────────────────────
        await using var channel = new PassiveNetChannel(
            cfg,
            TcpTransport.FromConfig(cfg),
            new BinaryProtocol(stx: 0xAA, fc: 0x01),  // 서버와 동일한 STX/FC
            autoRegister: true);

        // §3. 이벤트 구독
        // ──────────────────────────────────────────────────────────────
        channel.DeviceFrameReceived += OnFrameReceived;

        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example4] [{id}:{cfg.DeviceName}] 상태: {state}");

        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example4] [{id}:{cfg.DeviceName}] 오류: {ex.Message}");

        // §4. 시작
        // ──────────────────────────────────────────────────────────────
        Console.WriteLine("[Example4] Passive TCP 클라이언트 시작...");
        Console.WriteLine("[Example4] TcpTestServer를 Push 모드(포트 9000)로 먼저 시작하세요.");
        await channel.StartAsync(ct);
        Console.WriteLine("[Example4] 연결됨 — 서버 Push 프레임 대기 중...");

        // §5. 비동기 열거 (Passive 모드 권장)
        // ──────────────────────────────────────────────────────────────
        //
        //   DeviceFrameReceived 이벤트와 ReadAllAsync 는 동일한 데이터를
        //   다른 방식으로 소비합니다. 하나만 사용하세요.
        //
        // 방법 A — 이벤트 (OnFrameReceived 에서 처리)
        // 방법 B — 비동기 열거 (아래 코드 주석 해제)
        /*
        await foreach (var frame in channel.ReadAllAsync(ct))
        {
            ParseAndPrint(frame, "[ReadAllAsync]");
        }
        */

        // Ctrl+C 또는 CancellationToken 취소 대기
        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────
    // ⚠ 백그라운드 스레드 — WPF: Dispatcher.InvokeAsync 필수
    //    Console 앱: 그대로 사용 가능
    private static void OnFrameReceived(int deviceId, byte[] frame)
    {
        if (frame.Length < PAYLOAD_SIZE)
        {
            Console.WriteLine($"[Example4] 프레임 크기 오류: {frame.Length}B");
            return;
        }

        ParseAndPrint(frame, "[이벤트]");
    }

    private static void ParseAndPrint(byte[] frame, string source)
    {
        // 페이로드 파싱 (이미 BinaryProtocol 디코딩 완료 — 순수 데이터만)
        uint frameId = BitConverter.ToUInt32(frame, OFFSET_FRAME_ID);
        float temp = BitConverter.ToSingle(frame, OFFSET_TEMPERATURE);
        float hum = BitConverter.ToSingle(frame, OFFSET_HUMIDITY);

        Console.WriteLine(
            $"[Example4] {source} Frame#{frameId:D5} | " +
            $"온도: {temp,5:F1}°C | 습도: {hum,5:F1}%  " +
            $"({DateTime.Now:HH:mm:ss.fff})");
    }

}

// ══════════════════════════════════════════════════════════════════════
//  예시 5A — TCP RequestResponse 클라이언트 (Echo 서버 — 주기 폴링)
//
//  [TcpTestServer]  Echo 모드 → 포트 9000
//  [이 클라이언트]  RequestResponseChannel → PeriodicRead 자동 전송
//
//  PeriodicInterval=100ms 마다 ReadCommand([0x03,0x00,0x00]) 자동 전송
//  서버가 응답 → DeviceFrameReceived 이벤트 발생
// ══════════════════════════════════════════════════════════════════════
static class Example5A_TcpRequestResponse_Periodic
{
    // Echo 응답 페이로드 파싱 상수
    private const int OFFSET_FC = 0;   // byte  1B
    private const int OFFSET_DATA_LEN = 1;   // byte  1B
    private const int OFFSET_FRAME_ID = 2;   // uint  4B LE
    private const int OFFSET_TEMP = 6;   // float 4B LE
    private const int OFFSET_HUM = 10;  // float 4B LE
    private const int RESPONSE_SIZE = 14;  // FC(1)+DataLen(1)+FrameId(4)+Temp(4)+Hum(4)

    // 서버에게 보낼 읽기 명령 (Modbus FC03 유사)
    // payload: [FC=0x03][AddrHi=0x00][AddrLo=0x00]
    private static readonly byte[] READ_COMMAND = [0x03, 0x00, 0x00];

    public static async Task RunAsync(CancellationToken ct = default)
    {
        // §1. 설정
        var cfg = new TcpDeviceConfig(5, "EchoServer", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 5,
            RetryDelay = TimeSpan.FromSeconds(2),
            ReconnectBackoff = true,

            // PeriodicRead 설정 — 100ms 마다 ReadCommand 자동 전송
            IsSequential = true,
            PeriodicInterval = TimeSpan.FromMilliseconds(100),
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            HeartbeatInterval = TimeSpan.Zero,
        };

        // ReadCommand 등록 (PeriodicInterval 마다 자동 전송됨)
        cfg.AddReadCommand(READ_COMMAND);

        // §2. 채널 조립
        await using var channel = new RequestResponseChannel(
            cfg,
            TcpTransport.FromConfig(cfg),
            new BinaryProtocol(stx: 0xAA, fc: 0x01),
            autoRegister: true);

        // §3. 이벤트 구독
        // PeriodicRead 응답은 DeviceFrameReceived 로 전달됨
        channel.DeviceFrameReceived += OnFrameReceived;

        channel.DeviceStateChanged += (id, state) =>
        {
            Console.WriteLine($"[Example5A] [{id}:{cfg.DeviceName}] 상태: {state}");
            // WPF: Dispatcher.InvokeAsync(() => LblState.Content = state.ToString());
        };

        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example5A] [{id}:{cfg.DeviceName}] 오류: {ex.Message}");

        // §4. 시작
        Console.WriteLine("[Example5A] RequestResponse TCP 클라이언트 시작...");
        Console.WriteLine("[Example5A] TcpTestServer를 Echo 모드(포트 9000)로 먼저 시작하세요.");
        await channel.StartAsync(ct);
        Console.WriteLine($"[Example5A] 연결됨 — {cfg.PeriodicInterval.TotalMilliseconds}ms 주기 폴링 시작");

        // §5. 통계 출력 (5초마다)
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(5000, ct);
                var s = channel.Statistics;
                Console.WriteLine(
                    $"[Example5A] 통계 | 전송={s.TotalSent} 수신={s.TotalReceived} " +
                    $"오류={s.TotalErrors} 평균응답={s.AvgResponseMs:F1}ms");
            }
        }, ct);

        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
    }

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
            $"[Example5A] 주기응답 FC=0x{fc:X2} DataLen={dataLen} " +
            $"Frame#{frameId:D5} | 온도: {temp,5:F1}°C | 습도: {hum,5:F1}%  " +
            $"({DateTime.Now:HH:mm:ss.fff})");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 5B — TCP RequestResponse 클라이언트 (Echo 서버 — 단발 RequestAsync)
//
//  [TcpTestServer]  Echo 모드 → 포트 9000
//  [이 클라이언트]  RequestResponseChannel → RequestAsync 단발 호출
//
//  PeriodicInterval=Zero (주기 Read 없음)
//  channel.RequestAsync() 를 직접 호출하여 응답 대기
//  NetResult 성공/실패 패턴 + Map 패턴 + ThrowIfError 패턴 모두 예시
// ══════════════════════════════════════════════════════════════════════
static class Example5B_TcpRequestResponse_OneShot
{
    private static readonly byte[] QUERY = [0x03, 0x00, 0x00];  // 읽기 명령

    public static async Task RunAsync(CancellationToken ct = default)
    {
        // §1. 설정 — PeriodicInterval=Zero (단발 모드)
        var cfg = new TcpDeviceConfig(6, "EchoServer-OneShot", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 5,
            ReconnectBackoff = true,

            PeriodicInterval = TimeSpan.Zero,     // 주기 Read 없음
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            HeartbeatInterval = TimeSpan.Zero,
        };

        // §2. 채널 조립
        await using var channel = new RequestResponseChannel(
            cfg,
            TcpTransport.FromConfig(cfg),
            new BinaryProtocol(stx: 0xAA, fc: 0x01),
            autoRegister: true);

        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example5B] 상태: {state}");

        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example5B] 오류: {ex.Message}");

        // §3. 시작
        await channel.StartAsync(ct);
        Console.WriteLine("[Example5B] 연결됨 — 단발 RequestAsync 테스트 시작");

        // §4. 단발 요청-응답 (3가지 패턴 예시)
        // ──────────────────────────────────────────────────────────────
        for (int i = 0; i < 10 && !ct.IsCancellationRequested; i++)
        {
            // ── 패턴 A: IsOk / IsError 분기 ──────────────────────────
            NetResult r = await channel.RequestAsync(QUERY,
                timeout: TimeSpan.FromMilliseconds(500), ct: ct);

            if (r.IsOk)
            {
                var frame = r.Data!;
                if (frame.Length >= 14)
                {
                    uint frameId = BitConverter.ToUInt32(frame, 2);
                    float temp = BitConverter.ToSingle(frame, 6);
                    float hum = BitConverter.ToSingle(frame, 10);

                    Console.WriteLine(
                        $"[Example5B][A] Frame#{frameId:D5} " +
                        $"온도: {temp:F1}°C 습도: {hum:F1}%");
                }
            }
            else
            {
                Console.WriteLine($"[Example5B][A] 실패: {r.Error!.Message}");
            }

            // ── 패턴 B: Map + ValueOr ─────────────────────────────────
            NetResult<(uint Id, float Temp, float Hum)> parsed = r.Map(frame =>
            {
                if (frame.Length < 14) throw new InvalidOperationException("응답 크기 부족");
                return (
                    BitConverter.ToUInt32(frame, 2),
                    BitConverter.ToSingle(frame, 6),
                    BitConverter.ToSingle(frame, 10));
            });

            var (id2, t2, h2) = parsed.ValueOr((0, 0f, 0f));
            Console.WriteLine(
                $"[Example5B][B] Map 결과 — Frame#{id2:D5} " +
                $"온도: {t2:F1}°C 습도: {h2:F1}%");

            // ── 패턴 C: DataOr (실패 시 기본값) ──────────────────────
            byte[] safeData = r.DataOr(Array.Empty<byte>());
            Console.WriteLine(
                $"[Example5B][C] DataOr — {safeData.Length}B 수신");

            await Task.Delay(200, ct);  // 200ms 간격
        }

        // §5. 통계
        var s = channel.Statistics;
        Console.WriteLine(
            $"[Example5B] 완료 — 전송={s.TotalSent} 수신={s.TotalReceived} " +
            $"오류={s.TotalErrors} 평균응답={s.AvgResponseMs:F1}ms 최대={s.MaxResponseMs}ms");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 5C — TCP RequestResponse + WriteAsync (설정값 쓰기)
//
//  [TcpTestServer]  Echo 모드 → 포트 9000
//  [이 클라이언트]  WriteAsync → 설정값 전송 (응답 대기 없음)
//                               RequestAsync → 센서값 읽기
//
//  WriteAsync 는 Fire-and-forget (응답 없음)
//  실패는 DeviceErrorOccurred 이벤트로 통보
// ══════════════════════════════════════════════════════════════════════
static class Example5C_TcpWriteAndRead
{
    private static readonly byte[] READ_CMD = [0x03, 0x00, 0x00];   // 읽기
    private static readonly byte[] WRITE_CMD = [0x06, 0x00, 0x01,    // FC=06 (쓰기)
                                                  0x00, 0x64];         // 값=100

    public static async Task RunAsync(CancellationToken ct = default)
    {
        var cfg = new TcpDeviceConfig(7, "EchoServer-RW", "127.0.0.1", 9000)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 5,
            ReconnectBackoff = true,
            PeriodicInterval = TimeSpan.FromMilliseconds(200),  // 200ms 주기 Read
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            HeartbeatInterval = TimeSpan.Zero,
        };
        cfg.AddReadCommand(READ_CMD);

        await using var channel = new RequestResponseChannel(
            cfg,
            TcpTransport.FromConfig(cfg),
            new BinaryProtocol(stx: 0xAA, fc: 0x01),
            autoRegister: true);

        // PeriodicRead 응답 → 이벤트
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

        // WriteAsync 실패 → 이벤트 (v4: WriteAsync는 Task 반환, 실패는 이벤트)
        channel.DeviceErrorOccurred += (id, ex) =>
            Console.WriteLine($"[Example5C] WriteAsync 오류: {ex.Message}");

        channel.DeviceStateChanged += (id, state) =>
            Console.WriteLine($"[Example5C] 상태: {state}");

        await channel.StartAsync(ct);
        Console.WriteLine("[Example5C] 연결됨");

        // ── WriteAsync — 설정값 전송 (우선순위: Write(1) → 항상 Read 보다 먼저)
        await channel.WriteAsync(WRITE_CMD, NetPriority.Write, ct);
        Console.WriteLine("[Example5C] WriteAsync 전송 완료 (큐 투입)");

        // ── 긴급 정지 (Critical 최우선)
        byte[] emergencyStop = [0xFF, 0x00];
        await channel.WriteAsync(emergencyStop, NetPriority.Critical, ct);
        Console.WriteLine("[Example5C] Emergency Stop 전송 (Critical)");

        // ── 단발 RequestAsync (주기 Read 와 혼용 가능)
        await Task.Delay(1000, ct);
        NetResult r = await channel.RequestAsync(READ_CMD,
            timeout: TimeSpan.FromMilliseconds(500), ct: ct);

        Console.WriteLine(r.IsOk
            ? $"[Example5C] 단발 읽기 성공: {r.Data!.Length}B"
            : $"[Example5C] 단발 읽기 실패: {r.Error!.Message}");

        await Task.Delay(3000, ct);  // 3초 동작 후 종료

        var s = channel.Statistics;
        Console.WriteLine(
            $"[Example5C] 완료 — 전송={s.TotalSent} 수신={s.TotalReceived} " +
            $"재접속={s.TotalReconnects} 평균응답={s.AvgResponseMs:F1}ms");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 6 — Registry 다중 채널 (Push + Echo 동시 운용)
//
//  [TcpTestServer]  Push 서버(9000) + Echo 서버(9001) 동시 운용 필요
//  [이 클라이언트]  두 채널을 Registry 로 관리
// ══════════════════════════════════════════════════════════════════════
static class Example6_MultiChannel_Registry
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        // Push 서버(9000) — Passive 채널
        var pushCfg = new TcpDeviceConfig(10, "Push-9000", "127.0.0.1", 9000)
        {
            PeriodicInterval = TimeSpan.Zero,
            HeartbeatInterval = TimeSpan.Zero,
        };

        // Echo 서버(9001) — RequestResponse 채널
        var echoCfg = new TcpDeviceConfig(11, "Echo-9001", "127.0.0.1", 9001)
        {
            PeriodicInterval = TimeSpan.FromMilliseconds(200),
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            HeartbeatInterval = TimeSpan.Zero,
        };
        echoCfg.AddReadCommand([0x03, 0x00, 0x00]);

        // autoRegister=true → Registry 자동 등록
        var passiveCh = new PassiveNetChannel(
            pushCfg, TcpTransport.FromConfig(pushCfg),
            new BinaryProtocol(stx: 0xAA), autoRegister: true);

        var rrCh = new RequestResponseChannel(
            echoCfg, TcpTransport.FromConfig(echoCfg),
            new BinaryProtocol(stx: 0xAA), autoRegister: true);

        // 이벤트 구독
        passiveCh.DeviceFrameReceived += (id, frame) =>
            Console.WriteLine($"[Registry][{id}] Push 수신: {frame.Length}B");

        rrCh.DeviceFrameReceived += (id, frame) =>
            Console.WriteLine($"[Registry][{id}] Echo 응답: {frame.Length}B");

        // Registry 일괄 시작
        await NetDeviceRegistry.Instance.StartAllAsync(ct);
        Console.WriteLine("[Registry] 전체 채널 시작 완료");
        Console.WriteLine($"  등록: {NetDeviceRegistry.Instance.Count}개 / " +
                          $"연결: {NetDeviceRegistry.Instance.ConnectedCount}개");

        await Task.Delay(5000, ct);

        // DeviceId 로 개별 접근
        var echo = NetDeviceRegistry.Instance.Get(11);
        if (echo?.IsConnected == true)
        {
            await echo.WriteAsync([0x06, 0x00, 0x01, 0x00, 0x64], NetPriority.Write, ct);
            Console.WriteLine("[Registry] Echo 채널 Write 전송");
        }

        // 전체 상태 조회
        foreach (var status in NetDeviceRegistry.Instance.GetStatusAll())
        {
            Console.WriteLine(
                $"[Registry] [{status.DeviceId}:{status.DeviceName}] " +
                $"상태={status.State} " +
                $"전송={status.TotalSent} 수신={status.TotalReceived} " +
                $"오류={status.TotalErrors} 평균응답={status.AvgResponseMs:F1}ms");
        }

        // 전체 정지 (DisposeAsync 포함)
        await NetDeviceRegistry.Instance.StopAllAsync();
        Console.WriteLine("[Registry] 전체 채널 정지 완료");
    }
}