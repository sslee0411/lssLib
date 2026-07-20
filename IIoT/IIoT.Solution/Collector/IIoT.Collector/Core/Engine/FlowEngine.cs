// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/FlowEngine.cs
//  역할: 수집 흐름 실행 엔진
//        CollectorConfigLoader.Plcs 순회 → 드라이버 생성·연결
//        → AsyncScheduler.ScheduleRecurring(PollMs) 폴링 등록
//        → ReadTagsAsync() 결과 → EventBus.Publish(TagValueUpdatedEvent)
//  C-03: 신규
//  C-09: PlcPollStat 통계 추가
//  C-12: 드라이버 자동 재연결 (지수 백오프) 추가
//  C-15: Tag 강제값 쓰기(WriteTagAsync) 추가
//  C-16: 이상값 필터(AnomalyFilterService) 연동
//  C-19: PLC별 수집 일시정지/재개(PauseCollection/ResumeCollection) 추가
//  C-EX-03: 일시정지/재개 시 감사 로그 기록 추가
//  S-프로토콜01 Step B: _PollOnceAsync 에서 IsProtocolBlockField Tag 제외 +
//               _PollProtocolBlocksAsync 신규 — plc.ProtocolBlocks 를
//               IBlockProtocolDriver 로 읽어 필드 값을 TagValueUpdatedEvent 로
//               발행(합성 TagId, ProtocolFieldTagId.Make 규칙 공유).
//               연결된 드라이버가 IBlockProtocolDriver 미구현이면 PLC 당 1회만
//               경고 로그 후 건너뜀(매 폴링 로그 폭주 방지)
//  S-프로토콜01 Step B 후속: _PollProtocolBlocksAsync 에서 필드에 연결된
//               ScaleEntryId 를 합성 TagRuntimeConfig 로 조회해 일반 Tag 와
//               동일하게 _scaleEngine.Apply() 로 Raw→공학단위 변환 적용
//  생성: 2026-06-29 / 수정: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Events;
using IIoT.Collector.Core.Models;
using IIoT.Collector.Core.Plugin;
using IIoT.Collector.Storage;
using IIoT.Contracts;
using lssLib.Log;
using lssLib.Messaging;

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// 수집 흐름 실행 엔진 (DI 싱글턴).
/// <para>
/// device.json 로드 결과를 기반으로 PLC 1개당 드라이버 인스턴스 1개를 생성·연결하고,
/// PollMs 주기로 <c>AsyncScheduler</c> 에 폴링 작업을 등록한다.
/// 연결 실패·폴링 오류 발생 시 지수 백오프로 자동 재연결을 시도한다 (C-12).
/// </para>
/// <para>
/// ★ while-true 루프 절대 사용 금지 — AsyncScheduler.ScheduleRecurring() 필수.
/// </para>
/// </summary>
public sealed class FlowEngine : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader   _configLoader;
    private readonly CollectorPluginService  _pluginService;
    private readonly ScaleEngine             _scaleEngine;
    private readonly AlarmStateManager       _alarmManager;
    private readonly CollectorSettingsLoader _settingsLoader;
    private readonly AnomalyFilterService    _anomalyFilter;   // ★ C-16 신규
    private readonly AuditLogService         _auditLog;        // ★ C-EX-03 신규

    /// <summary>PlcId → 드라이버 인스턴스</summary>
    private readonly Dictionary<string, IProtocolDriver> _drivers = new();

    /// <summary>PlcId → 폴링 스케줄 핸들</summary>
    private readonly Dictionary<string, ScheduledTask> _scheduledTasks = new();

    /// <summary>PlcId → 재연결 스케줄 핸들 (재연결 중인 PLC 목록)</summary>
    private readonly Dictionary<string, ScheduledTask> _retryTasks = new();

    /// <summary>PlcId → 현재 재시도 횟수 (백오프 인덱스 계산용)</summary>
    private readonly Dictionary<string, int> _retryCount = new();

    /// <summary>★ S-프로토콜01 Step B: 블록 미지원 드라이버 경고를 이미 남긴 PlcId
    /// (매 폴링 주기마다 반복 경고하지 않도록 1회만 기록)</summary>
    private readonly HashSet<string> _blockUnsupportedWarned = new();

    private bool _isRunning;

    // §2 ─ 통계 (C-09) ────────────────────────────────────

    private readonly Dictionary<string, PlcPollStat> _stats = new();
    public IReadOnlyDictionary<string, PlcPollStat> Stats => _stats;

    public sealed class PlcPollStat
    {
        public string PlcId       { get; init; } = string.Empty;
        public string PlcName     { get; init; } = string.Empty;
        public string DriverId    { get; init; } = string.Empty;
        public int    TagCount    { get; init; }
        public int    PollMs      { get; init; }
        public bool   IsConnected { get; set; }
        public bool   IsPaused    { get; set; }   // ★ C-19 신규
        public long   PollCount   { get; set; }
        public long   ErrorCount  { get; set; }
        public double LastPollMs  { get; set; }
        public DateTimeOffset LastPollAt { get; set; }
        public string? LastError  { get; set; }

        // ★ C-12: 재연결 상태 표시 추가
        public bool   IsRetrying      { get; set; }
        public int    RetryCount      { get; set; }
        public string RetryStatusText { get; set; } = string.Empty;
    }

    // §3 ─ 상태 조회 ───────────────────────────────────────

    public int  RunningPlcCount => _scheduledTasks.Count;
    public bool IsRunning       => _isRunning;

    // §4 ─ 생성자 ──────────────────────────────────────────

    public FlowEngine(
        CollectorConfigLoader   configLoader,
        CollectorPluginService  pluginService,
        ScaleEngine             scaleEngine,
        AlarmStateManager       alarmManager,
        CollectorSettingsLoader settingsLoader,
        AnomalyFilterService    anomalyFilter,   // ★ C-16 신규
        AuditLogService         auditLog)        // ★ C-EX-03 신규
    {
        _configLoader   = configLoader;
        _pluginService  = pluginService;
        _scaleEngine    = scaleEngine;
        _alarmManager   = alarmManager;
        _settingsLoader = settingsLoader;
        _anomalyFilter  = anomalyFilter;
        _auditLog       = auditLog;
    }

    // §5 ─ 시작 ────────────────────────────────────────────

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
            else
                _ScheduleRetry(plc);  // ★ C-12: 연결 실패 → 재연결 예약
        }

        _isRunning = true;
        LogManager.Instance.Info("FlowEngine",
            $"수집 시작 — {started}/{plcs.Count}개 PLC 폴링 등록 완료");
    }

    // §6 ─ PLC 1개 시작 ────────────────────────────────────

    private async Task<bool> _TryStartPlcAsync(PlcRuntimeConfig plc)
    {
        if (string.IsNullOrWhiteSpace(plc.DriverId) ||
            !_pluginService.IsKnownDriver(plc.DriverId) ||
            plc.Tags.Count == 0)
            return false;

        var driver = _pluginService.CreateDriver(plc.DriverId);
        if (driver is null) return false;

        driver.OnConnected += _ => EventBus.Instance.Publish(
            new PlcConnectionChangedEvent(plc.PlcId, plc.DriverId, true));
        driver.OnError += (_, msg) => EventBus.Instance.Publish(
            new PlcConnectionChangedEvent(plc.PlcId, plc.DriverId, false, msg));

        var connected = await driver.ConnectAsync(plc.ToDriverConfig());
        if (!connected)
        {
            await driver.DisposeAsync();
            return false;
        }

        _drivers[plc.PlcId]      = driver;
        _retryCount[plc.PlcId]   = 0;

        var task = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromMilliseconds(Math.Max(plc.PollMs, 100)),
            ct => _PollOnceAsync(plc, driver, ct),
            name: $"poll:{plc.PlcId}");

        _scheduledTasks[plc.PlcId] = task;

        _stats[plc.PlcId] = new PlcPollStat
        {
            PlcId       = plc.PlcId,
            PlcName     = plc.Name,
            DriverId    = plc.DriverId,
            TagCount    = plc.Tags.Count,
            PollMs      = plc.PollMs,
            IsConnected = true,
            IsRetrying  = false,
        };

        LogManager.Instance.Info("FlowEngine",
            $"[{plc.Name}] 폴링 등록 완료 — {plc.Tags.Count}개 Tag, {plc.PollMs}ms 주기");
        return true;
    }

    // §7 ─ 폴링 1회 실행 ───────────────────────────────────

    private async Task _PollOnceAsync(
        PlcRuntimeConfig plc, IProtocolDriver driver, CancellationToken ct)
    {
        // ★ S-프로토콜01 Step B: 프로토콜 블록 필드 placeholder Tag 는 실주소가
        //   없으므로 일반 폴링에서 제외 — _PollProtocolBlocksAsync 에서 별도 처리
        var enabledTags = plc.Tags.Where(t => t.IsEnabled && !t.IsProtocolBlockField).ToList();

        // ★ 블록만 있고 일반 Tag 는 없는 PLC 도 있을 수 있으므로, 블록 폴링은
        //   enabledTags 가 비어도 항상 시도한다
        if (enabledTags.Count == 0)
        {
            if (plc.ProtocolBlocks.Count > 0)
                await _PollProtocolBlocksAsync(plc, driver, ct);
            return;
        }

        var requests = enabledTags
            .Select(t => new TagReadRequest(t.Id, t.Address, t.DataType))
            .ToList();

        var result = await driver.ReadTagsAsync(requests, ct);

        if (!result.IsSuccess || result.Values is null)
        {
            if (_stats.TryGetValue(plc.PlcId, out var errStat))
            {
                errStat.ErrorCount++;
                errStat.LastError   = result.Error ?? "알 수 없는 오류";
                errStat.IsConnected = false;
                errStat.LastPollAt  = DateTimeOffset.UtcNow;
            }
            LogManager.Instance.Warn("FlowEngine",
                $"[{plc.Name}] 읽기 실패: {result.Error ?? "알 수 없는 오류"}");

            // ★ C-12: 연속 실패 → 드라이버 연결 끊김으로 판단 → 재연결 예약
            await _HandlePollFailureAsync(plc, driver);
            return;
        }

        var tagsById = enabledTags.ToDictionary(t => t.Id);
        foreach (var value in result.Values)
        {
            if (!tagsById.TryGetValue(value.TagId, out var tagConfig)) continue;
            var scaled = _scaleEngine.Apply(tagConfig, value.RawValue);

            // ★ C-16: 이상값 필터 (스파이크/데드밴드) — Alarm/저장/UI 모두에 적용되기 전에 필터링
            if (_anomalyFilter.ShouldReject(value.TagId, scaled.EngValue, out var acceptedEng, out var rejectReason))
            {
                LogManager.Instance.Warn("FlowEngine",
                    $"[{plc.Name}] {tagConfig.Name} 이상값 감지({rejectReason}) — " +
                    $"{scaled.EngValue:F2} 폐기, 이전값 유지");
                continue; // 이번 Tag 값은 발행하지 않음
            }

            EventBus.Instance.Publish(new TagValueUpdatedEvent(
                Value:         value,
                PlcId:         plc.PlcId,
                EngValue:      acceptedEng,
                Unit:          scaled.Unit,
                DecimalPlaces: scaled.DecimalPlaces,
                WasScaled:     scaled.WasScaled));

            _alarmManager.ProcessValue(value.TagId, acceptedEng, value.Timestamp);
        }

        if (_stats.TryGetValue(plc.PlcId, out var okStat))
        {
            okStat.PollCount++;
            okStat.IsConnected = true;
            okStat.LastError   = null;
            okStat.LastPollAt  = DateTimeOffset.UtcNow;
        }

        // ★ S-프로토콜01 Step B: 이 PLC 에 연결된 프로토콜 블록도 함께 폴링
        if (plc.ProtocolBlocks.Count > 0)
            await _PollProtocolBlocksAsync(plc, driver, ct);
    }

    // §7B-1 ─ ★ S-프로토콜01 Step B: 프로토콜 블록 폴링 ─────────

    /// <summary>
    /// plc.ProtocolBlocks 를 IBlockProtocolDriver 로 읽어 필드 값을 발행합니다.
    /// 연결된 드라이버가 IBlockProtocolDriver 를 구현하지 않으면(예: 가상 Tag
    /// 전용 드라이버에 프로토콜을 잘못 연결한 경우) PLC 당 1회만 경고 로그를
    /// 남기고 조용히 건너뜁니다 — 일반 Tag 수집은 계속 정상 동작합니다.
    /// ★ S-프로토콜01 Step B 후속: 필드에 ScaleEntryId 가 연결되어 있으면
    /// CollectorConfigLoader 가 합성한 placeholder TagRuntimeConfig(ScaleEntryId
    /// 그대로 보유)를 찾아 일반 Tag 와 동일하게 _scaleEngine.Apply() 로 변환한다.
    /// </summary>
    private async Task _PollProtocolBlocksAsync(
        PlcRuntimeConfig plc, IProtocolDriver driver, CancellationToken ct)
    {
        if (driver is not IBlockProtocolDriver blockDriver)
        {
            if (_blockUnsupportedWarned.Add(plc.PlcId))
                LogManager.Instance.Warn("FlowEngine",
                    $"[{plc.Name}] 드라이버[{plc.DriverId}] 는 프로토콜 블록 읽기(IBlockProtocolDriver)를 " +
                    "지원하지 않습니다 — 연결된 프로토콜이 무시됩니다. " +
                    "표준 블록은 modbus-tcp/mitsubishi-mc, 커스텀 프레임 블록은 raw-frame 드라이버가 필요합니다.");
            return;
        }

        // ScaleEngine.Apply(TagRuntimeConfig, raw) 호출을 위해 합성 Tag 를 Id 로 조회
        var blockTagsById = plc.Tags
            .Where(t => t.IsProtocolBlockField)
            .ToDictionary(t => t.Id);

        foreach (var block in plc.ProtocolBlocks)
        {
            BlockReadResult result;
            try
            {
                result = await blockDriver.ReadBlockAsync(block, ct);
            }
            catch (Exception ex)
            {
                LogManager.Instance.Warn("FlowEngine",
                    $"[{plc.Name}] 프로토콜 블록[{block.Name}] 읽기 예외: {ex.Message}");
                continue;
            }

            if (!result.IsSuccess || result.FieldValues is null)
            {
                LogManager.Instance.Warn("FlowEngine",
                    $"[{plc.Name}] 프로토콜 블록[{block.Name}] 읽기 실패: " +
                    $"{result.Error ?? "알 수 없는 오류"}");
                continue;
            }

            foreach (var field in block.Fields)
            {
                if (!result.FieldValues.TryGetValue(field.Id, out var raw)) continue;

                var tagId   = ProtocolFieldTagId.Make(plc.PlcId, block.Id, field.Id);
                var quality = raw is null ? TagQuality.Bad : TagQuality.Good;

                // ★ Step B 후속: ScaleEntryId 연결 시 실제 선형/수식 변환 적용,
                //   미연결 또는 합성 Tag 를 못 찾은 경우(이론상 발생하지 않음) Raw 값 그대로.
                double eng; string unit; int decimals; bool wasScaled;
                if (blockTagsById.TryGetValue(tagId, out var tagConfig))
                {
                    var scaled = _scaleEngine.Apply(tagConfig, raw);
                    eng = scaled.EngValue; unit = scaled.Unit; decimals = scaled.DecimalPlaces; wasScaled = scaled.WasScaled;
                }
                else
                {
                    eng = _ToDoubleOrZero(raw); unit = field.Unit; decimals = 2; wasScaled = false;
                }

                EventBus.Instance.Publish(new TagValueUpdatedEvent(
                    Value:         new TagValue(tagId, raw, quality, DateTimeOffset.UtcNow),
                    PlcId:         plc.PlcId,
                    EngValue:      eng,
                    Unit:          unit,
                    DecimalPlaces: decimals,
                    WasScaled:     wasScaled));
            }
        }
    }

    /// <summary>raw 값을 double 로 변환(null/변환불가 시 0.0) — ScaleEngine._ToDouble 과 동일 관례.</summary>
    private static double _ToDoubleOrZero(object? raw) => raw switch
    {
        null     => 0.0,
        double d => d,
        float f  => f,
        int i    => i,
        uint ui  => ui,
        long l   => l,
        short s  => s,
        ushort us => us,
        bool b   => b ? 1.0 : 0.0,
        string s2 when double.TryParse(s2, out var v) => v,
        _ => 0.0
    };

    // §7B ─ Tag 강제값 쓰기 (C-15 신규) ────────────────────

    /// <summary>
    /// 지정한 PLC/Tag 에 값을 강제로 씁니다.
    /// <para>
    /// ForceWriteService 에서만 호출되어야 하며(설정 활성화 여부는 그쪽에서 검증),
    /// FlowEngine 자체는 연결된 드라이버 조회 + TagWriteRequest 변환만 담당한다.
    /// </para>
    /// </summary>
    /// <param name="plcId">대상 PLC/Device ID</param>
    /// <param name="tagId">대상 Tag ID</param>
    /// <param name="value">쓸 값 (문자열, Raw 값 기준 — 드라이버가 DataType 에 맞게 변환)</param>
    /// <param name="ct">취소 토큰</param>
    public async Task<DriverWriteResult> WriteTagAsync(
        string plcId, string tagId, string value, CancellationToken ct = default)
    {
        if (!_drivers.TryGetValue(plcId, out var driver) || !driver.IsConnected)
            return DriverWriteResult.Fail($"PLC[{plcId}] 드라이버 미연결 상태");

        var plc = _configLoader.Plcs.FirstOrDefault(p => p.PlcId == plcId);
        var tag = plc?.Tags.FirstOrDefault(t => t.Id == tagId);
        if (plc is null || tag is null)
            return DriverWriteResult.Fail($"Tag[{tagId}] 를 PLC[{plcId}] 에서 찾을 수 없음");

        var request = new TagWriteRequest(tag.Id, tag.Address, tag.DataType, value);
        var result  = await driver.WriteTagAsync(request, ct);

        LogManager.Instance.Info("FlowEngine",
            result.IsSuccess
                ? $"[강제쓰기] {plc.Name}.{tag.Name}({tag.Address}) = {value} → 성공"
                : $"[강제쓰기] {plc.Name}.{tag.Name}({tag.Address}) = {value} → 실패: {result.Error}");

        EventBus.Instance.Publish(new TagForceWriteEvent(
            PlcId:      plcId,
            TagId:      tagId,
            TagName:    tag.Name,
            Address:    tag.Address,
            Value:      value,
            IsSuccess:  result.IsSuccess,
            Error:      result.Error,
            OccurredAt: DateTimeOffset.UtcNow));

        return result;
    }

    // §8 ─ 일시정지 / 재개 (C-19 신규) ──────────────────────

    /// <summary>
    /// 지정 PLC 의 폴링을 일시정지합니다. 드라이버 연결은 유지됩니다.
    /// </summary>
    public bool PauseCollection(string plcId)
    {
        if (!_scheduledTasks.TryGetValue(plcId, out var task)) return false;

        task.Pause();
        if (_stats.TryGetValue(plcId, out var stat)) stat.IsPaused = true;

        LogManager.Instance.Info("FlowEngine", $"[{plcId}] 수집 일시정지");
        EventBus.Instance.Publish(new PlcPauseChangedEvent(plcId, true));

        // ★ C-EX-03: 감사 로그 기록 (fire-and-forget)
        _ = _auditLog.LogAsync("Pause", plcId, "수집 일시정지", true);

        return true;
    }

    /// <summary>
    /// 지정 PLC 의 폴링을 재개합니다.
    /// </summary>
    public bool ResumeCollection(string plcId)
    {
        if (!_scheduledTasks.TryGetValue(plcId, out var task)) return false;

        task.Resume();
        if (_stats.TryGetValue(plcId, out var stat)) stat.IsPaused = false;

        LogManager.Instance.Info("FlowEngine", $"[{plcId}] 수집 재개");
        EventBus.Instance.Publish(new PlcPauseChangedEvent(plcId, false));

        // ★ C-EX-03: 감사 로그 기록 (fire-and-forget)
        _ = _auditLog.LogAsync("Resume", plcId, "수집 재개", true);

        return true;
    }

    // §9 ─ 재연결 스케줄 등록 (C-12) ──────────────────────

    /// <summary>
    /// 지수 백오프 간격으로 재연결을 예약합니다.
    /// settings.json Retry.IntervalsSec 배열 기준.
    /// ★ while-true 금지 — AsyncScheduler.ScheduleRecurring 사용
    /// </summary>
    private void _ScheduleRetry(PlcRuntimeConfig plc)
    {
        var retry = _settingsLoader.Settings.Retry;
        if (!retry.Enabled) return;

        var attempt  = _retryCount.GetValueOrDefault(plc.PlcId, 0);
        var maxRetry = retry.MaxRetries;

        if (maxRetry > 0 && attempt >= maxRetry)
        {
            LogManager.Instance.Warn("FlowEngine",
                $"[{plc.Name}] 최대 재시도 횟수({maxRetry}회) 초과 — 재연결 중단");
            _UpdateRetryStatus(plc.PlcId, false, attempt, "재연결 중단 (최대 횟수 초과)");
            return;
        }

        // 지수 백오프 간격 계산
        var intervals = retry.IntervalsSec;
        var idx       = Math.Min(attempt, intervals.Length - 1);
        var delaySec  = intervals[idx];

        _UpdateRetryStatus(plc.PlcId, true, attempt + 1,
            $"재연결 시도 중 ({attempt + 1}회 / {delaySec}초 후)");

        LogManager.Instance.Info("FlowEngine",
            $"[{plc.Name}] 재연결 예약 — {delaySec}초 후 시도 ({attempt + 1}회차)");

        // ★ ScheduleRecurring 으로 1회 지연 후 재연결 시도
        //   (OneShot 이 없으므로 ScheduleRecurring + 내부에서 Cancel 패턴 사용)
        ScheduledTask? retryTask = null;
        var fired = false;

        retryTask = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromSeconds(delaySec),
            async ct =>
            {
                if (fired) return;
                fired = true;
                retryTask?.Cancel();
                _retryTasks.Remove(plc.PlcId);

                if (!_isRunning) return;

                _retryCount[plc.PlcId] = attempt + 1;

                LogManager.Instance.Info("FlowEngine",
                    $"[{plc.Name}] 재연결 시도 중... ({attempt + 1}회차)");

                var success = await _TryStartPlcAsync(plc);
                if (success)
                {
                    _retryCount[plc.PlcId] = 0;
                    _UpdateRetryStatus(plc.PlcId, false, 0, string.Empty);
                    LogManager.Instance.Info("FlowEngine",
                        $"[{plc.Name}] 재연결 성공 — 폴링 재개");
                }
                else
                {
                    LogManager.Instance.Warn("FlowEngine",
                        $"[{plc.Name}] 재연결 실패 — 다음 재시도 예약");
                    _ScheduleRetry(plc);  // 재귀적 재예약
                }
            },
            name: $"retry:{plc.PlcId}");

        _retryTasks[plc.PlcId] = retryTask;
    }

    /// <summary>연속 폴링 실패 시 재연결 절차로 넘어갑니다 (드라이버 해제 후 재예약).</summary>
    private async Task _HandlePollFailureAsync(PlcRuntimeConfig plc, IProtocolDriver driver)
    {
        if (_scheduledTasks.TryGetValue(plc.PlcId, out var task))
        {
            task.Cancel();
            _scheduledTasks.Remove(plc.PlcId);
        }

        try { await driver.DisposeAsync(); }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("FlowEngine", $"[{plc.Name}] 드라이버 해제 중 오류: {ex.Message}");
        }
        _drivers.Remove(plc.PlcId);

        _ScheduleRetry(plc);
    }

    // §10 ─ 재연결 상태 업데이트 ──────────────────────────

    private void _UpdateRetryStatus(string plcId, bool isRetrying, int count, string text)
    {
        if (!_stats.TryGetValue(plcId, out var stat)) return;
        stat.IsConnected     = false;
        stat.IsRetrying      = isRetrying;
        stat.RetryCount      = count;
        stat.RetryStatusText = text;

        EventBus.Instance.Publish(new PlcConnectionChangedEvent(
            plcId, stat.DriverId, false,
            isRetrying ? text : "연결 끊김"));
    }

    // §11 ─ 정지 ──────────────────────────────────────────

    public async Task StopAsync()
    {
        if (!_isRunning) return;

        // 재연결 스케줄 전부 취소
        foreach (var t in _retryTasks.Values) t.Cancel();
        _retryTasks.Clear();
        _retryCount.Clear();

        // 폴링 스케줄 전부 취소
        foreach (var t in _scheduledTasks.Values) t.Cancel();
        _scheduledTasks.Clear();

        // 드라이버 해제
        foreach (var d in _drivers.Values)
        {
            try { await d.DisposeAsync(); }
            catch (Exception ex)
            {
                LogManager.Instance.Warn("FlowEngine",
                    $"드라이버 해제 중 오류: {ex.Message}");
            }
        }
        _drivers.Clear();

        _isRunning = false;
        _stats.Clear();
        _blockUnsupportedWarned.Clear();   // ★ S-프로토콜01 Step B
        LogManager.Instance.Info("FlowEngine", "수집 정지 완료");
    }

    // §12 ─ 리소스 해제 ───────────────────────────────────

    public async ValueTask DisposeAsync() => await StopAsync();
}
