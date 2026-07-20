// ══════════════════════════════════════════════════════════
//  IIoT.Driver.RawFrame · RawFrameDriver.cs
//  역할: 커스텀 바이트 프레임 실제 통신 구현 (IProtocolDriver + IBlockProtocolDriver)
//        순수 TCP Socket으로 [STX][LEN?][CMD][DATA][CRC?] 프레임을 구성해
//        Studio 프로토콜 라이브러리(S-프로토콜01)의 "커스텀 프레임" 블록
//        (ProtocolBlockSpec.CmdCode 가 채워진 블록)을 실행한다.
//
//  프레임 구조 (요청/응답 공통):
//    [STX 1byte] [LEN 1byte(옵션)] [CMD 1byte] [DATA n byte] [CRC 0~2byte(옵션)]
//    - STX      : ProtocolBlockSpec.StxHex (16진 문자열, 예: "AA")
//    - LEN      : HasLengthField=true 일 때만 포함 — CMD+DATA 의 바이트 수
//    - CMD      : ProtocolBlockSpec.CmdCode (16진 문자열, 예: "01")
//    - DATA(읽기 요청)  : [StartAddress 2byte BE][Length 1byte]
//    - DATA(쓰기 요청)  : [StartAddress 2byte BE][Length 1byte][필드 값 bytes...]
//    - DATA(응답)       : 필드 값 원시 바이트 (Fields.ByteOffset/BufType 로 슬라이싱 — 바이트 단위)
//    - CRC      : CrcType 에 따라 0/1/2바이트 (None/Xor/Sum=1바이트, Crc16Modbus=2바이트)
//
//  ★ 이 구조는 STX/LEN/CMD/CRC 필드를 가진 "일반적인 산업용 바이트 프레임"의
//    참조 구현입니다. 실제 사용하는 장비의 프레임 규격이 다르면(예: DATA 순서,
//    응답 헤더 구성 등) 이 파일을 프로젝트별로 조정해야 할 수 있습니다.
//
//  S-프로토콜01 Step B: 신규
//  S-프로토콜01 Step B 후속(2026-07-20): 문서화된 한계 2건 해소
//    ① HasLengthField=false(LEN 필드 미사용) 응답도 파싱 지원 — block.Length
//       (커스텀 프레임 블록의 DATA 바이트 수 약속)를 고정 길이로 사용해 CMD+DATA
//       구간을 읽음. 여전히 "요청과 동일한 헤더 구조로 응답한다"는 참조 구현
//       전제는 유지(실제 장비가 다르면 프로젝트별 조정 필요, 문서화된 제약).
//    ② 응답 프레임 CRC 검증 추가 — 수신한 CRC 바이트를 STX(+LEN)+CMD+DATA 구간에
//       대해 재계산한 CRC와 비교, 불일치 시 통신 실패로 처리(값 무시 방지).
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;
using System.Net.Sockets;

namespace IIoT.Driver.RawFrame;

/// <summary>
/// 커스텀 프레임(STX/LEN/CMD/DATA/CRC) 드라이버.
/// Tag 주소 단위 통신(IProtocolDriver.ReadTagsAsync/WriteTagAsync)은 지원하지
/// 않으며, 반드시 프로토콜 라이브러리 블록(IBlockProtocolDriver)을 통해서만
/// 사용한다.
/// </summary>
public sealed class RawFrameDriver : IProtocolDriver, IBlockProtocolDriver
{
    // §1 ─ 상태 ────────────────────────────────────────────

    public string DriverName  => "raw-frame";
    public bool   IsConnected => _client?.Connected ?? false;

    public event Action<string>?         OnConnected;
    public event Action<string, string>? OnError;

    private TcpClient?     _client;
    private NetworkStream? _stream;

    // §2 ─ 연결 파라미터 ───────────────────────────────────

    private string _host      = "192.168.0.1";
    private int    _port      = 9000;
    private int    _timeoutMs = 3000;

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
                SendTimeout    = _timeoutMs,
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

    // §4 ─ Tag 주소 단위 통신 — 미지원 ──────────────────────
    //   이 드라이버는 프로토콜 블록 전용이라 개별 Tag 주소 읽기/쓰기를
    //   지원하지 않는다. PLC에 raw-frame 을 연결했는데 일반 Tag(직접 주소
    //   입력)를 만든 경우 이 경로로 호출되며, 실패를 명확히 알린다.

    public Task<DriverReadResult> ReadTagsAsync(
        IReadOnlyList<TagReadRequest> tags, CancellationToken ct = default)
        => Task.FromResult(DriverReadResult.Fail(
            "raw-frame 드라이버는 Tag 주소 단위 통신을 지원하지 않습니다 — " +
            "프로토콜 라이브러리의 블록(읽기/쓰기)을 통해서만 통신할 수 있습니다",
            TagQuality.Bad));

    public Task<DriverWriteResult> WriteTagAsync(
        TagWriteRequest tag, CancellationToken ct = default)
        => Task.FromResult(DriverWriteResult.Fail(
            "raw-frame 드라이버는 Tag 주소 단위 통신을 지원하지 않습니다 — " +
            "프로토콜 라이브러리의 블록(읽기/쓰기)을 통해서만 통신할 수 있습니다"));

    // §5 ─ ★ 블록 단위 읽기/쓰기 (IBlockProtocolDriver) ────────

    public async Task<BlockReadResult> ReadBlockAsync(
        ProtocolBlockSpec block, CancellationToken ct = default)
    {
        if (!IsConnected)
            return BlockReadResult.Fail("미연결 상태");
        if (block.IsStandardBlock)
            return BlockReadResult.Fail(
                $"블록[{block.Name}] 은 CmdCode 가 비어있는 표준 주소범위 블록입니다 — " +
                "raw-frame 드라이버는 커스텀 프레임(CmdCode 있음) 블록만 지원합니다");

        try
        {
            // DATA = StartAddress(2byte BE) + Length(1byte)
            var startAddr = _ParseAddress(block.StartAddress);
            var data = new byte[3];
            data[0] = (byte)(startAddr >> 8);
            data[1] = (byte)startAddr;
            data[2] = (byte)Math.Max(block.Length, 1);

            var request  = _BuildFrame(block, data);
            var response = await _SendReceiveAsync(request, block, ct);
            if (response is null)
                return BlockReadResult.Fail("응답 없음 또는 프레임 검증 실패(STX/CRC 불일치)");

            var payload = _ExtractPayload(block, response);
            var values  = new Dictionary<string, object?>();
            foreach (var field in block.Fields)
                values[field.Id] = _ExtractFieldValue(payload, field.ByteOffset, field.BufType);

            return BlockReadResult.Ok(values);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(DriverName, $"블록[{block.Name}] 읽기 오류: {ex.Message}");
            return BlockReadResult.Fail(ex.Message);
        }
    }

    public async Task<BlockWriteResult> WriteBlockAsync(
        ProtocolBlockSpec block, IReadOnlyDictionary<string, object> fieldValues,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            return BlockWriteResult.Fail("미연결 상태");
        if (block.IsStandardBlock)
            return BlockWriteResult.Fail(
                $"블록[{block.Name}] 은 CmdCode 가 비어있는 표준 주소범위 블록입니다 — " +
                "raw-frame 드라이버는 커스텀 프레임(CmdCode 있음) 블록만 지원합니다");

        try
        {
            var startAddr = _ParseAddress(block.StartAddress);
            var length    = Math.Max(block.Length, 1);

            // 필드 값을 바이트 페이로드로 인코딩 (블록 길이만큼 버퍼 확보 — 워드 기준 2바이트/개)
            var payload = new byte[length * 2];
            foreach (var field in block.Fields)
            {
                if (!fieldValues.TryGetValue(field.Id, out var v)) continue;
                _WriteFieldValue(payload, field.ByteOffset, field.BufType, v);
            }

            // DATA = StartAddress(2byte BE) + Length(1byte) + 값 바이트열
            var data = new byte[3 + payload.Length];
            data[0] = (byte)(startAddr >> 8);
            data[1] = (byte)startAddr;
            data[2] = (byte)length;
            Array.Copy(payload, 0, data, 3, payload.Length);

            var request  = _BuildFrame(block, data);
            var response = await _SendReceiveAsync(request, block, ct);
            if (response is null)
                return BlockWriteResult.Fail("응답 없음 또는 프레임 검증 실패(STX/CRC 불일치, ACK 없음)");

            return BlockWriteResult.Ok();
        }
        catch (Exception ex)
        {
            OnError?.Invoke(DriverName, $"블록[{block.Name}] 쓰기 오류: {ex.Message}");
            return BlockWriteResult.Fail(ex.Message);
        }
    }

    // §6 ─ 프레임 빌드/파싱 ─────────────────────────────────

    /// <summary>[STX][LEN?][CMD][DATA][CRC?] 요청 프레임을 조립합니다.</summary>
    private static byte[] _BuildFrame(ProtocolBlockSpec block, byte[] data)
    {
        var stx = _ParseHexByte(block.StxHex, 0xAA);
        var cmd = _ParseHexByte(block.CmdCode, 0x00);
        var crcLen = _CrcByteLength(block.CrcType);

        // CMD(1) + DATA(n) 의 바이트 수 — LEN 필드 값
        var bodyLen = 1 + data.Length;

        var headerLen = 1 + (block.HasLengthField ? 1 : 0);
        var frame = new byte[headerLen + bodyLen + crcLen];
        int i = 0;

        frame[i++] = stx;
        if (block.HasLengthField)
            frame[i++] = (byte)bodyLen;
        frame[i++] = cmd;
        Array.Copy(data, 0, frame, i, data.Length);
        i += data.Length;

        if (crcLen > 0)
        {
            var crc = _ComputeCrc(frame, i, block.CrcType);
            if (crcLen == 2)
            {
                frame[i++] = (byte)(crc & 0xFF);
                frame[i++] = (byte)(crc >> 8);
            }
            else
            {
                frame[i++] = (byte)crc;
            }
        }

        return frame;
    }

    /// <summary>프레임 전송 후 응답 프레임을 수신·검증합니다. 실패 시 null.</summary>
    private async Task<byte[]?> _SendReceiveAsync(byte[] request, ProtocolBlockSpec block, CancellationToken ct)
    {
        if (_stream is null) return null;

        await _stream.WriteAsync(request, ct);

        // STX(1) 수신 후 검증
        var stxByte = new byte[1];
        await _stream.ReadExactlyAsync(stxByte, ct);

        // 응답 프레임도 요청과 동일한 STX/헤더 구성 규약을 따른다고 전제한다.
        // (커스텀 프레임 특성상 응답 헤더 구조는 요청과 동일하다고 전제 — 실제 장비 규격이
        //  다르면 이 메서드를 프로젝트별로 조정해야 함)
        return stxByte[0] == request[0]
            ? await _ReadRestOfFrameAsync(stxByte[0], block, ct)
            : null;
    }

    /// <summary>
    /// STX 이후 나머지 프레임([LEN?][CMD][DATA][CRC?])을 수신하고 CRC 를 검증합니다.
    /// ★ S-프로토콜01 Step B 후속: HasLengthField=false 인 블록은 LEN 바이트가 없으므로
    /// block.Length(커스텀 프레임 블록의 DATA 바이트 수 약속)를 고정 길이로 사용해
    /// CMD(1)+DATA(block.Length) 만큼만 읽는다. CRC 가 설정된 블록은 수신 CRC 바이트를
    /// 재계산한 값과 비교해 불일치 시 null(실패)을 반환한다.
    /// </summary>
    private async Task<byte[]?> _ReadRestOfFrameAsync(byte stx, ProtocolBlockSpec block, CancellationToken ct)
    {
        if (_stream is null) return null;

        byte  lenByte;
        int   bodyLen;   // CMD(1) + DATA(n) 바이트 수
        int   headerLen; // STX(1) + LEN(0/1)

        if (block.HasLengthField)
        {
            var lenBuf = new byte[1];
            await _stream.ReadExactlyAsync(lenBuf, ct);
            lenByte   = lenBuf[0];
            bodyLen   = lenByte;
            headerLen = 2; // STX + LEN
        }
        else
        {
            // LEN 필드가 없으므로 DATA 길이를 block.Length(약속된 DATA 바이트 수)로 고정 간주.
            lenByte   = 0;
            bodyLen   = 1 + Math.Max(block.Length, 1); // CMD(1) + DATA(block.Length)
            headerLen = 1; // STX 만
        }

        var body = new byte[bodyLen];
        await _stream.ReadExactlyAsync(body, ct);

        var full = new byte[headerLen + bodyLen];
        full[0] = stx;
        if (block.HasLengthField) full[1] = lenByte;
        Array.Copy(body, 0, full, headerLen, bodyLen);

        // ★ 응답 CRC 검증 — CrcType 이 None 이면 검증하지 않음
        var crcLen = _CrcByteLength(block.CrcType);
        if (crcLen > 0)
        {
            var crcBuf = new byte[crcLen];
            await _stream.ReadExactlyAsync(crcBuf, ct);

            var expected = _ComputeCrc(full, full.Length, block.CrcType);
            var received = crcLen == 2 ? (crcBuf[0] | (crcBuf[1] << 8)) : crcBuf[0];

            if (received != expected)
            {
                OnError?.Invoke(DriverName,
                    $"응답 CRC 불일치(기대값 0x{expected:X}, 수신값 0x{received:X}) — 값을 무시합니다");
                return null;
            }
        }

        return full;
    }

    /// <summary>응답 프레임에서 CMD(1byte)를 제외한 DATA 페이로드만 추출합니다.</summary>
    private static byte[] _ExtractPayload(ProtocolBlockSpec block, byte[] response)
    {
        // response = [STX][LEN?][CMD][DATA...]
        var headerBeforeCmd = block.HasLengthField ? 2 : 1; // STX(+LEN)
        if (response.Length <= headerBeforeCmd + 1) return Array.Empty<byte>();
        return response[(headerBeforeCmd + 1)..];
    }

    // §7 ─ 필드 값 추출/기록 (바이트 오프셋 기준) ───────────

    private static object? _ExtractFieldValue(byte[] payload, int byteOffset, string bufType)
    {
        if (byteOffset < 0 || byteOffset >= payload.Length) return null;

        int Remain() => payload.Length - byteOffset;

        return bufType switch
        {
            "Bool"    => payload[byteOffset] != 0,
            "Int16"   => Remain() >= 2 ? (short)((payload[byteOffset] << 8) | payload[byteOffset + 1]) : null,
            "UInt16"  => Remain() >= 2 ? (ushort)((payload[byteOffset] << 8) | payload[byteOffset + 1]) : null,
            "Int32"   => Remain() >= 4 ? (int)_ReadUInt32Be(payload, byteOffset)  : null,
            "UInt32"  => Remain() >= 4 ? _ReadUInt32Be(payload, byteOffset)       : null,
            "Float32" => Remain() >= 4 ? BitConverter.Int32BitsToSingle((int)_ReadUInt32Be(payload, byteOffset)) : null,
            _         => (object)payload[byteOffset]
        };
    }

    private static void _WriteFieldValue(byte[] payload, int byteOffset, string bufType, object value)
    {
        if (byteOffset < 0 || byteOffset >= payload.Length) return;
        var s = value?.ToString() ?? "0";

        switch (bufType)
        {
            case "Bool":
                payload[byteOffset] = (byte)(s is "1" or "true" or "True" ? 1 : 0);
                break;
            case "Int16":
            case "UInt16":
                if (byteOffset + 1 < payload.Length && int.TryParse(s, out var i16))
                {
                    payload[byteOffset]     = (byte)(i16 >> 8);
                    payload[byteOffset + 1] = (byte)i16;
                }
                break;
            case "Int32":
            case "UInt32":
                if (byteOffset + 3 < payload.Length && uint.TryParse(s, out var i32))
                    _WriteUInt32Be(payload, byteOffset, i32);
                break;
            case "Float32":
                if (byteOffset + 3 < payload.Length && float.TryParse(s, out var f32))
                    _WriteUInt32Be(payload, byteOffset, (uint)BitConverter.SingleToInt32Bits(f32));
                break;
            default:
                if (byte.TryParse(s, out var b)) payload[byteOffset] = b;
                break;
        }
    }

    private static uint _ReadUInt32Be(byte[] buf, int offset)
        => ((uint)buf[offset] << 24) | ((uint)buf[offset + 1] << 16)
         | ((uint)buf[offset + 2] << 8) | buf[offset + 3];

    private static void _WriteUInt32Be(byte[] buf, int offset, uint v)
    {
        buf[offset]     = (byte)(v >> 24);
        buf[offset + 1] = (byte)(v >> 16);
        buf[offset + 2] = (byte)(v >> 8);
        buf[offset + 3] = (byte)v;
    }

    // §8 ─ CRC 구현 ─────────────────────────────────────────

    private static int _CrcByteLength(string crcType) => crcType switch
    {
        "Crc16Modbus" => 2,
        "Xor" or "Sum" => 1,
        _ => 0   // "None"
    };

    /// <summary>frame[0..length) 구간에 대해 CrcType 에 따른 CRC/체크섬을 계산합니다.</summary>
    private static int _ComputeCrc(byte[] frame, int length, string crcType) => crcType switch
    {
        "Crc16Modbus" => _Crc16Modbus(frame, length),
        "Xor"         => _XorChecksum(frame, length),
        "Sum"         => _SumChecksum(frame, length),
        _             => 0
    };

    /// <summary>표준 Modbus CRC16 (다항식 0xA001, 초기값 0xFFFF).</summary>
    private static int _Crc16Modbus(byte[] data, int length)
    {
        ushort crc = 0xFFFF;
        for (int pos = 0; pos < length; pos++)
        {
            crc ^= data[pos];
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
                else crc >>= 1;
            }
        }
        return crc;
    }

    private static int _XorChecksum(byte[] data, int length)
    {
        byte x = 0;
        for (int i = 0; i < length; i++) x ^= data[i];
        return x;
    }

    private static int _SumChecksum(byte[] data, int length)
    {
        int sum = 0;
        for (int i = 0; i < length; i++) sum += data[i];
        return sum & 0xFF;
    }

    // §9 ─ 파싱 헬퍼 ────────────────────────────────────────

    /// <summary>"AA" 같은 16진 문자열 1바이트 파싱. 실패 시 기본값.</summary>
    private static byte _ParseHexByte(string? hex, byte def)
    {
        if (string.IsNullOrWhiteSpace(hex)) return def;
        var trimmed = hex.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);
        return byte.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var b)
            ? b : def;
    }

    /// <summary>StartAddress 문자열을 정수 주소로 파싱 (10진/16진 모두 허용).</summary>
    private static int _ParseAddress(string address)
    {
        if (int.TryParse(address, out var dec)) return dec;
        var trimmed = address.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);
        return int.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var hex)
            ? hex : 0;
    }

    // §10 ─ 파라미터 읽기 ───────────────────────────────────

    private void _ReadParams(DriverConfig config)
    {
        var p = config.Params ?? new();
        _host      = p.GetValueOrDefault("Host", "192.168.0.1");
        _port      = int.TryParse(p.GetValueOrDefault("Port", "9000"), out var port) ? port : 9000;
        _timeoutMs = int.TryParse(p.GetValueOrDefault("TimeoutMs", "3000"), out var tms) ? tms : 3000;
    }

    // §11 ─ 리소스 해제 ─────────────────────────────────────

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
