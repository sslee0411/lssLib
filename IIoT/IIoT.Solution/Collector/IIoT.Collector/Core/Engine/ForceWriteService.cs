// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/ForceWriteService.cs
//  역할: Tag 강제값 쓰기(Force Write) 요청의 검증·안전장치 담당
//        실제 통신은 FlowEngine.WriteTagAsync() 에 위임
//
//  ★ 설계 원칙: FlowEngine 은 "쓰기 실행"만 담당하고,
//    "쓰기를 허용해도 되는가"에 대한 정책 판단은 이 서비스가 전담한다.
//    (책임 분리 — FlowEngine 은 이미 폴링/재연결 로직으로 충분히 복잡함)
//
//  C-15: 신규
//  생성: 2026-07-05
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Models;
using IIoT.Collector.Storage;
using IIoT.Contracts;
using lssLib.Log;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 강제쓰기 요청 결과 (검증 실패 시 통신 자체를 시도하지 않고 즉시 반환).
/// </summary>
public sealed record ForceWriteResult(bool IsSuccess, string? Error)
{
    public static ForceWriteResult Ok() => new(true, null);
    public static ForceWriteResult Fail(string error) => new(false, error);
}

/// <summary>
/// Tag 강제값 쓰기 서비스 (DI 싱글턴).
/// <para>
/// settings.json ForceWrite.Enabled = false(기본값) 이면 모든 쓰기 요청을 거부한다.
/// Enabled = true 이더라도 다음을 검증한 후에만 FlowEngine 에 실행을 위임한다:
/// <list type="bullet">
///   <item>Tag 존재 여부</item>
///   <item>Tag.IsEnabled (수집 비활성 Tag 는 쓰기도 차단 — 설정 불일치 방지)</item>
///   <item>드라이버 연결 상태 (FlowEngine 내부에서 재검증)</item>
///   <item>★ C-EX-02: Security.ForceWriteApiKey 설정 시 apiKey 일치 여부</item>
/// </list>
/// 모든 시도(성공/실패)는 ★ C-EX-03 AuditLogService 에 기록된다.
/// </para>
/// </summary>
public sealed class ForceWriteService
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorSettingsLoader _settingsLoader;
    private readonly CollectorConfigLoader   _configLoader;
    private readonly FlowEngine              _flowEngine;
    private readonly AuditLogService         _auditLog;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public ForceWriteService(
        CollectorSettingsLoader settingsLoader,
        CollectorConfigLoader   configLoader,
        FlowEngine              flowEngine,
        AuditLogService         auditLog)
    {
        _settingsLoader = settingsLoader;
        _configLoader   = configLoader;
        _flowEngine     = flowEngine;
        _auditLog       = auditLog;
    }

    // §3 ─ 공개 API ────────────────────────────────────────

    /// <summary>
    /// 검증 후 Tag 에 강제로 값을 씁니다.
    /// 검증 실패 시 통신을 시도하지 않고 즉시 실패를 반환합니다.
    /// </summary>
    /// <param name="plcId">대상 PLC/Device ID</param>
    /// <param name="tagId">대상 Tag ID</param>
    /// <param name="value">쓸 값 (문자열, Raw 값 기준)</param>
    /// <param name="apiKey">
    /// ★ C-EX-02 신규: settings.json Security.ForceWriteApiKey 가 설정된 경우 일치해야 함.
    /// 미설정(빈 문자열)이면 검증 생략 (하위 호환).
    /// </param>
    /// <param name="ct">취소 토큰</param>
    public async Task<ForceWriteResult> WriteAsync(
        string plcId, string tagId, string value, string apiKey = "", CancellationToken ct = default)
    {
        var target = $"{plcId}/{tagId}";

        // ① 기능 활성화 여부 (안전장치 — 기본 false)
        if (!_settingsLoader.Settings.ForceWrite.Enabled)
        {
            LogManager.Instance.Warn("ForceWrite",
                "강제쓰기 기능이 비활성화 상태입니다 (settings.json ForceWrite.Enabled=false)");
            await _auditLog.LogAsync("ForceWrite", target, $"value={value}", false);
            return ForceWriteResult.Fail("강제쓰기 기능이 비활성화되어 있습니다. settings.json 에서 활성화하세요.");
        }

        // ①B ★ C-EX-02: API Key 검증 (설정된 경우에만)
        var requiredKey = _settingsLoader.Settings.Security.ForceWriteApiKey;
        if (!string.IsNullOrEmpty(requiredKey) && requiredKey != apiKey)
        {
            await _auditLog.LogAsync("ForceWrite", target, "API Key 불일치", false);
            return ForceWriteResult.Fail("API Key 가 올바르지 않습니다.");
        }

        // ② Tag/PLC 존재 확인
        var plc = _configLoader.Plcs.FirstOrDefault(p => p.PlcId == plcId);
        var tag = plc?.Tags.FirstOrDefault(t => t.Id == tagId);
        if (plc is null || tag is null)
        {
            await _auditLog.LogAsync("ForceWrite", target, "Tag/PLC 없음", false);
            return ForceWriteResult.Fail($"Tag[{tagId}] 를 PLC[{plcId}] 에서 찾을 수 없습니다.");
        }

        // ③ 수집 비활성 Tag 차단 (설정 불일치 방지 — 꺼둔 Tag 는 쓰기도 차단)
        if (!tag.IsEnabled)
        {
            await _auditLog.LogAsync("ForceWrite", target, "Tag 비활성 상태", false);
            return ForceWriteResult.Fail($"Tag[{tag.Name}] 은 수집 비활성 상태입니다. 먼저 활성화하세요.");
        }

        // ④ 값 형식 최소 검증 (숫자/불리언 타입인데 파싱 불가한 값)
        if (!_IsValueCompatible(tag.DataType, value))
        {
            await _auditLog.LogAsync("ForceWrite", target, $"값 형식 불일치: {value}", false);
            return ForceWriteResult.Fail(
                $"입력값 '{value}' 이(가) Tag DataType({tag.DataType}) 과 호환되지 않습니다.");
        }

        // ⑤ 실제 쓰기 위임 (FlowEngine 이 드라이버 조회 + 통신 수행)
        var driverResult = await _flowEngine.WriteTagAsync(plcId, tagId, value, ct);

        // ★ C-EX-03: 성공/실패 모두 감사 로그 기록
        await _auditLog.LogAsync("ForceWrite", target,
            $"value={value}, tag={tag.Name}" + (driverResult.IsSuccess ? "" : $", error={driverResult.Error}"),
            driverResult.IsSuccess);

        return driverResult.IsSuccess
            ? ForceWriteResult.Ok()
            : ForceWriteResult.Fail(driverResult.Error ?? "알 수 없는 통신 오류");
    }

    // §4 ─ 값 호환성 검증 ──────────────────────────────────

    /// <summary>
    /// DataType 기준 최소한의 형식 검증.
    /// 드라이버별 인코딩 세부사항까지는 검증하지 않고, 명백한 오입력만 걸러낸다.
    /// </summary>
    private static bool _IsValueCompatible(string dataType, string value)
    {
        return dataType switch
        {
            "Bool" or "Coil" =>
                value is "0" or "1" or "true" or "false" or "True" or "False",

            "UInt16" or "Int16" or "UInt32" or "Int32" =>
                long.TryParse(value, out _),

            "Float32" or "Double" =>
                double.TryParse(value, out _),

            // 알 수 없는 DataType 은 드라이버 자체 검증에 위임 (차단하지 않음)
            _ => true
        };
    }
}
