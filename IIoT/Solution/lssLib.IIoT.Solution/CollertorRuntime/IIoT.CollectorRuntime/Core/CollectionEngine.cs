// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Core/CollectionEngine.cs
//  역할: 수집 엔진 — 실제 프로토콜 드라이버 + SDT 압축 + SQLite
//  Phase 7: 시뮬레이터 기반 WPF UI 동작 확인
//  Phase 8: IProtocolDriver + AsyncScheduler 배치 폴링
//           SwingingDoorCompressor SDT 압축
//           CommandQueue → TagHistoryDb SQLite 저장
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.CollectorRuntime.Collection;
using IIoT.CollectorRuntime.Protocols;
using IIoT.CollectorRuntime.Storage;
using lssLib.Log;
using lssLib.Messaging;
using System.Collections.ObjectModel;
using System.IO;

namespace IIoT.CollectorRuntime.Core;

public enum EngineState { Stopped, Starting, Running, Stopping, Error }

public sealed partial class CollectionEngine : ObservableObject, IAsyncDisposable
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc = "CollectionEngine";

    private readonly string _configDir;
    private readonly CompressorRegistry _compressors =
        new(defaultDevBand: 0.5, defaultMaxTimeSec: 300);

    // 드라이버 레지스트리 (DeviceName → IProtocolDriver)
    private readonly Dictionary<string, IProtocolDriver> _drivers = [];

    // AsyncScheduler 작업 핸들 — StopAsync 시 Cancel()
    private readonly List<ScheduledTask> _pollTasks = [];

    private TagHistoryDb?            _db;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    // §2 ─ 바인딩 프로퍼티 ────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private EngineState _state = EngineState.Stopped;

    public bool   IsRunning => State == EngineState.Running;
    public string StateText => State switch
    {
        EngineState.Running  => "수집 중",
        EngineState.Starting => "시작 중...",
        EngineState.Stopping => "중지 중...",
        EngineState.Error    => "오류",
        _                    => "중지됨",
    };

    [ObservableProperty] private double _tps;
    [ObservableProperty] private int    _totalPolled;
    [ObservableProperty] private int    _errorCount;
    [ObservableProperty] private string _configInfo = "설정 미로드";
    [ObservableProperty] private double _compressionRatio;

    /// <summary>전체 수집 태그 목록 (UI 바인딩)</summary>
    public ObservableCollection<LiveTagValue> LiveTags { get; } = [];

    // §3 ─ 생성자 ─────────────────────────────────────────────
    public CollectionEngine(string configDir) => _configDir = configDir;

    // §4 ─ 수집 제어 ──────────────────────────────────────────

    /// <summary>수집 시작 — DB 초기화 → 설정 로드 → 드라이버 연결 → 폴링 등록</summary>
    public async Task StartAsync()
    {
        if (State == EngineState.Running) return;
        State = EngineState.Starting;
        LogManager.Instance.Info(LogSrc, "수집 엔진 시작");

        try
        {
            await _InitDbAsync();
            CommandQueue.Instance.Start();
            await _LoadConfigAsync();
            await _ConnectDriversAsync();

            _cts = new CancellationTokenSource();
            _RegisterPollingSchedules(_cts.Token);
            _ = _RunTpsAsync(_cts.Token);

            State = EngineState.Running;
            LogManager.Instance.Info(LogSrc,
                $"수집 시작 완료 — {LiveTags.Count}개 태그, {_drivers.Count}개 드라이버");

            EventBus.Instance.Publish(new EngineStateChangedEvent("Started"));
        }
        catch (Exception ex)
        {
            State = EngineState.Error;
            LogManager.Instance.Error(LogSrc, $"시작 실패: {ex.Message}");
        }
    }

    /// <summary>수집 중지 — 폴링 취소 → 드라이버 해제 → DB 저장 완료 대기</summary>
    public async Task StopAsync()
    {
        if (State == EngineState.Stopped) return;
        State = EngineState.Stopping;
        LogManager.Instance.Info(LogSrc, "수집 엔진 중지");

        // 1. AsyncScheduler 폴링 전체 취소
        foreach (var t in _pollTasks) t.Cancel();
        _pollTasks.Clear();

        _cts?.Cancel();
        await Task.Delay(300); // 진행 중인 폴링 완료 대기

        // 2. 드라이버 연결 해제
        foreach (var driver in _drivers.Values)
            await driver.DisposeAsync();
        _drivers.Clear();

        // 3. CommandQueue 잔여 저장 작업 완료 대기
        await CommandQueue.Instance.StopAsync();

        // 4. 태그 상태 초기화 → UI 갱신
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var t in LiveTags)
                t.Quality = TagQuality.NotCollecting;
        });

        _compressors.ResetAll();
        State = EngineState.Stopped;
        LogManager.Instance.Info(LogSrc, "수집 엔진 중지 완료");

        EventBus.Instance.Publish(new EngineStateChangedEvent("Stopped"));
    }

    /// <summary>수집 재시작 (설정 변경 시 ConfigReloadWatcher 에서 호출)</summary>
    public async Task RestartAsync()
    {
        await StopAsync();
        await Task.Delay(500);
        await StartAsync();
    }

    // §5 ─ DB 초기화 ──────────────────────────────────────────
    private async Task _InitDbAsync()
    {
        var dbPath = Path.Combine(_configDir, "tag_history.db");
        _db = new TagHistoryDb(dbPath);
        await _db.InitializeAsync();
        LogManager.Instance.Info(LogSrc, $"SQLite DB → {dbPath}");
    }

    // §6 ─ 설정 로드 ──────────────────────────────────────────
    private async Task _LoadConfigAsync()
    {
        await Task.Run(() =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                LiveTags.Clear());

            var configPath = Path.Combine(_configDir, "device.json");
            bool hasConfig = File.Exists(configPath);

            // ★ Phase 9: device.json → ConfigTree → AbstractNode 파싱으로 교체 예정
            // 현재: 기본 태그 정의로 구동
            _CreateDefaultTags();

            ConfigInfo = hasConfig
                ? $"device.json 로드됨 — {LiveTags.Count}개 태그"
                : $"시뮬레이터 모드 — {LiveTags.Count}개 태그";

            LogManager.Instance.Info(LogSrc, ConfigInfo);
        });
    }

    private void _CreateDefaultTags()
    {
        // ★ Phase 9 에서 device.json ConfigTree 파싱으로 교체
        var defs = new[]
        {
            (id:"t001", name:"압연기#1 베어링온도",  addr:"40001", unit:"°C",   dev:"압연기-001"),
            (id:"t002", name:"압연기#1 차압",         addr:"40003", unit:"kPa",  dev:"압연기-001"),
            (id:"t003", name:"압연기#1 모터전류",     addr:"40005", unit:"A",    dev:"압연기-001"),
            (id:"t004", name:"압연기#1 롤속도",       addr:"40007", unit:"rpm",  dev:"압연기-001"),
            (id:"t005", name:"압연기#1 RunStatus",    addr:"40009", unit:"",     dev:"압연기-001"),
            (id:"t006", name:"사출기#1 실린더온도",   addr:"40001", unit:"°C",   dev:"사출기-001"),
            (id:"t007", name:"사출기#1 사출압력",     addr:"40003", unit:"bar",  dev:"사출기-001"),
            (id:"t008", name:"사출기#1 냉각수온",     addr:"40005", unit:"°C",   dev:"사출기-001"),
            (id:"t009", name:"냉각탑#1 입구온도",     addr:"40001", unit:"°C",   dev:"냉각탑-001"),
            (id:"t010", name:"냉각탑#1 출구온도",     addr:"40003", unit:"°C",   dev:"냉각탑-001"),
            (id:"t011", name:"냉각탑#1 유량",         addr:"40005", unit:"m³/h", dev:"냉각탑-001"),
            (id:"t012", name:"공압라인 메인압력",     addr:"40001", unit:"bar",  dev:"공압-001"),
        };

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var d in defs)
                LiveTags.Add(new LiveTagValue
                {
                    TagId      = d.id,
                    TagName    = d.name,
                    Address    = d.addr,
                    Unit       = d.unit,
                    DeviceName = d.dev,
                });
        });
    }

    // §7 ─ 드라이버 생성 + 연결 ───────────────────────────────

    private async Task _ConnectDriversAsync()
    {
        var configPath = Path.Combine(_configDir, "device.json");

        if (!File.Exists(configPath))
        {
            // device.json 없음 → VirtualDriver 로 전체 구동
            await _ConnectVirtualDriversAsync();
            return;
        }

        // ★ Phase 9: device.json CommConfig → 드라이버 팩토리
        // 예시 (Phase 9 구현 예정):
        // ─────────────────────────────────────────────────────
        // var json    = File.ReadAllText(configPath);
        // var devices = CommConfigParser.Parse(json);  // device.json 파싱
        //
        // foreach (var dev in devices)
        // {
        //     IProtocolDriver driver = dev.CommType switch
        //     {
        //         "ModbusTcp" => new ModbusTcpDriver(new ModbusTcpConfig(
        //                            dev.Id, dev.Host, dev.Port, dev.UnitId)),
        //         "ModbusRtu" => new ModbusRtuDriver(new ModbusRtuConfig(
        //                            dev.Id, dev.SerialPort, dev.BaudRate, dev.UnitId)),
        //         _           => new VirtualDriver(dev.Id),
        //     };
        //
        //     bool ok = await driver.ConnectAsync();
        //     if (ok)
        //         _drivers[dev.Id] = driver;
        //     else
        //     {
        //         // 연결 실패 시 VirtualDriver 로 대체 (수집 중단 방지)
        //         LogManager.Instance.Warn(LogSrc, $"[{dev.Id}] 연결 실패 → VirtualDriver 대체");
        //         var vd = new VirtualDriver(dev.Id);
        //         await vd.ConnectAsync();
        //         _drivers[dev.Id] = vd;
        //     }
        // }
        // ─────────────────────────────────────────────────────

        // Phase 8 임시: device.json 있어도 VirtualDriver 사용
        await _ConnectVirtualDriversAsync();
        LogManager.Instance.Info(LogSrc,
            "Phase 9 에서 실제 드라이버 연결 예정 — 현재 VirtualDriver");
    }

    private async Task _ConnectVirtualDriversAsync()
    {
        // 장비별 VirtualDriver 생성
        var deviceNames = LiveTags
            .Select(t => t.DeviceName)
            .Distinct()
            .ToList();

        foreach (var name in deviceNames)
        {
            var vd = new VirtualDriver(name);
            await vd.ConnectAsync();
            _drivers[name] = vd;
        }

        LogManager.Instance.Info(LogSrc,
            $"VirtualDriver {_drivers.Count}개 연결");
    }

    // §8 ─ AsyncScheduler 폴링 등록 ────────────────────────────

    private void _RegisterPollingSchedules(CancellationToken ct)
    {
        // 장비(DeviceName) 별로 태그를 묶어 배치 폴링 등록
        var groups = LiveTags
            .GroupBy(t => t.DeviceName)
            .ToList();

        foreach (var group in groups)
        {
            var deviceName  = group.Key;
            var tags        = group.ToList();

            if (!_drivers.TryGetValue(deviceName, out var driver))
                continue;

            var addressDefs = tags
                .Select(t => new TagAddressDef(t.TagId, t.Address, t.Unit, PollMs: 1000))
                .ToList();

            // ★ lssLib AsyncScheduler.ScheduleRecurring
            //   반환: ScheduledTask (Pause / Resume / Cancel 가능)
            var pollTask = AsyncScheduler.Instance.ScheduleRecurring(
                TimeSpan.FromMilliseconds(1000),
                pollCt => _PollDeviceAsync(driver, tags, addressDefs, pollCt),
                name: $"poll-{deviceName}");

            _pollTasks.Add(pollTask);
            LogManager.Instance.Info(LogSrc,
                $"폴링 등록: {deviceName} ({tags.Count}개 태그)");
        }
    }

    // §9 ─ 폴링 실행 (AsyncScheduler 콜백) ────────────────────

    private async Task _PollDeviceAsync(
        IProtocolDriver      driver,
        List<LiveTagValue>   tags,
        List<TagAddressDef>  addressDefs,
        CancellationToken    ct)
    {
        try
        {
            // 드라이버 배치 읽기 — 1회 통신으로 모든 태그 수집
            var result = await driver.ReadBatchAsync(addressDefs, ct);
            var now    = DateTime.Now;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var tag in tags)
                {
                    if (result.IsSuccess
                        && result.Values.TryGetValue(tag.TagId, out var val)
                        && !double.IsNaN(val))
                    {
                        // UI 갱신
                        tag.UpdateValue(val, TagQuality.Good, now);

                        // ★ SDT 압축 판정 — 의미 있는 변화점만 저장
                        var compressor = _compressors.GetOrCreate(tag.TagId);
                        if (compressor.ShouldStore(val, now))
                        {
                            var record = new TagHistoryRecord(
                                tag.TagId, now, val, tag.Quality.ToString());

                            _db?.Enqueue(record);       // 이력 INSERT
                            _db?.UpsertLatest(record);  // 최신값 UPSERT
                        }

                        // EventBus 발행 → Monitor / AlarmEngine 구독
                        EventBus.Instance.Publish(
                            new TagValueUpdatedEvent(tag.TagId, val, TagQuality.Good));
                    }
                    else
                    {
                        // 통신 오류 또는 NaN
                        tag.UpdateValue(0, TagQuality.Bad, now);
                        ErrorCount++;

                        // BAD 품질도 저장 (데이터 단절 기록)
                        var badRecord = new TagHistoryRecord(
                            tag.TagId, now, 0, "Bad");
                        _db?.UpsertLatest(badRecord);
                    }
                }
            });

            TotalPolled    += tags.Count;
            CompressionRatio = _compressors.OverallCompressionRatio;
        }
        catch (OperationCanceledException) { /* 정상 중지 */ }
        catch (Exception ex)
        {
            ErrorCount++;
            LogManager.Instance.Warn(LogSrc,
                $"폴링 오류 [{driver.DriverId}]: {ex.Message}");
        }
    }

    // §10 ─ TPS 계산 (1초 타이머) ─────────────────────────────
    private async Task _RunTpsAsync(CancellationToken ct)
    {
        int prev = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, ct);
                int cur = TotalPolled;
                Tps  = cur - prev;
                prev = cur;
            }
            catch (OperationCanceledException) { break; }
        }
    }

    // §11 ─ IAsyncDisposable ──────────────────────────────────
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (IsRunning) await StopAsync();
        if (_db is not null) await _db.DisposeAsync();
        _cts?.Dispose();
        _disposed = true;
    }
}

// ── lssLib EventBus 이벤트 (T : EventMessage 필수) ───────
public sealed record EngineStateChangedEvent(string NewState)  : EventMessage;
public sealed record TagValueUpdatedEvent(
    string     TagId,
    double     Value,
    TagQuality Quality) : EventMessage;
