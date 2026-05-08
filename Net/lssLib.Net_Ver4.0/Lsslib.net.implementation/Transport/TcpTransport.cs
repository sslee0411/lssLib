// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/TcpTransport.cs
//  역할: TCP 클라이언트 전송 계층
// ══════════════════════════════════════════════════════════════════════

using System.Net.Sockets;

using lssLib.Net;

namespace Lsslib.net.implementation;

/// <summary>
/// TCP 클라이언트 전송 계층.
/// </summary>
/// <remarks>
/// <b>생성 방법:</b>
/// <code>
/// // FromConfig 팩토리 권장 (LogSource 자동 주입)
/// var t = TcpTransport.FromConfig(tcpCfg);
///
/// // 직접 생성
/// var t = new TcpTransport("192.168.1.10", 502);
/// </code>
/// </remarks>
public sealed class TcpTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _connectTimeout;
    private TcpClient? _client;
    private NetworkStream? _stream;

    #endregion

    #region §2 ─ 생성자 / 팩토리

    /// <param name="host">서버 호스트명 또는 IP</param>
    /// <param name="port">포트 번호</param>
    /// <param name="connectTimeout">연결 타임아웃. null=5초.</param>
    public TcpTransport(string host, int port, TimeSpan? connectTimeout = null)
    {
        _host = host;
        _port = port;
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>TcpDeviceConfig 에서 생성합니다. LogSource 자동 주입.</summary>
    public static TcpTransport FromConfig(TcpDeviceConfig cfg)
        => new(cfg.Host, cfg.Port, cfg.ConnectTimeout)
        { LogSource = cfg.DeviceName };

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override async Task ConnectCoreAsync(CancellationToken ct)
    {
        _client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_connectTimeout);
        await _client.ConnectAsync(_host, _port, timeoutCts.Token).ConfigureAwait(false);
        _stream = _client.GetStream();
    }

    protected override Task DisconnectCoreAsync(CancellationToken ct)
    {
        _stream?.Dispose(); _client?.Dispose();
        _stream = null; _client = null;
        return Task.CompletedTask;
    }

    protected override async Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_stream is null){
            throw new InvalidOperationException($"포트가 열려있지 않습니다.");
            // throw new InvalidOperationException($"[{LogSource}] TCP 연결이 없습니다.");
        }
        await _stream.WriteAsync(data, ct).ConfigureAwait(false);
    }

    protected override async Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_stream is null) {
            throw new InvalidOperationException($"포트가 열려있지 않습니다.");
            //throw new InvalidOperationException($"[{LogSource}] TCP 연결이 없습니다.");
        }
        var buf = new byte[length];
        int read = await _stream.ReadAsync(buf, ct).ConfigureAwait(false);
        return buf[..read];
    }

    protected override void DisposeCore()
    {
        _stream?.Dispose();
        _client?.Dispose();
    }

    #endregion
}