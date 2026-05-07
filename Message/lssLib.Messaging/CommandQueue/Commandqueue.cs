// ══════════════════════════════════════════════════════════
//  lssLib.Messaging · CommandQueue.cs
//  역할: 우선순위 기반 비동기 커맨드 큐 (싱글톤)
// ══════════════════════════════════════════════════════════

//using lssLib.Log;
using System.Diagnostics;

namespace lssLib.Messaging;

/// <summary>
/// 우선순위 기반 비동기 커맨드 큐 싱글톤.
/// </summary>
/// <remarks>
/// <para>내부적으로 <see cref="PriorityQueue{TElement, TPriority}"/> + <see cref="SemaphoreSlim"/>을
/// 조합하여 구현합니다. 높은 <see cref="CommandPriority"/> 값의 커맨드가 먼저 처리됩니다.</para>
/// <para>단일 소비자(<c>ProcessLoopAsync</c>) 패턴으로 커맨드는 순차 실행됩니다.
/// 병렬 처리가 필요하면 <see cref="MaxConcurrency"/>를 늘리세요.</para>
/// <para>실행 결과는 <see cref="CommandCompleted"/> 이벤트로 통보됩니다.</para>
/// </remarks>
/// <example><code>
/// // 1. 시작
/// CommandQueue.Instance.Start();
///
/// // 2. 커맨드 등록
/// CommandQueue.Instance.Enqueue(new SaveFrameCommand(frame, "out/snap.bin"));
///
/// // 3. 람다 인라인 커맨드
/// CommandQueue.Instance.Enqueue(LambdaCommand.Create(async ct =>
/// {
///     await Task.Delay(500, ct);
///     Console.WriteLine("백그라운드 작업 완료");
/// }, CommandPriority.Low));
///
/// // 4. 완료 이벤트 구독
/// CommandQueue.Instance.CommandCompleted += r =>
///     Console.WriteLine(r.ToString());
///
/// // 5. 종료 (큐 소진 후 정지)
/// await CommandQueue.Instance.StopAsync();
/// </code></example>
public sealed class CommandQueue
{
    #region §1 ─ 싱글톤

    private static readonly Lazy<CommandQueue> _lazy = new(() => new CommandQueue());

    /// <summary>스레드 안전 싱글톤 인스턴스 (Lazy&lt;T&gt; 기반)</summary>
    public static CommandQueue Instance => _lazy.Value;

    private CommandQueue() { }

    #endregion

    #region §2 ─ 필드

    // 우선순위 큐: 높은 Priority 값 = 작은 숫자 키 → 먼저 꺼냄
    private readonly PriorityQueue<ICommand, int> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _queueLock = new();

    private CancellationTokenSource? _cts;
    private Task? _processTask;
    private bool _running;

    private const string LOG_SOURCE = "CommandQueue";

    #endregion

    #region §3 ─ 공개 프로퍼티

    /// <summary>동시 처리 수 (기본값: 1 = 순차 처리). Start() 호출 전에 설정해야 합니다.</summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>현재 큐에 대기 중인 커맨드 수</summary>
    public int PendingCount
    {
        get { lock (_queueLock) return _queue.Count; }
    }

    /// <summary>처리된 커맨드 누적 수</summary>
    public long ProcessedCount { get; private set; }

    /// <summary>실패한 커맨드 누적 수</summary>
    public long FailedCount { get; private set; }

    /// <summary>큐 동작 여부</summary>
    public bool IsRunning => _running;

    #endregion

    #region §4 ─ 이벤트

    /// <summary>
    /// 커맨드 실행 완료 시 발생 (성공·실패 모두).
    /// ※ 호출 스레드: 백그라운드 Task → UI 접근 시 Dispatcher 필요
    /// </summary>
    public event Action<CommandResult>? CommandCompleted;

    #endregion

    #region §5 ─ Start / Stop

    /// <summary>
    /// 커맨드 큐를 시작합니다. 내부 처리 루프 Task를 구동합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">이미 실행 중인 경우</exception>
    public void Start()
    {
        if (_running)
            throw new InvalidOperationException("CommandQueue 가 이미 실행 중입니다.");

        _cts = new CancellationTokenSource();
        _running = true;
        _processTask = RunProcessLoopsAsync(_cts.Token);

        //LogManager.Instance.Info(LOG_SOURCE,
        //    $"Start — MaxConcurrency={MaxConcurrency}");
    }

    /// <summary>
    /// 커맨드 큐를 비동기로 정지합니다.
    /// 현재 실행 중인 커맨드가 완료된 후 루프가 종료됩니다.
    /// 대기 중인 커맨드는 취소됩니다.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_running || _cts is null) return;

        //LogManager.Instance.Info(LOG_SOURCE,
        //    $"Stop 요청 — 대기 중={PendingCount}");

        _cts.Cancel();
        _running = false;

        if (_processTask is not null)
        {
            try { await _processTask; }
            catch (OperationCanceledException) { /* 정상 종료 */ }
        }

        //LogManager.Instance.Info(LOG_SOURCE,
        //    $"Stop 완료 — 처리={ProcessedCount}  실패={FailedCount}");
    }

    #endregion

    #region §6 ─ 커맨드 등록

    /// <summary>
    /// 커맨드를 큐에 등록합니다.
    /// </summary>
    /// <param name="command">등록할 커맨드</param>
    /// <exception cref="ArgumentNullException"><paramref name="command"/>가 null인 경우</exception>
    /// <exception cref="InvalidOperationException">큐가 시작되지 않은 경우</exception>
    /// <example><code>
    /// CommandQueue.Instance.Enqueue(new ParseFrameCommand(frame));
    /// CommandQueue.Instance.Enqueue(LambdaCommand.Create(() => SaveLog(), CommandPriority.Low));
    /// </code></example>
    public void Enqueue(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_running)
            throw new InvalidOperationException("CommandQueue.Start() 를 먼저 호출하세요.");

        // PriorityQueue 는 최솟값 먼저 꺼내므로 우선순위를 음수로 변환
        int key = -(int)command.Priority;

        lock (_queueLock)
            _queue.Enqueue(command, key);

        _signal.Release();

        //LogManager.Instance.Debug(LOG_SOURCE,
        //    $"Enqueue [{command.GetType().Name}] id={command.CommandId}  priority={command.Priority}");
    }

    /// <summary>
    /// 현재 대기 중인 모든 커맨드를 큐에서 제거합니다.
    /// 현재 실행 중인 커맨드는 영향을 받지 않습니다.
    /// </summary>
    public void Clear()
    {
        int removed;
        lock (_queueLock)
        {
            removed = _queue.Count;
            _queue.Clear();
        }
        //LogManager.Instance.Info(LOG_SOURCE, $"Clear — {removed}개 커맨드 제거");
    }

    #endregion

    #region §7 ─ 내부 처리 루프

    private Task RunProcessLoopsAsync(CancellationToken ct)
    {
        // MaxConcurrency 만큼 독립 루프 Task 생성
        var loops = Enumerable.Range(0, MaxConcurrency)
            .Select(i => ProcessLoopAsync(i, ct))
            .ToArray();

        return Task.WhenAll(loops);
    }

    private async Task ProcessLoopAsync(int workerId, CancellationToken ct)
    {
        //LogManager.Instance.Debug(LOG_SOURCE, $"Worker#{workerId} 시작");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 큐에 항목이 생길 때까지 대기
                await _signal.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            ICommand? command;
            lock (_queueLock)
                _queue.TryDequeue(out command, out _);

            if (command is null) continue;

            await ExecuteCommandAsync(command, ct);
        }

        //LogManager.Instance.Debug(LOG_SOURCE, $"Worker#{workerId} 종료");
    }

    private async Task ExecuteCommandAsync(ICommand command, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        //LogManager.Instance.Debug(LOG_SOURCE,
        //    $"Execute [{command.GetType().Name}] id={command.CommandId}  priority={command.Priority}");

        CommandResult result;
        try
        {
            await command.ExecuteAsync(ct);
            sw.Stop();

            ProcessedCount++;
            result = new CommandResult(
                CommandId: command.CommandId,
                CommandType: command.GetType().Name,
                IsSuccess: true,
                Elapsed: sw.Elapsed);

        //    LogManager.Instance.Debug(LOG_SOURCE,
        //        $"Done [{command.GetType().Name}] id={command.CommandId}  {result.ElapsedMs}ms");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            result = new CommandResult(
                CommandId: command.CommandId,
                CommandType: command.GetType().Name,
                IsSuccess: false,
                Elapsed: sw.Elapsed,
                Error: new OperationCanceledException("커맨드 취소됨"));

        //    LogManager.Instance.Warn(LOG_SOURCE,
        //        $"Cancelled [{command.GetType().Name}] id={command.CommandId}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            FailedCount++;
            result = new CommandResult(
                CommandId: command.CommandId,
                CommandType: command.GetType().Name,
                IsSuccess: false,
                Elapsed: sw.Elapsed,
                Error: ex);

        //    LogManager.Instance.Error(LOG_SOURCE,
        //        $"Error [{command.GetType().Name}] id={command.CommandId}: {ex.Message}");
        }

        // 완료 이벤트 발행
        try { CommandCompleted?.Invoke(result); }
        catch (Exception ex)
        {
        //    LogManager.Instance.Error(LOG_SOURCE,
        //        $"CommandCompleted 핸들러 예외: {ex.Message}");
        }
    }

    #endregion
}