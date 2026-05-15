// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/WebSocketTransport.cs  [v5.1]
//  SequenceMode = 0 (Parallel)
// ══════════════════════════════════════════════════════════════════════

using System.Net.WebSockets;

namespace lssLib.Net;

// ── Config ────────────────────────────────────────────────────────────

/// <summary>WebSocket 통신 장비 설정.</summary>
/// <example><code>
/// var cfg = new WebSocketDeviceConfig(7, "WS-Monitor", "ws://192.168.1.10:8080/ws");
/// // cfg.SequenceMode == 0 (Parallel) ← 기본값
/// </code></example>
public sealed class WebSocketDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.WebSocket;

    public string Url { get; set; }
    public string? SubProtocol { get; set; }
    public int ReceiveBufferSize { get; set; } = 8192;
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public WebSocketDeviceConfig(int deviceId, string deviceName, string url)
        : base(deviceId, deviceName)
    {
        Url = url;
        RetryDelay = TimeSpan.FromSeconds(2);
        ReconnectBackoff = true;
        SequenceMode = NetDeviceConfig.SequenceModes.Parallel;  // 0: 병렬
        HeartbeatInterval = TimeSpan.FromSeconds(30);
        RequestTimeout = TimeSpan.FromSeconds(5);
    }

    public override string ToString()
        => base.ToString() + $" | {Url}";
}

// ── Transport ─────────────────────────────────────────────────────────

/// <summary>WebSocket 실시간 양방향 통신 전송 계층.</summary>
public sealed class WebSocketTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly Uri _uri;
    private readonly string? _strSubProtocol;
    private readonly int _receiveBufferSize;
    private readonly TimeSpan _connectTimeout;
    private readonly bool _enablePassiveReceive;

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    #endregion

    #region §2 ─ 생성자 / 팩토리

    public WebSocketTransport(string url,
        string? subProtocol = null,
        int receiveBufferSize = 8192,
        TimeSpan? connectTimeout = null,
        bool enablePassiveReceive = false)
    {
        _uri = new Uri(url);
        _strSubProtocol = subProtocol;
        _receiveBufferSize = receiveBufferSize;
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
        _enablePassiveReceive = enablePassiveReceive;
    }

    public static WebSocketTransport FromConfig(WebSocketDeviceConfig cfg,
        bool enablePassiveReceive = false)
        => new(cfg.Url, cfg.SubProtocol, cfg.ReceiveBufferSize,
               cfg.ConnectTimeout, enablePassiveReceive)
        { LogSource = cfg.DeviceName };

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override async Task ConnectCoreAsync(CancellationToken ct)
    {
        _ws = new ClientWebSocket();
        if (_strSubProtocol is not null)
            _ws.Options.AddSubProtocol(_strSubProtocol);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_connectTimeout);
        await _ws.ConnectAsync(_uri, timeoutCts.Token).ConfigureAwait(false);

        if (_enablePassiveReceive)
        {
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => PassiveReceiveLoopAsync(_receiveCts.Token),
                _receiveCts.Token);
        }
    }

    protected override async Task DisconnectCoreAsync(CancellationToken ct)
    {
        _receiveCts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                    "정상 종료", CancellationToken.None).ConfigureAwait(false);
            }
            catch { }
        }
        _ws?.Dispose();
        _ws = null;
    }

    protected override async Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_ws is null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException($"[{LogSource}] WebSocket 연결 없음");
        await _ws.SendAsync(new ArraySegment<byte>(data),
            WebSocketMessageType.Binary, true, ct).ConfigureAwait(false);
    }

    protected override async Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_ws is null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException($"[{LogSource}] WebSocket 연결 없음");
        return await ReceiveOneMessageAsync(_ws, _receiveBufferSize, ct).ConfigureAwait(false);
    }

    protected override void DisposeCore()
    {
        _receiveCts?.Dispose();
        _ws?.Dispose();
    }

    #endregion

    #region §4 ─ Passive 수신 루프

    private async Task PassiveReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                var data = await ReceiveOneMessageAsync(_ws!, _receiveBufferSize, ct)
                    .ConfigureAwait(false);
                if (data.Length > 0) RaiseDataReceived(data);
            }
        }
        catch (OperationCanceledException) { }
        catch when (!ct.IsCancellationRequested) { State = NetState.Error; }
    }

    private static async Task<byte[]> ReceiveOneMessageAsync(
        ClientWebSocket ws, int bufferSize, CancellationToken ct)
    {
        var buffer = new byte[bufferSize];
        using var ms = new System.IO.MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct)
                .ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new System.IO.IOException("WebSocket 서버가 연결을 종료했습니다.");
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return ms.ToArray();
    }

    #endregion
}