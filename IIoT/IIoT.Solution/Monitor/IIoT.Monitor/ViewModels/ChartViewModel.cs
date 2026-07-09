// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/ChartViewModel.cs
//  역할: [차트] 탭 ViewModel
//        Collector → PLC → Tag 순서로 필터링하여 선택된 Tag 1개의
//        실시간 값 변화를 OxyPlot 라인 차트로 표시한다(롤링 윈도우 300포인트).
//  MN-06: 신규
//  생성: 2026-07-08
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace IIoT.Monitor.ViewModels;

/// <summary>[차트] 탭의 ViewModel.</summary>
public partial class ChartViewModel : ObservableObject
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly LiveTagAggregator _tagAggregator;

    private LiveTagRow? _subscribedTag;
    private LineSeries? _series;

    /// <summary>최대 보관 포인트 수 (롤링 윈도우 — 오래된 값은 자동 제거)</summary>
    private const int MaxPoints = 300;

    // §2 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>등록된 Collector 목록 (필터 콤보박스용, CollectorManageViewModel과 동일 참조)</summary>
    public ObservableCollection<CollectorEndpoint> Collectors { get; }

    /// <summary>Collector 필터: null 이면 전체</summary>
    [ObservableProperty]
    private CollectorEndpoint? _selectedCollector;

    /// <summary>PLC 필터 목록(선택된 Collector 기준으로 동적 구성)</summary>
    public ObservableCollection<string> AvailablePlcIds { get; } = new();

    [ObservableProperty]
    private string? _selectedPlcId;

    /// <summary>Tag 선택 목록(선택된 Collector+PLC 기준으로 동적 구성)</summary>
    public ObservableCollection<LiveTagRow> AvailableTags { get; } = new();

    [ObservableProperty]
    private LiveTagRow? _selectedTag;

    /// <summary>OxyPlot PlotView 에 바인딩되는 모델</summary>
    public PlotModel PlotModel { get; } = new()
    {
        Title = "실시간 트렌드 — Tag를 선택하세요"
    };

    // §3 ─ 생성자 ──────────────────────────────────────────

    public ChartViewModel(CollectorManageViewModel collectorVm, LiveTagAggregator tagAggregator)
    {
        Collectors     = collectorVm.Collectors;
        _tagAggregator = tagAggregator;

        PlotModel.Axes.Add(new DateTimeAxis
        {
            Position     = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss",
            Title        = "시각"
        });
        PlotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title    = "값"
        });

        // 최초 진입 시 전체 Tag 목록으로 채움
        _RefreshAvailableTags();
        tagAggregator.Rows.CollectionChanged += (_, _) => _RefreshAvailableTags();
    }

    // §4 ─ 필터 변경 처리 ──────────────────────────────────

    partial void OnSelectedCollectorChanged(CollectorEndpoint? value)
    {
        AvailablePlcIds.Clear();
        foreach (var plcId in _tagAggregator.Rows
                     .Where(t => value == null || t.CollectorId == value.Id)
                     .Select(t => t.PlcId)
                     .Distinct())
            AvailablePlcIds.Add(plcId);

        SelectedPlcId = null;
        _RefreshAvailableTags();
    }

    partial void OnSelectedPlcIdChanged(string? value) => _RefreshAvailableTags();

    private void _RefreshAvailableTags()
    {
        AvailableTags.Clear();
        foreach (var t in _tagAggregator.Rows.Where(t =>
                     (SelectedCollector is null || t.CollectorId == SelectedCollector.Id) &&
                     (SelectedPlcId is null || t.PlcId == SelectedPlcId)))
            AvailableTags.Add(t);
    }

    // §5 ─ Tag 선택 → 차트 구독 전환 ───────────────────────

    partial void OnSelectedTagChanged(LiveTagRow? value)
    {
        // 이전 선택 구독 해제
        if (_subscribedTag is not null)
            _subscribedTag.PropertyChanged -= _OnTagValueChanged;
        _subscribedTag = value;

        PlotModel.Series.Clear();
        _series = null;

        if (value is not null)
        {
            _series = new LineSeries
            {
                Title           = $"{value.CollectorName} · {value.PlcId} · {value.TagId}",
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_series);
            PlotModel.Title = $"{value.TagId} 실시간 트렌드";

            value.PropertyChanged += _OnTagValueChanged;
            _AppendPoint(value);
        }
        else
        {
            PlotModel.Title = "실시간 트렌드 — Tag를 선택하세요";
        }

        PlotModel.InvalidatePlot(true);
    }

    private void _OnTagValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LiveTagRow.EngValue)) return;
        if (sender is LiveTagRow row) _AppendPoint(row);
    }

    private void _AppendPoint(LiveTagRow row)
    {
        if (_series is null) return;

        _series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), row.EngValue));

        if (_series.Points.Count > MaxPoints)
            _series.Points.RemoveAt(0);

        // OxyPlot 은 자체적으로 UI 스레드 마샬링을 하지 않으므로 Dispatcher 로 갱신
        Application.Current?.Dispatcher.Invoke(() => PlotModel.InvalidatePlot(true));
    }
}
