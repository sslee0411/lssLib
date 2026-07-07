// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Config/ConfigReloadWatcher.cs
//  역할: Studio 저장 시 발행되는 .signal 파일을 FileSystemWatcher 로 감지
//        → FlowEngine/AlarmStateManager/DataCollectionService/StatusViewModel 재시작
//
//  ★ Studio signal 파일 경로:
//    {Studio실행파일}\Config\device.json.signal
//
//  ★ 공유 Config 폴더 설정 (settings.json):
//    Storage.WatchPath 를 Studio 의 Config 폴더 경로로 지정하면
//    Studio 저장 즉시 Collector 가 자동으로 새 설정을 반영한다.
//    미설정 시 Collector 자신의 Config 폴더를 감시한다.
//
//  ★ 재시작 순서:
//    ① DataCollectionService.DisposeAsync() — 저장 서비스 중단 (배치 Flush)
//    ② FlowEngine.StopAsync()               — 폴링 중단
//    ③ CollectorConfigLoader.LoadAsync()    — 새 device.json 로드
//    ④ AlarmStateManager.Initialize()       — 알람 감지기 재구성
//    ⑤ StatusViewModel.Initialize()         — LiveTag 목록 재구성
//    ⑤B DeviceInstanceService.Initialize()  — DeviceInstance 트리 재조립 (C-EX-01)
//    ⑤C AnomalyFilterService.Initialize()   — 이상값 필터 재구성 (C-16, 신규)
//    ⑤D VirtualTagEngine.Initialize()       — 가상 Tag 목록 재구성 (C-18, 신규)
//    ⑥ DataCollectionService.Initialize()  — SDT + 저장 서비스 재시작
//    ⑦ FlowEngine.StartAsync()             — 폴링 재시작
//
//  C-08: 신규
//  수정: 2026-07-06 — AnomalyFilterService/VirtualTagEngine 재시작 연동 추가
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Engine;
using IIoT.Collector.Storage;
using IIoT.Collector.ViewModels;
using lssLib.Log;
using System.IO;

namespace IIoT.Collector.Core.Config;

/// <summary>
/// .signal 파일 감지 → 설정 자동 재로드 서비스 (DI 싱글턴).
/// <para>
/// Studio 에서 [저장] 시 <c>device.json.signal</c> 파일이 생성된다.
/// 이 파일을 <see cref="FileSystemWatcher"/> 로 감지하여
/// 수집 파이프라인 전체를 무중단에 가깝게 재시작한다.
/// </para>
/// <para>
/// 재시작 소요 시간: 약 1~3초 (PLC 연결 재시도 시간 포함).
/// 재시작 중에는 수집·저장이 일시 중단되지만 데이터 유실은 없다.
/// (CommandQueue 에 대기 중인 쓰기는 Flush 후 재시작)
/// </para>
/// </summary>
public sealed class ConfigReloadWatcher : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader   _configLoader;
    private readonly FlowEngine              _flowEngine;
    private readonly AlarmStateManager       _alarmManager;
    private readonly StatusViewModel         _statusViewModel;
    private readonly AlarmViewModel          _alarmViewModel;
    private readonly DataCollectionService   _dataService;
    private readonly DeviceInstanceService   _deviceInstanceService;   // ★ C-EX-01 신규
    private readonly AnomalyFilterService    _anomalyFilter;           // ★ C-16 신규 (재시작 연동)
    private readonly VirtualTagEngine        _virtualTagEngine;        // ★ C-18 신규 (재시작 연동)

    private FileSystemWatcher? _watcher;

    /// <summary>재시작 중복 방지 플래그</summary>
    private int _isRestarting; // 0=idle, 1=restarting (Interlocked)

    // §2 ─ 생성자 ──────────────────────────────────────────

    public ConfigReloadWatcher(
        CollectorConfigLoader  configLoader,
        FlowEngine             flowEngine,
        AlarmStateManager      alarmManager,
        StatusViewModel        statusViewModel,
        AlarmViewModel         alarmViewModel,
        DataCollectionService  dataService,
        DeviceInstanceService  deviceInstanceService,   // ★ C-EX-01 신규
        AnomalyFilterService   anomalyFilter,           // ★ 신규
        VirtualTagEngine       virtualTagEngine)        // ★ 신규
    {
        _configLoader           = configLoader;
        _flowEngine             = flowEngine;
        _alarmManager           = alarmManager;
        _statusViewModel        = statusViewModel;
        _alarmViewModel         = alarmViewModel;
        _dataService            = dataService;
        _deviceInstanceService  = deviceInstanceService;   // ★ C-EX-01 신규
        _anomalyFilter          = anomalyFilter;           // ★ 신규
        _virtualTagEngine       = virtualTagEngine;        // ★ 신규
    }

    // §3 ─ 감시 시작 ───────────────────────────────────────

    /// <summary>
    /// .signal 파일 감시를 시작합니다.
    /// App.xaml.cs 에서 FlowEngine.StartAsync() 이후 호출.
    /// </summary>
    /// <param name="watchPath">
    /// 감시할 폴더 경로.
    /// null 이면 CollectorConfigLoader.DeviceJsonPath 의 부모 폴더를 사용.
    /// Studio 와 Config 폴더를 공유하려면 Studio 실행파일의 Config 폴더 경로를 지정.
    /// </param>
    public void Start(string? watchPath = null)
    {
        var dir = watchPath
            ?? Path.GetDirectoryName(CollectorConfigLoader.DeviceJsonPath)
            ?? AppDomain.CurrentDomain.BaseDirectory;

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            LogManager.Instance.Warn("FSW",
                $"감시 폴더 생성: {dir}");
        }

        _watcher = new FileSystemWatcher(dir, "*.signal")
        {
            NotifyFilter      = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += _OnSignalCreated;
        _watcher.Changed += _OnSignalCreated; // 덮어쓰기 감지용

        LogManager.Instance.Info("FSW",
            $"설정 변경 감시 시작: {dir}\\*.signal");
    }

    // §4 ─ signal 파일 감지 핸들러 ─────────────────────────

    private void _OnSignalCreated(object sender, FileSystemEventArgs e)
    {
        // 재시작 중복 방지 (다중 이벤트 발화 가능성)
        if (Interlocked.CompareExchange(ref _isRestarting, 1, 0) != 0)
            return;

        LogManager.Instance.Info("FSW",
            $"설정 변경 감지: {e.Name} → 재시작 준비");

        // signal 파일 즉시 삭제 (다음 감지 방지)
        try { File.Delete(e.FullPath); } catch { /* 삭제 실패 무시 */ }

        // 비동기 재시작 (FSW 핸들러에서 await 불가 → Task.Run)
        _ = Task.Run(_RestartAsync);
    }

    // §5 ─ 재시작 순서 ─────────────────────────────────────

    private async Task _RestartAsync()
    {
        try
        {
            LogManager.Instance.Info("FSW", "수집 파이프라인 재시작 시작...");

            // ① 저장 서비스 중단 (배치 Flush 포함)
            await _dataService.DisposeAsync();

            // ② 폴링 중단
            await _flowEngine.StopAsync();

            // ③ 새 device.json 로드 (1초 대기 — 파일 쓰기 완료 보장)
            await Task.Delay(1000);
            await _configLoader.LoadAsync();

            // ④ 알람 감지기 재구성
            _alarmManager.Initialize();

            // ⑤ LiveTag 목록 재구성 (UI 스레드 전환 불필요 — BindingOperations 동기화됨)
            _statusViewModel.Initialize();
            _alarmViewModel.Initialize();

            // ⑤B ★ C-EX-01: DeviceInstance 트리 재조립 (새 device.json 기준)
            _deviceInstanceService.Initialize();

            // ⑤C ★ C-16 신규: 이상값 필터 재구성 (새 스케일 범위 기준으로 재계산)
            _anomalyFilter.Initialize();

            // ⑤D ★ C-18 신규: 가상 Tag 목록 재구성
            _virtualTagEngine.Initialize();

            // ⑥ SDT + 저장 서비스 재시작
            _dataService.Initialize();

            // ⑦ 폴링 재시작
            await _flowEngine.StartAsync();

            LogManager.Instance.Info("FSW",
                $"재시작 완료 — {_configLoader.TotalTagCount}개 Tag 수집 재개");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("FSW", $"재시작 실패: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isRestarting, 0);
        }
    }

    // §6 ─ 리소스 해제 ─────────────────────────────────────

    public ValueTask DisposeAsync()
    {
        _watcher?.Dispose();
        LogManager.Instance.Info("FSW", "설정 변경 감시 종료");
        return ValueTask.CompletedTask;
    }
}
