// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/DeviceConfigSchema.cs
//  역할: lssLib.Config.ConfigSchema 기반 설정 파일 스키마 정의
//        scale-library / alarm-library 공통 검증 규칙 제공
//  Phase 1: Core 기반 구조
// ══════════════════════════════════════════════════════════

using lssLib.Config.Validation;

namespace IIoT.DeviceManager.Core.Config;

/// <summary>
/// IIoT 장비 설정 파일용 ConfigSchema 팩토리.
/// lssLib.Config.ConfigSchema 체이닝 빌더를 사용합니다.
/// </summary>
public static class DeviceConfigSchema
{
    // §1 ─ ScaleConfig 섹션 스키마 ────────────────────────────

    /// <summary>
    /// ScaleConfig 단일 섹션("ScaleLibrary:[id]") 검증 스키마를 반환합니다.
    /// </summary>
    /// <param name="sectionKey">검증 대상 섹션명 (예: "ScaleLibrary:sc001")</param>
    public static ConfigSchema ScaleSection(string sectionKey) =>
        new ConfigSchema()
            .Require(sectionKey, "name", ConfigValueType.String)
            .Require(sectionKey, "rawMin", ConfigValueType.Double)
            .Require(sectionKey, "rawMax", ConfigValueType.Double)
            .Require(sectionKey, "engMin", ConfigValueType.Double)
            .Require(sectionKey, "engMax", ConfigValueType.Double)
            .Optional(sectionKey, "unit", ConfigValueType.String, defaultValue: "")
            // 커스텀 검증: rawMin != rawMax
            .Custom(sectionKey, "rawMax", v =>
            {
                // rawMin, rawMax 비교는 호출자가 사후 검증
                return null; // ConfigSchema Custom 은 단일 필드만 접근 가능
            });

    // §2 ─ AlarmRule 섹션 스키마 ──────────────────────────────

    /// <summary>
    /// AlarmRule 단일 섹션("AlarmLibrary:[id]") 검증 스키마를 반환합니다.
    /// </summary>
    public static ConfigSchema AlarmSection(string sectionKey) =>
        new ConfigSchema()
            .Require(sectionKey, "name", ConfigValueType.String)
            .Optional(sectionKey, "hh", ConfigValueType.Double, defaultValue: "")
            .Optional(sectionKey, "h", ConfigValueType.Double, defaultValue: "")
            .Optional(sectionKey, "l", ConfigValueType.Double, defaultValue: "")
            .Optional(sectionKey, "ll", ConfigValueType.Double, defaultValue: "")
            .Optional(sectionKey, "deadBand", ConfigValueType.Double, defaultValue: "0")
            .Optional(sectionKey, "message", ConfigValueType.String,
                      defaultValue: "{tagName} {level} 알람: {value}");

    // §3 ─ Meta 섹션 스키마 ───────────────────────────────────

    /// <summary>
    /// 공통 Meta 섹션 스키마 (Version / UpdatedAt 필수).
    /// </summary>
    public static ConfigSchema MetaSection() =>
        new ConfigSchema()
            .Require("Meta", "Version", ConfigValueType.String)
            .Require("Meta", "UpdatedAt", ConfigValueType.String);

    // §4 ─ Tag Properties 수동 검증 ───────────────────────────

    /// <summary>
    /// ConfigNode(Tag) 의 Properties 딕셔너리를 직접 검증합니다.
    /// (ConfigSchema 는 섹션 기반이므로 Properties 는 수동 검증)
    /// </summary>
    /// <returns>오류 메시지 목록 (빈 목록 = 통과)</returns>
    public static List<string> ValidateTagProperties(
        string nodeName, IReadOnlyDictionary<string, string> props)
    {
        var errors = new List<string>();

        if (!props.ContainsKey("address") || string.IsNullOrWhiteSpace(props["address"]))
            errors.Add($"[{nodeName}] address 가 비어 있습니다.");

        if (props.TryGetValue("pollMs", out var pollMs) &&
            (!int.TryParse(pollMs, out var ms) || ms < 100))
            errors.Add($"[{nodeName}] pollMs 는 100ms 이상이어야 합니다 (현재: {pollMs}).");

        if (props.TryGetValue("deadBand", out var db) &&
            !double.TryParse(db, out _))
            errors.Add($"[{nodeName}] deadBand 숫자 형식 오류: {db}");

        return errors;
    }
}