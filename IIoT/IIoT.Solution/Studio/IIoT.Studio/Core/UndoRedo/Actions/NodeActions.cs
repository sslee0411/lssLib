// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/UndoRedo/Actions/NodeActions.cs
//  역할: 장비 트리 노드 관련 Undo/Redo 액션 4종
//  S-29: 신규
//
//  AddNodeAction    — 노드 추가 취소 = 노드 삭제
//  DeleteNodeAction — 노드 삭제 취소 = 노드 복원
//  RenameNodeAction — 이름 변경 취소 = 이전 이름 복원
//  MoveNodeAction   — 순서 이동 취소 = 이전 위치 복원
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.Core.UndoRedo.Actions;

// §1 ─ 노드 추가 액션 ─────────────────────────────────────

/// <summary>
/// 노드 추가 액션.
/// Execute: 컬렉션에 노드 추가 (지정 인덱스)
/// Undo:    컬렉션에서 노드 제거
/// </summary>
public sealed class AddNodeAction : IUndoAction
{
    private readonly ObservableCollection<AbstractTreeNode> _collection;
    private readonly AbstractTreeNode                       _node;
    private readonly int                                    _index;

    public string Description => $"'{_node.Name}' 추가";

    public AddNodeAction(
        ObservableCollection<AbstractTreeNode> collection,
        AbstractTreeNode                       node,
        int                                    index)
    {
        _collection = collection;
        _node       = node;
        _index      = index;
    }

    public void Execute()
    {
        if (_index >= 0 && _index <= _collection.Count)
            _collection.Insert(_index, _node);
        else
            _collection.Add(_node);
    }

    public void Undo() => _collection.Remove(_node);
}

// §2 ─ 노드 삭제 액션 ─────────────────────────────────────

/// <summary>
/// 노드 삭제 액션.
/// Execute: 컬렉션에서 노드 제거
/// Undo:    원래 인덱스에 노드 복원
/// </summary>
public sealed class DeleteNodeAction : IUndoAction
{
    private readonly ObservableCollection<AbstractTreeNode> _collection;
    private readonly AbstractTreeNode                       _node;
    private readonly int                                    _index;

    public string Description => $"'{_node.Name}' 삭제";

    public DeleteNodeAction(
        ObservableCollection<AbstractTreeNode> collection,
        AbstractTreeNode                       node,
        int                                    index)
    {
        _collection = collection;
        _node       = node;
        _index      = index;
    }

    public void Execute() => _collection.Remove(_node);

    public void Undo()
    {
        var idx = Math.Clamp(_index, 0, _collection.Count);
        _collection.Insert(idx, _node);
    }
}

// §3 ─ 이름 변경 액션 ─────────────────────────────────────

/// <summary>
/// 이름 변경 액션.
/// Execute: 새 이름으로 변경
/// Undo:    이전 이름으로 복원
/// </summary>
public sealed class RenameNodeAction : IUndoAction
{
    private readonly AbstractTreeNode _node;
    private readonly string           _oldName;
    private readonly string           _newName;

    public string Description => $"'{_oldName}' → '{_newName}' 이름 변경";

    public RenameNodeAction(
        AbstractTreeNode node,
        string           oldName,
        string           newName)
    {
        _node    = node;
        _oldName = oldName;
        _newName = newName;
    }

    public void Execute() => _node.Name = _newName;
    public void Undo()    => _node.Name = _oldName;
}

// §4 ─ 순서 이동 액션 ─────────────────────────────────────

/// <summary>
/// 노드 순서 이동 액션 (↑↓).
/// Execute: newIndex로 이동
/// Undo:    oldIndex로 복원
/// </summary>
public sealed class MoveNodeAction : IUndoAction
{
    private readonly ObservableCollection<AbstractTreeNode> _collection;
    private readonly AbstractTreeNode                       _node;
    private readonly int                                    _oldIndex;
    private readonly int                                    _newIndex;

    public string Description =>
        _newIndex < _oldIndex
            ? $"'{_node.Name}' 위로 이동"
            : $"'{_node.Name}' 아래로 이동";

    public MoveNodeAction(
        ObservableCollection<AbstractTreeNode> collection,
        AbstractTreeNode                       node,
        int                                    oldIndex,
        int                                    newIndex)
    {
        _collection = collection;
        _node       = node;
        _oldIndex   = oldIndex;
        _newIndex   = newIndex;
    }

    public void Execute() => _collection.Move(_oldIndex, _newIndex);
    public void Undo()    => _collection.Move(_newIndex, _oldIndex);
}
