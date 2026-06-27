// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Migration/CommTypeMigrator.cs
//  역할: 기존 device.json commType → 플러그인 driverId 자동 변환
//        레거시 하위 호환 유지 (기존 설정 파일 그대로 사용 가능)
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts.Migration;

/// <summary>
/// commType → driverId 변환 레이어 (Option C).
/// <para>
/// 기존 device.json은 commType: "ModbusTcp" 형식으로 저장되어 있다.
/// 플러그인 아키텍처에서는 driverId: "modbus-tcp" 형식을 사용한다.
/// 이 클래스는 두 형식을 투명하게 통합하여 기존 설정 파일을 수정 없이 사용할 수 있게 한다.
/// </para>
///
/// <b>변환 우선순위:</b>
/// <list type="number">
///   <item><description>driverId 가 있으면 그대로 사용 (신규 플러그인 형식)</description></item>
///   <item><description>driverId 없고 commType 있으면 변환 테이블 적용 (레거시 형식)</description></item>
///   <item><description>둘 다 없으면 빈 문자열 반환</description></item>
/// </list>
///
/// <example>
/// <code>
/// // 레거시 device.json 로드 시
/// string id = CommTypeMigrator.Resolve(dto.DriverId, dto.CommType);
/// // { commType: "ModbusTcp" } → "modbus-tcp"
/// // { driverId: "mitsubishi-mc" } → "mitsubishi-mc" (그대로)
/// // { commType: "None" } → "" (드라이버 없음)
/// </code>
/// </example>
/// </summary>
public static class CommTypeMigrator
{
    // §1 ─ 변환 테이블 ─────────────────────────────────────

    /// <summary>
    /// 레거시 commType → 플러그인 driverId 변환 테이블.
    /// Studio의 NodeCommType enum 값과 1:1 대응.
    /// </summary>
    private static readonly Dictionary<string, string> _legacyMap = new(
        StringComparer.OrdinalIgnoreCase)   // 대소문자 무관 매핑
    {
        // ── Modbus 계열 ──────────────────────────────────
        ["ModbusTcp"]  = "modbus-tcp",
        ["Modbus"]     = "modbus-tcp",      // 구버전 호환
        ["Serial"]     = "modbus-rtu",
        ["ModbusRtu"]  = "modbus-rtu",

        // ── 브로커/미들웨어 ───────────────────────────────
        ["Mqtt"]       = "mqtt-v311",
        ["MqttV5"]     = "mqtt-v5",
        ["OpcUa"]      = "opcua-v1",

        // ── 없음 / 자유 ──────────────────────────────────
        ["None"]       = "",
        ["Free"]       = "",
        [""]           = ""
    };

    // §2 ─ 공개 API ────────────────────────────────────────

    /// <summary>
    /// driverId 또는 commType 으로부터 최종 driverId 를 결정합니다.
    /// </summary>
    /// <param name="driverId">
    ///   device.json 의 driverId 필드 (신규 형식).
    ///   값이 있으면 그대로 반환.
    /// </param>
    /// <param name="commType">
    ///   device.json 의 commType 필드 (레거시 형식).
    ///   driverId 가 없을 때만 참조.
    /// </param>
    /// <returns>
    ///   최종 driverId 문자열.
    ///   드라이버 없음(None/Free)이면 빈 문자열.
    /// </returns>
    public static string Resolve(string? driverId, string? commType)
    {
        // 신규 형식: driverId 있으면 그대로 사용
        if (!string.IsNullOrWhiteSpace(driverId))
            return driverId.Trim();

        // 레거시 형식: commType → 변환 테이블 적용
        if (string.IsNullOrWhiteSpace(commType))
            return string.Empty;

        var key = commType.Trim();
        if (_legacyMap.TryGetValue(key, out var mapped))
            return mapped;

        // 테이블에 없는 값: 소문자 + 공백→하이픈 변환 (관례적 변환)
        // 예: "SiemensS7" → "siemenss7" (나중에 DRV 추가 시 매핑 보완)
        return key.ToLowerInvariant().Replace(" ", "-");
    }

    /// <summary>
    /// 변환 테이블에 새 항목을 런타임에 추가합니다.
    /// 새 드라이버 플러그인이 레거시 commType 과의 매핑이 필요할 때 사용합니다.
    /// </summary>
    /// <param name="legacyCommType">레거시 commType 값 (예: "SiemensS7")</param>
    /// <param name="driverId">대응하는 플러그인 driverId (예: "siemens-s7")</param>
    public static void RegisterLegacyMapping(string legacyCommType, string driverId)
    {
        if (!string.IsNullOrWhiteSpace(legacyCommType))
            _legacyMap[legacyCommType.Trim()] = driverId ?? string.Empty;
    }

    /// <summary>
    /// driverId 가 유효한 드라이버를 가리키는지 확인합니다.
    /// </summary>
    public static bool HasDriver(string? driverId)
        => !string.IsNullOrWhiteSpace(driverId);
}
