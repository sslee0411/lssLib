// ══════════════════════════════════════════════════════════
//  IIoT.Collector · ViewModels/DeviceTreeViewModel.cs
//  역할: [장비] 탭 ViewModel — DeviceInstanceService 를 1초 주기로 스냅샷하여
//        DeviceTreeNodeViewModel 트리로 재구성, 검색어 필터링 지원
//  C-EX-01-6: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Engine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Threading;

namespace IIoT.Collector.ViewModels;

/// <summary>
/// [장비] 탭 ViewModel (DI 싱글턴).
/// </summary>
public partial class DeviceTreeViewModel : ObservableObject, IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly DeviceInstanceService   _deviceInstanceService;
    private readonly CollectorSettingsLoader _settingsLoader;
    private DispatcherTimer?                 _refreshTimer;

    // §2 ─ 바인딩 ──────────────────────────────────────────

    public ObservableCollection<DeviceTreeNodeViewModel> Devices { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>
    /// 전역(공통) 설정 요약 — 데이터 압축(SDT)/이상값 필터/강제쓰기/알림/가상Tag
    /// 활성화 여부를 한 줄로 표시 (요청사항: "공통된 것 모두 다 표시").
    /// settings.json 은 재시작 없이는 바뀌지 않으므로 Initialize() 시 1회만 계산.
    /// </summary>
    [ObservableProperty]
    private string _globalSettingsText = string.Empty;

    // §3 ─ 생성자 ──────────────────────────────────────────

    public DeviceTreeViewModel(
        DeviceInstanceService   deviceInstanceService,
        CollectorSettingsLoader settingsLoader)
    {
        _deviceInstanceService = deviceInstanceService;
        _settingsLoader        = settingsLoader;
        BindingOperations.EnableCollectionSynchronization(Devices, new object());
    }

    // §4 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// 1초 주기 스냅샷 갱신을 시작합니다.
    /// App.xaml.cs 에서 DeviceInstanceService.Initialize() 이후 호출.
    /// </summary>
    public void Initialize()
    {
        _BuildGlobalSettingsText();
        _Refresh();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += (_, _) => _Refresh();
        _refreshTimer.Start();
    }

    // §4B ─ 전역 설정 요약 ─────────────────────────────────

    private void _BuildGlobalSettingsText()
    {
        var s = _settingsLoader.Settings;
        var parts = new List<string>
        {
            $"압축(SDT) {s.Storage.SdtExcDevPercent}%",
            s.Filter.SpikeFilterEnabled
                ? $"스파이크필터 {s.Filter.SpikeMaxDeltaPercent}%"
                : "스파이크필터 비활성",
            s.Filter.DeadbandEnabled
                ? $"데드밴드 {s.Filter.DeadbandPercent}%"
                : "데드밴드 비활성",
            s.ForceWrite.Enabled ? "강제쓰기 활성" : "강제쓰기 비활성",
            s.Notification.Enabled ? "알림 활성" : "알림 비활성",
            s.VirtualTag.Enabled ? "가상Tag 활성" : "가상Tag 비활성",
        };

        GlobalSettingsText = string.Join("  ·  ", parts);
    }

    // §5 ─ 검색어 변경 시 즉시 재조회 ──────────────────────

    partial void OnSearchTextChanged(string value) => _Refresh();

    // §6 ─ 스냅샷 갱신 (버그 수정: Clear+재생성 → diff 갱신) ─

    /// <summary>
    /// ★ 버그 수정 (2026-07-06): 기존에는 매 주기마다 Devices.Clear() 후
    /// 전부 새로 생성했는데, 이 경우 사용자가 접은 TreeViewItem 이
    /// 새 객체로 교체되며 매번 다시 펼쳐지는 문제가 있었다.
    /// 이제 PlcId 기준으로 diff 하여, 기존 항목은 Apply() 로 값만 갱신하고
    /// 삭제/추가된 항목만 컬렉션에서 제거/삽입한다 (객체 동일성 유지).
    /// </summary>
    private void _Refresh()
    {
        var source = string.IsNullOrWhiteSpace(SearchText)
            ? _deviceInstanceService.GetAll()
            : _deviceInstanceService.Search(SearchText);

        var latest   = source.OrderBy(d => d.Name).ToList();
        var latestIds = latest.Select(d => d.PlcId).ToHashSet();

        // ── 더 이상 존재하지 않는 Device 제거 ──
        for (var i = Devices.Count - 1; i >= 0; i--)
        {
            if (!latestIds.Contains(Devices[i].PlcId))
                Devices.RemoveAt(i);
        }

        // ── 추가/갱신 (순서 유지) ──
        for (var i = 0; i < latest.Count; i++)
        {
            var d        = latest[i];
            var existing = Devices.FirstOrDefault(x => x.PlcId == d.PlcId);

            if (existing is null)
                Devices.Insert(Math.Min(i, Devices.Count), new DeviceTreeNodeViewModel(d));
            else
                existing.Apply(d);
        }

        var alarmCount = _deviceInstanceService.GetAlarmedTags().Count;
        SummaryText = $"Device {Devices.Count}개 · 활성 알람 {alarmCount}건";
    }

    // §7 ─ 정리 ────────────────────────────────────────────

    public void Dispose() => _refreshTimer?.Stop();
}
