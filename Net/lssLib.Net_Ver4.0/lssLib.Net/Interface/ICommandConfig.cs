// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Interface/ICommandConfig.cs
//  역할: 커맨드 집합 인터페이스 — Config Lego 브릭 4/4
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 커맨드 집합 인터페이스.
/// 주기 Read 요청 프레임과 
/// Write 요청 프레임 목록을 관리합니다.
/// </summary>
/// <remarks>
/// <b>ReadCommands</b> — <see cref="NetPriority.Read"/> 로 <c>PeriodicInterval</c> 마다 자동 전송.
/// IsSequential 에 따라 순차/병렬 처리됩니다.
///
/// <b>추가 예시 (lssLib.Binary 연동):</b>
/// <code>
/// // Modbus FC=03 Read Holding Registers
/// cfg.AddReadCommand(
///     BufferWriter.Create()
///         .WriteUInt8(0x01).WriteUInt8(0x03)
///         .WriteUInt16BE(0x0000).WriteUInt16BE(0x000A)
///         .AppendCrc16Modbus()
///         .ToArray());
/// </code>
/// </remarks>
public interface ICommandConfig
{
    /// <summary>주기적으로 전송할 읽기 요청 프레임 목록 (읽기 전용).</summary>
    IReadOnlyList<byte[]> ReadCommands { get; }

    /// <summary>쓰기 요청 프레임 참조 목록 (읽기 전용).</summary>
    IReadOnlyList<byte[]> WriteCommands { get; }

    /// <summary>읽기 요청 프레임을 추가합니다. (프로토콜 인코딩 전 원시 페이로드)</summary>
    void AddReadCommand(byte[] command);

    /// <summary>쓰기 요청 프레임을 추가합니다.</summary>
    void AddWriteCommand(byte[] command);

    /// <summary>모든 ReadCommands 와 WriteCommands 를 제거합니다.</summary>
    void ClearCommands();
}