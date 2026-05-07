// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Interface/IDeviceConfig.cs
//  역할: 장비 식별 인터페이스 — Config Lego 브릭 1/4
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 장비 식별 인터페이스.
/// </summary>
/// <remarks>
/// <para><see cref="DeviceId"/> — 이벤트 핸들러 구분 키, NetDeviceRegistry 조회 키.</para>
/// <para><see cref="DeviceName"/> — LogManager Source 자동 적용. 파일명 형식 권장.
/// 예) "PLC-01" → <c>Log/날짜/PLC-01.txt</c></para>
/// </remarks>
public interface IDeviceConfig
{
    /// <summary>장비 고유 정수 ID. 앱 내에서 유일해야 합니다.</summary>
    int DeviceId { get; }

    /// <summary>장비 표시 이름. LogManager Source 로 자동 적용됩니다.</summary>
    string DeviceName { get; }
}