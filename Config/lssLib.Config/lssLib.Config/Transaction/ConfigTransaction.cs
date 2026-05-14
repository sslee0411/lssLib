// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Transaction/ConfigTransaction.cs
//  역할: 설정 변경 트랜잭션 — Commit / Rollback / Undo / Redo
// ══════════════════════════════════════════════════════════════════════════
namespace lssLib.Config.Transaction;

// ── ChangeRecord ──────────────────────────────────────────────────────────

/// <summary>단일 설정 변경 기록.</summary>
public sealed record ChangeRecord(
    string Section,
    string Key,
    string? OldValue,    // null = 키가 없었음
    string? NewValue,    // null = 키를 제거함
    bool OldEncrypted,
    bool NewEncrypted);

// ── ConfigTransaction ─────────────────────────────────────────────────────

/// <summary>
/// 설정 변경 트랜잭션.
/// </summary>
/// <remarks>
/// <see cref="ConfigManager.BeginTransaction"/> 으로 시작하고
/// <see cref="Commit"/> 또는 <see cref="Rollback"/> 으로 완료합니다.
/// <para>
/// <c>using</c> 블록을 사용하면 블록 종료 시 자동으로 <see cref="Dispose"/> 가 호출되며,
/// 커밋하지 않은 상태라면 <see cref="Rollback"/> 이 자동 수행됩니다.
/// </para>
/// <example><code>
/// // ── 기본 사용 ──────────────────────────────────────
/// using var tx = ConfigManager.Instance.BeginTransaction();
/// tx.Set("Network", "Host", "10.0.0.1");
/// tx.Set("Network", "Port", "1502");
/// tx.Commit();    // 한 번에 적용 + ConfigChanged 이벤트 1회 발생
///
/// // ── 롤백 예시 ──────────────────────────────────────
/// using var tx2 = ConfigManager.Instance.BeginTransaction();
/// tx2.Set("DB", "Host", "invalid-host");
/// // 검증 실패 → 롤백
/// tx2.Rollback(); // 또는 using 블록 이탈 시 자동 롤백
///
/// // ── Undo / Redo ──────────────────────────────────
/// ConfigManager.Instance.Undo(); // 마지막 커밋 이전 상태로
/// ConfigManager.Instance.Redo(); // 다시 적용
/// </code></example>
/// </remarks>
public sealed class ConfigTransaction : IDisposable
{
    #region §1 ─ 필드

    private readonly ConfigStore _store;
    private readonly Action<IReadOnlyList<ChangeRecord>>? _strOnCommit;
    private readonly List<ChangeRecord> _changes = new();
    private bool _committed;
    private bool _disposed;

    #endregion

    #region §2 ─ 생성자

    internal ConfigTransaction(
        ConfigStore store,
        Action<IReadOnlyList<ChangeRecord>>? onCommit = null)
    {
        _store = store;
        _strOnCommit = onCommit;
    }

    #endregion

    #region §3 ─ 변경 메서드

    /// <summary>
    /// 트랜잭션 내에서 설정 값을 변경합니다.
    /// <see cref="Commit"/> 전까지 실제 <see cref="ConfigStore"/> 에는 반영되지 않습니다.
    /// </summary>
    public ConfigTransaction Set(string section, string key, string value,
        bool isEncrypted = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_committed)
            throw new InvalidOperationException("이미 커밋된 트랜잭션입니다.");

        var entry = _store.GetEntry(section, key);
        var oldValue = entry?.Value;
        var oldEnc = entry?.IsEncrypted ?? false;

        _changes.Add(new ChangeRecord(section, key, oldValue, value, oldEnc, isEncrypted));
        return this;
    }

    /// <summary>
    /// 트랜잭션 내에서 키를 제거합니다.
    /// </summary>
    public ConfigTransaction Remove(string section, string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_committed)
            throw new InvalidOperationException("이미 커밋된 트랜잭션입니다.");

        var entry = _store.GetEntry(section, key);
        if (entry is not null)
            _changes.Add(new ChangeRecord(section, key, entry.Value, null, entry.IsEncrypted, false));
        return this;
    }

    /// <summary>보류 중인 변경 항목 수.</summary>
    public int PendingCount => _changes.Count;

    /// <summary>보류 중인 변경 목록 (읽기 전용).</summary>
    public IReadOnlyList<ChangeRecord> PendingChanges => _changes;

    #endregion

    #region §4 ─ Commit / Rollback

    /// <summary>
    /// 보류 중인 모든 변경을 <see cref="ConfigStore"/> 에 적용합니다.
    /// </summary>
    /// <remarks>
    /// 적용 후 <c>_onCommit</c> 콜백(ConfigManager 의 Undo 스택 등록 + 이벤트 발생)이 호출됩니다.
    /// </remarks>
    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_committed)
            throw new InvalidOperationException("이미 커밋된 트랜잭션입니다.");
        if (_changes.Count == 0) { _committed = true; return; }

        // 스토어에 일괄 적용
        foreach (var ch in _changes)
        {
            if (ch.NewValue is null)
                _store.Remove(ch.Section, ch.Key);
            else
                _store.Set(ch.Section, ch.Key, ch.NewValue, ch.NewEncrypted);
        }

        _committed = true;
        _strOnCommit?.Invoke(_changes.AsReadOnly());
    }

    /// <summary>
    /// 보류 중인 모든 변경을 취소합니다. 스토어는 변경되지 않습니다.
    /// </summary>
    public void Rollback()
    {
        if (_committed) return;
        _changes.Clear();
        _disposed = true;
    }

    #endregion

    #region §5 ─ IDisposable

    /// <inheritdoc/>
    /// <remarks>커밋하지 않은 상태로 Dispose 되면 자동으로 롤백됩니다.</remarks>
    public void Dispose()
    {
        if (_disposed) return;
        if (!_committed) Rollback();
        _disposed = true;
    }

    #endregion
}

// ── UndoRedoStack ─────────────────────────────────────────────────────────

/// <summary>
/// 설정 변경 Undo/Redo 스택.
/// </summary>
/// <remarks>
/// <see cref="ConfigManager"/> 내부에서 싱글 인스턴스로 관리됩니다.
/// 외부에서 직접 생성하지 않고 <see cref="ConfigManager.Undo"/> /
/// <see cref="ConfigManager.Redo"/> 를 통해 사용합니다.
/// </remarks>
public sealed class UndoRedoStack
{
    #region §1 ─ 필드

    private readonly Stack<IReadOnlyList<ChangeRecord>> _undoStack = new();
    private readonly Stack<IReadOnlyList<ChangeRecord>> _redoStack = new();
    private readonly int _strMaxDepth;

    #endregion

    #region §2 ─ 생성자

    /// <summary>
    /// <see cref="UndoRedoStack"/> 을 생성합니다.
    /// </summary>
    /// <param name="maxDepth">최대 Undo 깊이. 기본 50.</param>
    public UndoRedoStack(int maxDepth = 50)
    {
        _strMaxDepth = maxDepth;
    }

    #endregion

    #region §3 ─ 상태 조회

    /// <summary>Undo 가능 여부.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Redo 가능 여부.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>현재 Undo 스택 깊이.</summary>
    public int UndoDepth => _undoStack.Count;

    /// <summary>현재 Redo 스택 깊이.</summary>
    public int RedoDepth => _redoStack.Count;

    #endregion

    #region §4 ─ Push / Undo / Redo

    /// <summary>
    /// 커밋된 변경 목록을 Undo 스택에 기록합니다. Redo 스택은 초기화됩니다.
    /// </summary>
    internal void Push(IReadOnlyList<ChangeRecord> changes)
    {
        _undoStack.Push(changes);
        _redoStack.Clear();

        // 최대 깊이 초과 시 가장 오래된 항목 제거
        if (_undoStack.Count > _strMaxDepth)
        {
            var temp = _undoStack.ToArray();
            _undoStack.Clear();
            foreach (var item in temp.Take(_strMaxDepth))
                _undoStack.Push(item);
        }
    }

    /// <summary>
    /// Undo — 마지막 커밋을 되돌립니다.
    /// </summary>
    /// <param name="store">되돌릴 대상 저장소.</param>
    /// <returns>되돌린 변경 목록. Undo 불가면 <see langword="null"/>.</returns>
    public IReadOnlyList<ChangeRecord>? Undo(ConfigStore store)
    {
        if (!CanUndo) return null;

        var changes = _undoStack.Pop();
        _redoStack.Push(changes);

        // 역순으로 이전 값 복원
        foreach (var ch in changes.Reverse())
        {
            if (ch.OldValue is null)
                store.Remove(ch.Section, ch.Key);
            else
                store.Set(ch.Section, ch.Key, ch.OldValue, ch.OldEncrypted);
        }
        return changes;
    }

    /// <summary>
    /// Redo — 마지막 Undo 를 다시 적용합니다.
    /// </summary>
    /// <param name="store">적용할 대상 저장소.</param>
    /// <returns>다시 적용된 변경 목록. Redo 불가면 <see langword="null"/>.</returns>
    public IReadOnlyList<ChangeRecord>? Redo(ConfigStore store)
    {
        if (!CanRedo) return null;

        var changes = _redoStack.Pop();
        _undoStack.Push(changes);

        foreach (var ch in changes)
        {
            if (ch.NewValue is null)
                store.Remove(ch.Section, ch.Key);
            else
                store.Set(ch.Section, ch.Key, ch.NewValue, ch.NewEncrypted);
        }
        return changes;
    }

    /// <summary>스택 전체를 초기화합니다.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    #endregion
}