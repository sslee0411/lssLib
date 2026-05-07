// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Protocol/RawProtocol.cs
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;

namespace Lsslib.net.implementation;
/// <summary>
/// 인코딩·디코딩 없이 원시 바이트를 그대로 통과시키는 프로토콜.
/// </summary>
/// <remarks>
/// 사용 환경: 테스트, UDP, Modbus (CRC 포함 완성 프레임 직접 전달), SharedMemory IPC.
/// </remarks>
public sealed class RawProtocol : INetProtocol
{
    /// <inheritdoc/>
    public byte[] Encode(byte[] payload) => payload;

    /// <inheritdoc/>
    public bool TryDecode(byte[] raw, out byte[] payload)
    {
        payload = raw;
        return raw.Length > 0;
    }

    /// <inheritdoc/>
    public bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength)
    {
        frameLength = buffer.Length;
        return buffer.Length > 0;
    }

    /// <inheritdoc/>
    public byte[]? BuildHeartbeat() => null;
}