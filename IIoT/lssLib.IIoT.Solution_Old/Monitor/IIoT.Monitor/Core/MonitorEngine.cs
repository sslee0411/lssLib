// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/MonitorEngine.cs
//  역할: 모니터링 엔진
//        · EventBus 구독 (TagValueUpdatedEvent)
//        · AbstractDetector 목록 관리 + 비동기 실행
//        · AlarmStateManager 연동
//        · device.json / alarm.json 로드
//  Phase 10: 신규
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using lssLib.Config.Tree;
using lssLib.Log;
using lssLib.Messaging;
using System.Collections.Concurrent;
using System.IO;

namespace IIoT.Monitor.Core;

public enum MonitorState { Stopped, Running, Error }

public sealed partial class MonitorEngine : ObservableObject, IAsyncDisposable
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc = "MonitorEngine";

    private readonly string              _configDir;
    private readonly AlarmStateManager   _alarmMgr;

    // 감지기 레지스트리 (DetectorId → AbstractDetector)
    private readonly ConcurrentDictionary<string, AbstractDetector> _detectors = new();

    // EventBus 구독 핸들 (IDisposable)
    private IDisposable? _tagSub;
    private IDisposable? _alarmFiredSub;

    private bool _disposed;

    // §2 ─ 바인딩 프로퍼티 ────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private MonitorState _state = MonitorState.Stopped;

    public bool   IsRunning => State == MonitorState.Running;
    public string StateText => State switch
    {
        MonitorState.Running => "모니터링 중",
        MonitorState.Error   => "오류",
        _                    => "중지됨",
    };

    [ObservableProperty] private string _configInfo = "설정 미로드";
    [ObservableProperty] private int    _detectorCount;
    [ObservableProperty] private int    _processedCount;

    /// <summary>알람 상태 관리자 (UI 바인딩)</summary>
    public AlarmStateManager AlarmManager => _alarmMgr;

    // §3 ─ 생성자 ─────────────────────────────────────────────
    public MonitorEngine(string configDir)
    {
        _configDir = configDir;
        _alarmMgr  = new AlarmStateManager();
    }

    // §4 ─ 엔진 제어 ──────────────────────────────────────────

    /// <summary>모니터링 시작 — 설정 로드 → 감지기 등록 → EventBus 구독</summary>
    public async Task StartAsync()
    {
        if (State == MonitorState.Running) return;
        LogManager.Instance.Info(LogSrc, "모니터 엔진 시작");

        try
        {
            await _LoadDetectorsAsync();

            // TagValueUpdatedEvent 구독 (CollectorRuntime 발행)
            _tagSub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(
                async e => await _OnTagValueAsync(e));

            // AlarmFiredEvent 구독 (AbstractDetector 발행)
            _alarmFiredSub = EventBus.Instance.Subscribe<AlarmFiredEvent>(
                async e => await _alarmMgr.FireAsync(e.Alarm));

            State = MonitorState.Running;
            LogManager.Instance.Info(LogSrc,
                $"모니터 시작 완료 — {_detectors.Count}개 감지기");
        }
        catch (Exception ex)
        {
            State = MonitorState.Error;
            LogManager.Instance.Error(LogSrc, $"시작 실패: {ex.Message}");
        }
    }

    /// <summary>모니터링 중지</summary>
    public async Task StopAsync()
    {
        if (State == MonitorState.Stopped) return;
        LogManager.Instance.Info(LogSrc, "모니터 엔진 중지");

        _tagSub?.Dispose();
        _alarmFiredSub?.Dispose();
        _tagSub = null;
        _alarmFiredSub = null;

        await Task.Delay(100); // 진행 중 처리 완료 대기
        State = MonitorState.Stopped;
        LogManager.Instance.Info(LogSrc, "모니터 엔진 중지 완료");
    }

    /// <summary>재시작 (설정 변경 시)</summary>
    public async Task RestartAsync()
    {
        await StopAsync();
        _detectors.Clear();
        await Task.Delay(300);
        await StartAsync();
    }

    // §5 ─ 감지기 관리 ────────────────────────────────────────

    /// <summary>감지기를 동적으로 추가합니다.</summary>
    public void AddDetector(AbstractDetector detector)
    {
        _detectors[detector.DetectorId] = detector;
        DetectorCount = _detectors.Count;
        LogManager.Instance.Info(LogSrc,
            $"감지기 추가: {detector.DetectorId} → {detector.TargetTagId}");
    }

    /// <summary>감지기를 제거합니다.</summary>
    public void RemoveDetector(string detectorId)
    {
        if (_detectors.TryRemove(detectorId, out var d))
        {
            d.Dispose();
            DetectorCount = _detectors.Count;
        }
    }

    // §6 ─ 설정 로드 (device.json + alarm.json) ───────────────
    private async Task _LoadDetectorsAsync()
    {
        await Task.Run(() =>
        {
            var devicePath = Path.Combine(_configDir, "device.json");
            var alarmPath  = Path.Combine(_configDir, "alarm.json");

            // alarm.json 없으면 기본 감지기만 등록
            if (!File.Exists(alarmPath))
            {
                LogManager.Instance.Warn(LogSrc,
                    "alarm.json 없음 — 기본 감지기(통신감시)만 등록");
                _LoadDefaultDetectors(devicePath);
                ConfigInfo = $"시뮬레이터 모드 — {_detectors.Count}개 감지기";
                return;
            }

            try
            {
                // device.json → Tag 목록 로드
                var tagNames = new Dictionary<string, string>(); // tagId → tagName
                if (File.Exists(devicePath))
                {
                    var tree = new ConfigTree();
                    tree.FromJson(File.ReadAllText(devicePath));
                    foreach (var node in tree.Flatten().Where(n => n.Type == NodeType.Tag))
                        tagNames[node.Id] = node.Name;
                }

                // alarm.json → AlarmRule 섹션 파싱 → ThresholdDetector 생성
                var alarmJson = File.ReadAllText(alarmPath);
                _ParseAlarmJson(alarmJson, tagNames);

                ConfigInfo = $"alarm.json 로드 — {_detectors.Count}개 감지기";
                LogManager.Instance.Info(LogSrc, ConfigInfo);
            }
            catch (Exception ex)
            {
                LogManager.Instance.Error(LogSrc,
                    $"감지기 로드 오류: {ex.Message} — 기본 감지기 사용");
                _LoadDefaultDetectors(devicePath);
                ConfigInfo = $"파싱 오류 (기본 감지기) — {_detectors.Count}개";
            }

            DetectorCount = _detectors.Count;
        });
    }

    /// <summary>alarm.json 파싱 → ThresholdDetector 자동 생성</summary>
    private void _ParseAlarmJson(string json,
        Dictionary<string, string> tagNames)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json,
            new System.Text.Json.JsonDocumentOptions
            {
                CommentHandling    = System.Text.Json.JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            // "AlarmLibrary:ar-001" 형식 섹션만 처리
            if (!prop.Name.StartsWith("AlarmLibrary:", StringComparison.OrdinalIgnoreCase))
                continue;

            var alarmId  = prop.Name["AlarmLibrary:".Length..];
            var section  = prop.Value;

            // tagId 연결 정보 확인
            var tagId = _GetJsonString(section, "tagId") ?? alarmId;

            var detector = new ThresholdDetector($"thr-{alarmId}", tagId)
            {
                HH       = _GetDouble(section, "hh"),
                H        = _GetDouble(section, "h"),
                L        = _GetDouble(section, "l"),
                LL       = _GetDouble(section, "ll"),
                DeadBand = _GetDouble(section, "deadBand") ?? 0.5,
            };

            AddDetector(detector);

            // Watchdog 감지기도 함께 등록
            var watchdog = new CommunicationWatchdog(
                $"wdg-{alarmId}", tagId)
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            AddDetector(watchdog);
        }
    }

    /// <summary>alarm.json 없을 때 기본 감지기 (device.json Tag마다 Watchdog)</summary>
    private void _LoadDefaultDetectors(string devicePath)
    {
        if (!File.Exists(devicePath)) return;

        try
        {
            var tree = new ConfigTree();
            tree.FromJson(File.ReadAllText(devicePath));

            int i = 0;
            foreach (var node in tree.Flatten().Where(n => n.Type == NodeType.Tag))
            {
                AddDetector(new CommunicationWatchdog(
                    $"wdg-{++i:000}", node.Id)
                {
                    Timeout = TimeSpan.FromSeconds(30),
                });
            }
        }
        catch { /* device.json 없어도 시작 가능 */ }
    }

    // §7 ─ TagValue 수신 처리 ─────────────────────────────────
    private async Task _OnTagValueAsync(TagValueUpdatedEvent e)
    {
        var tagValue = new TagValue(e.TagId, e.Value, DateTime.Now, e.Quality);

        // 해당 TagId를 감시하는 감지기 전부 실행
        var targets = _detectors.Values
            .Where(d => d.TargetTagId == e.TagId)
            .ToList();

        foreach (var detector in targets)
            await detector.ProcessAsync(tagValue, CancellationToken.None);

        if (targets.Count > 0)
        {
            ProcessedCount++;

            // 복귀 처리 — 감지기가 정상이면 AlarmStateManager 복귀
            foreach (var d in targets.Where(d => !d.IsAnomalous))
                await _alarmMgr.ClearByDetectorAsync(d.DetectorId);
        }
    }

    // §8 ─ JSON 파싱 헬퍼 ─────────────────────────────────────
    private static string? _GetJsonString(
        System.Text.Json.JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var p) &&
            p.ValueKind == System.Text.Json.JsonValueKind.String)
            return p.GetString();
        return null;
    }

    private static double? _GetDouble(
        System.Text.Json.JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var p)) return null;
        if (p.ValueKind == System.Text.Json.JsonValueKind.Number &&
            p.TryGetDouble(out var d)) return d;
        if (p.ValueKind == System.Text.Json.JsonValueKind.String &&
            double.TryParse(p.GetString(), out var ds)) return ds;
        return null;
    }

    // §9 ─ IAsyncDisposable ───────────────────────────────────
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (IsRunning) await StopAsync();
        foreach (var d in _detectors.Values) d.Dispose();
        _detectors.Clear();
        _alarmMgr.Dispose();
        _disposed = true;
    }
}
