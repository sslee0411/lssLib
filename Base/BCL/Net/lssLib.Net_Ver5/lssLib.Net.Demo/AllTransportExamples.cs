// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net.Demo · AllTransportExamples.cs  [v5.1]
//  SequenceMode 적용 전체 예제 (Ex01~Ex11)
//
//  SequenceMode 값:
//    0 = Parallel   — 병렬 (TCP/UDP/HTTP/WS/MQTT/Virtual 기본)
//    1 = Sequential — 단일 순차 (Serial/NamedPipe 기본, RS-485 필수)
//    N≥2 = Window(N) — 슬라이딩 윈도우 N개 동시
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;
using System.Text;
using System.Windows.Media;

namespace lssLib.Net.Demo;

// ══════════════════════════════════════════════════════════════════════
//  Ex01 — TCP Passive (Push 서버)
//  SequenceMode: 0 (Parallel) ← TCP 기본값, ReadCommand 없으므로 무관
// ══════════════════════════════════════════════════════════════════════
static class Ex01_TcpPassive
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex01: TCP Passive (Push 서버) ===");

        var cfg = new TcpDeviceConfig(1, "TcpPush", "127.0.0.1", 9000)
        {
            MaxRetries = 0,
            HeartbeatInterval = TimeSpan.FromSeconds(30)
            // SequenceMode = 0 (Parallel) ← TcpDeviceConfig 기본값
        };

        await using var channel = new PassiveNetChannel(
            cfg,
            TcpTransport.FromConfig(cfg, enablePassiveReceive: true),
            new BinaryProtocol(stx: 0xAA), autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) =>
        {
            uint frameId = BitConverter.ToUInt32(frame, 0);
            float temp = BitConverter.ToSingle(frame, 4);
            Console.WriteLine($"  [TCP-{id}] Frame#{frameId:D5} 온도={temp:F1}°C");
        };
        channel.DeviceStateChanged += (id, s) => Console.WriteLine($"  [TCP-{id}] 상태: {s}");
        channel.DeviceErrorOccurred += (id, ex) => Console.WriteLine($"  [TCP-{id}] {ex.Message}");

        await channel.StartAsync(ct);
        Console.WriteLine($"  연결됨 SeqMode={cfg} | Push 수신 대기...");
        //await Task.Delay(100_000, ct); //Test
        await Task.Delay(5_000, ct);
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex02 — TCP RequestResponse (Echo 서버)
//  SequenceMode: 0 (Parallel) — 기본, 또는 3으로 슬라이딩 윈도우 시연
// ══════════════════════════════════════════════════════════════════════
static class Ex02_TcpRequestResponse
{
    private static readonly byte[] READ_CMD = [0x03, 0x00, 0x00];

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex02: TCP RequestResponse ===");
        Console.WriteLine("  SequenceMode 선택: 0=병렬 / 1=순차 / 3=윈도우(3)");

        var cfg = new TcpDeviceConfig(2, "TcpRR", "127.0.0.1", 9000)
        {
            SequenceMode = NetDeviceConfig.SequenceModes.Parallel,  // 0: 병렬
            PeriodicInterval = TimeSpan.FromMilliseconds(200),
            RequestTimeout = TimeSpan.FromMilliseconds(500)
        };
        cfg.AddReadCommand(READ_CMD);
        cfg.AddReadCommand([0x03, 0x00, 0x10]);   // 두 번째 ReadCommand 추가
        Console.WriteLine($"  {cfg}");

        await using var channel = new RequestResponseChannel(
            cfg, TcpTransport.FromConfig(cfg), new BinaryProtocol(stx: 0xAA), autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) =>
        {
            if (frame.Length >= 14)
                Console.WriteLine($"  [TCP-RR] 온도={BitConverter.ToSingle(frame, 6):F1}°C");
        };

        await channel.StartAsync(ct);

        // ── SequenceMode 비교 시연 ───────────────────────────────────
        Console.WriteLine("\n  [시연] SequenceMode=0 (병렬) 로 단발 요청:");

        /* // Test
        for (int i = 0; i < 1000; i++)
        {
            NetResult r = await channel.RequestAsync(READ_CMD,
                timeout: TimeSpan.FromMilliseconds(500), ct: ct);
            Console.WriteLine(r.IsOk
                ? $"  {i.ToString()} → {r.Data!.Length}B 수신 완료"
                : $"  → 실패: {r.Error!.Message}");

            await Task.Delay(5_000, ct);
        }
        //*/

        /*
        NetResult r = await channel.RequestAsync(READ_CMD,
                timeout: TimeSpan.FromMilliseconds(500), ct: ct);
        Console.WriteLine(r.IsOk
            ? $"  {i.ToString()} → {r.Data!.Length}B 수신 완료"
            : $"  → 실패: {r.Error!.Message}");
        */
        await Task.Delay(3_000, ct);
        var s = channel.Statistics;
        Console.WriteLine($"  통계: 전송={s.TotalSent} 수신={s.TotalReceived} 응답={s.AvgResponseMs:F1}ms");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex03 — Serial Modbus RTU
//  SequenceMode: 1 (Sequential) — RS-485 버스 충돌 방지 필수
// ══════════════════════════════════════════════════════════════════════
static class Ex03_Serial
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex03: Serial Modbus RTU ===");

        var cfg = new SerialDeviceConfig(3, "Modbus-PLC", "COM3", 9600)
        {
            // SequenceMode = 1 (Sequential) ← SerialDeviceConfig 기본값
            PeriodicInterval = TimeSpan.FromMilliseconds(50),
            RequestTimeout = TimeSpan.FromMilliseconds(500)
        };
        cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]); // 슬레이브 1
        cfg.AddReadCommand([0x02, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]); // 슬레이브 2
        Console.WriteLine($"  {cfg}");
        Console.WriteLine($"  → SeqMode=1 (Sequential): 슬레이브1 → 슬레이브2 순서 보장");

        await using var channel = new RequestResponseChannel(
            cfg, SerialTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) =>
            Console.WriteLine($"  [Serial] {frame.Length}B 수신");
        channel.DeviceStateChanged += (id, s) =>
            Console.WriteLine($"  [Serial] 상태: {s}");

        await channel.StartAsync(ct);

        // Write (Modbus FC06)
        await channel.WriteAsync([0x01, 0x06, 0x00, 0x01, 0x00, 0x64, 0xD9, 0x98]);
        Console.WriteLine("  SetPoint Write 전송");

        await Task.Delay(2_000, ct);
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex04 — UDP Passive
//  SequenceMode: 0 (Parallel)
// ══════════════════════════════════════════════════════════════════════
static class Ex04_Udp
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex04: UDP Passive ===");

        var cfg = new UdpDeviceConfig(4, "UDP-Sensor", "255.255.255.255", 9100)
        { LocalPort = 9100 };
        // SequenceMode = 0 (Parallel) ← 기본값
        Console.WriteLine($"  {cfg}");

        await using var channel = new PassiveNetChannel(
            cfg, UdpTransport.FromConfig(cfg, enablePassiveReceive: true),
            new RawProtocol(), autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) =>
            Console.WriteLine($"  [UDP] {frame.Length}B 수신");

        await channel.StartAsync(ct);
        Console.WriteLine("  UDP 수신 대기 중 (포트 9100)...");
        await Task.Delay(5_000, ct);
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex05 — Named Pipe IPC
//  SequenceMode: 1 (Sequential) — 파이프 구조적 순서 보장
// ══════════════════════════════════════════════════════════════════════
static class Ex05_NamedPipe
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex05: Named Pipe IPC ===");

        var cfg = new NamedPipeDeviceConfig(5, "Pipe-IPC", ".", "lssLib-control")
        {
            ConnectTimeoutMs = 3000
            // SequenceMode = 1 (Sequential) ← NamedPipeDeviceConfig 기본값
        };
        Console.WriteLine($"  {cfg}");
        Console.WriteLine($"  → SeqMode=1: 파이프 명령을 순서대로 전송");

        await using var channel = new RequestResponseChannel(
            cfg, NamedPipeTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true);

        channel.DeviceStateChanged += (id, s) => Console.WriteLine($"  [Pipe] 상태: {s}");
        channel.DeviceErrorOccurred += (id, ex) => Console.WriteLine($"  [Pipe] 오류: {ex.Message}");

        try
        {
            await channel.StartAsync(ct);
            var cmd = Encoding.UTF8.GetBytes("{\"cmd\":\"status\"}");
            NetResult r = await channel.RequestAsync(cmd,
                timeout: TimeSpan.FromSeconds(2), ct: ct);
            Console.WriteLine(r.IsOk
                ? $"  응답: {Encoding.UTF8.GetString(r.Data!)}"
                : $"  실패: {r.Error!.Message}");
        }
        catch (Exception ex) { Console.WriteLine($"  파이프 서버 없음: {ex.Message}"); }
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex06 — HTTP REST API
//  SequenceMode: 0 (Parallel) — HTTP 비연결 기본값
//  또는 Window(2) 로 동시 요청 제한 시연
// ══════════════════════════════════════════════════════════════════════
static class Ex06_Http
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex06: HTTP REST API ===");

        var cfg = new HttpDeviceConfig(6, "REST-Controller", "http://localhost:5000")
        {
            WriteEndpoint = "/api/command",
            ReadEndpoint = "/api/status",
            ContentType = "application/json",
            SequenceMode = NetDeviceConfig.SequenceModes.Parallel,  // 0: 병렬
            PeriodicInterval = TimeSpan.FromSeconds(1),
            HttpTimeout = TimeSpan.FromSeconds(5)
        };
        Console.WriteLine($"  {cfg}");

        await using var channel = new RequestResponseChannel(
            cfg, HttpTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) =>
            Console.WriteLine($"  [HTTP] {Encoding.UTF8.GetString(frame)}");
        channel.DeviceStateChanged += (id, s) => Console.WriteLine($"  [HTTP] 상태: {s}");
        channel.DeviceErrorOccurred += (id, ex) => Console.WriteLine($"  [HTTP] {ex.Message}");

        try
        {
            await channel.StartAsync(ct);
            await channel.WriteAsync(Encoding.UTF8.GetBytes("{\"action\":\"start\"}"));
            Console.WriteLine("  POST /api/command 전송");
            await Task.Delay(3_000, ct);
        }
        catch (Exception ex) { Console.WriteLine($"  서버 없음: {ex.Message}"); }
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex07 — WebSocket
//  SequenceMode: 0 (Parallel)
// ══════════════════════════════════════════════════════════════════════
static class Ex07_WebSocket
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex07: WebSocket ===");

        var cfg = new WebSocketDeviceConfig(7, "WS-Monitor", "ws://localhost:8080/ws/sensor")
        {
            MaxRetries = 3,
            HeartbeatInterval = TimeSpan.FromSeconds(30)
            // SequenceMode = 0 (Parallel) ← 기본값
        };
        Console.WriteLine($"  {cfg}");

        await using var channel = new PassiveNetChannel(
            cfg, WebSocketTransport.FromConfig(cfg, enablePassiveReceive: true),
            new RawProtocol(), autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) =>
            Console.WriteLine($"  [WS] {Encoding.UTF8.GetString(frame)}");
        channel.DeviceStateChanged += (id, s) => Console.WriteLine($"  [WS] 상태: {s}");
        channel.DeviceErrorOccurred += (id, ex) => Console.WriteLine($"  [WS] {ex.Message}");

        try
        {
            await channel.StartAsync(ct);
            await channel.WriteAsync(Encoding.UTF8.GetBytes("{\"subscribe\":\"sensor_1\"}"));
            await Task.Delay(5_000, ct);
        }
        catch (Exception ex) { Console.WriteLine($"  서버 없음: {ex.Message}"); }
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex08 — MQTT
//  SequenceMode: 0 (Parallel)
// ══════════════════════════════════════════════════════════════════════
static class Ex08_Mqtt
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex08: MQTT ===");

        var cfg = new MqttDeviceConfig(8, "MQTT-Sensor", "localhost", 1883)
        {
            ClientId = "lssLib-Demo-Client",
            SubscribeTopic = "factory/+/sensor",
            PublishTopic = "factory/line1/command",
            QoS = 1,
            MaxRetries = 3
            // SequenceMode = 0 (Parallel) ← 기본값
        };
        Console.WriteLine($"  {cfg}");

        await using var channel = new PassiveNetChannel(
            cfg, MqttTransport.FromConfig(cfg), new RawProtocol(), autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) =>
            Console.WriteLine($"  [MQTT] {Encoding.UTF8.GetString(frame)}");
        channel.DeviceStateChanged += (id, s) => Console.WriteLine($"  [MQTT] 상태: {s}");
        channel.DeviceErrorOccurred += (id, ex) => Console.WriteLine($"  [MQTT] {ex.Message}");

        try
        {
            await channel.StartAsync(ct);
            await channel.WriteAsync(Encoding.UTF8.GetBytes("{\"setpoint\":75}"));
            await Task.Delay(5_000, ct);
        }
        catch (Exception ex) { Console.WriteLine($"  브로커 없음: {ex.Message}"); }
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex09 — Virtual Transport ★ 하드웨어 불필요
//  SequenceMode: 0/1/3 모두 시연 (인메모리이므로 즉시 실행)
// ══════════════════════════════════════════════════════════════════════
static class Ex09_Virtual
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex09: Virtual Transport (인메모리) ===");

        var hub = VirtualTransportHub.Create("sensor-sim");

        // ── SequenceMode 별 동작 시연 ────────────────────────────────
        foreach (int mode in new[] { 0, 1, 3 })
        {
            Console.WriteLine($"\n  [SequenceMode = {mode}] " +
                $"{(mode == 0 ? "Parallel" : mode == 1 ? "Sequential" : $"Window({mode})")}");

            var clientCfg = new VirtualDeviceConfig(mode + 90, $"VirtualCh-{mode}", hub,
                isServer: false)
            {
                SequenceMode = mode
            };
            Console.WriteLine($"  {clientCfg}");

            // ReadCommands 3개 등록
            clientCfg.AddReadCommand([0x01]);
            clientCfg.AddReadCommand([0x02]);
            clientCfg.AddReadCommand([0x03]);

            await using var channel = new PassiveNetChannel(
                clientCfg,
                VirtualTransport.FromConfig(clientCfg, enablePassiveReceive: true),
                new BinaryProtocol(stx: 0xAA));

            int receivedCount = 0;
            channel.DeviceFrameReceived += (id, frame) =>
            {
                receivedCount++;
                Console.WriteLine($"    ← 수신 #{receivedCount}: {frame.Length}B");
            };

            await channel.StartAsync(ct);

            // 시뮬레이터에서 프레임 주입
            var sim = new VirtualTransport(hub, isServer: true);
            await sim.ConnectAsync(ct);
            var proto = new BinaryProtocol(stx: 0xAA);

            for (int i = 1; i <= 3; i++)
            {
                var payload = new byte[12];
                Buffer.BlockCopy(BitConverter.GetBytes((uint)i), 0, payload, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(20f + i), 0, payload, 4, 4);
                await sim.InjectAsync(proto.Encode(payload), ct);
                Console.WriteLine($"    → 주입 #{i}");
                await Task.Delay(100, ct);
            }

            await Task.Delay(300, ct);
            Console.WriteLine($"    결과: 주입 3 / 수신 {receivedCount}");

            await sim.DisposeAsync();
            await channel.DisposeAsync();
        }
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex10 — 다중 Transport 혼합 (Registry)
//  Virtual(SeqMode=0) + TCP(SeqMode=0) 동시 운용
// ══════════════════════════════════════════════════════════════════════
static class Ex10_MultiTransport
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex10: 다중 Transport 혼합 (Registry) ===");

        var hub = VirtualTransportHub.Create("multi-demo");
        var virtualCfg = new VirtualDeviceConfig(101, "VirtualSensor", hub, isServer: false)
        {
            SequenceMode = NetDeviceConfig.SequenceModes.Parallel  // 0: 병렬
        };

        await using var virtualCh = new PassiveNetChannel(
            virtualCfg,
            VirtualTransport.FromConfig(virtualCfg, enablePassiveReceive: true),
            new BinaryProtocol(stx: 0xAA), autoRegister: true);

        var tcpCfg = new TcpDeviceConfig(102, "TcpMonitor", "127.0.0.1", 9000)
        {
            MaxRetries = 2,
            ReconnectBackoff = false,
            SequenceMode = NetDeviceConfig.SequenceModes.Parallel  // 0: 병렬
        };

        await using var tcpCh = new PassiveNetChannel(
            tcpCfg,
            TcpTransport.FromConfig(tcpCfg, enablePassiveReceive: true),
            new BinaryProtocol(stx: 0xAA), autoRegister: true);

        void OnFrame(int id, byte[] frame) =>
            Console.WriteLine($"  [Registry][{id}] {frame.Length}B 수신");
        void OnState(int id, NetState s) =>
            Console.WriteLine($"  [Registry][{id}] 상태: {s}");
        void OnError(int id, Exception ex) =>
            Console.WriteLine($"  [Registry][{id}] {ex.Message}");

        virtualCh.DeviceFrameReceived += OnFrame;
        virtualCh.DeviceStateChanged += OnState;
        tcpCh.DeviceFrameReceived += OnFrame;
        tcpCh.DeviceStateChanged += OnState;
        tcpCh.DeviceErrorOccurred += OnError;

        Console.WriteLine($"  Virtual: {virtualCfg}");
        Console.WriteLine($"  TCP:     {tcpCfg}");

        await NetDeviceRegistry.Instance.StartAllAsync(ct);
        Console.WriteLine($"  Registry 시작: {NetDeviceRegistry.Instance.Count}개 채널");

        // Virtual 채널 프레임 주입
        var sim = new VirtualTransport(hub, isServer: true);
        await sim.ConnectAsync(ct);
        var proto = new BinaryProtocol(stx: 0xAA);
        for (int i = 1; i <= 3; i++)
        {
            var payload = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes((uint)i), 0, payload, 0, 4);
            await sim.InjectAsync(proto.Encode(payload), ct);
            await Task.Delay(200, ct);
        }

        Console.WriteLine("\n  Registry 전체 상태:");
        foreach (var status in NetDeviceRegistry.Instance.GetStatusAll())
            Console.WriteLine(
                $"    [{status.DeviceId}:{status.DeviceName}] " +
                $"{status.State} 수신={status.TotalReceived}");

        var emergencyStop = proto.Encode([0xFF, 0x00]);
        await NetDeviceRegistry.Instance.BroadcastAsync(emergencyStop, NetPriority.Critical, ct);
        Console.WriteLine("  Emergency Stop 브로드캐스트");

        await sim.DisposeAsync();
        await NetDeviceRegistry.Instance.StopAllAsync();
        NetDeviceRegistry.Instance.Clear();
    }
}

// ══════════════════════════════════════════════════════════════════════
//  Ex11 — SharedMemory IPC
//  SequenceMode: 0 (Parallel)
// ══════════════════════════════════════════════════════════════════════
static class Ex11_SharedMemory
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Ex11: Shared Memory IPC ===");

        const string MAP_NAME = "lssLib_Demo_SM";

        var writerCfg = new SharedMemDeviceConfig(110, "SM-Writer", MAP_NAME,
            SharedMemoryRole.Writer);
        // SequenceMode = 0 (Parallel) ← 기본값
        Console.WriteLine($"  Writer: {writerCfg}");

        await using var writerCh = new PassiveNetChannel(
            writerCfg, SharedMemoryTransport.FromConfig(writerCfg), new RawProtocol());

        var readerCfg = new SharedMemDeviceConfig(111, "SM-Reader", MAP_NAME,
            SharedMemoryRole.Reader)
        { PeriodicInterval = TimeSpan.FromMilliseconds(5) };

        await using var readerCh = new PassiveNetChannel(
            readerCfg, SharedMemoryTransport.FromConfig(readerCfg), new RawProtocol());

        int readCount = 0;
        readerCh.DeviceFrameReceived += (id, frame) =>
        {
            readCount++;
            Console.WriteLine($"  [SM-Reader] {frame.Length}B 수신 #{readCount}: " +
                $"{Encoding.UTF8.GetString(frame)}");
        };

        await writerCh.StartAsync(ct);
        await readerCh.StartAsync(ct);
        Console.WriteLine("  공유 메모리 시작");

        for (int i = 1; i <= 3; i++)
        {
            var payload = Encoding.UTF8.GetBytes($"{{\"frame\":{i},\"val\":{i * 10.5f:F1}}}");
            await writerCh.WriteAsync(payload);
            Console.WriteLine($"  [SM-Writer] 전송 #{i}");
            await Task.Delay(50, ct);
        }

        await Task.Delay(500, ct);
        Console.WriteLine($"  결과: 전송 3 / 수신 {readCount}");
    }
}