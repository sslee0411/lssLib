// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Canvas/CanvasConnection.cs
//  역할: 노드 간 연결선 모델
//  S-11: 초기 구현
//  S-12: UpdatePath(x1,y1,x2,y2) 메서드 추가
//        → 코드비하인드에서 좌표 계산 후 PathData 갱신
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Studio.Core.Canvas;

public partial class CanvasConnection : ObservableObject
{
    // §1 ─ 식별 ───────────────────────────────────────────────

    public string ConnectionId { get; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePortId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPortId { get; set; } = string.Empty;

    // §2 ─ 화면 좌표 + PathData ──────────────────────────────

    [ObservableProperty] private double _x1;
    [ObservableProperty] private double _y1;
    [ObservableProperty] private double _x2;
    [ObservableProperty] private double _y2;

    [ObservableProperty] private string _pathData = string.Empty;

    // §3 ─ 좌표 갱신 + PathData 재계산 ──────────────────────

    /// <summary>
    /// 출발(x1,y1) · 도착(x2,y2) 좌표로 베지어 PathData 갱신.
    /// 코드비하인드 또는 CanvasViewModel 에서 노드 이동 시마다 호출.
    /// </summary>
    public void UpdatePath(double x1, double y1, double x2, double y2)
    {
        X1 = x1; Y1 = y1;
        X2 = x2; Y2 = y2;

        // 수평 베지어 — 제어점은 출발·도착 X 중간값
        double cx = (x1 + x2) / 2;
        PathData = $"M {x1:F1},{y1:F1} C {cx:F1},{y1:F1} {cx:F1},{y2:F1} {x2:F1},{y2:F1}";
    }
}
