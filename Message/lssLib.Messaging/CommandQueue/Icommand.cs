// ══════════════════════════════════════════════════════════
//  lssLib.Messaging · ICommand.cs
//  역할: 커맨드 인터페이스, 우선순위, 추상 기반 클래스
// ══════════════════════════════════════════════════════════

namespace lssLib.Messaging;

#region CommandPriority — 커맨드 우선순위

/// <summary>
/// 커맨드 큐에서의 처리 우선순위.
/// 높은 값일수록 먼저 처리됩니다.
/// </summary>
/// <example><code>
/// public class EmergencyStopCommand : CommandBase
/// {
///     public override CommandPriority Priority => CommandPriority.Critical;
///     public override Task ExecuteAsync(CancellationToken ct) { ... }
/// }
/// </code></example>
public enum CommandPriority
{
    /// <summary>낮음 — 백그라운드 정리, 통계 집계 등 지연 허용 작업</summary>
    Low = 0,

    /// <summary>보통 — 일반 업무 처리 (기본값)</summary>
    Normal = 1,

    /// <summary>높음 — 사용자 입력 응답, 즉시 피드백 필요 작업</summary>
    High = 2,

    /// <summary>긴급 — 비상 정지, 알람, 즉각 처리 필수 작업</summary>
    Critical = 3,
}

#endregion

#region ICommand — 커맨드 인터페이스

/// <summary>
/// 커맨드 큐에서 처리되는 작업 단위 인터페이스.
/// </summary>
/// <remarks>
/// 직접 구현하거나 <see cref="CommandBase"/>를 상속하여 사용합니다.
/// 재사용·상태를 갖는 커맨드는 <see cref="CommandBase"/> 상속을 권장합니다.
/// </remarks>
/// <example><code>
/// public class SaveFrameCommand : CommandBase
/// {
///     private readonly byte[] _frame;
///     private readonly string _path;
///
///     public SaveFrameCommand(byte[] frame, string path) { _frame = frame; _path = path; }
///
///     public override async Task ExecuteAsync(CancellationToken ct)
///     {
///         await File.WriteAllBytesAsync(_path, _frame, ct);
///     }
/// }
///
/// CommandQueue.Instance.Enqueue(new SaveFrameCommand(frame, "output/snap.bin"));
/// </code></example>
public interface ICommand
{
    /// <summary>커맨드 고유 ID (8자리 대문자 16진수, 생성 시 자동 부여)</summary>
    string CommandId { get; }

    /// <summary>처리 우선순위 (기본값: <see cref="CommandPriority.Normal"/>)</summary>
    CommandPriority Priority { get; }

    /// <summary>
    /// 커맨드 로직을 비동기로 실행합니다.
    /// </summary>
    /// <param name="ct">취소 토큰. 취소 시 <see cref="OperationCanceledException"/>을 throw 해야 합니다.</param>
    Task ExecuteAsync(CancellationToken ct);
}

#endregion

#region CommandBase — 추상 기반 클래스

/// <summary>
/// <see cref="ICommand"/>의 추상 기반 클래스.
/// <see cref="CommandId"/> 자동 부여와 기본 <see cref="Priority"/> 설정을 제공합니다.
/// </summary>
/// <remarks>
/// <see cref="Priority"/>를 재정의하여 커맨드 우선순위를 변경할 수 있습니다.
/// </remarks>
/// <example><code>
/// public class ProcessFrameCommand : CommandBase
/// {
///     public override CommandPriority Priority => CommandPriority.High;
///
///     public override Task ExecuteAsync(CancellationToken ct)
///     {
///         // 프레임 처리 로직
///         return Task.CompletedTask;
///     }
/// }
/// </code></example>
public abstract class CommandBase : ICommand
{
    /// <inheritdoc/>
    public string CommandId { get; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <inheritdoc/>
    public virtual CommandPriority Priority => CommandPriority.Normal;

    /// <inheritdoc/>
    public abstract Task ExecuteAsync(CancellationToken ct);
}

#endregion

#region LambdaCommand — 람다 기반 인라인 커맨드

/// <summary>
/// 람다 표현식으로 커맨드를 인라인으로 생성하는 헬퍼 클래스.
/// 별도의 클래스 선언 없이 간단한 작업을 큐에 넣을 때 사용합니다.
/// </summary>
/// <example><code>
/// CommandQueue.Instance.Enqueue(LambdaCommand.Create(async ct =>
/// {
///     await Task.Delay(100, ct);
///     Console.WriteLine("완료");
/// }));
///
/// // 우선순위 지정
/// CommandQueue.Instance.Enqueue(
///     LambdaCommand.Create(() => Console.WriteLine("긴급"), CommandPriority.Critical));
/// </code></example>
public sealed class LambdaCommand : CommandBase
{
    private readonly Func<CancellationToken, Task> _action;

    /// <inheritdoc/>
    public override CommandPriority Priority { get; }

    private LambdaCommand(Func<CancellationToken, Task> action, CommandPriority priority)
    {
        _action = action;
        Priority = priority;
    }

    /// <summary>비동기 람다로 커맨드를 생성합니다.</summary>
    public static LambdaCommand Create(
        Func<CancellationToken, Task> action,
        CommandPriority priority = CommandPriority.Normal)
        => new(action, priority);

    /// <summary>동기 람다로 커맨드를 생성합니다.</summary>
    public static LambdaCommand Create(
        Action action,
        CommandPriority priority = CommandPriority.Normal)
        => new(_ => { action(); return Task.CompletedTask; }, priority);

    /// <inheritdoc/>
    public override Task ExecuteAsync(CancellationToken ct) => _action(ct);
}

#endregion