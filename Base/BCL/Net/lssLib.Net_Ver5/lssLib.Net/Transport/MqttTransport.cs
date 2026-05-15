// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/MqttTransport.cs  [v5.1]
//  SequenceMode = 0 (Parallel) — MQTT 브로커 비동기 기본값
//  MQTT 3.1.1 직접 구현 (BCL TCP 기반)
// ══════════════════════════════════════════════════════════════════════

using System.Net.Sockets;
using System.Text;

namespace lssLib.Net;

// ── Config ────────────────────────────────────────────────────────────

/// <summary>MQTT 통신 장비 설정.</summary>
/// <example><code>
/// var cfg = new MqttDeviceConfig(8, "IoT-Sensor", "192.168.1.100", 1883)
/// {
///     ClientId       = "lssLib-Client-01",
///     SubscribeTopic = "factory/+/sensor/#",
///     PublishTopic   = "factory/line1/command",
///     QoS            = 1
///     // SequenceMode = 0 (Parallel) ← 기본값
/// };
/// </code></example>
public sealed class MqttDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.Mqtt;

    public string BrokerHost { get; set; }
    public int BrokerPort { get; set; } = 1883;
    public string? ClientId { get; set; }
    public string? SubscribeTopic { get; set; }
    public string PublishTopic { get; set; } = "lssLib/data";
    public byte QoS { get; set; } = 1;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public ushort KeepAliveSeconds { get; set; } = 60;

    public MqttDeviceConfig(int deviceId, string deviceName,
        string brokerHost, int brokerPort = 1883)
        : base(deviceId, deviceName)
    {
        BrokerHost = brokerHost;
        BrokerPort = brokerPort;
        RetryDelay = TimeSpan.FromSeconds(3);
        ReconnectBackoff = true;
        SequenceMode = NetDeviceConfig.SequenceModes.Parallel;  // 0: 병렬
        HeartbeatInterval = TimeSpan.FromSeconds(30);
        RequestTimeout = TimeSpan.FromSeconds(5);
    }

    public override string ToString()
        => base.ToString() + $" | {BrokerHost}:{BrokerPort} [{SubscribeTopic ?? "-"}]";
}

// ── Transport ─────────────────────────────────────────────────────────

/// <summary>MQTT 3.1.1 전송 계층 (BCL TCP 기반 직접 구현).</summary>
public sealed class MqttTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly MqttDeviceConfig _mqttCfg;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private ushort _packetId = 1;

    #endregion

    #region §2 ─ 생성자 / 팩토리

    public MqttTransport(MqttDeviceConfig cfg)
    {
        _mqttCfg = cfg;
        LogSource = cfg.DeviceName;
    }

    public static MqttTransport FromConfig(MqttDeviceConfig cfg)
        => new(cfg);

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override async Task ConnectCoreAsync(CancellationToken ct)
    {
        _tcp = new TcpClient { NoDelay = true };
        await _tcp.ConnectAsync(_mqttCfg.BrokerHost, _mqttCfg.BrokerPort, ct)
            .ConfigureAwait(false);
        _stream = _tcp.GetStream();

        string strClientId = _mqttCfg.ClientId
            ?? $"lssLib-{Environment.MachineName}-{Random.Shared.Next(1000, 9999)}";

        await SendConnectAsync(strClientId, _mqttCfg.Username, _mqttCfg.Password,
            _mqttCfg.KeepAliveSeconds, ct).ConfigureAwait(false);
        await ReceiveConnAckAsync(ct).ConfigureAwait(false);

        if (_mqttCfg.SubscribeTopic is not null)
            await SendSubscribeAsync(_mqttCfg.SubscribeTopic, _mqttCfg.QoS, ct)
                .ConfigureAwait(false);

        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
    }

    protected override async Task DisconnectCoreAsync(CancellationToken ct)
    {
        _receiveCts?.Cancel();
        if (_stream is not null)
        {
            try { await SendDisconnectAsync(CancellationToken.None).ConfigureAwait(false); }
            catch { }
        }
        _stream?.Dispose();
        _tcp?.Dispose();
        _stream = null;
        _tcp = null;
    }

    protected override async Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException($"[{LogSource}] MQTT 연결 없음");
        await SendPublishAsync(_mqttCfg.PublishTopic, data, _mqttCfg.QoS, ct)
            .ConfigureAwait(false);
    }

    protected override async Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException($"[{LogSource}] MQTT 연결 없음");
        return await ReadNextPublishPayloadAsync(ct).ConfigureAwait(false);
    }

    protected override void DisposeCore()
    {
        _receiveCts?.Dispose();
        _stream?.Dispose();
        _tcp?.Dispose();
    }

    #endregion

    #region §4 ─ 수신 루프

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stream is not null)
            {
                int firstByte = await ReadByteAsync(_stream, ct).ConfigureAwait(false);
                if (firstByte < 0) break;

                byte packetType = (byte)(firstByte & 0xF0);
                int remaining = await ReadRemainingLengthAsync(_stream, ct).ConfigureAwait(false);

                switch (packetType)
                {
                    case 0x30: // PUBLISH
                        byte[] payload = await ReadPublishPayloadAsync(_stream, remaining, ct)
                            .ConfigureAwait(false);
                        if (payload.Length > 0) RaiseDataReceived(payload);
                        break;
                    default:
                        await SkipBytesAsync(_stream, remaining, ct).ConfigureAwait(false);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch when (!ct.IsCancellationRequested) { State = NetState.Error; }
    }

    private async Task<byte[]> ReadNextPublishPayloadAsync(CancellationToken ct)
    {
        if (_stream is null) return [];
        while (true)
        {
            int firstByte = await ReadByteAsync(_stream, ct).ConfigureAwait(false);
            if (firstByte < 0) throw new System.IO.IOException("MQTT 연결 종료");
            byte packetType = (byte)(firstByte & 0xF0);
            int remaining = await ReadRemainingLengthAsync(_stream, ct).ConfigureAwait(false);
            if (packetType == 0x30)
                return await ReadPublishPayloadAsync(_stream, remaining, ct).ConfigureAwait(false);
            await SkipBytesAsync(_stream, remaining, ct).ConfigureAwait(false);
        }
    }

    #endregion

    #region §5 ─ MQTT 패킷 빌더

    private async Task SendConnectAsync(string clientId, string? username, string? password,
        ushort keepAlive, CancellationToken ct)
    {
        using var ms = new System.IO.MemoryStream();
        WriteString(ms, "MQTT");
        ms.WriteByte(0x04);
        byte flags = 0x02;
        if (username is not null) flags |= 0x80;
        if (password is not null) flags |= 0x40;
        ms.WriteByte(flags);
        ms.WriteByte((byte)(keepAlive >> 8));
        ms.WriteByte((byte)(keepAlive & 0xFF));
        WriteString(ms, clientId);
        if (username is not null) WriteString(ms, username);
        if (password is not null) WriteString(ms, password);
        await SendPacketAsync(0x10, ms.ToArray(), ct).ConfigureAwait(false);
    }

    private async Task ReceiveConnAckAsync(CancellationToken ct)
    {
        if (_stream is null) return;
        await ReadByteAsync(_stream, ct).ConfigureAwait(false);
        await ReadByteAsync(_stream, ct).ConfigureAwait(false);
        await ReadByteAsync(_stream, ct).ConfigureAwait(false);
        int returnCode = await ReadByteAsync(_stream, ct).ConfigureAwait(false);
        if (returnCode != 0)
            throw new InvalidOperationException(
                $"[{LogSource}] MQTT CONNACK 실패: 코드={returnCode}");
    }

    private async Task SendSubscribeAsync(string topic, byte qos, CancellationToken ct)
    {
        using var ms = new System.IO.MemoryStream();
        ushort id = _packetId++;
        ms.WriteByte((byte)(id >> 8));
        ms.WriteByte((byte)(id & 0xFF));
        WriteString(ms, topic);
        ms.WriteByte(qos);
        await SendPacketAsync(0x82, ms.ToArray(), ct).ConfigureAwait(false);
    }

    private async Task SendPublishAsync(string topic, byte[] payload, byte qos,
        CancellationToken ct)
    {
        using var ms = new System.IO.MemoryStream();
        WriteString(ms, topic);
        if (qos > 0)
        {
            ushort id = _packetId++;
            ms.WriteByte((byte)(id >> 8));
            ms.WriteByte((byte)(id & 0xFF));
        }
        ms.Write(payload);
        byte flags = (byte)(0x30 | (qos << 1));
        await SendPacketAsync(flags, ms.ToArray(), ct).ConfigureAwait(false);
    }

    private async Task SendDisconnectAsync(CancellationToken ct)
    {
        if (_stream is null) return;
        await _stream.WriteAsync(new byte[] { 0xE0, 0x00 }, ct).ConfigureAwait(false);
    }

    private async Task SendPacketAsync(byte fixedHeader, byte[] data, CancellationToken ct)
    {
        if (_stream is null) return;
        using var ms = new System.IO.MemoryStream();
        ms.WriteByte(fixedHeader);
        EncodeRemainingLength(ms, data.Length);
        ms.Write(data);
        await _stream.WriteAsync(ms.ToArray(), ct).ConfigureAwait(false);
    }

    #endregion

    #region §6 ─ 유틸리티

    private static void WriteString(System.IO.Stream s, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        s.WriteByte((byte)(bytes.Length >> 8));
        s.WriteByte((byte)(bytes.Length & 0xFF));
        s.Write(bytes);
    }

    private static void EncodeRemainingLength(System.IO.Stream s, int length)
    {
        do
        {
            byte encoded = (byte)(length % 128);
            length /= 128;
            if (length > 0) encoded |= 0x80;
            s.WriteByte(encoded);
        }
        while (length > 0);
    }

    private static async Task<int> ReadByteAsync(NetworkStream s, CancellationToken ct)
    {
        var buf = new byte[1];
        int read = await s.ReadAsync(buf.AsMemory(), ct).ConfigureAwait(false);
        return read == 0 ? -1 : buf[0];
    }

    private static async Task<int> ReadRemainingLengthAsync(NetworkStream s, CancellationToken ct)
    {
        int multiplier = 1, value = 0;
        byte encoded;
        do
        {
            int b = await ReadByteAsync(s, ct).ConfigureAwait(false);
            if (b < 0) return 0;
            encoded = (byte)b;
            value += (encoded & 0x7F) * multiplier;
            multiplier *= 128;
        }
        while ((encoded & 0x80) != 0);
        return value;
    }

    private static async Task<byte[]> ReadPublishPayloadAsync(NetworkStream s,
        int remaining, CancellationToken ct)
    {
        var lenBuf = new byte[2];
        await s.ReadAsync(lenBuf.AsMemory(), ct).ConfigureAwait(false);
        int topicLen = (lenBuf[0] << 8) | lenBuf[1];
        var topicBuf = new byte[topicLen];
        await s.ReadAsync(topicBuf.AsMemory(), ct).ConfigureAwait(false);
        int payloadLen = remaining - 2 - topicLen;
        if (payloadLen <= 0) return [];
        var payload = new byte[payloadLen];
        await s.ReadAsync(payload.AsMemory(), ct).ConfigureAwait(false);
        return payload;
    }

    private static async Task SkipBytesAsync(NetworkStream s, int count, CancellationToken ct)
    {
        if (count <= 0) return;
        var buf = new byte[count];
        await s.ReadAsync(buf.AsMemory(), ct).ConfigureAwait(false);
    }

    #endregion
}