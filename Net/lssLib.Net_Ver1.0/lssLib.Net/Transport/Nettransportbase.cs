// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · NetTransportBase.cs
//  역할: INetTransport 공통 구현 추상 베이스 (상태 관리 / 이벤트 공통화)
// ══════════════════════════════════════════════════════════════════════

//using lssLib.Log;

namespace lssLib.Net.Transport;

/// <summary>
/// <see cref="INetTransport"/> 공통 구현을 제공하는 추상 베이스.
/// <para>파생 클래스는 실제 I/O 동작(<c>ConnectCoreAsync</c>, <c>WriteCoreAsync</c>, <c>ReadCoreAsync</c>)만 구현합니다.</para>
/// </summary>
/// <remarks>
/// 파생 클래스 목록:
/// <list type="bullet">
///   <item><description><c>TcpTransport</c></description></item>
///   <item><description><c>UdpTransport</c></description></item>
///   <item><description><c>SerialTransport</c></description></item>
///   <item><description><c>SharedMemoryTransport</c></description></item>
/// </list>
/// </remarks>
public abstract class NetTransportBase : INetTransport
{
    #region §1 ─ 필드

    private NetState _state = NetState.Disconnected;
    private volatile bool _disposed;

    private const string LOG_SRC = "Net.Transport";

    #endregion

    #region §2 ─ INetTransport 구현

    /// <inheritdoc/>
    public NetState State
    {
        get => _state;
        protected set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(value);
        }
    }

    /// <inheritdoc/>
    public event Action<NetState>? StateChanged;

    /// <inheritdoc/>
    public event Action<byte[]>? DataReceived;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        State = NetState.Connecting;
        try
        {
            await ConnectCoreAsync(ct).ConfigureAwait(false);
            State = NetState.Connected;
        //    LogManager.Instance.Info(LOG_SRC, $"[{GetType().Name}] 연결 성공");
        }
        catch (Exception ex)
        {
            State = NetState.Error;
         //   LogManager.Instance.Error(LOG_SRC, $"[{GetType().Name}] 연결 실패: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync()
    {
        try
        {
            await DisconnectCoreAsync().ConfigureAwait(false);
            State = NetState.Disconnected;
        //    LogManager.Instance.Info(LOG_SRC, $"[{GetType().Name}] 연결 해제");
        }
        catch (Exception ex)
        {
        //    LogManager.Instance.Warn(LOG_SRC, $"[{GetType().Name}] 해제 오류 (무시): {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task WriteAsync(byte[] data, CancellationToken ct = default)
        => WriteCoreAsync(data, ct);

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(int length, CancellationToken ct = default)
        => ReadCoreAsync(length, ct);

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(CancellationToken ct = default)
        => ReadFrameCoreAsync(ct);

    #endregion

    #region §3 ─ 추상 메서드 (파생 클래스 구현)

    /// <summary>실제 연결 로직. 성공 시 반환, 실패 시 예외 throw.</summary>
    protected abstract Task ConnectCoreAsync(CancellationToken ct);

    /// <summary>실제 연결 해제 로직.</summary>
    protected abstract Task DisconnectCoreAsync();

    /// <summary>데이터 전송 로직.</summary>
    protected abstract Task WriteCoreAsync(byte[] data, CancellationToken ct);

    /// <summary>고정 길이 수신 로직.</summary>
    protected abstract Task<byte[]> ReadCoreAsync(int length, CancellationToken ct);

    /// <summary>
    /// 프레임 단위 수신 로직.
    /// 기본 구현은 1024B 읽기. 프레임 경계 인식이 필요한 구현체에서 재정의합니다.
    /// </summary>
    protected virtual Task<byte[]> ReadFrameCoreAsync(CancellationToken ct)
        => ReadCoreAsync(1024, ct);

    #endregion

    #region §4 ─ 수신 이벤트 발생 헬퍼 (파생 클래스에서 호출)

    /// <summary>
    /// 수신 데이터를 상위 계층(<see cref="NetChannelBase"/>)으로 올려보냅니다.
    /// Passive 수신 루프를 가진 전송 계층에서 호출합니다.
    /// </summary>
    protected void RaiseDataReceived(byte[] data) => DataReceived?.Invoke(data);

    #endregion

    #region §5 ─ IAsyncDisposable

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await DisconnectAsync().ConfigureAwait(false);
        State = NetState.Disposed;

        DisposeCoreAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>파생 클래스 리소스 정리 (소켓, 핸들 등).</summary>
    protected virtual void DisposeCoreAsync() { }

    #endregion
}