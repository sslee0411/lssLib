// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/FlowEngine.cs
//  역할: 수집 흐름 실행 엔진
//        CollectorConfigLoader.Plcs 순회 → 드라이버 생성·연결
//        → AsyncScheduler.ScheduleRecurring(PollMs) 폴링 등록
//        → ReadTagsAsync() 결과 → EventBus.Publish(TagValueUpdatedEvent)
//  C-03: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Events;
using IIoT.Collector.Core.Models;
using IIoT.Collector.Core.Plugin;
using IIoT.Contracts;
using lssLib.Log;
using lssLib.Messaging;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 수집 흐름 실행 엔진 (DI 싱글턴).
/// <para>
/// device.json 로드 결과(<see cref="CollectorConfigLoader.Plcs"/>)를 기반으로
/// PLC 1개당 드라이버 인스턴스 1개를 생성·연결하고, PollMs 주기로
/// <c>AsyncScheduler</c> 에 폴링 작업을 등록한다.
/// </para>
/// <para>
/// ★ while-true 루프 절대 사용 금지 — AsyncScheduler.ScheduleRecurring() 필수.
/// </para>
/// </summary>
public sealed class FlowEngine : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader  _configLoader;
    private readonly CollectorPluginService _pluginService;
    private readonly ScaleEngine            _scaleEngine;
    private readonly AlarmStateManager      _alarmManager;

    /// <summary>PlcId → 생성된 드라이버 인스턴스 (정리 시 DisposeAsync 호출용)</summary>
    private readonly Dictionary<string, IProtocolDriver> _drivers = new();

    /// <summary>PlcId → AsyncScheduler 가 반환한 작업 핸들 (정지 시 Cancel 호출용)</summary>
    private readonly Dictionary<string, ScheduledTask> _scheduledTasks = new();

    private bool _isRunning;

    /// <summary>PLC별 실시간 폴링 통계 (C-09 FlowView 표시용)</summary>
    private readonly Dictionary<string, PlcPollStat> _stats = new();

    /// <summary>PLC별 폴링 통계 조회 (읽기 전용 스냅샷)</summary>
    public IReadOnlyDictionary<string, PlcPollStat> Stats => _stats;

    /// <summary>PLC 폴링 통계 컨테이너</summary>
    public sealed class PlcPollStat
    {
        public string PlcId      { get; init; } = string.Empty;
        public string PlcName    { get; init; } = string.Empty;
        public string DriverId   { get; init; } = string.Empty;
        public int    TagCount   { get; init; }
        public int    PollMs     { get; init; }
        public bool   IsConnected { get; set; }
        public long   PollCount  { get; set; }
        public long   ErrorCount { get; set; }
        public double LastPollMs { get; set; }
        public DateTimeOffset LastPollAt { get; set; }
        public string? LastError { get; set; }
    }

    // §2 ─ 상태 조회 ───────────────────────────────────────

    /// <summary>현재 폴링 중인 PLC 수</summary>
    public int RunningPlcCount => _scheduledTasks.Count;

    /// <summary>엔진 실행 여부</summary>
    public bool IsRunning => _isRunning;

    // §3 ─ 생성자 ──────────────────────────────────────────

    public FlowEngine(
        CollectorConfigLoader  configLoader,
        CollectorPluginService pluginService,
        ScaleEngine            scaleEngine,
        AlarmStateManager      alarmManager)
    {
        _configLoader  = configLoader;
        _pluginService = pluginService;
        _scaleEngine   = scaleEngine;
        _alarmManager  = alarmManager;
    }

    // §4 ─ 시작 ────────────────────────────────────────────

    /// <summary>
    /// CollectorConfigLoader.Plcs 의 모든 PLC 에 대해 드라이버를 생성·연결하고
    /// 폴링을 시작합니다. driverId 가 미등록이거나 연결 실패한 PLC 는 건너뜁니다
    /// (다른 PLC 수집에는 영향 없음).
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            LogManager.Instance.Warn("FlowEngine", "이미 실행 중 — StartAsync 중복 호출 무시");
            return;
        }

        var plcs = _configLoader.Plcs;
        if (plcs.Count == 0)
        {
            LogManager.Instance.Warn("FlowEngine",
                "수집 대상 PLC 없음 — device.json 로드 상태를 확인하세요.");
            return;
        }

        int started = 0;
        foreach (var plc in plcs)
        {
            if (await _TryStartPlcAsync(plc))
                started++;
        }

        _isRunning = true;
        LogManager.Instance.Info("FlowEngine",
            $"수집 시작 — {started}/{plcs.Count}개 PLC 폴링 등록 완료");
    }

    // §5 ─ PLC 1개 시작 ────────────────────────────────────

    /// <summary>
    /// 단일 PLC 에 대해 드라이버 생성 → 연결 → 폴링 등록까지 수행합니다.
    /// 실패해도 예외를 던지지 않고 false 를 반환하여 다른 PLC 처리에 영향 없도록 합니다.
    /// </summary>
    private async Task<bool> _TryStartPlcAsync(PlcRuntimeConfig plc)
    {
        if (string.IsNullOrWhiteSpace(plc.DriverId))
        {
            LogManager.Instance.Warn("FlowEngine",
                $"[{plc.Name}] DriverId 없음 — 폴링 제외");
            return false;
        }

        if (!_pluginService.IsKnownDriver(plc.DriverId))
        {
            LogManager.Instance.Warn("FlowEngine",
                $"[{plc.Name}] 드라이버 미등록 \"{plc.DriverId}\" — 폴링 제외");
            return false;
        }

        if (plc.Tags.Count == 0)
        {
            LogManager.Instance.Warn("FlowEngine",
                $"[{plc.Name}] 수집 Tag 없음 — 폴링 제외");
            return false;
        }

        var driver = _pluginService.CreateDriver(plc.DriverId);
        if (driver is null)
        {
            LogManager.Instance.Error("FlowEngine",
                $"[{plc.Name}] 드라이버 인스턴스 생성 실패 \"{plc.DriverId}\"");
            return false;
        }

        // 연결 상태 이벤트 → EventBus 중계
        driver.OnConnected += _ => EventBus.Instance.Publish(
            new PlcConnectionChangedEvent(plc.PlcId, plc.DriverId, true));
        driver.OnError += (_, msg) => EventBus.Instance.Publish(
            new PlcConnectionChangedEvent(plc.PlcId, plc.DriverId, false, msg));

        var connected = await driver.ConnectAsync(plc.ToDriverConfig());
        if (!connected)
        {
            LogManager.Instance.Error("FlowEngine",
                $"[{plc.Name}] 연결 실패 \"{plc.DriverId}\" — 폴링 제외");
            await driver.DisposeAsync();
            return false;
        }

        _drivers[plc.PlcId] = driver;

        // ★ while-true 금지 — AsyncScheduler.ScheduleRecurring 필수
        var task = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromMilliseconds(Math.Max(plc.PollMs, 100)),
            ct => _PollOnceAsync(plc, driver, ct),
            name: $"poll:{plc.PlcId}");

        _scheduledTasks[plc.PlcId] = task;

        // C-09: 통계 초기화
        _stats[plc.PlcId] = new PlcPollStat
        {
            PlcId     = plc.PlcId,
            PlcName   = plc.Name,
            DriverId  = plc.DriverId,
            TagCount  = plc.Tags.Count,
            PollMs    = plc.PollMs,
            IsConnected = true,
        };

        LogManager.Instance.Info("FlowEngine",
            $"[{plc.Name}] 폴링 등록 완료 — {plc.Tags.Count}개 Tag, {plc.PollMs}ms 주기");

        return true;
    }

    // §6 ─ 폴링 1회 실행 ───────────────────────────────────

    /// <summary>
    /// AsyncScheduler 가 PollMs 주기로 호출하는 본체.
    /// ReadTagsAsync() 결과를 Tag 단위로 분해하여 EventBus 에 발행한다.
    /// </summary>
    private async Task _PollOnceAsync(
        PlcRuntimeConfig plc, IProtocolDriver driver, CancellationToken ct)
    {
        var enabledTags = plc.Tags.Where(t => t.IsEnabled).ToList();
        if (enabledTags.Count == 0) return;

        var requests = enabledTags
            .Select(t => new TagReadRequest(t.Id, t.Address, t.DataType))
            .ToList();

        var result = await driver.ReadTagsAsync(requests, ct);

        if (!result.IsSuccess || result.Values is null)
        {
            // C-09: 오류 통계 갱신
            if (_stats.TryGetValue(plc.PlcId, out var errStat))
            {
                errStat.ErrorCount++;
                errStat.LastError   = result.Error ?? "알 수 없는 오류";
                errStat.IsConnected = false;
                errStat.LastPollAt  = DateTimeOffset.UtcNow;
            }
            LogManager.Instance.Warn("FlowEngine",
                $"[{plc.Name}] 읽기 실패: {result.Error ?? "알 수 없는 오류"}");
            return;
        }

        // ★ C-05: TagId → TagRuntimeConfig O(1) 조회용 (ScaleEngine.Apply 에 전달)
        var tagsById = enabledTags.ToDictionary(t => t.Id);

        foreach (var value in result.Values)
        {
            if (!tagsById.TryGetValue(value.TagId, out var tagConfig))
                continue; // 드라이버가 요청하지 않은 TagId 를 반환하는 비정상 케이스 방어

            var scaled = _scaleEngine.Apply(tagConfig, value.RawValue);

            EventBus.Instance.Publish(new TagValueUpdatedEvent(
                Value:         value,
                PlcId:         plc.PlcId,
                EngValue:      scaled.EngValue,
                Unit:          scaled.Unit,
                DecimalPlaces: scaled.DecimalPlaces,
                WasScaled:     scaled.WasScaled));

            // ★ C-06: 공학값으로 임계값 검사 (ScaleEngine 이미 계산된 EngValue 재사용)
            _alarmManager.ProcessValue(value.TagId, scaled.EngValue, value.Timestamp);
        }

        // C-09: 폴링 성공 통계
        if (_stats.TryGetValue(plc.PlcId, out var okStat))
        {
            okStat.PollCount++;
            okStat.IsConnected = true;
            okStat.LastError   = null;
            okStat.LastPollAt  = DateTimeOffset.UtcNow;
        }
    }

    // §7 ─ 정지 ────────────────────────────────────────────

    /// <summary>
    /// 모든 PLC 의 폴링을 정지하고 드라이버 연결을 해제합니다.
    /// C-08 FSW 감지 시 설정 재로드 전에 호출되는 진입점.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        foreach (var task in _scheduledTasks.Values)
            task.Cancel();
        _scheduledTasks.Clear();

        foreach (var driver in _drivers.Values)
        {
            try
            {
                await driver.DisposeAsync();
            }
            catch (Exception ex)
            {
                LogManager.Instance.Warn("FlowEngine",
                    $"드라이버 해제 중 오류: {ex.Message}");
            }
        }
        _drivers.Clear();

        _isRunning = false;
        _stats.Clear();
        LogManager.Instance.Info("FlowEngine", "수집 정지 완료");
    }

    // §8 ─ 리소스 해제 ─────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
