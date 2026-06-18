// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/CanvasViewModel.cs
//  역할: NodeRed 캔버스 ViewModel
//        노드 CRUD + 연결선 관리 + 줌·패닝 상태
//  S-11: 초기 구현
//  S-12: 연결선 추가
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Canvas;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class CanvasViewModel : ObservableObject
{
    // §1 ─ 노드 + 연결선 컬렉션 ──────────────────────────────

    public ObservableCollection<AbstractCanvasNode> Nodes       { get; } = new();
    public ObservableCollection<CanvasConnection>   Connections { get; } = new();

    // §2 ─ 팔레트 항목 (정적) ─────────────────────────────────

    public IReadOnlyList<PaletteItem> PaletteItems
        => CanvasNodeFactory.AllItems;

    // §3 ─ 선택 노드 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private AbstractCanvasNode? _selectedNode;

    public bool HasSelection => SelectedNode is not null;

    // §4 ─ 줌·패닝 상태 ───────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePercent))]
    private double _scale = 1.0;

    [ObservableProperty] private double _offsetX;
    [ObservableProperty] private double _offsetY;

    /// <summary>상태바 표시용 줌 % 텍스트</summary>
    public string ScalePercent => $"{Scale * 100:F0}%";

    // §5 ─ 노드 추가 커맨드 ───────────────────────────────────

    /// <summary>팔레트 버튼 클릭 → nodeType 으로 노드 생성 후 캔버스 중앙에 배치</summary>
    [RelayCommand]
    private void AddNode(string nodeType)
    {
        var node = CanvasNodeFactory.Create(nodeType);
        if (node is null) return;

        // 캔버스 가시 영역 중앙에 배치 (간단히 기존 노드 수 × 20 오프셋)
        node.X = 200 + (Nodes.Count % 5) * 30;
        node.Y = 120 + (Nodes.Count / 5) * 100;

        Nodes.Add(node);
        SelectNode(node);
    }

    // §6 ─ 선택·삭제 커맨드 ──────────────────────────────────

    public void SelectNode(AbstractCanvasNode? node)
    {
        if (SelectedNode is not null)
            SelectedNode.IsSelected = false;

        SelectedNode = node;

        if (SelectedNode is not null)
            SelectedNode.IsSelected = true;
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedNode is null) return;

        // 연결선 제거
        var toRemove = Connections
            .Where(c => c.SourceNodeId == SelectedNode.NodeId
                     || c.TargetNodeId == SelectedNode.NodeId)
            .ToList();
        foreach (var c in toRemove) Connections.Remove(c);

        Nodes.Remove(SelectedNode);
        SelectedNode = null;
    }

    // §7 ─ 줌 커맨드 ─────────────────────────────────────────

    [RelayCommand]
    private void ZoomIn()  => Scale = Math.Min(3.0, Scale + 0.1);

    [RelayCommand]
    private void ZoomOut() => Scale = Math.Max(0.3, Scale - 0.1);

    [RelayCommand]
    private void ZoomReset() { Scale = 1.0; OffsetX = 0; OffsetY = 0; }

    // §8 ─ 휠 줌 (코드비하인드에서 호출) ────────────────────

    public void ApplyWheelZoom(double delta)
    {
        var factor = delta > 0 ? 1.1 : 0.9;
        Scale = Math.Clamp(Scale * factor, 0.3, 3.0);
        OnPropertyChanged(nameof(ScalePercent));
    }

    // §9 ─ 연결선 추가 (S-12) ─────────────────────────────────

    public void AddConnection(
        string sourceNodeId, string sourcePortId,
        string targetNodeId, string targetPortId)
    {
        // 중복 연결 방지
        if (Connections.Any(c =>
                c.SourcePortId == sourcePortId &&
                c.TargetPortId == targetPortId)) return;

        Connections.Add(new CanvasConnection
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        });
    }
}
