// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · TcpTransport.cs
//  역할: TCP 클라이언트 전송 계층
// ══════════════════════════════════════════════════════════════════════

using System.IO.MemoryMappedFiles;
using System.Net.Sockets;

namespace lssLib.Net.Transport;

/// <summary>
/// TCP 클라이언트 전송 계층.
/// </summary>
/// <example><code>
/// var transport = new TcpTransport("192.168.1.100", 5000);
/// var channel   = new SensorRequestChannel(transport, new BinaryProtocol(), NetConfig.Tcp);
/// await channel.StartAsync();
/// </code></example>
public sealed class TcpTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _strHost;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;

    #endregion

    #region §2 ─ 생성자

    /// <param name="host">서버 호스트명 또는 IP</param>
    /// <param name="port">포트 번호</param>
    public TcpTransport(string host, int port)
    {
        _strHost = host;
        _port = port;
    }

    #endregion

    #region §3 ─ 구현

    protected override async Task ConnectCoreAsync(CancellationToken ct)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_strHost, _port, ct).ConfigureAwait(false);
        _stream = _client.GetStream();
    }

    protected override Task DisconnectCoreAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        return Task.CompletedTask;
    }

    protected override async Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException("연결되지 않은 상태입니다.");
        await _stream.WriteAsync(data, ct).ConfigureAwait(false);
    }

    protected override async Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException("연결되지 않은 상태입니다.");
        var buf = new byte[length];
        int read = await _stream.ReadAsync(buf, ct).ConfigureAwait(false);
        return buf[..read];
    }

    protected override void DisposeCoreAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
    }

    #endregion
}

/*
// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · SerialTransport.cs
//  역할: COM 포트 직렬 통신 전송 계층
// ══════════════════════════════════════════════════════════════════════

//using System.IO.Ports;

//namespace lssLib.Net.Transport;

/// <summary>
/// COM 포트 직렬 통신 전송 계층.
/// DataReceived 이벤트로 수동 수신을 지원합니다.
/// </summary>
/// <example><code>
/// var transport = new SerialTransport("COM3", 115200);
/// var channel   = new SensorPassiveChannel(transport, new BinaryProtocol(), NetConfig.Serial);
/// await channel.StartAsync();
///
/// await foreach (var frame in channel.ReadAllAsync())
///     ProcessFrame(frame);
/// </code></example>
public sealed class SerialTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _strPortName;
    private readonly int _baudRate;
    private readonly int _dataBits;
    private readonly Parity _parity;
    private readonly StopBits _stopBits;
    private SerialPort? _port;

    #endregion

    #region §2 ─ 생성자

    public SerialTransport(string portName, int baudRate,
        int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
    {
        _strPortName = portName;
        _baudRate = baudRate;
        _dataBits = dataBits;
        _parity = parity;
        _stopBits = stopBits;
    }

    #endregion

    #region §3 ─ 구현

    protected override Task ConnectCoreAsync(CancellationToken ct)
    {
        _port = new SerialPort(_strPortName, _baudRate, _parity, _dataBits, _stopBits)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        // Passive 수신: DataReceived 이벤트 → RaiseDataReceived
        _port.DataReceived += (_, _) =>
        {
            if (_port.BytesToRead <= 0) return;
            var buf = new byte[_port.BytesToRead];
            _port.Read(buf, 0, buf.Length);
            RaiseDataReceived(buf);
        };

        _port.Open();
        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync()
    {
        _port?.Close();
        return Task.CompletedTask;
    }

    protected override Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_port is null || !_port.IsOpen) throw new InvalidOperationException("포트가 열려있지 않습니다.");
        _port.Write(data, 0, data.Length);
        return Task.CompletedTask;
    }

    protected override Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_port is null || !_port.IsOpen) throw new InvalidOperationException("포트가 열려있지 않습니다.");
        var buf = new byte[length];
        int read = _port.Read(buf, 0, length);
        return Task.FromResult(buf[..read]);
    }

    protected override void DisposeCoreAsync() => _port?.Dispose();

    #endregion
}


// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · SharedMemoryTransport.cs
//  역할: 프로세스 간 공유 메모리 IPC 전송 계층 (System.IO.MemoryMappedFiles)
// ══════════════════════════════════════════════════════════════════════

using System.IO.MemoryMappedFiles;

namespace lssLib.Net.Transport;

/// <summary>
/// 공유 메모리 기반 IPC 전송 계층.
/// 동일 머신 내 프로세스 간 고속 데이터 교환에 사용합니다.
/// </summary>
/// <remarks>
/// 공유 메모리 레이아웃 (기본 64KB):
/// <code>
/// [0~3]   WriteFlag   (int32) — 쓰기 프로세스가 1 설정 후 데이터 기록
/// [4~7]   DataLength  (int32) — 데이터 크기
/// [8~N]   Data        (bytes) — 실제 페이로드
/// </code>
/// 단순 플래그 방식입니다. 고성능이 필요하면 NamedPipe 또는 Mutex 로 동기화 강화를 권장합니다.
/// </remarks>
/// <example><code>
/// // 프로세스 A (쓰기)
/// var tx = new SharedMemoryTransport("lssLib_IPC_Sensor", SharedMemoryRole.Writer, size: 65536);
/// await tx.ConnectAsync();
/// await tx.WriteAsync(frameBytes);
///
/// // 프로세스 B (읽기 — Passive 수신)
/// var rx = new SharedMemoryTransport("lssLib_IPC_Sensor", SharedMemoryRole.Reader, size: 65536);
/// var channel = new SensorPassiveChannel(rx, new RawProtocol(), NetConfig.SharedMemory);
/// await channel.StartAsync();
/// </code></example>
public sealed class SharedMemoryTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _strMapName;
    private readonly SharedMemoryRole _role;
    private readonly long _size;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    private const int OFFSET_FLAG = 0;
    private const int OFFSET_LENGTH = 4;
    private const int OFFSET_DATA = 8;

    #endregion

    #region §2 ─ 생성자

    public SharedMemoryTransport(string mapName, SharedMemoryRole role, long size = 65536)
    {
        _strMapName = mapName;
        _role = role;
        _size = size;
    }

    #endregion

    #region §3 ─ 구현

    protected override Task ConnectCoreAsync(CancellationToken ct)
    {
        _mmf = _role == SharedMemoryRole.Writer
            ? MemoryMappedFile.CreateOrOpen(_strMapName, _size)
            : MemoryMappedFile.OpenExisting(_strMapName);

        _accessor = _mmf.CreateViewAccessor();

        // Reader: 폴링 루프로 수신 감지
        if (_role == SharedMemoryRole.Reader)
        {
            _pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _pollTask = Task.Run(() => PollAsync(_pollCts.Token), _pollCts.Token);
        }

        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync()
    {
        _pollCts?.Cancel();
        _accessor?.Dispose();
        _mmf?.Dispose();
        return Task.CompletedTask;
    }

    protected override Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_accessor is null) throw new InvalidOperationException("공유 메모리가 열려있지 않습니다.");

        _accessor.Write(OFFSET_LENGTH, data.Length);
        _accessor.WriteArray(OFFSET_DATA, data, 0, data.Length);
        _accessor.Write(OFFSET_FLAG, 1);  // 쓰기 완료 플래그
        return Task.CompletedTask;
    }

    protected override Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_accessor is null) throw new InvalidOperationException("공유 메모리가 열려있지 않습니다.");

        int dataLen = _accessor.ReadInt32(OFFSET_LENGTH);
        var buf = new byte[dataLen];
        _accessor.ReadArray(OFFSET_DATA, buf, 0, dataLen);
        _accessor.Write(OFFSET_FLAG, 0);  // 읽기 완료 → 플래그 초기화
        return Task.FromResult(buf);
    }

    // 폴링 루프 (Reader 전용)
    private async Task PollAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_accessor is not null && _accessor.ReadInt32(OFFSET_FLAG) == 1)
            {
                var data = await ReadCoreAsync(0, ct).ConfigureAwait(false);
                RaiseDataReceived(data);
            }
            await Task.Delay(5, ct).ConfigureAwait(false);  // 5ms 폴링
        }
    }

    protected override void DisposeCoreAsync()
    {
        _pollCts?.Dispose();
        _accessor?.Dispose();
        _mmf?.Dispose();
    }

    #endregion
}

/// <summary>공유 메모리 역할.</summary>
public enum SharedMemoryRole
{
    /// <summary>데이터를 기록하는 프로세스.</summary>
    Writer,
    /// <summary>데이터를 읽는 프로세스 (폴링 수신).</summary>
    Reader
}
*/