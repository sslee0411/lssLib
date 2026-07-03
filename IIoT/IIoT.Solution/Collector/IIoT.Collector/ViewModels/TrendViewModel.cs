// ══════════════════════════════════════════════════════════
//  IIoT.Collector · ViewModels/TrendViewModel.cs
//  역할: 수집 이력 조회 탭([📈 트렌드]) ViewModel
//        Tag 선택 + 기간 설정 → TrendQueryService 조회 → LiveCharts2 데이터 바인딩
//  C-13: 신규
//  생성: 2026-07-01
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Collector.Core.Config;
using IIoT.Collector.Storage.Query;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace IIoT.Collector.ViewModels;

/// <summary>
/// 수집 이력 조회 ViewModel (DI 싱글턴).
/// <para>
/// Tag 선택 → 기간 선택 → [조회] 버튼 → SQLite 조회 → LiveCharts2 LineChart 표시.
/// </para>
/// </summary>
public partial class TrendViewModel : ObservableObject
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly TrendQueryService      _queryService;
    private readonly CollectorConfigLoader  _configLoader;

    // §2 ─ 드롭다운 목록 ───────────────────────────────────

    /// <summary>Tag 선택 드롭다운 목록</summary>
    public ObservableCollection<TrendTagItem> AvailableTags { get; } = new();

    /// <summary>기간 프리셋 목록</summary>
    public IReadOnlyList<string> RangePresets { get; } =
        ["최근 1시간", "최근 6시간", "최근 24시간", "최근 7일", "사용자 지정"];

    // §3 ─ 선택 상태 ───────────────────────────────────────

    [ObservableProperty] private TrendTagItem? _selectedTag;
    [ObservableProperty] private string        _selectedRange = "최근 1시간";

    [ObservableProperty] private DateTime _fromDate = DateTime.Today;
    [ObservableProperty] private DateTime _toDate   = DateTime.Today;
    [ObservableProperty] private TimeSpan _fromTime = TimeSpan.Zero;
    [ObservableProperty] private TimeSpan _toTime   = DateTime.Now.TimeOfDay;

    /// <summary>사용자 지정 기간 컨트롤 표시 여부</summary>
    public bool IsCustomRange => SelectedRange == "사용자 지정";

    partial void OnSelectedRangeChanged(string value)
        => OnPropertyChanged(nameof(IsCustomRange));

    // §4 ─ 상태 ────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusText = "Tag 와 기간을 선택 후 [조회] 버튼을 클릭하세요.";
    [ObservableProperty] private string _unitText   = string.Empty;

    // §5 ─ 차트 설정 ───────────────────────────────────────

    /// <summary>LiveCharts2 LineChart Series 바인딩</summary>
    public ObservableCollection<ISeries> Series { get; } = new();

    /// <summary>X축 (시간)</summary>
    public Axis[] XAxes { get; } =
    [
        new DateTimeAxis(TimeSpan.FromMinutes(1), d => d.ToString("HH:mm"))
        {
            Name     = "시각",
            TextSize = 10,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            SeparatorsPaint = new SolidColorPaint(SKColors.DimGray.WithAlpha(60)),
        }
    ];

    /// <summary>Y축 (공학값)</summary>
    public Axis[] YAxes { get; } =
    [
        new Axis
        {
            Name     = "값",
            TextSize = 10,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            SeparatorsPaint = new SolidColorPaint(SKColors.DimGray.WithAlpha(60)),
        }
    ];

    // §6 ─ 생성자 ──────────────────────────────────────────

    public TrendViewModel(
        TrendQueryService     queryService,
        CollectorConfigLoader configLoader)
    {
        _queryService  = queryService;
        _configLoader  = configLoader;
    }

    // §7 ─ 초기화 ──────────────────────────────────────────

    public void Initialize()
    {
        AvailableTags.Clear();
        foreach (var tag in _queryService.GetAvailableTags())
            AvailableTags.Add(tag);

        if (AvailableTags.Count > 0)
            SelectedTag = AvailableTags[0];

        StatusText = $"{AvailableTags.Count}개 Tag 준비됨. 기간을 선택하고 [조회]를 클릭하세요.";
    }

    // §8 ─ 기간 계산 ───────────────────────────────────────

    private (DateTimeOffset From, DateTimeOffset To) _GetRange()
    {
        var to   = DateTimeOffset.Now;
        var from = SelectedRange switch
        {
            "최근 1시간"  => to.AddHours(-1),
            "최근 6시간"  => to.AddHours(-6),
            "최근 24시간" => to.AddHours(-24),
            "최근 7일"    => to.AddDays(-7),
            _             => new DateTimeOffset(
                                 FromDate.Add(FromTime),
                                 TimeZoneInfo.Local.GetUtcOffset(FromDate)),
        };

        if (SelectedRange == "사용자 지정")
            to = new DateTimeOffset(
                     ToDate.Add(ToTime),
                     TimeZoneInfo.Local.GetUtcOffset(ToDate));

        return (from, to);
    }

    // §9 ─ 조회 커맨드 ─────────────────────────────────────

    [RelayCommand]
    private async Task QueryAsync()
    {
        if (SelectedTag is null)
        {
            StatusText = "조회할 Tag 를 선택해 주세요.";
            return;
        }

        IsLoading  = true;
        StatusText = $"[{SelectedTag.TagName}] 조회 중...";
        Series.Clear();

        try
        {
            var (from, to) = _GetRange();

            var points = await _queryService.QueryAsync(
                SelectedTag.TagId, from, to, maxPoints: 3000);

            if (points.Count == 0)
            {
                StatusText = $"[{SelectedTag.TagName}] 해당 기간에 이력 데이터가 없습니다.";
                return;
            }

            // LiveCharts2 DateTimePoint 변환
            var engPoints = points
                .Select(p => new DateTimePoint(p.Timestamp.LocalDateTime, p.EngValue))
                .ToArray();

            var rawPoints = points
                .Select(p => new DateTimePoint(p.Timestamp.LocalDateTime, p.RawValue))
                .ToArray();

            // 공학값 라인 (주 표시)
            Series.Add(new LineSeries<DateTimePoint>
            {
                Name            = $"{SelectedTag.TagName} (공학값)",
                Values          = engPoints,
                Stroke          = new SolidColorPaint(SKColors.DodgerBlue, 2),
                Fill            = null,
                GeometrySize    = 0,
                LineSmoothness  = 0,
            });

            // Raw 값 라인 (스케일 적용된 Tag 만 표시 — Raw≠Eng 일 때만)
            var hasScale = points.Any(p => Math.Abs(p.EngValue - p.RawValue) > 0.001);
            if (hasScale)
            {
                Series.Add(new LineSeries<DateTimePoint>
                {
                    Name            = $"{SelectedTag.TagName} (Raw)",
                    Values          = rawPoints,
                    Stroke          = new SolidColorPaint(SKColors.DimGray, 1),
                    Fill            = null,
                    GeometrySize    = 0,
                    LineSmoothness  = 0,
                });
            }

            // Y축 이름 갱신
            UnitText = string.IsNullOrWhiteSpace(SelectedTag.Unit)
                ? "값"
                : $"값 ({SelectedTag.Unit})";
            YAxes[0].Name = UnitText;

            var duration = to - from;
            StatusText =
                $"[{SelectedTag.TagName}] {from:MM/dd HH:mm} ~ {to:MM/dd HH:mm} " +
                $"({points.Count:#,0}건 표시)";
        }
        catch (Exception ex)
        {
            StatusText = $"조회 오류: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
