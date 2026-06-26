// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/UndoRedo/IUndoAction.cs
//  역할: 실행취소 가능한 액션 인터페이스
//  S-29: 신규
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

namespace IIoT.Studio.Core.UndoRedo;

/// <summary>
/// 실행취소/다시실행 가능한 액션 계약.
/// Execute() : 액션 실행 (Redo 시에도 호출)
/// Undo()    : 액션 취소 (Undo 시 호출)
/// </summary>
public interface IUndoAction
{
    /// <summary>액션 설명 (디버깅·상태바 표시용)</summary>
    string Description { get; }

    /// <summary>액션 실행 / 다시 실행</summary>
    void Execute();

    /// <summary>액션 취소</summary>
    void Undo();
}
