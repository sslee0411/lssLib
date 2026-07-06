// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/DeviceInstanceService.cs
//  역할: CollectorConfigLoader(정적 설정) + ScaleLibrary/AlarmLibrary(규칙)
//        + EventBus(실시간 값/알람/연결 상태) 를 조합하여
//        DeviceInstance 트리를 만들고 실시간으로 최신 상태를 유지한다.
//
//  ★ 설계 원칙: 이 서비스는 오직 "조립·갱신"만 담당한다.
//    원본 데이터를 소유·계산하지 않으며, 다른 서비스(FlowEngine, AlarmStateManager,
//    ScaleEngine 등)의 내부 로직에는 전혀 개입하지 않는다.
//    → 기존 C-01~C-19 파이프라인은 이 서비스 존재 여부와 무관하게 그대로 동작한다.
//
//  C-EX-01: 신규 (Collector 실무강화 이후, Monitor 착수 전 사전 작업)
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Events;
using IIoT.Collector.Core.Models;
using lssLib.Log;
using lssLib.Messaging;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// DeviceInstance 트리 조립·실시간 갱신 서비스 (DI 싱글턴).
/// <para>
/// Initialize() 호출 시 CollectorConfigLoader 기준으로 트리를 1회 조립하고,
/// 이후에는 EventBus 구독으로 값·알람·연결 상태만 갱신한다 (트리 구조 자체는 불변).
/// </para>
/// </summary>
public sealed class DeviceInstanceService : IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader _configLoader;

    /// <summary>PlcId → DeviceInstance (조립된 트리)</summary>
    private readonly Dictionary<string, DeviceInstance> _devices = new();

    /// <summary>TagId → TagInstance (O(1) 조회용 평탄화 인덱스)</summary>
    private readonly Dictionary<string, TagInstance> _tagIndex = new();

    private IDisposable? _tagValueSub;
    private IDisposable? _alarmSub;
    private IDisposable? _connectionSub;
    private IDisposable? _pauseSub;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public DeviceInstanceService(CollectorConfigLoader configLoader)
    {
        _configLoader = configLoader;
    }

    // §3 ─ 공개 조회 API ───────────────────────────────────

    /// <summary>전체 DeviceInstance 목록 (Monitor SignalR/API 가 그대로 직렬화하여 사용)</summary>
    public IReadOnlyList<DeviceInstance> GetAll() => _devices.Values.ToList();

    /// <summary>PlcId 로 DeviceInstance 조회</summary>
    public DeviceInstance? GetDevice(string plcId)
        => _devices.TryGetValue(plcId, out var d) ? d : null;

    /// <summary>TagId 로 TagInstance 조회</summary>
    public TagInstance? GetTag(string tagId)
        => _tagIndex.TryGetValue(tagId, out var t) ? t : null;

    // §4 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// CollectorConfigLoader 기준으로 트리를 조립하고 EventBus 구독을 시작합니다.
    /// App.xaml.cs 에서 CollectorConfigLoader.LoadAsync() 직후 호출.
    /// (ConfigReloadWatcher 재시작 시에도 다시 호출되어 트리를 재조립함)
    /// </summary>
    public void Initialize()
    {
        _devices.Clear();
        _tagIndex.Clear();

        foreach (var plc in _configLoader.Plcs)
        {
            var tags = new List<TagInstance>(plc.Tags.Count);

            foreach (var tag in plc.Tags)
            {
                ScaleEntryDto? scale = null;
                if (!string.IsNullOrWhiteSpace(tag.ScaleEntryId))
                    _configLoader.ScaleLibrary.TryGetValue(tag.ScaleEntryId, out scale);

                AlarmEntryDto? alarmRule = null;
                if (!string.IsNullOrWhiteSpace(tag.AlarmEntryId))
                    _configLoader.AlarmLibrary.TryGetValue(tag.AlarmEntryId, out alarmRule);

                var tagInstance = new TagInstance
                {
                    PlcId      = plc.PlcId,
                    TagId      = tag.Id,
                    Name       = tag.Name,
                    Address    = tag.Address,
                    DataType   = tag.DataType,
                    Memo       = tag.Memo,
                    IsEnabled  = tag.IsEnabled,
                    IsVirtual  = tag.IsVirtual,
                    Scale      = scale,
                    AlarmRule  = alarmRule,
                    Unit       = tag.Unit
                };

                tags.Add(tagInstance);
                _tagIndex[tag.Id] = tagInstance;
            }

            _devices[plc.PlcId] = new DeviceInstance
            {
                PlcId    = plc.PlcId,
                Name     = plc.Name,
                NodeType = plc.NodeType,
                DriverId = plc.DriverId,
                PollMs   = plc.PollMs,
                Tags     = tags
            };
        }

        // 중복 구독 방지 (ConfigReloadWatcher 재시작 대비)
        _tagValueSub?.Dispose();
        _alarmSub?.Dispose();
        _connectionSub?.Dispose();
        _pauseSub?.Dispose();

        _tagValueSub   = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(_OnTagValue);
        _alarmSub      = EventBus.Instance.Subscribe<AlarmChangedEvent>(_OnAlarmChanged);
        _connectionSub = EventBus.Instance.Subscribe<PlcConnectionChangedEvent>(_OnConnectionChanged);
        _pauseSub      = EventBus.Instance.Subscribe<PlcPauseChangedEvent>(_OnPauseChanged);

        LogManager.Instance.Info("DeviceInstance",
            $"트리 조립 완료 — {_devices.Count}개 Device, {_tagIndex.Count}개 Tag");
    }

    // §5 ─ 실시간 갱신 핸들러 ──────────────────────────────

    private void _OnTagValue(TagValueUpdatedEvent e)
    {
        if (!_tagIndex.TryGetValue(e.Value.TagId, out var tag)) return;

        tag.RawValue  = e.Value.RawValue is double d ? d : null;
        tag.EngValue  = e.EngValue;
        tag.Unit      = e.Unit;
        tag.Quality   = e.Value.Quality;
        tag.UpdatedAt = e.Value.Timestamp;
    }

    private void _OnAlarmChanged(AlarmChangedEvent e)
    {
        if (!_tagIndex.TryGetValue(e.TagId, out var tag)) return;

        tag.AlarmStatusText = e.Status.ToString();

        // Recovered 면 더 이상 활성 알람 없음 — 레벨 표시 해제
        tag.ActiveAlarmLevel = e.Status.ToString() == "Recovered"
            ? null
            : e.Level.ToString();
    }

    private void _OnConnectionChanged(PlcConnectionChangedEvent e)
    {
        if (!_devices.TryGetValue(e.PlcId, out var device)) return;
        device.IsConnected = e.IsConnected;
    }

    private void _OnPauseChanged(PlcPauseChangedEvent e)
    {
        if (!_devices.TryGetValue(e.PlcId, out var device)) return;
        device.IsPaused = e.IsPaused;
    }

    // §6 ─ 정리 ────────────────────────────────────────────

    public void Dispose()
    {
        _tagValueSub?.Dispose();
        _alarmSub?.Dispose();
        _connectionSub?.Dispose();
        _pauseSub?.Dispose();
    }
}
