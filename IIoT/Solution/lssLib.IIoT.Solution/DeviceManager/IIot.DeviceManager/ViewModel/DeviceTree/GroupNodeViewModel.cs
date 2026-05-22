// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · GroupNodeViewModel.cs
//  역할: 논리 그룹 노드 ViewModel
//  생성: 2025-05-22
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Xml.Linq;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// 논리 그룹 노드 — Group &gt; Device / 하위 Group 무제한 중첩 가능.
/// </summary>
public partial class GroupNodeViewModel : DeviceNodeViewModel
{
    // §1 ─ 공개 속성 ──────────────────────────────────────────

    public override NodeKind Kind => NodeKind.Group;

    /// <summary>📁</summary>
    public override string IconGlyph => IsExpanded ? "📂" : "📁";

    public override IReadOnlyList<NodeKind> AllowedChildKinds =>
        [NodeKind.Group, NodeKind.Device];

    // §2 ─ 배지 ───────────────────────────────────────────────

    /// <summary>하위 Device 개수 배지</summary>
    public override string? Badge
    {
        get
        {
            var cnt = Children.Count(c => c.Kind == NodeKind.Device);
            return cnt > 0 ? cnt.ToString() : null;
        }
    }

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public GroupNodeViewModel(string name = "새 그룹")
    {
        Name = name;
        // 자식 변경 시 배지 갱신
        Children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Badge));
    }
}