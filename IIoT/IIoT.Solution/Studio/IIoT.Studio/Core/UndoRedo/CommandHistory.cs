// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/UndoRedo/CommandHistory.cs
//  역할: Undo/Redo 히스토리 스택 관리
//  S-29: 신규
//  규칙:
//    - Push() 시 Redo 스택 초기화 (새 액션 분기)
//    - Undo 스택 최대 50개 (초과 시 가장 오래된 것 제거)
//    - CanUndo / CanRedo 프로퍼티 → XAML CanExecute 연동
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

namespace IIoT.Studio.Core.UndoRedo;

public sealed class CommandHistory
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly int                 _maxSize;
    private readonly Stack<IUndoAction>  _undoStack = new();
    private readonly Stack<IUndoAction>  _redoStack = new();

    // §2 ─ 이벤트 (ViewModel에서 CanExecute 갱신용) ───────────

    public event Action? HistoryChanged;

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public CommandHistory(int maxSize = 50)
    {
        _maxSize = maxSize;
    }

    // §4 ─ 상태 프로퍼티 ──────────────────────────────────────

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? NextUndoDescription =>
        _undoStack.TryPeek(out var a) ? a.Description : null;

    public string? NextRedoDescription =>
        _redoStack.TryPeek(out var a) ? a.Description : null;

    // §5 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// 새 액션을 히스토리에 추가.
    /// Redo 스택 초기화 + 50개 초과 시 가장 오래된 것 제거.
    /// ★ 액션 Execute()는 호출하지 않음 — ViewModel에서 이미 실행한 결과를 Push
    /// </summary>
    public void Push(IUndoAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();

        // 최대 개수 초과 시 가장 오래된 것 제거
        if (_undoStack.Count > _maxSize)
        {
            var temp = _undoStack.ToArray();
            _undoStack.Clear();
            foreach (var a in temp.Take(_maxSize).Reverse())
                _undoStack.Push(a);
        }

        HistoryChanged?.Invoke();
    }

    /// <summary>
    /// 마지막 액션 취소.
    /// Undo 스택에서 꺼내 Undo() 호출 → Redo 스택으로 이동.
    /// </summary>
    public void Undo()
    {
        if (!_undoStack.TryPop(out var action)) return;
        action.Undo();
        _redoStack.Push(action);
        HistoryChanged?.Invoke();
    }

    /// <summary>
    /// 취소된 액션 다시 실행.
    /// Redo 스택에서 꺼내 Execute() 호출 → Undo 스택으로 이동.
    /// </summary>
    public void Redo()
    {
        if (!_redoStack.TryPop(out var action)) return;
        action.Execute();
        _undoStack.Push(action);
        HistoryChanged?.Invoke();
    }

    /// <summary>전체 히스토리 초기화</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke();
    }
}
