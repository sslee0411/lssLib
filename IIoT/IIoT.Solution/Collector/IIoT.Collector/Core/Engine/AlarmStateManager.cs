// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/AlarmStateManager.cs
//  역할: ThresholdDetector 결과 수신 → 알람 상태 관리
//        활성 알람 추가/ACK/복귀 처리 → EventBus.Publish(AlarmChangedEvent)
//  C-06: 신규
//  C-EX-03: ACK 처리 시 감사 로그(AuditLogService) 기록 추가
//  생성: 2026-06-29 / 수정: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Events;
using IIoT.Collector.Core.Models;
using IIoT.Collector.Storage;
using lssLib.Log;
using lssLib.Messaging;
using System.Linq;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 알람 상태 관리자 (DI 싱글턴).
/// <para>
/// CollectorConfigLoader 의 AlarmLibrary 와 TagRuntimeConfig 를 연결하여
/// Tag 당 ThresholdDetector 를 생성·보관하고, 폴링 결과(공학값)를 전달받아
/// 알람 발생/복귀 시 <see cref="AlarmChangedEvent"/> 를 EventBus 로 발행한다.
/// </para>
/// </summary>
public sealed class AlarmStateManager
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader _configLoader;
    private readonly AuditLogService       _auditLog;   // ★ C-EX-03 신규

    /// <summary>TagId → ThresholdDetector (AlarmEntryId 가 설정된 Tag만)</summary>
    private readonly Dictionary<string, ThresholdDetector> _detectors  = new();

    /// <summary>TagId → TagRuntimeConfig 빠른 조회 (이름·PlcId 참조용)</summary>
    private readonly Dictionary<string, TagRuntimeConfig>  _tagIndex   = new();

    /// <summary>AlarmKey → AlarmStatus (현재 알람 상태 추적)</summary>
    private readonly Dictionary<string, AlarmStatus>       _alarmState = new();

    // §2 ─ 생성자 ──────────────────────────────────────────

    public AlarmStateManager(CollectorConfigLoader configLoader, AuditLogService auditLog)
    {
        _configLoader = configLoader;
        _auditLog     = auditLog;
    }

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// CollectorConfigLoader 로드 완료 후 호출.
    /// AlarmEntryId 가 있는 Tag 에 대해 ThresholdDetector 를 생성합니다.
    /// </summary>
    public void Initialize()
    {
        _detectors.Clear();
        _tagIndex.Clear();
        _alarmState.Clear();

        int count = 0;
        foreach (var plc in _configLoader.Plcs)
        {
            foreach (var tag in plc.Tags)
            {
                _tagIndex[tag.Id] = tag;

                if (string.IsNullOrWhiteSpace(tag.AlarmEntryId)) continue;
                if (!_configLoader.AlarmLibrary.TryGetValue(tag.AlarmEntryId, out var entry)) continue;

                _detectors[tag.Id] = new ThresholdDetector(tag.Id, entry);
                count++;
            }
        }

        LogManager.Instance.Info("AlarmMgr",
            $"알람 감지기 초기화 완료 — {count}개 Tag");
    }

    // §4 ─ 값 처리 ─────────────────────────────────────────

    /// <summary>
    /// FlowEngine 폴링 결과(공학값)를 수신하여 임계값을 검사합니다.
    /// AlarmChangedEvent 는 상태 변경 시에만 발행합니다 (매 폴링마다 발행하지 않음).
    /// </summary>
    public void ProcessValue(string tagId, double engValue, DateTimeOffset timestamp)
    {
        if (!_detectors.TryGetValue(tagId, out var detector)) return;

        var results = detector.Check(engValue, timestamp);
        if (results.Count == 0) return;

        foreach (var r in results)
        {
            var alarmKey = $"{tagId}:{r.Level}";

            // 현재 상태와 동일하면 중복 발행 방지
            if (_alarmState.TryGetValue(alarmKey, out var current) && current == r.Status)
                continue;

            _alarmState[alarmKey] = r.Status;

            _tagIndex.TryGetValue(tagId, out var tag);

            var ev = new AlarmChangedEvent(
                AlarmKey:       alarmKey,
                TagId:          tagId,
                TagName:        tag?.Name ?? tagId,
                PlcId:          tag?.ParentPlcId ?? string.Empty,
                Level:          r.Level,
                Status:         r.Status,
                Message:        r.Message,
                OccurredAt:     r.OccurredAt,
                CurrentEngValue: engValue
            );

            EventBus.Instance.Publish(ev);

            var statusLabel = r.Status == AlarmStatus.Active   ? "발생" :
                              r.Status == AlarmStatus.Recovered ? "복귀" : "ACK";
            LogManager.Instance.Warn("AlarmMgr",
                $"[{tag?.Name ?? tagId}] 알람 {statusLabel}: {r.Level} — {r.Message} (값={engValue:F2})");
        }
    }

    // §5 ─ ACK 처리 ────────────────────────────────────────

    /// <summary>
    /// 특정 알람을 ACK 처리합니다 (AlarmView 에서 호출).
    /// AlarmChangedEvent(Status=Acked) 를 발행합니다.
    /// </summary>
    public void Acknowledge(string alarmKey)
    {
        if (!_alarmState.TryGetValue(alarmKey, out var current)) return;
        if (current != AlarmStatus.Active) return;

        _alarmState[alarmKey] = AlarmStatus.Acked;

        // AlarmKey 에서 TagId 와 Level 파싱
        var parts = alarmKey.Split(':');
        if (parts.Length < 2) return;

        var tagId = parts[0];
        var level = Enum.TryParse<AlarmLevel>(parts[1], out var lv) ? lv : AlarmLevel.H;
        _tagIndex.TryGetValue(tagId, out var tag);

        EventBus.Instance.Publish(new AlarmChangedEvent(
            AlarmKey:       alarmKey,
            TagId:          tagId,
            TagName:        tag?.Name ?? tagId,
            PlcId:          tag?.ParentPlcId ?? string.Empty,
            Level:          level,
            Status:         AlarmStatus.Acked,
            Message:        string.Empty,
            OccurredAt:     DateTimeOffset.UtcNow,
            CurrentEngValue: 0
        ));

        LogManager.Instance.Info("AlarmMgr", $"알람 ACK: {alarmKey}");

        // ★ C-EX-03: 감사 로그 기록 (fire-and-forget — 기존 동기 시그니처 유지)
        _ = _auditLog.LogAsync("AlarmAck", alarmKey, $"Tag={tag?.Name ?? tagId}", true);
    }

    /// <summary>현재 활성 알람 수 (Active 상태만)</summary>
    public int ActiveAlarmCount
        => _alarmState.Values.Count(s => s == AlarmStatus.Active);
}
