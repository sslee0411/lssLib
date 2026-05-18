// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/VirtualTransport.cs  [v5.1]
//  SequenceMode = 0 (Parallel) — 인메모리 테스트 기본값
// ══════════════════════════════════════════════════════════════════════

using System.Threading.Channels;

namespace lssLib.Net;

// ── VirtualTransportHub ───────────────────────────────────────────────

/// <summary>두 VirtualTransport 인스턴스를 연결하는 인메모리 허브.</summary>
/// <example><code>
/// var hub    = VirtualTransportHub.Create("sim-pipe");
/// var server = new VirtualTransport(hub, isServer: true);
/// var client = new VirtualTransport(hub, isServer: false, enablePassiveReceive: true);
///
/// await client.ConnectAsync();
/// await server.ConnectAsync();
///
/// // 서버 → 클라이언트 주입
/// await server.InjectAsync(frame);
/// </code></example>
public sealed class VirtualTransportHub
{
    internal readonly Channel<byte[]> ServerToClient =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleWriter = true });
    internal readonly Channel<byte[]> ClientToServer =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleWriter = true });

    public string Name { get; }

    private VirtualTransportHub(string name) => Name = name;

    public static VirtualTransportHub Create(string name) => new(name);
}

// ── Transport ─────────────────────────────────────────────────────────

/// <summary>인메모리 가상 Transport. VirtualTransportHub 를 통해 두 채널이 통신합니다.</summary>
public sealed class VirtualTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly VirtualTransportHub _hub;
    private readonly bool _isServer;
    private readonly bool _enablePassiveReceive;

    private Channel<byte[]> WriteChannel => _isServer
        ? _hub.ServerToClient
        : _hub.ClientToServer;

    private Channel<byte[]> ReadChannel => _isServer
        ? _hub.ClientToServer
        : _hub.ServerToClient;

    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    #endregion

    #region §2 ─ 생성자 / 팩토리

    public VirtualTransport(VirtualTransportHub hub,
        bool isServer = false, bool enablePassiveReceive = false)
    {
        _hub = hub;
        _isServer = isServer;
        _enablePassiveReceive = enablePassiveReceive;
        LogSource = $"Virtual({hub.Name})";
    }

    public static VirtualTransport FromConfig(VirtualDeviceConfig cfg,
        bool enablePassiveReceive = false)
        => new(cfg.Hub, cfg.IsServer, enablePassiveReceive)
        { LogSource = cfg.DeviceName };

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override Task ConnectCoreAsync(CancellationToken ct)
    {
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
        return Task.CompletedTask;
    }

    protected override async Task WriteCoreAsync(byte[] data, CancellationToken ct)
        => await WriteChannel.Writer.WriteAsync(data, ct).ConfigureAwait(false);

    protected override async Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
        => await ReadChannel.Reader.ReadAsync(ct).ConfigureAwait(false);

    protected override void DisposeCore() => _receiveCts?.Dispose();

    #endregion

    #region §4 ─ Passive 수신 루프

    private async Task PassiveReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var data in ReadChannel.Reader.ReadAllAsync(ct))
                RaiseDataReceived(data);
        }
        catch (OperationCanceledException) { }
    }

    #endregion

    /// <summary>시뮬레이터 Helper — 반대편 채널에 데이터를 직접 주입합니다.</summary>
    public async Task InjectAsync(byte[] data, CancellationToken ct = default)
        => await WriteChannel.Writer.WriteAsync(data, ct).ConfigureAwait(false);
}