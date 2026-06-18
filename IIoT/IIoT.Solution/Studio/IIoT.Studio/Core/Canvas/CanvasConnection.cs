// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Canvas/CanvasConnection.cs
//  역할: 노드 간 연결선 모델
//  S-11: 초기 구현
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Studio.Core.Canvas;

// §1 ─ 연결선 모델 ────────────────────────────────────────

/// <summary>두 노드 포트 간 연결선</summary>
public partial class CanvasConnection : ObservableObject
{
    public string ConnectionId   { get; } = Guid.NewGuid().ToString();

    public string SourceNodeId   { get; set; } = string.Empty;
    public string SourcePortId   { get; set; } = string.Empty;
    public string TargetNodeId   { get; set; } = string.Empty;
    public string TargetPortId   { get; set; } = string.Empty;

    // §2 ─ 화면 좌표 (CanvasView 코드비하인드에서 갱신) ──────

    [ObservableProperty] private double _x1;
    [ObservableProperty] private double _y1;
    [ObservableProperty] private double _x2;
    [ObservableProperty] private double _y2;

    /// <summary>베지어 곡선 제어점 Path Data (뷰에서 바인딩)</summary>
    [ObservableProperty] private string _pathData = string.Empty;

    // §3 ─ 베지어 계산 ────────────────────────────────────────

    /// <summary>X1/Y1/X2/Y2 변경 시 PathData 갱신</summary>
    public void RefreshPath()
    {
        double cx = (X1 + X2) / 2;
        PathData = $"M {X1:F0},{Y1:F0} C {cx:F0},{Y1:F0} {cx:F0},{Y2:F0} {X2:F0},{Y2:F0}";
    }
}
