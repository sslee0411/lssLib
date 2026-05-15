// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Protocol/RawProtocol.cs
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 인코딩·디코딩 없이 원시 바이트를 그대로 통과시키는 프로토콜.
/// </summary>
/// <remarks>
/// <para>
/// <b>사용 환경:</b>
/// <list type="bullet">
///   <item><description>테스트 및 프로토타이핑</description></item>
///   <item><description>UDP (데이터그램 단위로 이미 프레임 완성)</description></item>
///   <item><description>Modbus RTU (CRC 포함 완성 프레임 직접 전달)</description></item>
///   <item><description>SharedMemory IPC (이미 구조화된 데이터)</description></item>
///   <item><description>HTTP / MQTT (페이로드 그대로 전달)</description></item>
/// </list>
/// </para>
/// <para>
/// Encode: 입력 그대로 반환.<br/>
/// TryDecode: 비어있지 않으면 항상 true.<br/>
/// BuildHeartbeat: null 반환 (Heartbeat 비활성).
/// </para>
/// </remarks>
public sealed class RawProtocol : INetProtocol
{
    /// <inheritdoc/>
    /// <remarks>pass-through — payload 를 그대로 반환합니다.</remarks>
    public byte[] Encode(byte[] payload) => payload;

    /// <inheritdoc/>
    /// <remarks>비어있지 않으면 항상 true 를 반환합니다.</remarks>
    public bool TryDecode(byte[] raw, out byte[] payload)
    {
        payload = raw;
        return raw.Length > 0;
    }

    /// <inheritdoc/>
    /// <remarks>버퍼에 데이터가 있으면 항상 완전한 프레임으로 간주합니다.</remarks>
    public bool IsFrameComplete(ReadOnlySpan<byte> buffer, out int frameLength)
    {
        frameLength = buffer.Length;
        return buffer.Length > 0;
    }

    /// <inheritdoc/>
    /// <remarks>RawProtocol 은 Heartbeat 를 지원하지 않습니다. null 반환 시 해당 주기 건너뜀.</remarks>
    public byte[]? BuildHeartbeat() => null;
}