// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/VirtualTagEngine.cs
//  역할: 가상(계산) Tag 엔진
//        다른 Tag 값을 [TagId] 로 참조하는 NCalc 수식을 주기적으로 평가하고,
//        결과를 TagValueUpdatedEvent 로 발행한다.
//
//  ★ 설계 원칙: FlowEngine 은 전혀 수정하지 않음 — EventBus 구독만으로
//    모든 Tag(실제+가상)의 최신 값을 캐싱하고, 가상 Tag 값도 동일한 이벤트로
//    발행하므로 DataCollectionService(저장)·StatusViewModel(UI) 는
//    코드 수정 없이 자동으로 가상 Tag 를 함께 처리한다.
//    단, AlarmStateManager 는 FlowEngine 이 직접 호출하는 구조이므로
//    가상 Tag 알람 연동을 위해 이 엔진에서도 동일하게 직접 호출한다.
//
//  ━━━ 수식 문법 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  다른 Tag 값 참조: [TagId]
//  예) "[T001] + [T002] * 0.5"
//  참조된 Tag 중 하나라도 아직 값이 없으면 이번 주기는 평가를 건너뛴다.
//
//  C-18: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Events;
using IIoT.Collector.Core.Models;
using IIoT.Contracts;
using lssLib.Log;
using lssLib.Messaging;
using NCalc;
using System.Text.RegularExpressions;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 가상(계산) Tag 엔진 (DI 싱글턴).
/// </summary>
public sealed partial class VirtualTagEngine : IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader   _configLoader;
    private readonly CollectorSettingsLoader _settingsLoader;
    private readonly AlarmStateManager       _alarmManager;

    /// <summary>TagId → 최신 공학값 (실제 + 가상 Tag 전체)</summary>
    private readonly Dictionary<string, double> _liveValues = new();

    /// <summary>가상 Tag 목록 (TagId → 설정)</summary>
    private readonly Dictionary<string, TagRuntimeConfig> _virtualTags = new();

    private IDisposable?   _sub;
    private ScheduledTask? _task;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public VirtualTagEngine(
        CollectorConfigLoader   configLoader,
        CollectorSettingsLoader settingsLoader,
        AlarmStateManager       alarmManager)
    {
        _configLoader   = configLoader;
        _settingsLoader = settingsLoader;
        _alarmManager   = alarmManager;
    }

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// 가상 Tag 목록 구성 + EventBus 구독 + 평가 스케줄 등록.
    /// App.xaml.cs 에서 FlowEngine.StartAsync() 이후 호출.
    /// </summary>
    public void Initialize()
    {
        _liveValues.Clear();
        _virtualTags.Clear();

        var settings = _settingsLoader.Settings.VirtualTag;
        if (!settings.Enabled)
        {
            LogManager.Instance.Info("VirtualTag", "가상 Tag 엔진 비활성화 (설정)");
            return;
        }

        foreach (var plc in _configLoader.Plcs)
        {
            foreach (var tag in plc.Tags)
            {
                if (tag.IsVirtual && !string.IsNullOrWhiteSpace(tag.Expression))
                    _virtualTags[tag.Id] = tag;
            }
        }

        _sub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(_OnTagValue);

        _task = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromMilliseconds(Math.Max(settings.IntervalMs, 100)),
            _EvaluateAllAsync,
            name: "virtualtag:eval");

        LogManager.Instance.Info("VirtualTag",
            $"가상 Tag 엔진 초기화 완료 — {_virtualTags.Count}개 계산 Tag, {settings.IntervalMs}ms 주기");
    }

    // §4 ─ 최신값 캐싱 ─────────────────────────────────────

    private void _OnTagValue(TagValueUpdatedEvent e)
    {
        _liveValues[e.Value.TagId] = e.EngValue;
    }

    // §5 ─ 수식 평가 ───────────────────────────────────────

    private Task _EvaluateAllAsync(CancellationToken ct)
    {
        foreach (var (tagId, tag) in _virtualTags)
        {
            try
            {
                if (!_TryEvaluate(tag.Expression!, out var computed))
                    continue; // 참조 Tag 값 미확보 — 이번 주기 건너뜀

                var now = DateTimeOffset.UtcNow;

                _liveValues[tagId] = computed;

                var value = new TagValue(tagId, computed, TagQuality.Good, now);

                EventBus.Instance.Publish(new TagValueUpdatedEvent(
                    Value:         value,
                    PlcId:         tag.ParentPlcId,
                    EngValue:      computed,
                    Unit:          tag.Unit,
                    DecimalPlaces: 2,
                    WasScaled:     false));

                // ★ 알람 연동 — FlowEngine 과 동일 패턴으로 직접 호출
                //   (AlarmStateManager 는 EventBus 를 구독하지 않는 구조이므로 필요)
                _alarmManager.ProcessValue(tagId, computed, now);
            }
            catch (Exception ex)
            {
                LogManager.Instance.Warn("VirtualTag",
                    $"[{tag.Name}] 수식 평가 실패: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// [TagId] 참조를 캐싱된 값으로 치환한 뒤 NCalc 로 평가합니다.
    /// 참조된 Tag 중 값이 아직 없는 것이 있으면 false 를 반환합니다.
    /// </summary>
    private bool _TryEvaluate(string expression, out double result)
    {
        result = 0.0;

        var substituted = TagRefPattern().Replace(expression, m =>
        {
            var refTagId = m.Groups[1].Value;
            return _liveValues.TryGetValue(refTagId, out var v)
                ? v.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "\uFFFF"; // 존재하지 않는 값 — 아래에서 감지
        });

        if (substituted.Contains('\uFFFF'))
            return false;

        var expr = new Expression(substituted);
        var eval = expr.Evaluate();
        result = Convert.ToDouble(eval);
        return true;
    }

    [GeneratedRegex(@"\[([^\[\]]+)\]")]
    private static partial Regex TagRefPattern();

    // §6 ─ 정리 ────────────────────────────────────────────

    public void Dispose()
    {
        _sub?.Dispose();
        _task?.Cancel();
    }
}
