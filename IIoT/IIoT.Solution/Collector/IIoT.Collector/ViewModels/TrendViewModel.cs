// ══════════════════════════════════════════════════════════
//  IIoT.Collector · ViewModels/TrendViewModel.cs
//  역할: 수집 이력 조회 탭([📈 트렌드]) ViewModel
//        Tag 선택 + 기간 설정 → TrendQueryService 조회 → OxyPlot LineChart 바인딩
//  C-13: 신규 (OxyPlot.Wpf — 순수 .NET, net8.0-windows 완전 지원)
//  C-EX-07: CSV 내보내기 커맨드 추가
//  생성: 2026-07-01 / 수정: 2026-07-06
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Collector.Core.Config;
using IIoT.Collector.Storage.Query;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System.Collections.ObjectModel;

namespace IIoT.Collector.ViewModels;

/// <summary>
/// 수집 이력 조회 ViewModel (DI 싱글턴).
/// <para>
/// Tag 선택 → 기간 선택 → [조회] 버튼 → SQLite 조회 → OxyPlot PlotModel 바인딩.
/// </para>
/// <para>
/// <b>OxyPlot 채택 이유:</b><br/>
/// LiveChartsCore.SkiaSharpView.WPF 는 SkiaSharp 3.x 의존으로
/// net8.0-windows 런타임 XamlParseException 위험이 있음.
/// OxyPlot.Wpf 는 순수 .NET 구현으로 SkiaSharp 의존성이 없어
/// net8.0-windows 완전 지원 및 10년+ 산업 현장 검증됨.
/// </para>
/// </summary>
public partial class TrendViewModel : ObservableObject
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly TrendQueryService      _queryService;
    private readonly CollectorConfigLoader  _configLoader;
    private readonly CsvExportService       _csvExport;   // ★ C-EX-07 신규

    /// <summary>가장 최근 조회 결과 (CSV 내보내기용 보관)</summary>
    private IReadOnlyList<TrendPoint>? _lastPoints;

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

    // §5 ─ OxyPlot 모델 ───────────────────────────────────

    /// <summary>OxyPlot PlotModel — XAML에 바인딩</summary>
    [ObservableProperty]
    private PlotModel _plotModel = _CreateEmptyModel();

    // §6 ─ 생성자 ──────────────────────────────────────────

    public TrendViewModel(
        TrendQueryService     queryService,
        CollectorConfigLoader configLoader,
        CsvExportService      csvExport)   // ★ C-EX-07 신규
    {
        _queryService = queryService;
        _configLoader = configLoader;
        _csvExport    = csvExport;
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
            _             => new DateTimeOffset(FromDate.Add(FromTime),
                                 TimeZoneInfo.Local.GetUtcOffset(FromDate)),
        };

        if (SelectedRange == "사용자 지정")
            to = new DateTimeOffset(ToDate.Add(ToTime),
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

        try
        {
            var (from, to) = _GetRange();

            var points = await _queryService.QueryAsync(
                SelectedTag.TagId, from, to, maxPoints: 3000);

            if (points.Count == 0)
            {
                StatusText = $"[{SelectedTag.TagName}] 해당 기간에 이력 데이터가 없습니다.";
                PlotModel  = _CreateEmptyModel("데이터 없음");
                _lastPoints = null;
                return;
            }

            PlotModel   = _BuildPlotModel(SelectedTag, points);
            _lastPoints = points;   // ★ C-EX-07 신규 — CSV 내보내기용 보관

            StatusText =
                $"[{SelectedTag.TagName}] {from:MM/dd HH:mm} ~ {to:MM/dd HH:mm} " +
                $"({points.Count:#,0}건 표시)";
        }
        catch (Exception ex)
        {
            StatusText = $"조회 오류: {ex.Message}";
            PlotModel  = _CreateEmptyModel("조회 오류");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // §9B ─ CSV 내보내기 (C-EX-07 신규) ────────────────────

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (_lastPoints is null || _lastPoints.Count == 0 || SelectedTag is null)
        {
            StatusText = "내보낼 데이터가 없습니다. 먼저 [조회]를 실행하세요.";
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "CSV 파일 (*.csv)|*.csv",
            FileName = $"{SelectedTag.TagName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            await _csvExport.ExportAsync(_lastPoints, dialog.FileName, SelectedTag.TagName);
            StatusText = $"CSV 저장 완료 → {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"CSV 저장 실패: {ex.Message}";
        }
    }

    // §10 ─ PlotModel 생성 ─────────────────────────────────

    private static PlotModel _CreateEmptyModel(string subtitle = "")
    {
        var model = new PlotModel
        {
            Background   = OxyColor.FromRgb(0x0D, 0x11, 0x17), // BgBrush(DarkNavy)
            TextColor    = OxyColor.FromRgb(0xC9, 0xD1, 0xD9),
            PlotAreaBorderColor = OxyColor.FromRgb(0x30, 0x36, 0x3D),
        };

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            model.Subtitle     = subtitle;
            model.SubtitleColor = OxyColor.FromRgb(0x8B, 0x94, 0x9E);
        }

        return model;
    }

    private static PlotModel _BuildPlotModel(
        TrendTagItem tag, IReadOnlyList<TrendPoint> points)
    {
        var model = new PlotModel
        {
            Title        = tag.TagName,
            Background   = OxyColor.FromRgb(0x0D, 0x11, 0x17),
            TextColor    = OxyColor.FromRgb(0xC9, 0xD1, 0xD9),
            PlotAreaBorderColor = OxyColor.FromRgb(0x30, 0x36, 0x3D),
        };

        // ★ OxyPlot 2.2.0: Legend 설정은 DefaultLegend 통해서 적용
        var legend = new OxyPlot.Legends.Legend
        {
            LegendBackground = OxyColor.FromArgb(0xCC, 0x16, 0x1B, 0x22),
            LegendBorder     = OxyColor.FromRgb(0x30, 0x36, 0x3D),
            LegendTextColor  = OxyColor.FromRgb(0xC9, 0xD1, 0xD9),
            LegendPosition   = OxyPlot.Legends.LegendPosition.TopRight,
        };
        model.Legends.Add(legend);

        // X축 — 시간
        model.Axes.Add(new DateTimeAxis
        {
            Position          = AxisPosition.Bottom,
            Title             = "시각",
            StringFormat      = "HH:mm",
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(0x30, 0x36, 0x3D),
            TicklineColor     = OxyColor.FromRgb(0x58, 0x6E, 0x7E),
            TitleColor        = OxyColor.FromRgb(0x8B, 0x94, 0x9E),
            TextColor         = OxyColor.FromRgb(0x8B, 0x94, 0x9E),
        });

        // Y축 — 공학값
        var unit = string.IsNullOrWhiteSpace(tag.Unit) ? "값" : $"값 ({tag.Unit})";
        model.Axes.Add(new LinearAxis
        {
            Position          = AxisPosition.Left,
            Title             = unit,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(0x30, 0x36, 0x3D),
            TicklineColor     = OxyColor.FromRgb(0x58, 0x6E, 0x7E),
            TitleColor        = OxyColor.FromRgb(0x8B, 0x94, 0x9E),
            TextColor         = OxyColor.FromRgb(0x8B, 0x94, 0x9E),
        });

        // 공학값 라인 (파란색)
        var engSeries = new LineSeries
        {
            Title            = $"{tag.TagName} (공학값)",
            Color            = OxyColor.FromRgb(0x1F, 0x6F, 0xEB),
            StrokeThickness  = 1.5,
            MarkerType       = MarkerType.None,
        };

        // Raw 값 라인 (회색 — 스케일이 있는 경우만)
        var rawSeries = new LineSeries
        {
            Title            = $"{tag.TagName} (Raw)",
            Color            = OxyColor.FromRgb(0x58, 0x6E, 0x7E),
            StrokeThickness  = 1.0,
            MarkerType       = MarkerType.None,
        };

        var hasScale = false;
        foreach (var p in points)
        {
            var x = DateTimeAxis.ToDouble(p.Timestamp.LocalDateTime);
            engSeries.Points.Add(new DataPoint(x, p.EngValue));
            rawSeries.Points.Add(new DataPoint(x, p.RawValue));

            if (!hasScale && Math.Abs(p.EngValue - p.RawValue) > 0.001)
                hasScale = true;
        }

        model.Series.Add(engSeries);
        if (hasScale)
            model.Series.Add(rawSeries);

        return model;
    }
}
