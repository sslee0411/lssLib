// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/UdpTransport.cs
//  역할: UDP 송수신 전송 계층
// ══════════════════════════════════════════════════════════════════════

using System.Net;
using System.Net.Sockets;

namespace lssLib.Net;

/// <summary>
/// UDP 송수신 전송 계층.
/// </summary>
/// <remarks>
/// <para>UDP 는 비연결 프로토콜입니다. Connect() 는 기본 원격 주소만 설정합니다.</para>
/// <para>브로드캐스트 수신 시 LocalPort 를 RemotePort 와 동일하게 설정하세요.</para>
///
/// <b>사용 예시:</b>
/// <code>
/// // 브로드캐스트 수신 (포트 9100)
/// var cfg = new UdpDeviceConfig(4, "UDP-Sensor", "255.255.255.255", 9100)
/// { LocalPort = 9100 };
///
/// var t = UdpTransport.FromConfig(cfg, enablePassiveReceive: true);
///
/// await using var channel = new PassiveNetChannel(
///     cfg, t, new RawProtocol(), autoRegister: true);
///
/// channel.DeviceFrameReceived += (id, frame) => ProcessDatagram(frame);
/// await channel.StartAsync();
/// </code>
/// </remarks>
public sealed class UdpTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _strRemoteHost;
    private readonly int _remotePort;
    private readonly int _localPort;
    private readonly bool _enablePassiveReceive;

    private UdpClient? _udp;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    #endregion

    #region §2 ─ 생성자 / 팩토리

    /// <param name="remoteHost">원격 호스트 IP 또는 브로드캐스트 주소</param>
    /// <param name="remotePort">원격 포트 번호</param>
    /// <param name="localPort">로컬 수신 포트. 0=OS 자동 할당.</param>
    /// <param name="enablePassiveReceive">true=백그라운드 수신 루프 활성화</param>
    public UdpTransport(string remoteHost, int remotePort,
        int localPort = 0, bool enablePassiveReceive = false)
    {
        _strRemoteHost = remoteHost;
        _remotePort = remotePort;
        _localPort = localPort;
        _enablePassiveReceive = enablePassiveReceive;
    }

    /// <summary>UdpDeviceConfig 에서 생성합니다. DeviceName → LogSource 자동 주입.</summary>
    public static UdpTransport FromConfig(UdpDeviceConfig cfg,
        bool enablePassiveReceive = false)
        => new(cfg.RemoteHost, cfg.RemotePort, cfg.LocalPort, enablePassiveReceive)
        { LogSource = cfg.DeviceName };

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override Task ConnectCoreAsync(CancellationToken ct)
    {
        // LocalPort > 0 이면 지정 포트 바인딩 (브로드캐스트 수신 등)
        _udp = _localPort > 0
            ? new UdpClient(_localPort)
            : new UdpClient();

        // UDP Connect: 기본 원격 주소 설정 (실제 연결 아님)
        _udp.Connect(_strRemoteHost, _remotePort);

        if (_enablePassiveReceive)
        {
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => PassiveReceiveLoopAsync(_receiveCts.Token),
                _receiveCts.Token);
        }
        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync(CancellationToken ct)
    {
        _receiveCts?.Cancel();
        _udp?.Dispose();
        _udp = null;
        return Task.CompletedTask;
    }

    protected override async Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_udp is null)
            throw new InvalidOperationException($"[{LogSource}] UDP 소켓 없음");
        await _udp.SendAsync(data, data.Length).ConfigureAwait(false);
    }

    protected override async Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_udp is null)
            throw new InvalidOperationException($"[{LogSource}] UDP 소켓 없음");
        var result = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
        return result.Buffer;
    }

    protected override void DisposeCore()
    {
        _receiveCts?.Dispose();
        _udp?.Dispose();
    }

    #endregion

    #region §4 ─ Passive 수신 루프

    /// <summary>UDP 데이터그램 백그라운드 수신 루프.</summary>
    private async Task PassiveReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _udp is not null)
            {
                var result = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
                if (result.Buffer.Length > 0)
                    RaiseDataReceived(result.Buffer);
            }
        }
        catch (OperationCanceledException) { }
        catch when (!ct.IsCancellationRequested)
        {
            State = NetState.Error;
        }
    }

    #endregion
}