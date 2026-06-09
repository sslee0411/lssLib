// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Core/CollectionEngine.cs
//  역할: 수집 엔진 — 폴링 루프 + 상태 관리
//        현재: 시뮬레이터 모드 (UI 동작 확인용)
//        Phase 8: IProtocolDriver.ReadAsync() 로 교체 예정
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using lssLib.Log;
using lssLib.Messaging;
using System.Collections.ObjectModel;
using System.IO;

namespace IIoT.CollectorRuntime.Core;

public enum EngineState { Stopped, Starting, Running, Stopping, Error }

public sealed partial class CollectionEngine : ObservableObject, IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────────
    private const string LogSrc = "CollectionEngine";
    private readonly string  _configDir;
    private readonly Random  _rng = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    // §2 ─ 바인딩 프로퍼티 ─────────────────────────────────────
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

    /// <summary>전체 수집 태그 목록 (UI 바인딩)</summary>
    public ObservableCollection<LiveTagValue> LiveTags { get; } = [];

    // §3 ─ 생성자 ──────────────────────────────────────────────
    public CollectionEngine(string configDir) => _configDir = configDir;

    // §4 ─ 수집 제어 ───────────────────────────────────────────
    public async Task StartAsync()
    {
        if (State == EngineState.Running) return;
        State = EngineState.Starting;

        try
        {
            await _LoadTagsAsync();
            _cts = new CancellationTokenSource();
            _ = _RunPollingAsync(_cts.Token);
            _ = _RunTpsAsync(_cts.Token);
            State = EngineState.Running;

            LogManager.Instance.Info(LogSrc, $"수집 시작 — {LiveTags.Count}개 태그");
            EventBus.Instance.Publish(new EngineStateChangedEvent("Started"));
        }
        catch (Exception ex)
        {
            State = EngineState.Error;
            LogManager.Instance.Error(LogSrc, $"시작 실패: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        if (State == EngineState.Stopped) return;
        State = EngineState.Stopping;

        _cts?.Cancel();
        await Task.Delay(300);

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var t in LiveTags) t.Quality = TagQuality.NotCollecting;
        });

        State = EngineState.Stopped;
        LogManager.Instance.Info(LogSrc, "수집 중지");
        EventBus.Instance.Publish(new EngineStateChangedEvent("Stopped"));
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await Task.Delay(500);
        await StartAsync();
    }

    // §5 ─ 설정 로드 ───────────────────────────────────────────
    private async Task _LoadTagsAsync()
    {
        await Task.Run(() =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                LiveTags.Clear());

            var path = Path.Combine(_configDir, "device.json");
            ConfigInfo = File.Exists(path)
                ? $"device.json 로드됨 (시뮬레이터 모드)"
                : "시뮬레이터 모드 (device.json 없음)";

            // ★ Phase 8: device.json → ConfigTree → AbstractNode 계층으로 교체
            _CreateSimTags();
            LogManager.Instance.Info(LogSrc, $"태그 로드 완료 — {LiveTags.Count}개");
        });
    }

    private void _CreateSimTags()
    {
        var defs = new (string id, string name, string addr, string unit, string dev)[]
        {
            ("t001", "압연기#1 베어링온도",  "40001", "°C",   "압연기-001"),
            ("t002", "압연기#1 차압",         "40003", "kPa",  "압연기-001"),
            ("t003", "압연기#1 모터전류",     "40005", "A",    "압연기-001"),
            ("t004", "압연기#1 롤속도",       "40007", "rpm",  "압연기-001"),
            ("t005", "압연기#1 RunStatus",    "M0.0",  "",     "압연기-001"),
            ("t006", "사출기#1 실린더온도",   "40001", "°C",   "사출기-001"),
            ("t007", "사출기#1 사출압력",     "40003", "bar",  "사출기-001"),
            ("t008", "사출기#1 냉각수온",     "40005", "°C",   "사출기-001"),
            ("t009", "냉각탑#1 입구온도",     "40001", "°C",   "냉각탑-001"),
            ("t010", "냉각탑#1 출구온도",     "40003", "°C",   "냉각탑-001"),
            ("t011", "냉각탑#1 유량",         "40005", "m³/h", "냉각탑-001"),
            ("t012", "공압라인 메인압력",     "40001", "bar",  "공압-001"),
        };

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var (id, name, addr, unit, dev) in defs)
                LiveTags.Add(new LiveTagValue
                {
                    TagId = id, TagName = name,
                    Address = addr, Unit = unit, DeviceName = dev
                });
        });
    }

    // §6 ─ 폴링 루프 ───────────────────────────────────────────
    private async Task _RunPollingAsync(CancellationToken ct)
    {
        var tasks = LiveTags.Select(t => _PollLoopAsync(t, ct)).ToList();
        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { }
    }

    private async Task _PollLoopAsync(LiveTagValue tag, CancellationToken ct)
    {
        // 시작 분산
        await Task.Delay(_rng.Next(0, 400), ct);

        int intervalMs = tag.TagId == "t005" ? 200 : 1000;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _SimPollAsync(tag, ct);
                TotalPolled++;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                ErrorCount++;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => tag.Quality = TagQuality.Bad);
                LogManager.Instance.Warn(LogSrc, $"폴링 오류 [{tag.TagName}]: {ex.Message}");
            }
            await Task.Delay(intervalMs, ct);
        }
    }

    private async Task _SimPollAsync(LiveTagValue tag, CancellationToken ct)
    {
        await Task.Delay(5, ct); // I/O 시뮬레이션

        // 태그별 시뮬레이션 파라미터 (기준값, 노이즈, 통신장애 확률)
        var (baseVal, noise, failP) = tag.TagId switch
        {
            "t001" => (150.0, 5.0,   0.01),
            "t002" => (30.0,  2.0,   0.005),
            "t003" => (28.5,  1.5,   0.02),
            "t004" => (1842.0,50.0,  0.01),
            "t005" => (1.0,   0.0,   0.05),
            "t006" => (220.0, 8.0,   0.01),
            "t007" => (120.0, 10.0,  0.02),
            "t008" => (32.0,  1.0,   0.005),
            "t009" => (28.0,  1.0,   0.01),
            "t010" => (35.0,  1.5,   0.01),
            "t011" => (42.5,  3.0,   0.02),
            "t012" => (6.8,   0.3,   0.01),
            _      => (100.0, 5.0,   0.01),
        };

        // 통신 장애 시뮬레이션
        if (_rng.NextDouble() < failP)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => tag.UpdateValue(0, TagQuality.Bad));
            return;
        }

        double val = tag.TagId == "t005"
            ? (_rng.NextDouble() > 0.1 ? 1 : 0)
            : Math.Round(baseVal + (_rng.NextDouble() - 0.5) * noise * 2, 3);

        // 알람 판정
        string alarm = tag.TagId switch
        {
            "t001" when val > 160 => "HH",
            "t001" when val > 155 => "H",
            "t001" when val < 10  => "LL",
            "t003" when val > 32  => "H",
            _                     => string.Empty,
        };

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            tag.UpdateValue(val, TagQuality.Good);
            tag.AlarmLevel = alarm;
        });

        EventBus.Instance.Publish(new TagValueUpdatedEvent(tag.TagId, val, TagQuality.Good));
    }

    // §7 ─ TPS 계산 ────────────────────────────────────────────
    private async Task _RunTpsAsync(CancellationToken ct)
    {
        int prev = 0;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
            var cur = TotalPolled;
            Tps = cur - prev;
            prev = cur;
        }
    }

    // §8 ─ IDisposable ─────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _cts?.Cancel();
        _cts?.Dispose();
        _disposed = true;
    }
}

// ── 이벤트 ────────────────────────────────────────────────
public record EngineStateChangedEvent(string NewState);
public record TagValueUpdatedEvent(string TagId, double Value, TagQuality Quality);
