// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Interfaces/IBlockProtocolDriver.cs
//  역할: 프로토콜 블록(읽기/쓰기 N개) 단위 통신을 지원하는 드라이버의
//        선택적(opt-in) 확장 인터페이스
//        IProtocolDriver(Tag 주소 단위)와 별도로, 구현하는 드라이버만 캐스팅
//        (driver is IBlockProtocolDriver)으로 사용 — 미구현 드라이버는 무시됨
//  S-프로토콜01 Step B: 신규
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

/// <summary>
/// 프로토콜 블록 단위 읽기/쓰기를 지원하는 드라이버가 추가로 구현하는 인터페이스.
/// <para>
/// ModbusTcpDriver/MitsubishiDriver 는 표준 주소범위 블록(ProtocolBlockSpec.
/// IsStandardBlock=true)을 자신의 기존 레지스터 읽기/쓰기 로직으로 처리하도록
/// 구현한다. IIoT.Driver.RawFrame 은 커스텀 프레임 블록(STX/LEN/CMD/CRC)
/// 전용으로 구현한다.
/// </para>
/// <para>
/// FlowEngine 은 PLC 의 연결된 드라이버가 이 인터페이스를 구현하는지
/// (driver is IBlockProtocolDriver) 확인 후에만 블록 폴링을 수행하며,
/// 구현하지 않는 드라이버(예: 가상 Tag 전용 IIoT.Driver.Virtual)에 프로토콜이
/// 연결된 경우 경고 로그만 남기고 건너뛴다.
/// </para>
/// </summary>
public interface IBlockProtocolDriver
{
    /// <summary>
    /// 블록 1개를 읽습니다. 표준 블록은 StartAddress/Length 기준 레지스터 읽기,
    /// 커스텀 프레임 블록은 CmdCode 기반 프레임 통신으로 처리됩니다.
    /// </summary>
    Task<BlockReadResult> ReadBlockAsync(
        ProtocolBlockSpec block,
        CancellationToken ct = default);

    /// <summary>
    /// 블록 1개에 필드 값을 씁니다. fieldValues 는 ProtocolFieldSpec.Id → 쓸 값.
    /// </summary>
    Task<BlockWriteResult> WriteBlockAsync(
        ProtocolBlockSpec block,
        IReadOnlyDictionary<string, object> fieldValues,
        CancellationToken ct = default);
}
