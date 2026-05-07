
// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/SerialTransport.cs
//  역할: COM 포트 직렬 통신 전송 계층
// ══════════════════════════════════════════════════════════════════════

using System.IO.Ports;
using lssLib.Net;

namespace Lsslib.net.implementation;
/// <summary>
/// COM 포트 직렬 통신 전송 계층.
/// </summary>
/// <remarks>
/// <para>SerialPort.DataReceived 이벤트 기반 Passive 수신을 지원합니다.</para>
/// <para>RS-485 환경에서는 반드시 <c>IsSequential=true</c> 로 설정하세요 (버스 충돌 방지).</para>
///
/// <b>생성 방법:</b>
/// <code>
/// // FromConfig 팩토리 권장 (DataBits/Parity/Timeout 자동 적용)
/// var t = SerialTransport.FromConfig(serialCfg);
///
/// // 직접 생성
/// var t = new SerialTransport("COM3", 115200);
/// </code>
/// </remarks>
public sealed class SerialTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _portName;
    private readonly int _baudRate;
    private readonly int _dataBits;
    private readonly Parity _parity;
    private readonly StopBits _stopBits;
    private readonly int _readTimeout;
    private readonly int _writeTimeout;
    private SerialPort? _port;

    #endregion

    #region §2 ─ 생성자 / 팩토리

    public SerialTransport(string portName, int baudRate,
        int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One,
        int readTimeout = 1000, int writeTimeout = 1000)
    {
        _portName = portName;
        _baudRate = baudRate;
        _dataBits = dataBits;
        _parity = parity;
        _stopBits = stopBits;
        _readTimeout = readTimeout;
        _writeTimeout = writeTimeout;
    }

    /// <summary>SerialDeviceConfig 에서 생성합니다. LogSource 자동 주입.</summary>
    public static SerialTransport FromConfig(SerialDeviceConfig cfg)
        => new(cfg.PortName, cfg.BaudRate, cfg.DataBits, cfg.Parity, cfg.StopBits,
               cfg.ReadTimeout, cfg.WriteTimeout)
        { LogSource = cfg.DeviceName };

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override Task ConnectCoreAsync(CancellationToken ct)
    {
        _port = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
        {
            ReadTimeout = _readTimeout,
            WriteTimeout = _writeTimeout
        };

        // Passive 수신: DataReceived → RaiseDataReceived → NetChannelBase
        _port.DataReceived += (_, _) =>
        {
            if (_port is null || !_port.IsOpen || _port.BytesToRead <= 0) return;
            var buf = new byte[_port.BytesToRead];
            int read = _port.Read(buf, 0, buf.Length);
            if (read > 0) RaiseDataReceived(buf[..read]);
        };

        _port.Open();
        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync(CancellationToken ct)
    {
        if (_port?.IsOpen == true) _port.Close();
        return Task.CompletedTask;
    }

    protected override Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_port is null || !_port.IsOpen)
        {
            throw new InvalidOperationException($"포트가 열려있지 않습니다.");
            //throw new InvalidOperationException($"[{LogSource}] 포트가 열려있지 않습니다.");
        }
        _port.Write(data, 0, data.Length);
        return Task.CompletedTask;
    }

    protected override Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_port is null || !_port.IsOpen){
            throw new InvalidOperationException($"포트가 열려있지 않습니다.");
            //throw new InvalidOperationException($"[{LogSource}] 포트가 열려있지 않습니다.");
        }
        var buf = new byte[length];
        int read = _port.Read(buf, 0, length);
        return Task.FromResult(buf[..read]);
    }

    protected override void DisposeCore() => _port?.Dispose();

    #endregion
}