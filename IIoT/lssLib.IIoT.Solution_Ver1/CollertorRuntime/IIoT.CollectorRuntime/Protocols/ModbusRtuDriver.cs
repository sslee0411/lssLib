// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Protocols/ModbusRtuDriver.cs
//  수정: Phase 8R
//    ① SerialDeviceConfig(int deviceId, string deviceName, string portName, int baudRate)
//       — deviceName 필수 파라미터 → _cfg.DriverId 로 전달
//    ② RequestAsync — timeoutMs / retries 없음 → ct 만 전달
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Net;

namespace IIoT.CollectorRuntime.Protocols;

public sealed record ModbusRtuConfig(
    string DriverId,
    string PortName,
    int    BaudRate   = 9600,
    byte   UnitId     = 1,
    int    TimeoutMs  = 2000,
    int    RetryCount = 2,
    string Parity     = "None",
    int    DataBits   = 8,
    int    StopBits   = 1);

public sealed class ModbusRtuDriver : IProtocolDriver
{
    private const string LogSrc = "ModbusRtuDriver";
    private readonly ModbusRtuConfig _cfg;
    private RequestResponseChannel?  _channel;
    private bool _disposed;

    public string DriverId    => _cfg.DriverId;
    public bool   IsConnected => _channel is not null;

    public ModbusRtuDriver(ModbusRtuConfig cfg) => _cfg = cfg;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            // ★ SerialDeviceConfig(int deviceId, string deviceName, string portName, int baudRate)
            //   — deviceName 은 2번째 필수 파라미터
            var serialCfg = new SerialDeviceConfig(
                deviceId  : 1,
                deviceName: _cfg.DriverId,
                portName  : _cfg.PortName,
                baudRate  : _cfg.BaudRate);

            var transport = SerialTransport.FromConfig(serialCfg);
            _channel = new RequestResponseChannel(
                serialCfg, transport, new RawProtocol(),
                autoRegister: false);

            await _channel.StartAsync(ct);

            LogManager.Instance.Info(LogSrc,
                $"[{_cfg.DriverId}] RTU 연결 → {_cfg.PortName} {_cfg.BaudRate}bps");
            return true;
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error(LogSrc,
                $"[{_cfg.DriverId}] RTU 연결 실패: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_channel is null) return;
        await _channel.StopAsync();
        _channel = null;
    }

    public async Task<TagReadResult> ReadAsync(TagAddressDef tag, CancellationToken ct = default)
    {
        var batch = await ReadBatchAsync([tag], ct);
        return batch.IsSuccess && batch.Values.TryGetValue(tag.TagId, out var val)
            ? TagReadResult.Ok(tag.TagId, val)
            : TagReadResult.Fail(tag.TagId, batch.ErrorMsg);
    }

    public async Task<BatchReadResult> ReadBatchAsync(
        IEnumerable<TagAddressDef> tags, CancellationToken ct = default)
    {
        if (_channel is null)
            return BatchReadResult.Fail("연결되지 않음");

        try
        {
            var tagList = tags.ToList();
            var values  = new Dictionary<string, double>(tagList.Count);

            foreach (var tag in tagList)
            {
                var (fc, addr) = _ParseAddress(tag.Address);
                byte[] req     = _BuildRtuRequest(fc, (ushort)addr, 1);

                // ★ RequestAsync(byte[] request, TimeSpan? timeout, CancellationToken ct)
                var result = await _channel.RequestAsync(
                    req,
                    TimeSpan.FromMilliseconds(_cfg.TimeoutMs),
                    ct);

                if (!result.IsOk || result.Data is null || result.Data.Length < 5)
                {
                    values[tag.TagId] = double.NaN;
                    continue;
                }

                var data = result.Data;
                if (_ValidateCrc(data))
                {
                    ushort raw = (ushort)((data[3] << 8) | data[4]);
                    values[tag.TagId] = raw;
                }
                else
                {
                    values[tag.TagId] = double.NaN;
                    LogManager.Instance.Warn(LogSrc, $"CRC 오류: {tag.TagId}");
                }
            }

            return BatchReadResult.Ok(values);
        }
        catch (Exception ex)
        {
            return BatchReadResult.Fail(ex.Message);
        }
    }

    private byte[] _BuildRtuRequest(byte fc, ushort startAddr, ushort quantity)
    {
        byte[] pdu =
        [
            _cfg.UnitId,
            fc,
            (byte)(startAddr >> 8), (byte)(startAddr & 0xFF),
            (byte)(quantity  >> 8), (byte)(quantity  & 0xFF),
        ];
        ushort crc = _CalcCrc(pdu);
        return [.. pdu, (byte)(crc & 0xFF), (byte)(crc >> 8)];
    }

    private static (byte fc, int addr) _ParseAddress(string address)
    {
        if (int.TryParse(address, out int iec))
        {
            if (iec >= 40001) return (0x03, iec - 40001);
            if (iec >= 30001) return (0x04, iec - 30001);
            if (iec >= 10001) return (0x02, iec - 10001);
            if (iec >= 1)     return (0x01, iec - 1);
        }
        return (0x03, 0);
    }

    private static ushort _CalcCrc(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
                else                     { crc >>= 1; }
            }
        }
        return crc;
    }

    private static bool _ValidateCrc(byte[] data)
    {
        if (data.Length < 4) return false;
        ushort received = (ushort)((data[^1] << 8) | data[^2]);
        ushort calc     = _CalcCrc(data[..^2]);
        return received == calc;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync();
        _disposed = true;
    }
}

// ═══════════════════════════════════════════════════════════
//  VirtualDriver — 오프라인 시뮬레이터
// ═══════════════════════════════════════════════════════════
public sealed class VirtualDriver : IProtocolDriver
{
    private const string LogSrc = "VirtualDriver";
    private readonly Random _rng = new();
    private bool _connected;
    private bool _disposed;

    public string DriverId    { get; }
    public bool   IsConnected => _connected;

    public VirtualDriver(string driverId = "Virtual") => DriverId = driverId;

    public Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        _connected = true;
        LogManager.Instance.Info(LogSrc, $"[{DriverId}] 가상 드라이버 연결");
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        _connected = false;
        return Task.CompletedTask;
    }

    public async Task<TagReadResult> ReadAsync(TagAddressDef tag, CancellationToken ct = default)
    {
        await Task.Delay(5, ct);
        return TagReadResult.Ok(tag.TagId, _SimulateValue(tag));
    }

    public async Task<BatchReadResult> ReadBatchAsync(
        IEnumerable<TagAddressDef> tags, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        var values = tags.ToDictionary(t => t.TagId, t => _SimulateValue(t));
        return BatchReadResult.Ok(values);
    }

    private double _SimulateValue(TagAddressDef tag)
    {
        if (tag.Address.StartsWith("sim:", StringComparison.OrdinalIgnoreCase))
        {
            var    parts  = tag.Address[4..].Split('/');
            string type   = parts[0].ToUpper();
            double nowSec = (DateTime.Now - DateTime.Today).TotalSeconds;

            return type switch
            {
                "SIN" when parts.Length >= 3 =>
                    double.Parse(parts[2]) * Math.Sin(2 * Math.PI * nowSec / double.Parse(parts[1])),
                "RAMP" when parts.Length >= 4 =>
                    double.Parse(parts[2])
                    + (double.Parse(parts[3]) - double.Parse(parts[2]))
                    * ((nowSec % double.Parse(parts[1])) / double.Parse(parts[1])),
                "CONST" when parts.Length >= 2 => double.Parse(parts[1]),
                "RAND"  when parts.Length >= 3 =>
                    double.Parse(parts[2])
                    + _rng.NextDouble() * (double.Parse(parts[3]) - double.Parse(parts[2])),
                _ => 0.0
            };
        }

        int    seed      = Math.Abs(tag.TagId.GetHashCode());
        double baseVal   = (seed % 100) + 50.0;
        double amplitude = (seed % 20) + 5.0;
        double period    = (seed % 30) + 20.0;
        double elapsed   = (DateTime.Now - DateTime.Today).TotalSeconds;

        return Math.Round(baseVal + amplitude * Math.Sin(2 * Math.PI * elapsed / period), 3);
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
