// ══════════════════════════════════════════════════════════
//  IIoT.Collector · ViewModels/StatusViewModel.cs
//  역할: 수집 현황 탭(StatusView) ViewModel
//        EventBus 구독 → LiveTags 컬렉션 갱신 + 통계 집계
//        CollectorConfigLoader.Plcs 기준으로 초기 행 생성 (값 미수신 상태로 시작)
//  C-04: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Events;
using IIoT.Collector.Models;
using IIoT.Contracts;
using lssLib.Messaging;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;

namespace IIoT.Collector.ViewModels;

/// <summary>
/// 수집 현황 ViewModel (DI 싱글턴).
/// <para>
/// CollectorConfigLoader 가 로드한 Plc/Tag 구조로 LiveTags 초기 행을 만들고,
/// EventBus 의 TagValueUpdatedEvent / PlcConnectionChangedEvent 를 구독하여
/// 실시간으로 값과 통계를 갱신한다.
/// </para>
/// <para>
/// MainWindow.Loaded 이후(즉 CollectorConfigLoader.LoadAsync 완료 후) 명시적으로
/// <see cref="InitializeAsync"/> 를 호출해야 LiveTags 가 채워진다.
/// </para>
/// </summary>
public partial class StatusViewModel : ObservableObject, IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConfigLoader _configLoader;

    private IDisposable? _tagValueSub;
    private IDisposable? _connectionSub;

    /// <summary>TagId → LiveTagViewModel 빠른 조회용 (EventBus 수신 시 O(1) 매핑)</summary>
    private readonly Dictionary<string, LiveTagViewModel> _tagIndex = new();

    /// <summary>PlcId → 표시 이름 (PlcConnectionChangedEvent 로그·카드 표시용)</summary>
    private readonly Dictionary<string, string> _plcNames = new();

    // §2 ─ 바인딩 컬렉션 ───────────────────────────────────

    /// <summary>수집 현황 DataGrid ItemsSource</summary>
    public ObservableCollection<LiveTagViewModel> LiveTags { get; } = new();

    // §3 ─ 통계 프로퍼티 ───────────────────────────────────

    [ObservableProperty] private int _totalTagCount;
    [ObservableProperty] private int _goodCount;
    [ObservableProperty] private int _badCount;
    [ObservableProperty] private int _connectedPlcCount;
    [ObservableProperty] private int _totalPlcCount;

    // §4 ─ 생성자 ──────────────────────────────────────────

    public StatusViewModel(CollectorConfigLoader configLoader)
    {
        _configLoader = configLoader;

        // ★ DataGrid 가 어느 스레드에서 만들어지든 안전하게 갱신되도록
        //   컬렉션 변경을 UI 스레드와 동기화 (다중 스레드 Insert 방지)
        BindingOperations.EnableCollectionSynchronization(LiveTags, new object());
    }

    // §5 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// CollectorConfigLoader.Plcs 기준으로 LiveTags 초기 행을 구성하고
    /// EventBus 구독을 시작합니다. App.xaml.cs 에서 ConfigLoader.LoadAsync 직후 호출.
    /// </summary>
    public void Initialize()
    {
        LiveTags.Clear();
        _tagIndex.Clear();
        _plcNames.Clear();

        foreach (var plc in _configLoader.Plcs)
        {
            _plcNames[plc.PlcId] = plc.Name;

            foreach (var tag in plc.Tags)
            {
                var vm = new LiveTagViewModel(tag.Id, tag.Name, plc.PlcId, plc.Name, tag.Unit);
                LiveTags.Add(vm);
                _tagIndex[tag.Id] = vm;
            }
        }

        TotalTagCount = LiveTags.Count;
        TotalPlcCount = _configLoader.Plcs.Count;
        _RecountQuality();

        // 중복 구독 방지 (재호출 대비)
        _tagValueSub?.Dispose();
        _connectionSub?.Dispose();

        _tagValueSub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(_OnTagValueUpdated);
        _connectionSub = EventBus.Instance.Subscribe<PlcConnectionChangedEvent>(_OnPlcConnectionChanged);
    }

    // §6 ─ EventBus 핸들러 ─────────────────────────────────

    /// <summary>
    /// TagValueUpdatedEvent 수신 — 반드시 UI 스레드로 전환 후 LiveTagViewModel.Update() 호출.
    /// </summary>
    private void _OnTagValueUpdated(TagValueUpdatedEvent e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (_tagIndex.TryGetValue(e.Value.TagId, out var vm))
            {
                vm.Update(e.Value);
                _RecountQuality();
            }
        });
    }

    /// <summary>
    /// PlcConnectionChangedEvent 수신 — 연결된 PLC 수 갱신 (드라이버 상태 카드 용도).
    /// C-04 단계에서는 카운트만 갱신, PLC별 상세 표시는 C-13 에서 강화 예정.
    /// </summary>
    private readonly HashSet<string> _connectedPlcIds = new();

    private void _OnPlcConnectionChanged(PlcConnectionChangedEvent e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (e.IsConnected)
                _connectedPlcIds.Add(e.PlcId);
            else
                _connectedPlcIds.Remove(e.PlcId);

            ConnectedPlcCount = _connectedPlcIds.Count;
        });
    }

    // §7 ─ 통계 재계산 ─────────────────────────────────────

    private void _RecountQuality()
    {
        GoodCount = LiveTags.Count(t => t.Quality == TagQuality.Good);
        BadCount  = LiveTags.Count(t =>
            t.Quality is TagQuality.Bad or TagQuality.Timeout or TagQuality.Disconnected);
    }

    // §8 ─ 정리 ────────────────────────────────────────────

    public void Dispose()
    {
        _tagValueSub?.Dispose();
        _connectionSub?.Dispose();
    }
}
