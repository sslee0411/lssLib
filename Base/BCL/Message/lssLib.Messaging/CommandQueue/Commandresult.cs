// ══════════════════════════════════════════════════════════
//  lssLib.Messaging · CommandResult.cs
//  역할: 커맨드 실행 결과 readonly record struct
// ══════════════════════════════════════════════════════════

namespace lssLib.Messaging;

/// <summary>
/// 커맨드 단일 실행 결과를 표현하는 불변 값 타입.
/// </summary>
/// <param name="CommandId">실행된 커맨드의 <see cref="ICommand.CommandId"/></param>
/// <param name="CommandType">커맨드 클래스 이름</param>
/// <param name="IsSuccess">성공 여부</param>
/// <param name="Elapsed">실행 소요 시간</param>
/// <param name="Error">실패 시 예외 인스턴스 (성공 시 <see langword="null"/>)</param>
/// <example><code>
/// // CommandCompleted 이벤트에서 결과 확인
/// CommandQueue.Instance.CommandCompleted += result =>
/// {
///     if (result.IsSuccess)
///         Console.WriteLine($"[OK] {result.CommandType}  {result.Elapsed.TotalMilliseconds:F0}ms");
///     else
///         Console.WriteLine($"[ERR] {result.CommandType}: {result.Error!.Message}");
/// };
/// </code></example>
public readonly record struct CommandResult(
    string CommandId,
    string CommandType,
    bool IsSuccess,
    TimeSpan Elapsed,
    Exception? Error = null)
{
    /// <summary>실패 여부 (<see cref="IsSuccess"/>의 반전)</summary>
    public bool IsError => !IsSuccess;

    /// <summary>소요 시간 (밀리초 정수)</summary>
    public long ElapsedMs => (long)Elapsed.TotalMilliseconds;

    /// <inheritdoc/>
    public override string ToString() =>
        IsSuccess
            ? $"[OK]  {CommandType} #{CommandId}  {ElapsedMs}ms"
            : $"[ERR] {CommandType} #{CommandId}  {ElapsedMs}ms  → {Error?.Message}";
}