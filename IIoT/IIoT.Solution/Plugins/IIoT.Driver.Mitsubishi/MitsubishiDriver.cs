// ══════════════════════════════════════════════════════════
//  IIoT.Driver.Mitsubishi · MitsubishiDriver.cs
//  역할: 미쓰비시 MELSEC MC 프로토콜 3E 프레임 통신 구현
//
//  수정 이력:
//    v1.1 (2026-06-27): CS 오류 3종 수정
//      · using System.IO 누락 → MemoryStream/BinaryWriter 제거,
//        고정 바이트 배열로 프레임 직접 조립 (외부 의존 완전 제거)
//      · byte[] vs ushort[] 삼항식 타입 불일치 →
//        _ReadBitsRaw / _ReadWordsRaw 별도 메서드로 분리
//      · (byte) 캐스트 누락 → 모든 byte 대입에 명시적 캐스트 추가
//
//  프레임 구조 (3E):
//    요청: 서브헤더(2) 네트워크(1) PC(1) IO번호(2) 국번(1)
//          데이터길이(2) 감시타이머(2) 커맨드(2) 서브커맨드(2)
//          소자번호(3) 소자코드(1) 점수(2) = 20 바이트
//    응답: 서브헤더(2) 네트워크(1) PC(1) IO번호(2) 국번(1)
//          데이터길이(2) 종료코드(2) 데이터...
//
//  소자 코드:
//    X=0x9C Y=0x9D M=0x90 L=0x92 B=0xA0 F=0x93
//    D=0xA8 W=0xB4 R=0xAF ZR=0xB0 TN=0xC2 CN=0xC5
//
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;
using System.Net.Sockets;

namespace IIoT.Driver.Mitsubishi;

public sealed class MitsubishiDriver : IProtocolDriver
{
    // §1 ─ 상태 ────────────────────────────────────────────

    public string DriverName => "mitsubishi-mc";
    public bool IsConnected => _client?.Connected ?? false;

    public event Action<string>? OnConnected;
    public event Action<string, string>? OnError;

    private TcpClient? _client;
    private NetworkStream? _stream;

    // §2 ─ 연결 파라미터 ───────────────────────────────────

    private string _host = "192.168.0.1";
    private int _port = 5007;
    private byte _networkNo = 0x00;
    private byte _pcNo = 0xFF;
    private ushort _unitIoNo = 0x03FF;
    private byte _stationNo = 0x00;
    private int _timeoutMs = 5000;
    private bool _is4EFrame = false;

    // 감시 타이머 (10ms 단위, 0x0010 = 160ms)
    private const ushort WatchdogTimer = 0x0010;

    // §3 ─ 연결 / 해제 ─────────────────────────────────────

    public async Task<bool> ConnectAsync(
        DriverConfig config, CancellationToken ct = default)
    {
        _ReadParams(config);
        try
        {
            _client?.Dispose();
            _client = new TcpClient
            {
                SendTimeout = _timeoutMs,
                ReceiveTimeout = _timeoutMs
            };
            await _client.ConnectAsync(_host, _port, ct);
            _stream = _client.GetStream();
            OnConnected?.Invoke(DriverName);
            return true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(DriverName, $"연결 실패 [{_host}:{_port}]: {ex.Message}");
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        return Task.CompletedTask;
    }

    // §4 ─ 배치 읽기 ───────────────────────────────────────

    public async Task<DriverReadResult> ReadTagsAsync(
        IReadOnlyList<TagReadRequest> tags, CancellationToken ct = default)
    {
        if (!IsConnected)
            return DriverReadResult.Fail("미연결", TagQuality.Disconnected);

        var values = new List<TagValue>();
        var groups = _GroupByDevice(tags);

        foreach (var (deviceCode, group) in groups)
        {
            bool isBit = _IsBitDevice(deviceCode);
            var sorted = group.OrderBy(t => _ParseDeviceNo(t.Address)).ToList();
            var batches = _MakeBatches(sorted, isBit ? 256 : 120);

            foreach (var batch in batches)
            {
                try
                {
                    var startNo = _ParseDeviceNo(batch[0].Address);
                    var endNo = _ParseDeviceNo(batch[^1].Address)
                                  + _WordSize(batch[^1].DataType) - 1;
                    var count = (ushort)(endNo - startNo + 1);

                    if (isBit)
                    {
                        // ★ 비트: byte[] 반환
                        var bits = await _ReadBitsRawAsync(deviceCode, startNo, count, ct);
                        if (bits is null)
                        {
                            foreach (var t in batch)
                                values.Add(_MakeValue(t, null, TagQuality.Bad));
                            continue;
                        }
                        foreach (var tag in batch)
                        {
                            var offset = _ParseDeviceNo(tag.Address) - startNo;
                            values.Add(_MakeValue(tag, (object)(bits[offset] != 0), TagQuality.Good));
                        }
                    }
                    else
                    {
                        // ★ 워드: ushort[] 반환 — 삼항식 타입 혼용 없음
                        var words = await _ReadWordsRawAsync(deviceCode, startNo, count, ct);
                        if (words is null)
                        {
                            foreach (var t in batch)
                                values.Add(_MakeValue(t, null, TagQuality.Bad));
                            continue;
                        }
                        foreach (var tag in batch)
                        {
                            var offset = _ParseDeviceNo(tag.Address) - startNo;
                            var v = _ExtractWordValue(words, offset, tag.DataType);
                            values.Add(_MakeValue(tag, v, TagQuality.Good));
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(DriverName, $"읽기 오류: {ex.Message}");
                    foreach (var t in batch)
                        values.Add(_MakeValue(t, null, TagQuality.Bad));
                }
            }
        }

        return DriverReadResult.Ok(values);
    }

    // §5 ─ 단일 쓰기 ───────────────────────────────────────

    public async Task<DriverWriteResult> WriteTagAsync(
        TagWriteRequest tag, CancellationToken ct = default)
    {
        if (!IsConnected) return DriverWriteResult.Fail("미연결");

        try
        {
            var deviceCode = _GetDeviceCode(tag.Address);
            var deviceNo = _ParseDeviceNo(tag.Address);
            bool isBit = _IsBitDevice(deviceCode);

            if (isBit)
            {
                var val = tag.Value is "1" or "true" or "True";
                await _WriteBitAsync(deviceCode, deviceNo, val, ct);
            }
            else
            {
                var words = _EncodeWordValue(tag.Value, tag.DataType);
                await _WriteWordsAsync(deviceCode, deviceNo, words, ct);
            }
            return DriverWriteResult.Ok();
        }
        catch (Exception ex)
        {
            OnError?.Invoke(DriverName, $"쓰기 오류 [{tag.Address}]: {ex.Message}");
            return DriverWriteResult.Fail(ex.Message);
        }
    }

    // §6 ─ MC 프로토콜 3E 프레임 — 읽기 ──────────────────

    /// <summary>워드 소자 일괄 읽기 (커맨드 0x0401, 서브 0x0000)</summary>
    private async Task<ushort[]?> _ReadWordsRawAsync(
        byte deviceCode, int startNo, ushort count, CancellationToken ct)
    {
        var pdu = _BuildPdu(0x0401, 0x0000, deviceCode, startNo, count, null);
        var resp = await _SendReceive(pdu, ct);
        if (resp is null) return null;

        var words = new ushort[count];
        for (int i = 0; i < count; i++)
            words[i] = (ushort)((resp[i * 2 + 1] << 8) | resp[i * 2]);
        return words;
    }

    /// <summary>비트 소자 일괄 읽기 (커맨드 0x0401, 서브 0x0001)</summary>
    private async Task<byte[]?> _ReadBitsRawAsync(
        byte deviceCode, int startNo, ushort count, CancellationToken ct)
    {
        var pdu = _BuildPdu(0x0401, 0x0001, deviceCode, startNo, count, null);
        var resp = await _SendReceive(pdu, ct);
        if (resp is null) return null;

        // 응답: 2점씩 니블 패킹 (1바이트에 2비트)
        var bits = new byte[count];
        for (int i = 0; i < count; i++)
        {
            byte b = resp[i / 2];
            // ★ 명시적 (byte) 캐스트 — CS0266 수정
            bits[i] = (byte)((i % 2 == 0) ? (b & 0x0F) : ((b >> 4) & 0x0F));
        }
        return bits;
    }

    // §7 ─ MC 프로토콜 3E 프레임 — 쓰기 ──────────────────

    /// <summary>워드 소자 일괄 쓰기 (커맨드 0x1401, 서브 0x0000)</summary>
    private async Task _WriteWordsAsync(
        byte deviceCode, int startNo, ushort[] values, CancellationToken ct)
    {
        var data = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            data[i * 2] = (byte)(values[i] & 0xFF);
            data[i * 2 + 1] = (byte)(values[i] >> 8);
        }
        var pdu = _BuildPdu(0x1401, 0x0000, deviceCode, startNo,
                            (ushort)values.Length, data);
        await _SendReceive(pdu, ct);
    }

    /// <summary>비트 소자 단점 쓰기 (커맨드 0x1401, 서브 0x0001)</summary>
    private async Task _WriteBitAsync(
        byte deviceCode, int deviceNo, bool value, CancellationToken ct)
    {
        var data = new byte[] { value ? (byte)0x01 : (byte)0x00 };
        var pdu = _BuildPdu(0x1401, 0x0001, deviceCode, deviceNo, 1, data);
        await _SendReceive(pdu, ct);
    }

    // §8 ─ 3E 프레임 빌더 (byte[] 직접 조립) ─────────────

    /// <summary>
    /// MC 프로토콜 3E 요청 프레임을 byte[] 로 직접 조립합니다.
    /// MemoryStream/BinaryWriter 사용 없음 → System.IO 불필요.
    /// writeData null = 읽기 요청 / not null = 쓰기 요청
    /// </summary>
    private byte[] _BuildPdu(
        ushort command, ushort subCommand,
        byte deviceCode, int deviceNo, ushort count,
        byte[]? writeData)
    {
        // 고정 헤더부: 서브헤더(2)+네트워크(1)+PC(1)+IO번호(2)+국번(1)+길이(2) = 9
        // PDU부: 감시타이머(2)+커맨드(2)+서브커맨드(2)+소자번호(3)+소자코드(1)+점수(2) = 12
        // 쓰기 데이터: writeData?.Length
        int dataLen = 12 + (writeData?.Length ?? 0);   // 길이 필드가 감시타이머부터 끝까지
        int totalLen = 9 + dataLen;
        var buf = new byte[totalLen];
        int i = 0;

        // ── 서브헤더 (3E 고정: 0x50 0x00) ────────────────
        buf[i++] = 0x50;
        buf[i++] = 0x00;

        // ── 네트워크 번호 / PC 번호 ───────────────────────
        buf[i++] = _networkNo;
        buf[i++] = _pcNo;

        // ── 요청 선두 I/O 번호 (Little-Endian) ────────────
        buf[i++] = (byte)(_unitIoNo & 0xFF);
        buf[i++] = (byte)(_unitIoNo >> 8);

        // ── 다중 국번 ─────────────────────────────────────
        buf[i++] = _stationNo;

        // ── 데이터 길이 (감시타이머 이후 바이트 수, Little-Endian) ──
        buf[i++] = (byte)(dataLen & 0xFF);
        buf[i++] = (byte)(dataLen >> 8);

        // ── 감시 타이머 (Little-Endian) ───────────────────
        buf[i++] = (byte)(WatchdogTimer & 0xFF);
        buf[i++] = (byte)(WatchdogTimer >> 8);

        // ── 커맨드 (Little-Endian) ────────────────────────
        buf[i++] = (byte)(command & 0xFF);
        buf[i++] = (byte)(command >> 8);

        // ── 서브커맨드 (Little-Endian) ────────────────────
        buf[i++] = (byte)(subCommand & 0xFF);
        buf[i++] = (byte)(subCommand >> 8);

        // ── 소자 번호 (3바이트 Little-Endian) ────────────
        buf[i++] = (byte)(deviceNo & 0xFF);
        buf[i++] = (byte)((deviceNo >> 8) & 0xFF);
        buf[i++] = (byte)((deviceNo >> 16) & 0xFF);

        // ── 소자 코드 (1바이트) ───────────────────────────
        buf[i++] = deviceCode;

        // ── 점수 (Little-Endian) ──────────────────────────
        buf[i++] = (byte)(count & 0xFF);
        buf[i++] = (byte)(count >> 8);

        // ── 쓰기 데이터 (쓰기 요청 시만) ─────────────────
        if (writeData is not null)
        {
            Array.Copy(writeData, 0, buf, i, writeData.Length);
        }

        return buf;
    }

    // §9 ─ 송수신 ──────────────────────────────────────────

    private async Task<byte[]?> _SendReceive(byte[] request, CancellationToken ct)
    {
        if (_stream is null) return null;

        await _stream.WriteAsync(request, ct);

        // 응답 헤더: 서브헤더(2)+네트워크(1)+PC(1)+IO번호(2)+국번(1)+길이(2) = 9
        var header = new byte[9];
        await _stream.ReadExactlyAsync(header, ct);

        // 데이터 길이 (Little-Endian, 오프셋 7~8)
        int dataLen = (header[8] << 8) | header[7];
        if (dataLen < 2) return null;

        var resp = new byte[dataLen];
        await _stream.ReadExactlyAsync(resp, ct);

        // 종료 코드 확인 (Little-Endian, 0x0000 = 정상)
        int endCode = (resp[1] << 8) | resp[0];
        if (endCode != 0x0000)
        {
            OnError?.Invoke(DriverName, $"MC 프로토콜 오류 코드: 0x{endCode:X4}");
            return null;
        }

        // 종료코드(2) 이후 실제 데이터 반환
        return resp[2..];
    }

    // §10 ─ 소자 코드 / 주소 파싱 ─────────────────────────

    private static readonly Dictionary<string, byte> _DeviceCodes
        = new(StringComparer.OrdinalIgnoreCase)
        {
            // 비트 소자
            ["X"] = 0x9C,   // 입력
            ["Y"] = 0x9D,   // 출력
            ["M"] = 0x90,   // 내부 릴레이
            ["L"] = 0x92,   // 래치 릴레이
            ["B"] = 0xA0,   // 링크 릴레이
            ["F"] = 0x93,   // 어넌시에이터
            ["SB"] = 0xA1,   // 링크 특수 릴레이
            ["SM"] = 0x91,   // 특수 릴레이
                             // 워드 소자
            ["D"] = 0xA8,   // 데이터 레지스터 ← 가장 많이 사용
            ["W"] = 0xB4,   // 링크 레지스터
            ["R"] = 0xAF,   // 파일 레지스터
            ["ZR"] = 0xB0,   // 파일 레지스터 (확장)
            ["TN"] = 0xC2,   // 타이머 현재값
            ["CN"] = 0xC5,   // 카운터 현재값
            ["SD"] = 0xA9,   // 특수 레지스터
            ["SW"] = 0xB5,   // 링크 특수 레지스터
        };

    private static readonly HashSet<string> _BitDeviceNames
        = new(StringComparer.OrdinalIgnoreCase)
        { "X", "Y", "M", "L", "B", "F", "SB", "SM" };

    private static byte _GetDeviceCode(string address)
    {
        var name = _ExtractDeviceName(address);
        return _DeviceCodes.TryGetValue(name, out var code) ? code : (byte)0xA8;
    }

    private static string _ExtractDeviceName(string address)
    {
        var upper = address.ToUpperInvariant();
        // 2글자 소자 우선 (ZR, SB, SM, SD, SW, TN, CN)
        foreach (var key in _DeviceCodes.Keys.OrderByDescending(k => k.Length))
            if (upper.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return key;
        return "D";
    }

    private static bool _IsBitDevice(byte deviceCode)
        => deviceCode is 0x9C or 0x9D or 0x90 or 0x92
                      or 0xA0 or 0x93 or 0xA1 or 0x91;

    private static int _ParseDeviceNo(string address)
    {
        var name = _ExtractDeviceName(address);
        var rest = address[name.Length..].TrimStart('.');
        // X / Y 는 16진수
        if (name is "X" or "Y")
        {
            var hex = rest.Split('.')[0];
            return Convert.ToInt32(hex, 16);
        }
        return int.TryParse(rest.Split('.')[0], out var n) ? n : 0;
    }

    // §11 ─ 값 변환 헬퍼 ────────────────────────────────────

    private static int _WordSize(string dataType) => dataType switch
    {
        "Float32" or "Int32" or "UInt32" => 2,
        "Float64" or "Int64" or "UInt64" => 4,
        _ => 1
    };

    private static object? _ExtractWordValue(ushort[] words, int offset, string dataType)
    {
        if (offset >= words.Length) return null;
        return dataType switch
        {
            "Int16" => (short)words[offset],
            "UInt16" => words[offset],
            "Int32" => (int)((uint)((words[offset + 1] << 16) | words[offset])),
            "UInt32" => (uint)((words[offset + 1] << 16) | words[offset]),
            "Float32" => BitConverter.ToSingle(
                             BitConverter.GetBytes(
                                 (uint)((words[offset + 1] << 16) | words[offset]))),
            _ => (int)words[offset]
        };
    }

    private static ushort[] _EncodeWordValue(string value, string dataType)
    {
        return dataType switch
        {
            "Int16" or "UInt16" => [(ushort)(short.Parse(value))],
            "Int32" or "UInt32" => _Split32(uint.Parse(value)),
            "Float32" => _SplitFloat(float.Parse(value)),
            _ => [(ushort)(short.Parse(value))]
        };
    }

    // 미쓰비시 Little-Endian: 하위 워드 먼저
    private static ushort[] _Split32(uint v)
        => [(ushort)(v & 0xFFFF), (ushort)(v >> 16)];

    private static ushort[] _SplitFloat(float v)
    {
        var u = BitConverter.ToUInt32(BitConverter.GetBytes(v));
        return [(ushort)(u & 0xFFFF), (ushort)(u >> 16)];
    }

    private static TagValue _MakeValue(TagReadRequest t, object? v, TagQuality q)
        => new(t.TagId, v, q, DateTimeOffset.UtcNow);

    // §12 ─ 배치 그룹화 ────────────────────────────────────

    private static Dictionary<byte, List<TagReadRequest>> _GroupByDevice(
        IReadOnlyList<TagReadRequest> tags)
    {
        var result = new Dictionary<byte, List<TagReadRequest>>();
        foreach (var t in tags)
        {
            var code = _GetDeviceCode(t.Address);
            if (!result.TryGetValue(code, out var list))
                result[code] = list = new();
            list.Add(t);
        }
        return result;
    }

    private static List<List<TagReadRequest>> _MakeBatches(
        List<TagReadRequest> tags, int maxCount)
    {
        var batches = new List<List<TagReadRequest>>();
        var current = new List<TagReadRequest>();
        foreach (var tag in tags)
        {
            if (current.Count == 0) { current.Add(tag); continue; }
            var span = _ParseDeviceNo(tag.Address)
                     - _ParseDeviceNo(current[0].Address) + 1;
            if (span <= maxCount) current.Add(tag);
            else { batches.Add(current); current = [tag]; }
        }
        if (current.Count > 0) batches.Add(current);
        return batches;
    }

    // §13 ─ 파라미터 읽기 ──────────────────────────────────

    private void _ReadParams(DriverConfig config)
    {
        var p = config.Params ?? new();
        _host = p.GetValueOrDefault("Host", "192.168.0.1");
        _port = int.TryParse(p.GetValueOrDefault("Port", "5007"), out var port) ? port : 5007;
        _networkNo = byte.TryParse(p.GetValueOrDefault("NetworkNo", "0"), out var nn) ? nn : (byte)0;
        _pcNo = byte.TryParse(p.GetValueOrDefault("PCNo", "255"), out var pc) ? pc : (byte)0xFF;
        _stationNo = byte.TryParse(p.GetValueOrDefault("StationNo", "0"), out var sn) ? sn : (byte)0;
        _timeoutMs = int.TryParse(p.GetValueOrDefault("TimeoutMs", "5000"), out var tms) ? tms : 5000;
        _is4EFrame = p.GetValueOrDefault("FrameType", "3E") == "4E";

        if (ushort.TryParse(
                p.GetValueOrDefault("UnitIoNo", "03FF"),
                System.Globalization.NumberStyles.HexNumber,
                null, out var io))
            _unitIoNo = io;
    }

    // §14 ─ 리소스 해제 ────────────────────────────────────

    public async ValueTask DisposeAsync()
        => await DisconnectAsync();
}