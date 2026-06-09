// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Protocols/ModbusRtuDriver.cs
//  역할: Modbus RTU (RS-485/232) 드라이버
//        lssLib.Net SerialDeviceConfig + RequestResponseChannel
//  Phase 8: 신규
//
//  Modbus RTU 패킷 구조:
//    요청: [Unit ID 1B][FC 1B][Start Addr 2B][Quantity 2B][CRC 2B]
//    응답: [Unit ID 1B][FC 1B][Byte Count 1B][Data N*2B][CRC 2B]
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Net;
using lssLib.Net.Config;

namespace IIoT.CollectorRuntime.Protocols;

// ── ModbusRtuConfig ───────────────────────────────────────
public sealed record ModbusRtuConfig(
    string DriverId,
    string PortName,           // "COM3"
    int    BaudRate   = 9600,
    byte   UnitId     = 1,
    int    TimeoutMs  = 2000,
    int    RetryCount = 2,
    string Parity     = "None",
    int    DataBits   = 8,
    int    StopBits   = 1);

// ── ModbusRtuDriver ───────────────────────────────────────
/// <summary>
/// Modbus RTU 프로토콜 드라이버.
/// lssLib.Net SerialDeviceConfig (Sequential 모드) 를 사용합니다.
///
/// ★ Serial 통신은 단방향 반이중(Half-Duplex) 이므로
///   lssLib.Net SequenceMode.Sequential 을 사용해야 합니다.
///   동시 요청은 충돌 발생 → Sequential 엄수.
/// </summary>
public sealed class ModbusRtuDriver : IProtocolDriver

    private const string LogSrc = "ModbusRtuDriver";
    private readonly ModbusRtuConfig _cfg;
    private RequestResponseChannel?  _channel;
    private bool _disposed;

    public string DriverId    => _cfg.DriverId;
    public bool   IsConnected => _channel is not null;

    public ModbusRtuDriver(ModbusRtuConfig cfg) => _cfg = cfg;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    
        try
        
            var serialCfg = new SerialDeviceConfig(
                deviceId : 1,
                name     : _cfg.DriverId,
                portName : _cfg.PortName,
                baudRate : _cfg.BaudRate);

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
        
            LogManager.Instance.Error(LogSrc,
                $"[{_cfg.DriverId}] RTU 연결 실패: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    
        if (_channel is null) return;
        await _channel.StopAsync();
        _channel = null;
    }

    public async Task<TagReadResult> ReadAsync(
        TagAddressDef tag, CancellationToken ct = default)
    
        var batch = await ReadBatchAsync([tag], ct);
        return batch.IsSuccess && batch.Values.TryGetValue(tag.TagId, out var val)
            ? TagReadResult.Ok(tag.TagId, val)
            : TagReadResult.Fail(tag.TagId, batch.ErrorMsg);
    }

    public async Task<BatchReadResult> ReadBatchAsync(
        IEnumerable<TagAddressDef> tags,
        CancellationToken ct = default)
    
        if (_channel is null)
            return BatchReadResult.Fail("연결되지 않음");

        try
        
            var tagList = tags.ToList();
            var values  = new Dictionary<string, double>(tagList.Count);

            // RTU: Sequential — 태그 1개씩 순차 처리
            foreach (var tag in tagList)
            
                var (fc, addr) = _ParseAddress(tag.Address);
                byte[] req     = _BuildRtuRequest(fc, (ushort)addr, 1);

                var result = await _channel.RequestAsync(
                    deviceId: 1, req, ct,
                    timeoutMs: _cfg.TimeoutMs,
                    retries:   _cfg.RetryCount);

                if (!result.IsOk || result.Data is null || result.Data.Length < 5)
                
                    values[tag.TagId] = double.NaN;
                    continue;
                }

                // RTU 응답: [UnitID][FC][ByteCount][Hi][Lo][CRC Hi][CRC Lo]
                var data = result.Data;
                if (_ValidateCrc(data))
                
                    ushort raw = (ushort)((data[3] << 8) | data[4]);
                    values[tag.TagId] = raw;
                }
                else
                
                    values[tag.TagId] = double.NaN;
                    LogManager.Instance.Warn(LogSrc, $"CRC 오류: {tag.TagName}");
                }
            }

            return BatchReadResult.Ok(values);
        }
        catch (Exception ex)
        
            return BatchReadResult.Fail(ex.Message);
        }
    }

    // ── Modbus RTU 패킷 빌드 ──────────────────────────────────
    private byte[] _BuildRtuRequest(byte fc, ushort startAddr, ushort quantity)
    
        byte[] pdu =
        [
            _cfg.UnitId,
            fc,
            (byte)(startAddr >> 8), (byte)(startAddr & 0xFF),
            (byte)(quantity  >> 8), (byte)(quantity  & 0xFF),
        ];
        ushort crc = _CalcCrc(pdu);
        return [.. pdu, (byte)(crc & 0xFF), (byte)(crc >> 8)]; // RTU: CRC Lo-Hi
    }

    private static (byte fc, int addr) _ParseAddress(string address)
    
        if (int.TryParse(address, out int iec))
        
            if (iec >= 40001) return (0x03, iec - 40001);
            if (iec >= 30001) return (0x04, iec - 30001);
            if (iec >= 10001) return (0x02, iec - 10001);
            if (iec >= 1)     return (0x01, iec - 1);
        }
        return (0x03, 0);
    }

    private static ushort _CalcCrc(byte[] data)
    
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        
            crc ^= b;
            for (int i = 0; i < 8; i++)
            
                if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
                else                     { crc >>= 1; }
            }
        }
        return crc;
    }

    private static bool _ValidateCrc(byte[] data)
    
        if (data.Length < 4) return false;
        ushort received = (ushort)((data[^1] << 8) | data[^2]);
        ushort calc     = _CalcCrc(data[..^2]);
        return received == calc;
    }

    public async ValueTask DisposeAsync()
    
        if (_disposed) return;
        await DisconnectAsync();
        _disposed = true;
    }
}

// ══════════════════════════════════════════════════════════
//  VirtualDriver — 테스트·오프라인 시뮬레이터
//  실제 하드웨어 없이 lssLib.Net VirtualTransport 활용
// ══════════════════════════════════════════════════════════
/// <summary>
/// 가상 드라이버 — 실제 통신 없이 시뮬레이션 값을 반환합니다.
/// 개발·테스트·하드웨어 미연결 환경에서 사용합니다.
///
/// 주소 형식:
///   "sim:SIN/100/10"   → 주기 100초 sin파, 진폭 10
///   "sim:RAMP/50/0/100"→ 0→100 램프, 주기 50초
///   "sim:CONST/42.5"   → 상수 42.5
///   "sim:RAND/10/20"   → 10~20 랜덤
///   일반 주소("40001") → 태그 고유 시드로 sin파 생성
/// </summary>
public sealed class VirtualDriver : IProtocolDriver

    private const string LogSrc = "VirtualDriver";
    private readonly Random _rng = new();
    private bool _connected;
    private bool _disposed;

    public string DriverId    { get; }
    public bool   IsConnected => _connected;

    public VirtualDriver(string driverId = "Virtual") => DriverId = driverId;

    public Task<bool> ConnectAsync(CancellationToken ct = default)
    
        _connected = true;
        LogManager.Instance.Info(LogSrc, $"[{DriverId}] 가상 드라이버 연결");
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    
        _connected = false;
        return Task.CompletedTask;
    }

    public async Task<TagReadResult> ReadAsync(
        TagAddressDef tag, CancellationToken ct = default)
    
        await Task.Delay(5, ct); // 통신 지연 시뮬레이션
        double value = _SimulateValue(tag);
        return TagReadResult.Ok(tag.TagId, value);
    }

    public async Task<BatchReadResult> ReadBatchAsync(
        IEnumerable<TagAddressDef> tags,
        CancellationToken ct = default)
    
        await Task.Delay(10, ct);
        var values = tags.ToDictionary(
            t => t.TagId,
            t => _SimulateValue(t));
        return BatchReadResult.Ok(values);
    }

    private double _SimulateValue(TagAddressDef tag)
    
        // "sim:TYPE/param1/param2..." 형식 파싱
        if (tag.Address.StartsWith("sim:", StringComparison.OrdinalIgnoreCase))
        
            var parts = tag.Address[4..].Split('/');
            string type = parts[0].ToUpper();
            double now  = (DateTime.Now - DateTime.Today).TotalSeconds;

            return type switch
            
                "SIN" when parts.Length >= 3 =>
                    double.Parse(parts[2]) * Math.Sin(2 * Math.PI * now / double.Parse(parts[1])),
                "RAMP" when parts.Length >= 4 =>
                
                    double min = double.Parse(parts[2]);
                    double max = double.Parse(parts[3]);
                    double period = double.Parse(parts[1]);
                    yield return min + (max - min) * ((now % period) / period);
                },
                "CONST" when parts.Length >= 2 =>
                    double.Parse(parts[1]),
                "RAND" when parts.Length >= 3 =>
                    double.Parse(parts[2]) + _rng.NextDouble()
                        * (double.Parse(parts[3]) - double.Parse(parts[2])),
                _ => 0.0
            };
        }

        // 일반 주소 → 태그 ID 해시 기반 sin파
        int seed = Math.Abs(tag.TagId.GetHashCode());
        double baseVal  = (seed % 100) + 50.0;
        double amplitude = seed % 20 + 5.0;
        double period    = (seed % 30) + 20.0;
        double elapsed   = (DateTime.Now - DateTime.Today).TotalSeconds;

        return Math.Round(
            baseVal + amplitude * Math.Sin(2 * Math.PI * elapsed / period), 3);
    }

    public ValueTask DisposeAsync()
    
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
