// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Abstractions/NetTransportBase.cs
//  역할: INetTransport 공통 구현 추상 베이스
//        파생 클래스는 ConnectCore/WriteCore/ReadCore 만 구현합니다.
// ══════════════════════════════════════════════════════════════════════

//using lssLib.Log;

namespace lssLib.Net;

/// <summary>
/// <see cref="INetTransport"/> 공통 구현 추상 베이스.
/// </summary>
/// <remarks>
/// <b>파생 클래스 구현 목록 (Transport/):</b>
/// <c>TcpTransport</c> / <c>SerialTransport</c> / <c>UdpTransport</c> / <c>SharedMemoryTransport</c>
///
/// <b>파생 클래스가 구현할 메서드:</b>
/// <list type="table">
///   <listheader><term>메서드</term><description>내용</description></listheader>
///   <item><term><see cref="ConnectCoreAsync"/></term><description>소켓/포트 연결. 실패 시 예외.</description></item>
///   <item><term><see cref="DisconnectCoreAsync"/></term><description>소켓/포트 닫기.</description></item>
///   <item><term><see cref="WriteCoreAsync"/></term><description>바이트 전송.</description></item>
///   <item><term><see cref="ReadCoreAsync(int,CancellationToken)"/></term><description>고정 길이 수신.</description></item>
///   <item><term><see cref="ReadFrameCoreAsync"/></term><description>(선택) 프레임 단위 수신. 기본: 4096B.</description></item>
///   <item><term><see cref="DisposeCore"/></term><description>(선택) 관리 리소스 해제.</description></item>
/// </list>
///
/// <b>Passive 수신 시 파생 클래스에서 호출:</b>
/// <code>
/// // SerialPort.DataReceived 핸들러 내부
/// RaiseDataReceived(receivedBytes);  // → NetChannelBase 로 전달
/// </code>
/// </remarks>
public abstract class NetTransportBase : INetTransport
{
    #region §1 ─ 필드

    private NetState _state = NetState.Disconnected;
    private volatile bool _disposed;

    /*
    /// <summary>
    /// 로그 Source 식별자.
    /// <para><c>XxxTransport.FromConfig(cfg)</c> 팩토리 사용 시 cfg.DeviceName 이 자동 주입됩니다.</para>
    /// </summary>
    public string LogSource { get; set; } = "Net.Transport"; */

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
            //Log(LogLevel.Info, $"[{GetType().Name}] 연결 성공");
        }
        catch (Exception ex)
        {
            State = NetState.Error;
            ex.Message.ToString();
            //Log(LogLevel.Error, $"[{GetType().Name}] 연결 실패: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        try
        {
            await DisconnectCoreAsync(ct).ConfigureAwait(false);
            State = NetState.Disconnected;
            //Log(LogLevel.Info, $"[{GetType().Name}] 연결 해제");
        }
        catch (Exception ex)
        {
            ex.Message.ToString();
            //Log(LogLevel.Warn, $"[{GetType().Name}] 해제 오류(무시): {ex.Message}");
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

    #region §3 ─ 추상 메서드 (파생 클래스 구현 필수)

    /// <summary>실제 연결 로직. 성공 시 반환, 실패 시 예외 throw.</summary>
    protected abstract Task ConnectCoreAsync(CancellationToken ct);

    /// <summary>실제 연결 해제 로직. 이미 닫혀 있어도 예외 없어야 함.</summary>
    protected abstract Task DisconnectCoreAsync(CancellationToken ct);

    /// <summary>데이터 전송 로직.</summary>
    protected abstract Task WriteCoreAsync(byte[] data, CancellationToken ct);

    /// <summary>고정 길이 수신 로직.</summary>
    protected abstract Task<byte[]> ReadCoreAsync(int length, CancellationToken ct);

    #endregion

    #region §4 ─ 가상 메서드 (선택 오버라이드)

    /// <summary>프레임 단위 수신. 기본: 4096B 읽기. 프레임 경계 인식 필요 시 재정의.</summary>
    protected virtual Task<byte[]> ReadFrameCoreAsync(CancellationToken ct)
        => ReadCoreAsync(4096, ct);

    /// <summary>파생 클래스 리소스 정리 (소켓, 핸들, 폴링 CTS 등).</summary>
    protected virtual void DisposeCore() { }

    #endregion

    #region §5 ─ 헬퍼 (파생 클래스에서 호출)

    /// <summary>
    /// 수신 데이터를 <see cref="NetChannelBase"/> 로 전달합니다.
    /// <para>Passive 수신 루프(SerialPort.DataReceived, SharedMemory 폴링 등)에서 호출합니다.</para>
    /// </summary>
    protected void RaiseDataReceived(byte[] data) => DataReceived?.Invoke(data);

    /*
    private void Log(LogLevel lv, string msg)
        => LogManager.Instance.AddLog(lv, LogSource, msg);
    */
    #endregion

    #region §6 ─ IAsyncDisposable

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        State = NetState.Disposed;
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    #endregion
}