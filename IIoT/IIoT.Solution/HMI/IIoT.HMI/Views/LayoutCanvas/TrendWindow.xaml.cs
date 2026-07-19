// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/LayoutCanvas/TrendWindow.xaml.cs
//  역할: 실시간 트렌드 창 코드비하인드 — 우클릭된 노드 1개의 EngValue 변화를
//        구독해 OxyPlot LineSeries 에 계속 점을 추가한다.
//        (Monitor ViewModels/ChartViewModel.cs 의 롤링 윈도우/PropertyChanged
//         구독 로직을 이식 — Collector/PLC/Tag 선택 로직은 필요 없어 제외하고,
//         DI ViewModel 없이 코드비하인드에서 직접 PlotModel 을 구성한다
//         (ForceWriteDialog 와 동일하게 "일반 다이얼로그/창" 패턴 — DI 미사용)
//  ★ 여러 개를 동시에 열 수 있다 — 매번 새 TrendWindow 인스턴스를 생성하므로
//    노드별로 독립된 창에서 각자 구독/누적한다(비모달, Owner=MainWindow).
//  HM-17: 신규
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Layout;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.ComponentModel;
using System.Windows;

namespace IIoT.HMI.Views.LayoutCanvas;

public partial class TrendWindow : Window
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    /// <summary>최대 보관 포인트 수 (롤링 윈도우 — 오래된 값은 자동 제거)</summary>
    private const int MaxPoints = 300;

    private readonly AbstractLayoutNode _node;
    private readonly LineSeries _series;

    /// <summary>OxyPlot PlotView 에 바인딩되는 모델</summary>
    public PlotModel PlotModel { get; } = new();

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public TrendWindow(AbstractLayoutNode node)
    {
        InitializeComponent();

        _node       = node;
        DataContext = this;

        Title = $"실시간 트렌드 — {node.Label}";
        TxtHeader.Text = $"{node.Label} · {node.BoundTagName} (PlcId: {node.BoundPlcId}) — 이 창을 연 시점부터의 값만 표시됩니다.";

        PlotModel.Title = $"{node.BoundTagName} 실시간 트렌드";
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

        _series = new LineSeries
        {
            Title           = node.BoundTagName,
            StrokeThickness = 2
        };
        PlotModel.Series.Add(_series);

        // 창을 열자마자 현재값 1점부터 시작
        _AppendPointIfAvailable();

        node.PropertyChanged += _OnNodePropertyChanged;
        Closed += (_, _) => node.PropertyChanged -= _OnNodePropertyChanged;
    }

    // §3 ─ 값 수신 ────────────────────────────────────────────

    private void _OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AbstractLayoutNode.EngValue)) return;
        _AppendPointIfAvailable();
    }

    private void _AppendPointIfAvailable()
    {
        // EngValue 는 미바인딩/값없음 시 null — 그런 경우는 점을 찍지 않는다
        if (_node.EngValue is not double value) return;

        _series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), value));

        if (_series.Points.Count > MaxPoints)
            _series.Points.RemoveAt(0);

        // OxyPlot 은 자체적으로 UI 스레드 마샬링을 하지 않으므로 Dispatcher 로 갱신
        // (Monitor ChartViewModel._AppendPoint() 와 동일 패턴)
        Dispatcher.Invoke(() => PlotModel.InvalidatePlot(true));
    }
}
