// ══════════════════════════════════════════════════════════
//  lssLib.Messaging · EventBus.cs
//  역할: 타입 안전 Pub/Sub 싱글톤 이벤트 버스
// ══════════════════════════════════════════════════════════

//using lssLib.Log;
using System.Collections.Concurrent;

namespace lssLib.Messaging;

/// <summary>
/// 타입 안전 발행·구독(Pub/Sub) 싱글톤 이벤트 버스.
/// </summary>
/// <remarks>
/// <para>메시지 타입(<see cref="EventMessage"/> 상속)을 채널로 사용합니다.
/// 같은 타입을 구독한 핸들러 전체에 메시지가 전달되며,
/// 핸들러 간 실행 순서는 구독 등록 순서를 따릅니다.</para>
/// <para>구독 해제: <see cref="Subscribe{T}(Action{T})"/> 반환값(<see cref="IDisposable"/>)을
/// Dispose하거나 <see cref="UnsubscribeAll{T}"/>를 호출합니다.</para>
/// <para>LogManager 연동: 발행·예외를 자동으로 <c>lssLib.Log</c>에 기록합니다.</para>
/// </remarks>
/// <example><code>
/// // 1. 람다 구독
/// var sub = EventBus.Instance.Subscribe&lt;SensorDataEvent&gt;(e =>
///     Console.WriteLine($"온도: {e.Temperature}°C"));
///
/// // 2. 비동기 람다 구독
/// var sub2 = EventBus.Instance.Subscribe&lt;SensorDataEvent&gt;(async e =>
/// {
///     await Task.Delay(10);
///     Console.WriteLine($"[Async] {e.MessageId}");
/// });
///
/// // 3. IEventHandler 구독
/// var sub3 = EventBus.Instance.Subscribe&lt;SensorDataEvent&gt;(new SensorAlertHandler());
///
/// // 4. 동기 발행
/// EventBus.Instance.Publish(new SensorDataEvent(1, 42.5f));
///
/// // 5. 비동기 발행 (핸들러 모두 완료 대기)
/// await EventBus.Instance.PublishAsync(new SensorDataEvent(1, 42.5f));
///
/// // 6. 구독 해제
/// sub.Dispose();
/// </code></example>
public sealed class EventBus
{
    #region §1 ─ 싱글톤

    private static readonly Lazy<EventBus> _lazy = new(() => new EventBus());

    /// <summary>스레드 안전 싱글톤 인스턴스 (Lazy&lt;T&gt; 기반)</summary>
    public static EventBus Instance => _lazy.Value;

    private EventBus() { }

    #endregion

    #region §2 ─ 내부 타입

    private sealed class SubscriptionEntry
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Func<EventMessage, CancellationToken, Task> Handler { get; }

        public SubscriptionEntry(Func<EventMessage, CancellationToken, Task> handler)
            => Handler = handler;
    }

    private sealed class EventSubscription : IDisposable
    {
        private readonly EventBus _bus;
        private readonly Type _messageType;
        private readonly Guid _id;
        private bool _disposed;

        public EventSubscription(EventBus bus, Type messageType, Guid id)
        {
            _bus = bus;
            _messageType = messageType;
            _id = id;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.RemoveSubscription(_messageType, _id);
        }
    }

    #endregion

    #region §3 ─ 필드

    // 타입 → 구독자 목록
    private readonly ConcurrentDictionary<Type, List<SubscriptionEntry>> _subscriptions = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private const string LOG_SOURCE = "EventBus";

    #endregion

    #region §4 ─ 구독 (Subscribe)

    /// <summary>
    /// 동기 핸들러(<see cref="Action{T}"/>)로 메시지 타입 <typeparamref name="T"/>를 구독합니다.
    /// </summary>
    /// <typeparam name="T">구독할 메시지 타입</typeparam>
    /// <param name="handler">메시지 수신 시 호출될 동기 핸들러</param>
    /// <returns>구독 해제용 <see cref="IDisposable"/>. Dispose 시 구독이 자동 해제됩니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/>가 null인 경우</exception>
    /// <example><code>
    /// var sub = EventBus.Instance.Subscribe&lt;SensorDataEvent&gt;(e =>
    ///     TxtStatus.Text = $"{e.Temperature:F1}°C");
    ///
    /// // UserControl 언로드 시 해제
    /// sub.Dispose();
    /// </code></example>
    public IDisposable Subscribe<T>(Action<T> handler) where T : EventMessage
    {
        ArgumentNullException.ThrowIfNull(handler);

        return AddSubscription<T>((msg, _) =>
        {
            handler((T)msg);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 비동기 핸들러(<see cref="Func{T, Task}"/>)로 메시지 타입 <typeparamref name="T"/>를 구독합니다.
    /// </summary>
    /// <typeparam name="T">구독할 메시지 타입</typeparam>
    /// <param name="handler">메시지 수신 시 호출될 비동기 핸들러</param>
    /// <returns>구독 해제용 <see cref="IDisposable"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/>가 null인 경우</exception>
    public IDisposable Subscribe<T>(Func<T, Task> handler) where T : EventMessage
    {
        ArgumentNullException.ThrowIfNull(handler);

        return AddSubscription<T>((msg, _) => handler((T)msg));
    }

    /// <summary>
    /// 비동기 핸들러(<see cref="Func{T, CancellationToken, Task}"/>)로 메시지 타입 <typeparamref name="T"/>를 구독합니다.
    /// </summary>
    /// <typeparam name="T">구독할 메시지 타입</typeparam>
    /// <param name="handler">취소 토큰을 포함한 비동기 핸들러</param>
    /// <returns>구독 해제용 <see cref="IDisposable"/></returns>
    public IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : EventMessage
    {
        ArgumentNullException.ThrowIfNull(handler);

        return AddSubscription<T>((msg, ct) => handler((T)msg, ct));
    }

    /// <summary>
    /// <see cref="IEventHandler{T}"/> 구현 인스턴스로 메시지 타입 <typeparamref name="T"/>를 구독합니다.
    /// </summary>
    /// <typeparam name="T">구독할 메시지 타입</typeparam>
    /// <param name="handler"><see cref="IEventHandler{T}"/> 구현 인스턴스</param>
    /// <returns>구독 해제용 <see cref="IDisposable"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/>가 null인 경우</exception>
    public IDisposable Subscribe<T>(IEventHandler<T> handler) where T : EventMessage
    {
        ArgumentNullException.ThrowIfNull(handler);

        return AddSubscription<T>((msg, ct) => handler.HandleAsync((T)msg, ct));
    }

    #endregion

    #region §5 ─ 발행 (Publish)

    /// <summary>
    /// 메시지를 동기 발행합니다. 모든 구독자의 핸들러를 순서대로 호출하며,
    /// 비동기 핸들러는 <c>.GetAwaiter().GetResult()</c>로 블로킹 처리됩니다.
    /// </summary>
    /// <typeparam name="T">발행할 메시지 타입</typeparam>
    /// <param name="message">발행할 메시지 인스턴스</param>
    /// <remarks>UI 스레드에서 호출 시 비동기 핸들러의 블로킹으로 인한 데드락 위험이 있습니다.
    /// UI 컨텍스트에서는 <see cref="PublishAsync{T}"/>를 사용하세요.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="message"/>가 null인 경우</exception>
    public void Publish<T>(T message) where T : EventMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var handlers = GetHandlers(typeof(T));
        if (handlers.Count == 0) return;

        //LogManager.Instance.Debug(LOG_SOURCE,
        //    $"Publish [{typeof(T).Name}] id={message.MessageId}  handlers={handlers.Count}");

        foreach (var entry in handlers)
        {
            try
            {
                entry.Handler(message, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
            //    LogManager.Instance.Error(LOG_SOURCE,
            //        $"핸들러 예외 [{typeof(T).Name}] id={message.MessageId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 메시지를 비동기 발행합니다. 모든 구독자의 핸들러를 병렬로 실행하고
    /// 전체 완료를 대기합니다.
    /// </summary>
    /// <typeparam name="T">발행할 메시지 타입</typeparam>
    /// <param name="message">발행할 메시지 인스턴스</param>
    /// <param name="ct">취소 토큰</param>
    /// <remarks>
    /// 각 핸들러는 독립적으로 실행되므로 한 핸들러의 예외가 다른 핸들러의 실행에 영향을 주지 않습니다.
    /// 예외는 <c>lssLib.Log</c>에 기록되고 무시됩니다.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="message"/>가 null인 경우</exception>
    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : EventMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var handlers = GetHandlers(typeof(T));
        if (handlers.Count == 0) return;

     //   LogManager.Instance.Debug(LOG_SOURCE,
     //       $"PublishAsync [{typeof(T).Name}] id={message.MessageId}  handlers={handlers.Count}");

        var tasks = handlers.Select(entry => SafeInvokeAsync(entry, message, ct));
        await Task.WhenAll(tasks);
    }

    #endregion

    #region §6 ─ 구독 해제

    /// <summary>
    /// 지정한 메시지 타입 <typeparamref name="T"/>의 구독자 전체를 해제합니다.
    /// </summary>
    /// <typeparam name="T">구독 해제할 메시지 타입</typeparam>
    public void UnsubscribeAll<T>() where T : EventMessage
    {
        _lock.EnterWriteLock();
        try
        {
            _subscriptions.TryRemove(typeof(T), out _);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

    //    LogManager.Instance.Debug(LOG_SOURCE, $"UnsubscribeAll [{typeof(T).Name}]");
    }

    /// <summary>모든 타입의 구독자를 전체 해제합니다.</summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _subscriptions.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

    //    LogManager.Instance.Debug(LOG_SOURCE, "Clear — 전체 구독 해제");
    }

    #endregion

    #region §7 ─ 상태 조회

    /// <summary>특정 메시지 타입의 현재 구독자 수를 반환합니다.</summary>
    /// <typeparam name="T">조회할 메시지 타입</typeparam>
    public int GetSubscriberCount<T>() where T : EventMessage
        => GetHandlers(typeof(T)).Count;

    /// <summary>등록된 전체 구독 수를 반환합니다.</summary>
    public int TotalSubscriptions
    {
        get
        {
            _lock.EnterReadLock();
            try { return _subscriptions.Values.Sum(l => l.Count); }
            finally { _lock.ExitReadLock(); }
        }
    }

    #endregion

    #region §8 ─ 내부 메서드

    private IDisposable AddSubscription<T>(
        Func<EventMessage, CancellationToken, Task> wrappedHandler)
        where T : EventMessage
    {
        var entry = new SubscriptionEntry(wrappedHandler);
        var type = typeof(T);

        _lock.EnterWriteLock();
        try
        {
            var list = _subscriptions.GetOrAdd(type, _ => []);
            list.Add(entry);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

     //   LogManager.Instance.Debug(LOG_SOURCE,
     //       $"Subscribe [{type.Name}] id={entry.Id:N}[..8]");

        return new EventSubscription(this, type, entry.Id);
    }

    private void RemoveSubscription(Type messageType, Guid id)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_subscriptions.TryGetValue(messageType, out var list))
                list.RemoveAll(e => e.Id == id);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private List<SubscriptionEntry> GetHandlers(Type messageType)
    {
        _lock.EnterReadLock();
        try
        {
            return _subscriptions.TryGetValue(messageType, out var list)
                ? [.. list]   // 스냅샷 복사 — 반복 중 변경 방지
                : [];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private async Task SafeInvokeAsync(
        SubscriptionEntry entry, EventMessage message, CancellationToken ct)
    {
        try
        {
            await entry.Handler(message, ct);
        }
        catch (Exception ex)
        {
    //        LogManager.Instance.Error(LOG_SOURCE,
    //            $"핸들러 예외 [{message.GetType().Name}] id={message.MessageId}: {ex.Message}");
        }
    }

    #endregion
}