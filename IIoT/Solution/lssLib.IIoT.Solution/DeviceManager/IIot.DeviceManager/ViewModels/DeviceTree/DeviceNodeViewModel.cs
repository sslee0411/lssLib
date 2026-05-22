// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceNodeViewModel.cs
//  역할: 장비 트리 노드 추상 기반 ViewModel
//  생성: 2025-05-22
//  수정: 2025-05-22 v2 — 코드 정리
//        ① using System.Xml.Linq 미사용 제거
//        ② BadgeBrushKey 기본값 "AccentBrush" → "AccBrush" (테마 올바른 키)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// 장비 트리 모든 노드 공통 기반 ViewModel.
/// Group / Device / Plc / Tag 가 상속한다.
/// </summary>
public abstract partial class DeviceNodeViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editBuffer = string.Empty;

    // §2 ─ 공개 속성 ─────────────────────────────────────────

    /// <summary>노드 고유 ID (GUID)</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>노드 종류</summary>
    public abstract NodeKind Kind { get; }

    /// <summary>트리 아이콘 문자 (Unicode 기호 활용)</summary>
    public abstract string IconGlyph { get; }

    /// <summary>배지 텍스트 (null = 비표시)</summary>
    public virtual string? Badge => null;

    /// <summary>
    /// 배지 색상 DynamicResource 키.
    /// ★ 수정: "AccentBrush" → "AccBrush" (IIoT.UI.Themes 올바른 키)
    /// </summary>
    public virtual string BadgeBrushKey => "AccBrush";

    /// <summary>자식 노드 컬렉션</summary>
    public ObservableCollection<DeviceNodeViewModel> Children { get; } = [];

    /// <summary>부모 노드 (루트는 null)</summary>
    public DeviceNodeViewModel? Parent { get; internal set; }

    /// <summary>자식 추가 가능 종류</summary>
    public abstract IReadOnlyList<NodeKind> AllowedChildKinds { get; }

    // §3 ─ 커맨드 ────────────────────────────────────────────

    /// <summary>인라인 이름 편집 시작</summary>
    [RelayCommand]
    private void BeginEdit()
    {
        EditBuffer = Name;
        IsEditing = true;
    }

    /// <summary>인라인 이름 편집 확정</summary>
    [RelayCommand]
    private void CommitEdit()
    {
        if (!string.IsNullOrWhiteSpace(EditBuffer))
            Name = EditBuffer.Trim();
        IsEditing = false;
    }

    /// <summary>인라인 이름 편집 취소</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        EditBuffer = Name;
        IsEditing = false;
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>자식 노드 추가 (부모 역참조 자동 설정)</summary>
    internal void AddChild(DeviceNodeViewModel child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    /// <summary>자식 노드 제거</summary>
    internal bool RemoveChild(DeviceNodeViewModel child)
    {
        if (!Children.Remove(child)) return false;
        child.Parent = null;
        return true;
    }

    /// <summary>재귀 순회 (자신 포함)</summary>
    public IEnumerable<DeviceNodeViewModel> Flatten()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var n in child.Flatten())
                yield return n;
    }
}