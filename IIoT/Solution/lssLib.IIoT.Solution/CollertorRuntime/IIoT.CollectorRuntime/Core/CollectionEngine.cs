// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Core/CollectionEngine.cs
//  역할: 수집 엔진 — 실제 프로토콜 드라이버 + SDT 압축 + SQLite
//  Phase 7: 시뮬레이터 기반 WPF UI 동작 확인
//  Phase 8: IProtocolDriver + AsyncScheduler 배치 폴링
//           SwingingDoorCompressor SDT 압축
//           CommandQueue → TagHistoryDb SQLite 저장
//  Phase 9: device.json ConfigTree 파싱 → 드라이버 팩토리
//           _LoadConfigAsync()  — 동적 태그 구성
//           _ConnectDriversAsync() — CommType 기반 드라이버 선택
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.CollectorRuntime.Collection;
using IIoT.CollectorRuntime.Protocols;
using IIoT.CollectorRuntime.Storage;
using lssLib.Config.Tree;
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

    // §6 ─ 설정 로드 (Phase 9: device.json ConfigTree 파싱) ────
    private async Task _LoadConfigAsync()
    {
        await Task.Run(() =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                LiveTags.Clear());

            var configPath = Path.Combine(_configDir, "device.json");

            if (!File.Exists(configPath))
            {
                // device.json 없음 → 시뮬레이터 모드
                LogManager.Instance.Warn(LogSrc,
                    "device.json 없음 — 시뮬레이터 모드로 구동");
                _CreateDefaultTags();
                ConfigInfo = $"시뮬레이터 모드 — {LiveTags.Count}개 태그";
                return;
            }

            try
            {
                // ★ Phase 9: ConfigTree.FromJson() 으로 파싱
                var json = File.ReadAllText(configPath);
                var tree = new ConfigTree();
                tree.FromJson(json);

                var tagNodes = tree.Flatten()
                    .Where(n => n.Type == NodeType.Tag)
                    .ToList();

                if (tagNodes.Count == 0)
                {
                    LogManager.Instance.Warn(LogSrc,
                        "device.json에 Tag 노드가 없음 — 시뮬레이터 모드 병행");
                    _CreateDefaultTags();
                    ConfigInfo = $"device.json (태그 없음) — 시뮬레이터 {LiveTags.Count}개";
                    return;
                }

                // Tag 노드 → LiveTagValue 동적 생성
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var node in tagNodes)
                    {
                        // 수집 주체 Device 탐색 (부모 방향 상향)
                        var ownerName = _ResolveOwnerName(node, tree);

                        LiveTags.Add(new LiveTagValue
                        {
                            TagId      = node.Id,
                            TagName    = node.Name,
                            Address    = node.GetProperty("address") ?? string.Empty,
                            Unit       = _ResolveUnit(node, tree),
                            DeviceName = ownerName,
                        });
                    }
                });

                ConfigInfo = $"device.json 로드됨 — {LiveTags.Count}개 태그";
                LogManager.Instance.Info(LogSrc, ConfigInfo);
            }
            catch (Exception ex)
            {
                LogManager.Instance.Error(LogSrc,
                    $"device.json 파싱 오류: {ex.Message} — 시뮬레이터 모드 전환");
                _CreateDefaultTags();
                ConfigInfo = $"파싱 오류 (시뮬레이터) — {LiveTags.Count}개 태그";
            }
        });
    }

    /// <summary>
    /// Tag 노드로부터 수집 주체 Device/Plc 이름을 탐색합니다.
    /// 우선순위: ownerDeviceId 명시 → 부모 방향 상향 탐색
    /// </summary>
    private static string _ResolveOwnerName(ConfigNode tagNode, ConfigTree tree)
    {
        // ownerDeviceId 명시 지정 확인
        var ownerId = tagNode.GetProperty("ownerDeviceId");
        if (!string.IsNullOrEmpty(ownerId))
        {
            var owner = tree.FindById(ownerId);
            if (owner is not null) return owner.Name;
        }

        // 부모 방향 상향 탐색 — Device/Plc 타입 첫 번째 노드
        var parent = tagNode.Parent;
        while (parent is not null)
        {
            if (parent.Type is NodeType.Device or NodeType.Root)
                return parent.Name;
            parent = parent.Parent;
        }

        return "Unknown";
    }

    /// <summary>
    /// Tag 노드로부터 단위(unit)를 조회합니다.
    /// Tag 자체에 없으면 부모 Sensor 노드의 unit을 참조합니다.
    /// </summary>
    private static string _ResolveUnit(ConfigNode tagNode, ConfigTree tree)
    {
        var unit = tagNode.GetProperty("unit");
        if (!string.IsNullOrEmpty(unit)) return unit;

        // 형제 Sensor 노드 또는 부모 scaleConfigId 에서 unit 참조 (간략 처리)
        var parent = tagNode.Parent;
        if (parent is not null)
        {
            foreach (var sibling in parent.Children)
            {
                if (sibling.Type == NodeType.Sensor)
                {
                    var sensorUnit = sibling.GetProperty("unit");
                    if (!string.IsNullOrEmpty(sensorUnit)) return sensorUnit;
                }
            }
        }

        return string.Empty;
    }

    // §7 ─ 시뮬레이터 기본 태그 (device.json 없을 때 폴백) ────
    private void _CreateDefaultTags()
    {
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

    // §8 ─ 드라이버 팩토리 (Phase 9: CommType 기반 자동 선택) ──
    private async Task _ConnectDriversAsync()
    {
        var configPath = Path.Combine(_configDir, "device.json");

        if (!File.Exists(configPath))
        {
            await _ConnectVirtualDriversAsync();
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var tree = new ConfigTree();
            tree.FromJson(json);

            // Device 노드 탐색 → 드라이버 생성
            var deviceNodes = tree.FindAll(NodeType.Device).ToList();

            if (deviceNodes.Count == 0)
            {
                LogManager.Instance.Warn(LogSrc,
                    "device.json에 Device 노드 없음 — VirtualDriver 사용");
                await _ConnectVirtualDriversAsync();
                return;
            }

            foreach (var device in deviceNodes)
            {
                var driverName = device.Name;

                // 이미 등록된 드라이버 스킵
                if (_drivers.ContainsKey(driverName)) continue;

                var protocol = device.GetProperty("protocol") ?? string.Empty;
                var driver   = _CreateDriver(device, protocol, driverName);
                var connected = await driver.ConnectAsync();

                if (connected)
                {
                    _drivers[driverName] = driver;
                    LogManager.Instance.Info(LogSrc,
                        $"[{driverName}] {protocol} 드라이버 연결 성공");
                }
                else
                {
                    // 연결 실패 → VirtualDriver 폴백 (수집 중단 방지)
                    LogManager.Instance.Warn(LogSrc,
                        $"[{driverName}] 연결 실패 → VirtualDriver 대체");
                    await driver.DisposeAsync();

                    var vd = new VirtualDriver(driverName);
                    await vd.ConnectAsync();
                    _drivers[driverName] = vd;
                }
            }

            // device.json에 정의됐지만 Device 노드가 없는 태그들의
            // DeviceName 에 해당하는 드라이버가 없으면 VirtualDriver 추가
            await _EnsureDriversForAllTags();

            LogManager.Instance.Info(LogSrc,
                $"드라이버 연결 완료 — {_drivers.Count}개 (Real:{_drivers.Values.Count(d => d is not VirtualDriver)}, Virtual:{_drivers.Values.Count(d => d is VirtualDriver)})");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error(LogSrc,
                $"드라이버 팩토리 오류: {ex.Message} — 전체 VirtualDriver 전환");
            foreach (var d in _drivers.Values) await d.DisposeAsync();
            _drivers.Clear();
            await _ConnectVirtualDriversAsync();
        }
    }

    /// <summary>
    /// CommType(protocol) 문자열 기반 드라이버 생성.
    /// 지원: ModbusTcp / ModbusRtu / Virtual(명시) / 기타(→ VirtualDriver 폴백)
    /// </summary>
    private IProtocolDriver _CreateDriver(ConfigNode device, string protocol, string driverName)
    {
        return protocol.ToLowerInvariant() switch
        {
            "modbus tcp" or "modbustcp" or "modbus tcp/ip" =>
                _CreateModbusTcpDriver(device, driverName),

            "modbus rtu" or "modbusrtu" or "modbus serial" =>
                _CreateModbusRtuDriver(device, driverName),

            "virtual" or "simulation" or "simulator" =>
                new VirtualDriver(driverName),

            _ =>
                _CreateFallbackDriver(device, driverName, protocol),
        };
    }

    private IProtocolDriver _CreateModbusTcpDriver(ConfigNode device, string driverName)
    {
        var host = device.GetProperty("ip")
                ?? device.GetProperty("host")
                ?? device.GetProperty("ipAddress")
                ?? "127.0.0.1";

        var portStr = device.GetProperty("port") ?? "502";
        int.TryParse(portStr, out int port);
        if (port <= 0) port = 502;

        var unitIdStr = device.GetProperty("unitId")
                     ?? device.GetProperty("slaveId")
                     ?? "1";
        byte.TryParse(unitIdStr, out byte unitId);
        if (unitId == 0) unitId = 1;

        var timeoutStr = device.GetProperty("timeout")
                      ?? device.GetProperty("timeoutMs")
                      ?? "3000";
        int.TryParse(timeoutStr, out int timeout);
        if (timeout <= 0) timeout = 3000;

        LogManager.Instance.Debug(LogSrc,
            $"[{driverName}] ModbusTCP 생성 → {host}:{port} UnitId={unitId}");

        return new ModbusTcpDriver(new ModbusTcpConfig(
            DriverId  : driverName,
            Host      : host,
            Port      : port,
            UnitId    : unitId,
            TimeoutMs : timeout));
    }

    private IProtocolDriver _CreateModbusRtuDriver(ConfigNode device, string driverName)
    {
        var portName = device.GetProperty("portName")
                    ?? device.GetProperty("comPort")
                    ?? device.GetProperty("serialPort")
                    ?? "COM1";

        var baudStr = device.GetProperty("baudRate") ?? "9600";
        int.TryParse(baudStr, out int baud);
        if (baud <= 0) baud = 9600;

        var unitIdStr = device.GetProperty("unitId")
                     ?? device.GetProperty("slaveId")
                     ?? "1";
        byte.TryParse(unitIdStr, out byte unitId);
        if (unitId == 0) unitId = 1;

        LogManager.Instance.Debug(LogSrc,
            $"[{driverName}] ModbusRTU 생성 → {portName} {baud}bps UnitId={unitId}");

        return new ModbusRtuDriver(new ModbusRtuConfig(
            DriverId : driverName,
            PortName : portName,
            BaudRate : baud,
            UnitId   : unitId));
    }

    private IProtocolDriver _CreateFallbackDriver(
        ConfigNode device, string driverName, string protocol)
    {
        // OPC-UA, MQTT 등 미구현 프로토콜 → VirtualDriver 폴백
        LogManager.Instance.Warn(LogSrc,
            $"[{driverName}] 미지원 프로토콜 '{protocol}' → VirtualDriver 대체");
        return new VirtualDriver(driverName);
    }

    /// <summary>
    /// LiveTags 전체를 순회하여 DeviceName에 해당하는 드라이버가 없으면
    /// VirtualDriver를 자동 생성합니다.
    /// </summary>
    private async Task _EnsureDriversForAllTags()
    {
        var missingDevices = LiveTags
            .Select(t => t.DeviceName)
            .Distinct()
            .Where(name => !_drivers.ContainsKey(name))
            .ToList();

        foreach (var name in missingDevices)
        {
            var vd = new VirtualDriver(name);
            await vd.ConnectAsync();
            _drivers[name] = vd;
            LogManager.Instance.Info(LogSrc,
                $"[{name}] 드라이버 미정의 → VirtualDriver 자동 생성");
        }
    }

    private async Task _ConnectVirtualDriversAsync()
    {
        var deviceNames = LiveTags
            .Select(t => t.DeviceName)
            .Distinct()
            .ToList();

        foreach (var name in deviceNames)
        {
            if (_drivers.ContainsKey(name)) continue;
            var vd = new VirtualDriver(name);
            await vd.ConnectAsync();
            _drivers[name] = vd;
        }

        LogManager.Instance.Info(LogSrc,
            $"VirtualDriver {_drivers.Count}개 연결");
    }

    // §9 ─ AsyncScheduler 폴링 등록 ────────────────────────────
    private void _RegisterPollingSchedules(CancellationToken ct)
    {
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

            var pollTask = AsyncScheduler.Instance.ScheduleRecurring(
                TimeSpan.FromMilliseconds(1000),
                pollCt => _PollDeviceAsync(driver, tags, addressDefs, pollCt),
                name: $"poll-{deviceName}");

            _pollTasks.Add(pollTask);
            LogManager.Instance.Info(LogSrc,
                $"폴링 등록: {deviceName} ({tags.Count}개 태그)");
        }
    }

    // §10 ─ 폴링 실행 (AsyncScheduler 콜백) ───────────────────
    private async Task _PollDeviceAsync(
        IProtocolDriver      driver,
        List<LiveTagValue>   tags,
        List<TagAddressDef>  addressDefs,
        CancellationToken    ct)
    {
        try
        {
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
                        tag.UpdateValue(val, TagQuality.Good, now);

                        var compressor = _compressors.GetOrCreate(tag.TagId);
                        if (compressor.ShouldStore(val, now))
                        {
                            var record = new TagHistoryRecord(
                                tag.TagId, now, val, tag.Quality.ToString());

                            _db?.Enqueue(record);
                            _db?.UpsertLatest(record);
                        }

                        EventBus.Instance.Publish(
                            new TagValueUpdatedEvent(tag.TagId, val, TagQuality.Good));
                    }
                    else
                    {
                        tag.UpdateValue(0, TagQuality.Bad, now);
                        ErrorCount++;

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

    // §11 ─ TPS 계산 ──────────────────────────────────────────
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

    // §12 ─ IAsyncDisposable ───────────────────────────────────
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (IsRunning) await StopAsync();
        if (_db is not null) await _db.DisposeAsync();
        _cts?.Dispose();
        _disposed = true;
    }
}

// ── lssLib EventBus 이벤트 ────────────────────────────────
public sealed record EngineStateChangedEvent(string NewState)  : EventMessage;
public sealed record TagValueUpdatedEvent(
    string     TagId,
    double     Value,
    TagQuality Quality) : EventMessage;
