// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/AnomalyFilterService.cs
//  역할: Tag 당 SpikeFilter + DeadbandFilter 인스턴스 관리
//        FlowEngine 폴링 결과(공학값)에 적용되어 이상값을 걸러낸다
//  C-16: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Models;
using lssLib.Log;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 이상값 필터 관리자 (DI 싱글턴).
/// <para>
/// CollectorConfigLoader 로드 완료 후 <see cref="Initialize"/> 를 호출하면
/// Tag 당 1개의 SpikeFilter/DeadbandFilter 를 생성한다.
/// FlowEngine 이 폴링 결과(EngValue)마다 <see cref="ShouldReject"/> 를 호출하여
/// 이상값 여부를 판정한다.
/// </para>
/// </summary>
public sealed class AnomalyFilterService
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader   _configLoader;
    private readonly CollectorSettingsLoader _settingsLoader;

    private readonly Dictionary<string, SpikeFilter>    _spikeFilters    = new();
    private readonly Dictionary<string, DeadbandFilter> _deadbandFilters = new();

    // §2 ─ 생성자 ──────────────────────────────────────────

    public AnomalyFilterService(
        CollectorConfigLoader   configLoader,
        CollectorSettingsLoader settingsLoader)
    {
        _configLoader   = configLoader;
        _settingsLoader = settingsLoader;
    }

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// Tag별 필터 인스턴스를 생성합니다.
    /// App.xaml.cs 에서 FlowEngine.StartAsync() 이전에 호출.
    /// </summary>
    public void Initialize()
    {
        _spikeFilters.Clear();
        _deadbandFilters.Clear();

        var f           = _settingsLoader.Settings.Filter;
        var spikePct    = f.SpikeMaxDeltaPercent / 100.0;
        var deadbandPct = f.DeadbandPercent      / 100.0;

        int count = 0;
        foreach (var plc in _configLoader.Plcs)
        {
            foreach (var tag in plc.Tags)
            {
                var range = _GetEngRange(tag);

                var spikeMax = f.SpikeFilterEnabled ? range * spikePct    : 0.0;
                var deadband = f.DeadbandEnabled    ? range * deadbandPct : 0.0;

                _spikeFilters[tag.Id]    = new SpikeFilter(spikeMax);
                _deadbandFilters[tag.Id] = new DeadbandFilter(deadband);
                count++;
            }
        }

        LogManager.Instance.Info("AnomalyFilter",
            $"이상값 필터 초기화 완료 — {count}개 Tag / " +
            $"Spike={(f.SpikeFilterEnabled ? $"{f.SpikeMaxDeltaPercent}%" : "비활성")}, " +
            $"Deadband={(f.DeadbandEnabled ? $"{f.DeadbandPercent}%" : "비활성")}");
    }

    // §4 ─ 판정 ────────────────────────────────────────────

    /// <summary>
    /// Tag 값을 필터링합니다.
    /// </summary>
    /// <param name="tagId">대상 Tag ID</param>
    /// <param name="engValue">ScaleEngine 적용 후 공학값</param>
    /// <param name="acceptedValue">
    /// 폐기(true) 시 하위 단계에 대신 사용할 값(직전 정상값).
    /// 통과(false) 시 engValue 그대로.
    /// </param>
    /// <param name="reason">폐기 사유 ("Spike" 또는 "Deadband"). 통과 시 null.</param>
    /// <returns>true = 이번 값을 폐기(발행 생략)해야 함</returns>
    public bool ShouldReject(string tagId, double engValue, out double acceptedValue, out string? reason)
    {
        acceptedValue = engValue;
        reason        = null;

        if (_spikeFilters.TryGetValue(tagId, out var spike) &&
            spike.IsSpike(engValue, out var accepted))
        {
            acceptedValue = accepted;
            reason        = "Spike";
            return true;
        }

        if (_deadbandFilters.TryGetValue(tagId, out var deadband) &&
            deadband.ShouldSuppress(engValue))
        {
            reason = "Deadband";
            return true;
        }

        return false;
    }

    // §5 ─ 헬퍼 ────────────────────────────────────────────

    /// <summary>
    /// 공학값 범위(EngMax-EngMin)를 반환합니다.
    /// 스케일 미설정 Tag 는 DataCollectionService(SDT) 와 동일하게 Raw 100 범위로 가정.
    /// </summary>
    private double _GetEngRange(TagRuntimeConfig tag)
    {
        if (!string.IsNullOrWhiteSpace(tag.ScaleEntryId) &&
            _configLoader.ScaleLibrary.TryGetValue(tag.ScaleEntryId, out var scale))
            return scale.EngMax - scale.EngMin;

        return 100.0;
    }
}
