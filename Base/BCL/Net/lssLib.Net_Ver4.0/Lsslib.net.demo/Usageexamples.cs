// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Demo/UsageExamples.cs
//  역할: 조립 패턴 예시 + v3→v4 마이그레이션 가이드
// ══════════════════════════════════════════════════════════════════════

//using lssLib.Log;
using lssLib.Net;
using lssLib.Net.Implementation;

namespace lssLib.Net.Demo;

// ══════════════════════════════════════════════════════════════════════
//  예시 1 — Serial Modbus RTU (RequestResponse, 순차, autoRegister)
// ══════════════════════════════════════════════════════════════════════
static class Example1_SerialModbus
{
    public static async Task RunAsync()
    {
        // §1. 설정
        var cfg = new SerialDeviceConfig(1, "Modbus-PLC", "COM3", 9600)
        {
            IsRetryEnabled = true,
            RetryTarget = RetryTarget.All,
            MaxRetries = 5,
            RetryDelay = TimeSpan.FromMilliseconds(100),
            ReconnectBackoff = false,
            IsSequential = true,
            PeriodicInterval = TimeSpan.FromMilliseconds(50),
            RequestTimeout = TimeSpan.FromMilliseconds(500)
        };
        cfg.AddReadCommand([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD]);
        cfg.AddReadCommand([0x01, 0x03, 0x00, 0x10, 0x00, 0x05, 0x84, 0x0E]);

        // §2. 채널 조립 (autoRegister=true → Registry 자동 등록)
        await using var channel = new RequestResponseChannel(
            cfg,
            SerialTransport.FromConfig(cfg),
            new RawProtocol(),
            autoRegister: true);

        // §3. 이벤트 구독
        channel.DeviceFrameReceived += (id, frame) =>
        {
            // lssLib.Binary 파싱 (주석 해제)
            // var result  = frame.ToParser().Parse(ModbusSchema.Default);
            // ushort[] regs = result.GetUInt16BEArray("Data");
            // LogManager.Instance.Debug("Modbus-PLC", $"수신 {frame.Length}B");
        };

        channel.DeviceStateChanged += (id, state) => { };
        // LogManager.Instance.Info("Modbus-PLC", $"상태: {state}");

        // v4: WriteAsync 실패는 이벤트로 통보
        channel.DeviceErrorOccurred += (id, ex) => { };
        // LogManager.Instance.Error("Modbus-PLC", $"오류: {ex.Message}");

        // §4. 시작
        await channel.StartAsync();

        // §5. Write (Read 보다 항상 우선)
        await channel.WriteAsync(
            [0x01, 0x06, 0x00, 0x01, 0x00, 0x64, 0xD9, 0x98]);

        // §6. 단발 요청-응답
        NetResult r = await channel.RequestAsync(
            [0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A],
            timeout: TimeSpan.FromMilliseconds(500));

        if (r.IsOk) { 
            // LogManager.Instance.Info("Modbus-PLC", $"응답: {r.Data!.Length}B"); 
        }
        else { 
           // LogManager.Instance.Error("Modbus-PLC", r.Error!.Message);
        }
        // §7. 통계 조회
        // LogManager.Instance.Info("Modbus-PLC", channel.Statistics.ToString());
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 2 — TCP Passive (카메라, Heartbeat, 지수 백오프)
// ══════════════════════════════════════════════════════════════════════
static class Example2_TcpPassive
{
    public static async Task RunAsync(CancellationToken ct)
    {
        var cfg = new TcpDeviceConfig(2, "Vision-Cam", "192.168.1.50", 5000)
        {
            RetryTarget = RetryTarget.Connect | RetryTarget.Write,
            MaxRetries = 5,
            ReconnectBackoff = true,
            HeartbeatInterval = TimeSpan.FromSeconds(30)
        };

        await using var channel = new PassiveNetChannel(
            cfg, TcpTransport.FromConfig(cfg), new BinaryProtocol(0xAA),
            autoRegister: true);

        channel.DeviceFrameReceived += (id, frame) => { };
        //    LogManager.Instance.Debug("Vision-Cam", $"프레임 {frame.Length}B");

        channel.DeviceStateChanged += (id, state) => { };
        //    LogManager.Instance.Info("Vision-Cam", $"상태: {state}");

        await channel.StartAsync(ct);

        // 비동기 열거 (Passive 채널 권장)
        await foreach (var frame in channel.ReadAllAsync(ct)) { };
        //    LogManager.Instance.Debug("Vision-Cam", $"처리 {frame.Length}B");
    }
}

// ══════════════════════════════════════════════════════════════════════
//  예시 3 — NetDeviceRegistry 다중 장비 관리 (v4 핵심)
// ══════════════════════════════════════════════════════════════════════
static class Example3_MultiDevice
{
    static readonly byte[] EmergencyStop = [0xFF, 0x00];

    public static async Task RunAsync()
    {
        // 채널 팩토리 (autoRegister=true 로 Registry 자동 등록)
        var channels = new NetChannelBase[]
        {
            new RequestResponseChannel(
                new TcpDeviceConfig(1, "PLC-Line1", "192.168.1.10", 502),
                TcpTransport.FromConfig(new TcpDeviceConfig(1, "PLC-Line1", "192.168.1.10", 502)),
                new BinaryProtocol(), autoRegister: true),

            new RequestResponseChannel(
                new SerialDeviceConfig(2, "Inverter-COM3", "COM3", 9600),
                SerialTransport.FromConfig(new SerialDeviceConfig(2, "Inverter-COM3", "COM3", 9600)),
                new RawProtocol(), autoRegister: true)
        };

        foreach (var ch in channels)
        {
            ch.DeviceFrameReceived += (id, f) => { };
            //   LogManager.Instance.Debug($"Device-{id}", $"수신 {f.Length}B");
            ch.DeviceStateChanged += (id, s) => { };
            //    LogManager.Instance.Info($"Device-{id}", $"상태: {s}");
        }

        // Registry 일괄 시작
        await NetDeviceRegistry.Instance.StartAllAsync();

        // DeviceId 로 개별 접근
        var plc = NetDeviceRegistry.Instance.Get(1);
        if (plc?.IsConnected == true)
            await plc.WriteAsync(EmergencyStop, NetPriority.Critical);

        // 연결된 장비 전체 브로드캐스트
        await NetDeviceRegistry.Instance.BroadcastAsync(EmergencyStop, NetPriority.Critical);

        // 전체 통계 로그
        NetDeviceRegistry.Instance.LogStatus();

        // 앱 종료 시 일괄 정지
        await NetDeviceRegistry.Instance.StopAllAsync();
    }
}

// ══════════════════════════════════════════════════════════════════════
//  v3 → v4 마이그레이션 가이드
// ══════════════════════════════════════════════════════════════════════
//
//  ✅ WriteAsync 반환값 변경
//     v3: var r = await channel.WriteAsync(frame); if (r.IsError) { ... }
//     v4: await channel.WriteAsync(frame);
//         // 실패 → channel.DeviceErrorOccurred 이벤트에서 처리
//
//  ✅ autoRegister 옵션 추가
//     v4: new RequestResponseChannel(cfg, transport, protocol, autoRegister: true)
//         → NetDeviceRegistry.Instance.Get(1) 로 전역 접근
//
//  ✅ 통계 수집
//     channel.Statistics.TotalSent / AvgResponseMs / TotalReconnects 등
//     channel.Statistics.Snapshot() → WPF DataGrid 바인딩
//
//  ✅ 앱 종료 단순화
//     v3: await Task.WhenAll(channels.Select(c => c.DisposeAsync().AsTask()));
//     v4: await NetDeviceRegistry.Instance.StopAllAsync();
//
//  ✅ 스케줄러 Pause/Resume
//     재접속 시 자동 처리 — 별도 코드 불필요
