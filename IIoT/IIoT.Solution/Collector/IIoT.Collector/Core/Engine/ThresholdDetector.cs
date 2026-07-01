// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/ThresholdDetector.cs
//  역할: 공학값(EngValue)과 AlarmEntryDto 임계값(HH/H/L/LL)을 비교
//        DelayMs: 임계 초과 후 해당 시간 이상 지속 시에만 알람 발생 (스파이크 무시)
//        RecoveryDelayMs: 복귀 후 해당 시간 이상 정상 유지 시에만 복귀 처리
//  C-06: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Events;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 단일 Tag 에 대한 임계값 감지기.
/// AlarmStateManager 가 Tag당 1개씩 생성·보관한다.
/// </summary>
internal sealed class ThresholdDetector
{
    // §1 ─ 설정 ────────────────────────────────────────────

    private readonly string        _tagId;
    private readonly AlarmEntryDto _entry;

    // §2 ─ 상태 (레벨별 진입 시각 — DelayMs 판정용) ───────

    /// <summary>각 레벨 임계 초과 진입 시각 (null = 현재 정상)</summary>
    private readonly Dictionary<AlarmLevel, DateTimeOffset?> _enteredAt = new()
    {
        [AlarmLevel.HH] = null,
        [AlarmLevel.H]  = null,
        [AlarmLevel.L]  = null,
        [AlarmLevel.LL] = null
    };

    /// <summary>각 레벨 복귀 시작 시각 (null = 아직 복귀 안 됨)</summary>
    private readonly Dictionary<AlarmLevel, DateTimeOffset?> _recoveredAt = new()
    {
        [AlarmLevel.HH] = null,
        [AlarmLevel.H]  = null,
        [AlarmLevel.L]  = null,
        [AlarmLevel.LL] = null
    };

    // §3 ─ 생성자 ──────────────────────────────────────────

    public ThresholdDetector(string tagId, AlarmEntryDto entry)
    {
        _tagId = tagId;
        _entry = entry;
    }

    // §4 ─ 감지 ────────────────────────────────────────────

    /// <summary>
    /// 새로운 공학값으로 임계값을 검사하고,
    /// 알람 발생/복귀 여부를 <see cref="ThresholdResult"/> 목록으로 반환합니다.
    /// </summary>
    public IReadOnlyList<ThresholdResult> Check(double engValue, DateTimeOffset now)
    {
        var results = new List<ThresholdResult>();

        _CheckLevel(AlarmLevel.HH, _entry.HhEnabled, engValue >= _entry.HhValue,
                    _entry.HhMessage, engValue, now, results);
        _CheckLevel(AlarmLevel.H,  _entry.HEnabled,  engValue >= _entry.HValue,
                    _entry.HMessage,  engValue, now, results);
        _CheckLevel(AlarmLevel.L,  _entry.LEnabled,  engValue <= _entry.LValue,
                    _entry.LMessage,  engValue, now, results);
        _CheckLevel(AlarmLevel.LL, _entry.LlEnabled, engValue <= _entry.LlValue,
                    _entry.LlMessage, engValue, now, results);

        return results;
    }

    // §5 ─ 레벨별 판정 ─────────────────────────────────────

    private void _CheckLevel(
        AlarmLevel level,
        bool       enabled,
        bool       isBreached,
        string     message,
        double     engValue,
        DateTimeOffset now,
        List<ThresholdResult> results)
    {
        if (!enabled) return;

        var delayMs    = _entry.DelayMs;
        var recoveryMs = _entry.RecoveryDelayMs;

        if (isBreached)
        {
            // 복귀 시각 초기화 (다시 초과됐으므로)
            _recoveredAt[level] = null;

            if (_enteredAt[level] is null)
            {
                // 처음 임계 진입
                _enteredAt[level] = now;
            }
            else
            {
                // DelayMs 경과 확인
                var elapsed = (now - _enteredAt[level]!.Value).TotalMilliseconds;
                if (elapsed >= delayMs && elapsed < delayMs + 1200)
                {
                    // 딜레이 경과 직후 1.2초 안에 한 번만 발생 이벤트
                    results.Add(new ThresholdResult(
                        _tagId, level, AlarmStatus.Active, message, engValue, now));
                }
            }
        }
        else
        {
            // 임계값 이하 (정상 범위)
            if (_enteredAt[level] is not null)
            {
                // 복귀 시작 기록
                if (_recoveredAt[level] is null)
                    _recoveredAt[level] = now;

                // RecoveryDelayMs 경과 확인
                var recoveryElapsed = (now - _recoveredAt[level]!.Value).TotalMilliseconds;
                if (recoveryElapsed >= recoveryMs)
                {
                    // 복귀 확정
                    results.Add(new ThresholdResult(
                        _tagId, level, AlarmStatus.Recovered, message, engValue, now));
                    _enteredAt[level]  = null;
                    _recoveredAt[level] = null;
                }
            }
        }
    }
}

/// <summary>ThresholdDetector 감지 결과 (AlarmStateManager 에 전달)</summary>
internal sealed record ThresholdResult(
    string         TagId,
    AlarmLevel     Level,
    AlarmStatus    Status,
    string         Message,
    double         EngValue,
    DateTimeOffset OccurredAt
);
