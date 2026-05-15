// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/NamedPipeTransport.cs  [v5.1]
//  SequenceMode = 1 (Sequential) — 파이프 구조적 IPC 기본값
// ══════════════════════════════════════════════════════════════════════

using System.IO.Pipes;

namespace lssLib.Net;

// ── Config ────────────────────────────────────────────────────────────

/// <summary>Named Pipe 통신 장비 설정.</summary>
/// <example><code>
/// // 동일 PC
/// var cfg = new NamedPipeDeviceConfig(5, "Pipe-IPC", ".", "lssLib-control");
/// // cfg.SequenceMode == 1 (Sequential) ← 기본값
///
/// // 네트워크 파이프 (Windows 전용)
/// var cfg2 = new NamedPipeDeviceConfig(6, "Net-Pipe", "192.168.1.10", "lssLib-ctrl");
/// </code></example>
public sealed class NamedPipeDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.NamedPipe;

    public string ServerName { get; set; }
    public string PipeName { get; set; }
    public int ConnectTimeoutMs { get; set; } = 5000;

    public NamedPipeDeviceConfig(int deviceId, string deviceName,
        string serverName, string pipeName)
        : base(deviceId, deviceName)
    {
        ServerName = serverName;
        PipeName = pipeName;
        RetryDelay = TimeSpan.FromSeconds(1);
        ReconnectBackoff = false;
        SequenceMode = NetDeviceConfig.SequenceModes.Sequential;  // 1: 단일 순차
        RequestTimeout = TimeSpan.FromSeconds(2);
        HeartbeatInterval = TimeSpan.Zero;
    }

    public override string ToString()
        => base.ToString() + $" | \\\\{ServerName}\\pipe\\{PipeName}";
}

// ── Transport ─────────────────────────────────────────────────────────

/// <summary>Named Pipe 클라이언트 전송 계층.</summary>
public sealed class NamedPipeTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _strServerName;
    private readonly string _strPipeName;
    private readonly int _connectTimeoutMs;
    private readonly bool _enablePassiveReceive;

    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    #endregion

    #region §2 ─ 생성자 / 팩토리

    public NamedPipeTransport(string serverName, string pipeName,
        int connectTimeoutMs = 5000, bool enablePassiveReceive = false)
    {
        _strServerName = serverName;
        _strPipeName = pipeName;
        _connectTimeoutMs = connectTimeoutMs;
        _enablePassiveReceive = enablePassiveReceive;
    }

    public static NamedPipeTransport FromConfig(NamedPipeDeviceConfig cfg,
        bool enablePassiveReceive = false)
        => new(cfg.ServerName, cfg.PipeName, cfg.ConnectTimeoutMs, enablePassiveReceive)
        { LogSource = cfg.DeviceName };

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override async Task ConnectCoreAsync(CancellationToken ct)
    {
        _pipe = new NamedPipeClientStream(_strServerName, _strPipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        await _pipe.ConnectAsync(_connectTimeoutMs, ct).ConfigureAwait(false);

        if (_enablePassiveReceive)
        {
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => PassiveReceiveLoopAsync(_receiveCts.Token),
                _receiveCts.Token);
        }
    }

    protected override Task DisconnectCoreAsync(CancellationToken ct)
    {
        _receiveCts?.Cancel();
        _pipe?.Dispose();
        _pipe = null;
        return Task.CompletedTask;
    }

    protected override async Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_pipe is null || !_pipe.IsConnected)
            throw new InvalidOperationException($"[{LogSource}] 파이프 연결 없음");
        var lenBuf = BitConverter.GetBytes(data.Length);
        await _pipe.WriteAsync(lenBuf, ct).ConfigureAwait(false);
        await _pipe.WriteAsync(data, ct).ConfigureAwait(false);
        await _pipe.FlushAsync(ct).ConfigureAwait(false);
    }

    protected override async Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_pipe is null || !_pipe.IsConnected)
            throw new InvalidOperationException($"[{LogSource}] 파이프 연결 없음");
        var lenBuf = new byte[4];
        await ReadExactAsync(_pipe, lenBuf, ct).ConfigureAwait(false);
        int dataLen = BitConverter.ToInt32(lenBuf);
        var buf = new byte[dataLen];
        await ReadExactAsync(_pipe, buf, ct).ConfigureAwait(false);
        return buf;
    }

    protected override void DisposeCore()
    {
        _receiveCts?.Dispose();
        _pipe?.Dispose();
    }

    #endregion

    #region §4 ─ Passive 수신 루프

    private async Task PassiveReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _pipe?.IsConnected == true)
            {
                var lenBuf = new byte[4];
                await ReadExactAsync(_pipe!, lenBuf, ct).ConfigureAwait(false);
                int dataLen = BitConverter.ToInt32(lenBuf);
                var buf = new byte[dataLen];
                await ReadExactAsync(_pipe!, buf, ct).ConfigureAwait(false);
                RaiseDataReceived(buf);
            }
        }
        catch (OperationCanceledException) { }
        catch when (!ct.IsCancellationRequested) { State = NetState.Error; }
    }

    private static async Task ReadExactAsync(PipeStream pipe, byte[] buf, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buf.Length)
        {
            int read = await pipe.ReadAsync(buf.AsMemory(offset), ct).ConfigureAwait(false);
            if (read == 0) throw new System.IO.IOException("파이프 연결이 끊어졌습니다.");
            offset += read;
        }
    }

    #endregion
}