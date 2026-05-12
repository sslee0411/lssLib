// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/TcpTransport.cs
//  역할: TCP 클라이언트 전송 계층
//
//  ┌─ enablePassiveReceive 옵션 ───────────────────────────────────────┐
//  │                                                                   │
//  │  false (기본 / RequestResponse 모드):                             │
//  │    DispatchAsync 에서 Write → Read 직접 처리                     │
//  │    백그라운드 루프 없음                                            │
//  │                                                                   │
//  │  true (Passive 모드 / TCP Push 서버 연동):                        │
//  │    연결 직후 PassiveReceiveLoopAsync 백그라운드 루프 시작         │
//  │    stream.ReadAsync → RaiseDataReceived → NetChannelBase         │
//  │    → PushReceived → DeviceFrameReceived 이벤트                  │
//  │    루프 내 연결 끊김 감지 → State = Error → 재접속 트리거       │
//  │                                                                   │
//  │  ★ PassiveNetChannel 사용 시 반드시 true 지정:                    │
//  │    TcpTransport.FromConfig(cfg, enablePassiveReceive: true)       │
//  └───────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

using System.IO;
using System.Net.Sockets;

using lssLib.Net;

namespace lssLib.Net.Implementation;

/// <summary>
/// TCP 클라이언트 전송 계층.
/// </summary>
/// <remarks>
/// <b>모드별 생성 방법:</b>
/// <code>
/// // RequestResponse (PeriodicRead / RequestAsync) — 기본
/// var t = TcpTransport.FromConfig(tcpCfg);
///
/// // Passive (서버 Push 수신) — enablePassiveReceive: true 필수
/// var t = TcpTransport.FromConfig(tcpCfg, enablePassiveReceive: true);
/// </code>
///
/// <b>enablePassiveReceive=true 수신 흐름:</b>
/// <code>
/// 서버 stream.Write()
///   → [TcpTransport] PassiveReceiveLoopAsync: stream.ReadAsync
///     → RaiseDataReceived(bytes)
///       → [NetChannelBase] OnDataReceived
///         → [NetDispatchPipeline] PushReceived
///           → INetProtocol.TryDecode
///             → DeviceFrameReceived 이벤트
/// </code>
/// </remarks>
public sealed class TcpTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _connectTimeout;
    private readonly bool _enablePassiveReceive;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    #endregion

    #region §2 ─ 생성자 / 팩토리

    /// <param name="host">서버 호스트명 또는 IP</param>
    /// <param name="port">포트 번호</param>
    /// <param name="connectTimeout">연결 타임아웃. null=5초.</param>
    /// <param name="enablePassiveReceive">
    /// true = 백그라운드 수신 루프 활성화 (PassiveNetChannel 필수).
    /// false = DispatchAsync 에서 Write+Read 직접 처리 (RequestResponseChannel 기본).
    /// </param>
    public TcpTransport(string host, int port,
        TimeSpan? connectTimeout = null,
        bool enablePassiveReceive = false)
    {
        _host = host;
        _port = port;
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
        _enablePassiveReceive = enablePassiveReceive;
    }

    /// <summary>
    /// TcpDeviceConfig 에서 생성합니다.
    /// </summary>
    /// <param name="cfg">TCP 장비 설정</param>
    /// <param name="enablePassiveReceive">
    /// PassiveNetChannel 사용 시 반드시 true.<br/>
    /// RequestResponseChannel 은 false (기본).
    /// </param>
    public static TcpTransport FromConfig(TcpDeviceConfig cfg,
        bool enablePassiveReceive = false)
        => new(cfg.Host, cfg.Port, cfg.ConnectTimeout, enablePassiveReceive)
        { LogSource = cfg.DeviceName };

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override async Task ConnectCoreAsync(CancellationToken ct)
    {
        _client = new TcpClient { NoDelay = true };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_connectTimeout);

        await _client.ConnectAsync(_host, _port, timeoutCts.Token)
                     .ConfigureAwait(false);

        _stream = _client.GetStream();

        // Passive 모드: 연결 직후 백그라운드 수신 루프 시작
        if (_enablePassiveReceive)
        {
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(
                () => PassiveReceiveLoopAsync(_receiveCts.Token),
                _receiveCts.Token);
        }
    }

    protected override async Task DisconnectCoreAsync(CancellationToken ct)
    {
        // 수신 루프 먼저 중단
        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync().ConfigureAwait(false);
            try
            {
                if (_receiveTask is not null)
                    await _receiveTask.ConfigureAwait(false);
            }
            catch { }
            _receiveCts.Dispose();
            _receiveCts = null;
            _receiveTask = null;
        }

        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }

    protected override async Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_stream is null)
            throw new InvalidOperationException($"[{LogSource}] TCP 연결이 없습니다.");

        await _stream.WriteAsync(data, ct).ConfigureAwait(false);
    }

    protected override async Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_stream is null)
            throw new InvalidOperationException($"[{LogSource}] TCP 연결이 없습니다.");

        // enablePassiveReceive=true 이면 이 메서드는 호출되지 않습니다.
        // RequestResponse 모드의 DispatchAsync 에서만 호출됩니다.
        var buf = new byte[length > 0 ? length : 4096];
        int read = await _stream.ReadAsync(buf, ct).ConfigureAwait(false);

        if (read == 0)
            throw new IOException($"[{LogSource}] 서버가 연결을 종료했습니다.");

        return buf[..read];
    }

    protected override void DisposeCore()
    {
        _receiveCts?.Dispose();
        _stream?.Dispose();
        _client?.Dispose();
    }

    #endregion

    #region §4 ─ Passive 수신 루프

    /// <summary>
    /// Passive 모드 백그라운드 수신 루프.
    /// stream.ReadAsync → RaiseDataReceived → NetChannelBase.OnDataReceived 경로.
    ///
    /// 연결 끊김 감지:
    ///   read == 0 → 서버 정상 종료
    ///   IOException  → 서버 강제 종료 / 네트워크 단절
    ///   → State = Error → NetChannelBase.OnStateChanged(Error)
    ///     → NetConnectionManager.HandleErrorAsync → 재접속 루프
    /// </summary>
    private async Task PassiveReceiveLoopAsync(CancellationToken ct)
    {
        var buf = new byte[4096];

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_stream is null) break;

                int read = await _stream.ReadAsync(buf.AsMemory(), ct)
                                        .ConfigureAwait(false);

                if (read == 0)
                    throw new IOException($"[{LogSource}] 서버가 연결을 종료했습니다. (read=0)");

                // 수신 데이터 → NetChannelBase.OnDataReceived → PushReceived → 이벤트
                RaiseDataReceived(buf[..read]);
            }
        }
        catch (OperationCanceledException)
        {
            // 정상 종료 (DisconnectAsync / DisposeAsync 호출)
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // 예기치 못한 연결 끊김 → State = Error → OnStateChanged → 재접속 트리거
            // DisconnectAsync 는 NetConnectionManager 가 처리하므로 여기서는 State 변경만
            State = NetState.Error;
        }
    }

    #endregion
}