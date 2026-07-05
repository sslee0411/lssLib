// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/DataCollectionService.cs
//  역할: FlowEngine/AlarmStateManager → SDT 필터 → ITimeSeriesStore
//        EventBus 구독 → TagValueUpdatedEvent → SDT → 저장
//        EventBus 구독 → AlarmChangedEvent → 저장
//        AsyncScheduler → 1분 주기 수집 통계 집계·저장
//  C-07: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Engine;
using IIoT.Collector.Core.Events;
using IIoT.Contracts;
using lssLib.Log;
using lssLib.Messaging;
using System.Collections.Concurrent;

namespace IIoT.Collector.Storage;

/// <summary>
/// 데이터 수집·저장 서비스 (DI 싱글턴).
/// <para>
/// EventBus 의 TagValueUpdatedEvent / AlarmChangedEvent 를 구독하여<br/>
/// SDT 필터 통과 후 ITimeSeriesStore (SQLite 또는 InfluxDB) 에 저장한다.
/// </para>
/// <para>
/// <b>SDT 압축 동작:</b><br/>
/// Tag당 1개의 SdtCompressor 를 보관하며, 공학값(EngValue) 변화가
/// settings.json 의 SdtExcDevPercent 를 초과할 때만 저장한다.
/// </para>
/// </summary>
public sealed class DataCollectionService : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly ITimeSeriesStore        _store;
    private readonly CollectorConfigLoader   _configLoader;
    private readonly CollectorSettingsLoader _settingsLoader;

    /// <summary>TagId → SdtCompressor (Tag당 1개)</summary>
    private readonly ConcurrentDictionary<string, SdtCompressor> _compressors = new();

    /// <summary>TagId → 정보 캐시 (저장 레코드 구성용)</summary>
    private readonly ConcurrentDictionary<string, TagInfoCache> _tagCache = new();

    private IDisposable? _tagValueSub;
    private IDisposable? _alarmSub;

    /// <summary>통계 집계용 폴링 카운터 (PlcId → 카운터)</summary>
    private readonly ConcurrentDictionary<string, PollCounter> _pollCounters = new();

    // §2 ─ 내부 레코드 ─────────────────────────────────────

    private sealed record TagInfoCache(
        string TagId, string TagName, string PlcId);

    private sealed class PollCounter
    {
        public int PollCount;
        public int ErrorCount;
        public double TotalPollMs;
        public int TagCount;
    }

    // §3 ─ 생성자 ──────────────────────────────────────────

    public DataCollectionService(
        ITimeSeriesStore        store,
        CollectorConfigLoader   configLoader,
        CollectorSettingsLoader settingsLoader)
    {
        _store          = store;
        _configLoader   = configLoader;
        _settingsLoader = settingsLoader;
    }

    // §4 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// Tag별 SdtCompressor 생성 + EventBus 구독 시작 + 통계 스케줄러 등록.
    /// App.xaml.cs 에서 FlowEngine.StartAsync() 이후 호출.
    /// </summary>
    public void Initialize()
    {
        _compressors.Clear();
        _tagCache.Clear();
        _pollCounters.Clear();

        var excDevPct = _settingsLoader.Settings.Storage.SdtExcDevPercent / 100.0;

        foreach (var plc in _configLoader.Plcs)
        {
            _pollCounters[plc.PlcId] = new PollCounter { TagCount = plc.Tags.Count };

            foreach (var tag in plc.Tags)
            {
                double excDev;

                // ★ SdtExcDevPercent=0 → SDT 비활성화 (전량 저장)
                //   SdtCompressor 에 0 을 전달하면 항상 ShouldStore=true 반환
                if (excDevPct <= 0.0)
                {
                    excDev = 0.0;
                }
                else if (_configLoader.ScaleLibrary.TryGetValue(tag.ScaleEntryId ?? "", out var scale))
                {
                    excDev = (scale.EngMax - scale.EngMin) * excDevPct;
                }
                else
                {
                    // 스케일 없는 경우 Raw 값 기준 100 범위로 가정
                    excDev = 100.0 * excDevPct;
                }

                _compressors[tag.Id] = new SdtCompressor(excDev);
                _tagCache[tag.Id]    = new TagInfoCache(tag.Id, tag.Name, plc.PlcId);
            }
        }

        // EventBus 구독
        _tagValueSub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(_OnTagValue);
        _alarmSub    = EventBus.Instance.Subscribe<AlarmChangedEvent>(_OnAlarmChanged);

        // 통계 집계 스케줄러
        var statInterval = TimeSpan.FromSeconds(
            _settingsLoader.Settings.Storage.StatIntervalSec);

        AsyncScheduler.Instance.ScheduleRecurring(
            statInterval,
            _WriteStatsAsync,
            name: "collector:stats");

        LogManager.Instance.Info("DataSvc",
            $"데이터 수집 서비스 초기화 완료 — " +
            $"{_compressors.Count}개 Tag SDT 감시, Provider={_settingsLoader.Settings.Storage.Provider}");
    }

    // §5 ─ Tag 값 이벤트 핸들러 ───────────────────────────

    private void _OnTagValue(TagValueUpdatedEvent e)
    {
        if (!_compressors.TryGetValue(e.Value.TagId, out var sdt)) return;

        // SDT 필터 — 변화가 허용 오차 이내면 저장 생략
        if (!sdt.ShouldStore(e.EngValue)) return;

        _tagCache.TryGetValue(e.Value.TagId, out var info);

        var record = new TagHistoryRecord(
            TagId:     e.Value.TagId,
            TagName:   info?.TagName ?? e.Value.TagId,
            PlcId:     e.PlcId,
            RawValue:  e.Value.RawValue is double d ? d : 0,
            EngValue:  e.EngValue,
            Unit:      e.Unit,
            Quality:   e.Value.Quality.ToString(),
            Timestamp: e.Value.Timestamp);

        // 비동기 저장 (fire-and-forget — 내부 CommandQueue 로 순서 보장)
        _ = _store.WriteTagHistoryAsync(record);

        // 폴링 카운터 갱신
        if (_pollCounters.TryGetValue(e.PlcId, out var counter))
            Interlocked.Increment(ref counter.PollCount);
    }

    // §6 ─ 알람 이벤트 핸들러 ─────────────────────────────

    private void _OnAlarmChanged(AlarmChangedEvent e)
    {
        var record = new AlarmHistoryRecord(
            AlarmKey:  e.AlarmKey,
            TagId:     e.TagId,
            TagName:   e.TagName,
            PlcId:     e.PlcId,
            Level:     e.Level.ToString(),
            Status:    e.Status.ToString(),
            Message:   e.Message,
            EngValue:  e.CurrentEngValue,
            OccurredAt: e.OccurredAt);

        _ = _store.WriteAlarmHistoryAsync(record);
    }

    // §7 ─ 통계 집계 (1분 주기) ────────────────────────────

    private async Task _WriteStatsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (plcId, counter) in _pollCounters)
        {
            var pollCount  = Interlocked.Exchange(ref counter.PollCount,  0);
            var errorCount = Interlocked.Exchange(ref counter.ErrorCount, 0);

            if (pollCount == 0) continue;

            var record = new CollectorStatsRecord(
                PlcId:      plcId,
                PollCount:  pollCount,
                ErrorCount: errorCount,
                AvgPollMs:  counter.TotalPollMs / Math.Max(pollCount, 1),
                TagCount:   counter.TagCount,
                Timestamp:  now);

            await _store.WriteStatsAsync(record, ct);
        }
    }

    // §8 ─ 리소스 해제 ─────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _tagValueSub?.Dispose();
        _alarmSub?.Dispose();
        await _store.FlushAsync();
        await _store.DisposeAsync();
        LogManager.Instance.Info("DataSvc", "데이터 수집 서비스 종료 완료");
    }
}
