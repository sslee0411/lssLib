// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net.TcpTestServer · TcpServerCore.cs
//  역할: TCP 서버 엔진 — Push 모드 / Echo 모드 지원
//
//  ┌─ Push 모드 (Passive 클라이언트 테스트용) ────────────────────────┐
//  │  클라이언트 1개당 HandlePushClientAsync Task 1개 생성            │
//  │  각 Task 가 PushIntervalMs 마다 독립적으로 센서 프레임 전송      │
//  │                                                                   │
//  │  클라이언트 측 설정:                                              │
//  │    new PassiveNetChannel(cfg,                                     │
//  │        TcpTransport.FromConfig(cfg, enablePassiveReceive: true), │
//  │        new BinaryProtocol(stx: 0xAA))                            │
//  └──────────────────────────────────────────────────────────────────┘
//  ┌─ Echo 모드 (RequestResponse 클라이언트 테스트용) ────────────────┐
//  │  클라이언트 요청 수신 → 센서 응답 프레임 반환                    │
//  └──────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

using System.Net;
using System.Net.Sockets;

namespace lssLib.Net.TcpTestServer;

/// <summary>서버 동작 모드.</summary>
public enum ServerMode
{
    /// <summary>주기적으로 센서 데이터를 push — Passive 클라이언트 테스트.</summary>
    Push,
    /// <summary>클라이언트 요청에 응답 — RequestResponse 클라이언트 테스트.</summary>
    Echo
}

/// <summary>연결된 클라이언트 정보.</summary>
public sealed record ClientInfo(int Id, string RemoteEndPoint, DateTime ConnectedAt)
{
    public string Elapsed => $"{(DateTime.Now - ConnectedAt).TotalSeconds:F0}s";
}

/// <summary>
/// TCP 테스트 서버 엔진.
/// Push / Echo 두 모드를 지원합니다.
/// </summary>
public sealed class TcpServerCore : IAsyncDisposable
{
    #region §1 ─ 필드

    private TcpListener? _listener;
    private CancellationTokenSource _cts = new();
    private readonly List<Task> _clientTasks = [];
    private readonly Dictionary<int, ClientInfo> _clients = [];
    private readonly object _clientLock = new();

    private uint _frameId;      // 전송 프레임 카운터
    private long _totalSent;    // 총 전송 프레임 수
    private long _totalReceived;// 총 수신 프레임 수
    private int _nextClientId = 1;

    private readonly Random _rng = new();

    #endregion

    #region §2 ─ 공개 프로퍼티

    public bool IsRunning { get; private set; }
    public int Port { get; private set; }
    public ServerMode Mode { get; private set; }
    public int PushIntervalMs { get; private set; }
    public long TotalSent => Interlocked.Read(ref _totalSent);
    public long TotalReceived => Interlocked.Read(ref _totalReceived);

    public IReadOnlyList<ClientInfo> ConnectedClients
    {
        get { lock (_clientLock) return _clients.Values.ToList(); }
    }

    #endregion

    #region §3 ─ 이벤트

    /// <summary>클라이언트 연결/해제 변경 시 발생.</summary>
    public event Action? ClientsChanged;

    /// <summary>로그 메시지 발생 시.</summary>
    public event Action<string>? Log;

    /// <summary>통계 변경 시 (전송/수신 카운터 갱신).</summary>
    public event Action? StatsChanged;

    #endregion

    #region §4 ─ Start / Stop

    /// <summary>TCP 서버를 시작합니다.</summary>
    public async Task StartAsync(int port, ServerMode mode, int pushIntervalMs = 500)
    {
        if (IsRunning) return;

        Port = port;
        Mode = mode;
        PushIntervalMs = pushIntervalMs;
        _cts = new CancellationTokenSource();
        IsRunning = true;

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        RaiseLog($"서버 시작 — 포트: {port} / 모드: {mode}" +
                 (mode == ServerMode.Push ? $" / 간격: {pushIntervalMs}ms" : ""));

        // Accept 루프 시작 (fire-and-forget)
        _ = AcceptLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    /// <summary>TCP 서버를 정지합니다.</summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;

        IsRunning = false;
        _cts.Cancel();
        _listener?.Stop();

        try { await Task.WhenAll(_clientTasks).WaitAsync(TimeSpan.FromSeconds(3)); }
        catch { }

        lock (_clientLock) _clients.Clear();
        _clientTasks.Clear();
        ClientsChanged?.Invoke();
        RaiseLog("서버 정지");
    }

    #endregion

    #region §5 ─ Accept 루프

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                client.NoDelay = true;

                int id = _nextClientId++;
                var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                var info = new ClientInfo(id, endpoint, DateTime.Now);

                lock (_clientLock) _clients[id] = info;
                ClientsChanged?.Invoke();
                RaiseLog($"[{id}] 클라이언트 연결: {endpoint}");

                var task = Mode == ServerMode.Push
                    ? HandlePushClientAsync(id, client, ct)
                    : HandleEchoClientAsync(id, client, ct);

                _clientTasks.Add(task);

                _ = task.ContinueWith(_ =>
                {
                    lock (_clientLock) _clients.Remove(id);
                    ClientsChanged?.Invoke();
                    RaiseLog($"[{id}] 클라이언트 해제: {endpoint}");
                    client.Dispose();
                });
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                RaiseLog($"Accept 오류: {ex.Message}");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region §6 ─ Push 모드

    /// <summary>
    /// Push 모드 — 클라이언트 1개 전담 Task.
    /// PushIntervalMs 마다 센서 데이터 프레임을 클라이언트로 전송.
    ///
    /// <b>서버 전송 → 클라이언트 수신 전체 흐름:</b>
    /// <code>
    /// [서버] Task.Delay(PushIntervalMs)
    ///   → BuildSensorPayload(FrameId, Temp, Hum)   ← 12B 페이로드
    ///     → ServerProtocolHelper.BuildFrame()       ← BinaryProtocol 프레임 조립
    ///       → stream.WriteAsync()                   ← TCP 전송
    ///
    /// [클라이언트] TcpTransport.PassiveReceiveLoopAsync
    ///   → stream.ReadAsync()                        ← TCP 수신
    ///     → RaiseDataReceived(bytes)
    ///       → NetChannelBase.OnDataReceived()
    ///         → NetDispatchPipeline.PushReceived()
    ///           → BinaryProtocol.TryDecode()        ← STX/CRC 검증 + 페이로드 추출
    ///             → _receiveChannel.TryWrite()
    ///             → FrameReceived 이벤트
    ///               → DeviceFrameReceived 이벤트   ← 사용자 코드
    /// </code>
    /// </summary>
    private async Task HandlePushClientAsync(int clientId, TcpClient client, CancellationToken ct)
    {
        using var stream = client.GetStream();

        while (!ct.IsCancellationRequested && client.Connected)
        {
            try
            {
                // pushIntervalMs 대기
                await Task.Delay(PushIntervalMs, ct).ConfigureAwait(false);

                // 센서 데이터 생성
                uint id = Interlocked.Increment(ref _frameId);
                float temp = 20f + (float)_rng.NextDouble() * 15f;   // 20~35°C
                float hum = 40f + (float)_rng.NextDouble() * 40f;   // 40~80%

                var payload = ServerProtocolHelper.BuildSensorPayload(id, temp, hum);
                var frame = ServerProtocolHelper.BuildFrame(payload);

                await stream.WriteAsync(frame, ct).ConfigureAwait(false);
                Interlocked.Increment(ref _totalSent);
                StatsChanged?.Invoke();

                RaiseLog($"[{clientId}] Push → Frame#{id:D5} " +
                         $"Temp={temp:F1}°C Hum={hum:F1}%  ({frame.Length}B)");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                RaiseLog($"[{clientId}] Push 오류: {ex.Message}");
                break;
            }
        }
    }

    #endregion

    #region §7 ─ Echo 모드

    /// <summary>
    /// Echo 모드 — 클라이언트 요청 수신 → 응답 전송.
    ///
    /// <b>요청-응답 전체 흐름:</b>
    /// <code>
    /// [클라이언트] channel.RequestAsync(queryFrame)
    ///   → TcpTransport.WriteCoreAsync → stream.WriteAsync  ← 요청 전송
    ///
    /// [서버] stream.ReadAsync → TryExtractFrame → BuildEchoResponsePayload
    ///   → BuildFrame → stream.WriteAsync                   ← 응답 전송
    ///
    /// [클라이언트] TcpTransport.ReadCoreAsync → stream.ReadAsync
    ///   → BinaryProtocol.TryDecode → tcs.SetResult(NetResult.Ok)
    ///     → RequestAsync await 완료
    /// </code>
    /// </summary>
    private async Task HandleEchoClientAsync(int clientId, TcpClient client, CancellationToken ct)
    {
        using var stream = client.GetStream();
        var recvBuf = new byte[4096];
        var accum = new List<byte>(512);  // 스트림 누적 버퍼

        while (!ct.IsCancellationRequested && client.Connected)
        {
            try
            {
                // 스트림 읽기
                int read = await stream.ReadAsync(recvBuf, ct).ConfigureAwait(false);
                if (read == 0) break;  // 클라이언트 종료

                accum.AddRange(recvBuf[..read]);

                // 완전한 프레임 추출 루프
                while (ServerProtocolHelper.TryExtractFrame(accum, out var payload))
                {
                    Interlocked.Increment(ref _totalReceived);
                    StatsChanged?.Invoke();

                    // 요청 페이로드 분석
                    byte reqFc = payload.Length > 0 ? payload[0] : (byte)0x03;
                    ushort addr = payload.Length >= 3
                                  ? (ushort)((payload[1] << 8) | payload[2]) : (ushort)0;

                    uint id = Interlocked.Increment(ref _frameId);
                    float temp = 20f + (float)_rng.NextDouble() * 15f;
                    float hum = 40f + (float)_rng.NextDouble() * 40f;

                    var respPayload = ServerProtocolHelper.BuildEchoResponsePayload(id, temp, hum);
                    var respFrame = ServerProtocolHelper.BuildFrame(respPayload);

                    await stream.WriteAsync(respFrame, ct).ConfigureAwait(false);
                    Interlocked.Increment(ref _totalSent);
                    StatsChanged?.Invoke();

                    RaiseLog($"[{clientId}] 수신 FC=0x{reqFc:X2} Addr=0x{addr:X4} " +
                             $"({payload.Length}B) → " +
                             $"응답 Frame#{id:D5} Temp={temp:F1}°C Hum={hum:F1}%");
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                RaiseLog($"[{clientId}] Echo 오류: {ex.Message}");
                break;
            }
        }
    }

    #endregion

    #region §8 ─ 헬퍼

    private void RaiseLog(string msg)
        => Log?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");

    #endregion

    #region §9 ─ IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts.Dispose();
    }

    #endregion
}