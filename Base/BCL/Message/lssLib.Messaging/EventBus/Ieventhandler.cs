// ══════════════════════════════════════════════════════════
//  lssLib.Messaging · IEventHandler.cs
//  역할: 클래스 기반 이벤트 핸들러 인터페이스
// ══════════════════════════════════════════════════════════

namespace lssLib.Messaging;

/// <summary>
/// 특정 메시지 타입 <typeparamref name="T"/>를 처리하는 클래스 기반 핸들러 인터페이스.
/// </summary>
/// <typeparam name="T"><see cref="EventMessage"/>를 상속한 메시지 타입</typeparam>
/// <remarks>
/// 핸들러를 클래스로 캡슐화하거나 DI 컨테이너와 연동할 때 사용합니다.
/// 람다·메서드 구독은 <c>EventBus.Instance.Subscribe&lt;T&gt;(Action&lt;T&gt;)</c> 오버로드가 더 간결합니다.
/// </remarks>
/// <example><code>
/// public class SensorAlertHandler : IEventHandler&lt;SensorDataEvent&gt;
/// {
///     public async Task HandleAsync(SensorDataEvent msg, CancellationToken ct)
///     {
///         if (msg.Temperature > 80f)
///             await AlertService.SendAsync($"과열 감지: {msg.Temperature}°C", ct);
///     }
/// }
///
/// // 등록
/// var sub = EventBus.Instance.Subscribe&lt;SensorDataEvent&gt;(new SensorAlertHandler());
/// // 해제
/// sub.Dispose();
/// </code></example>
public interface IEventHandler<in T> where T : EventMessage
{
    /// <summary>
    /// 수신된 메시지를 비동기로 처리합니다.
    /// </summary>
    /// <param name="message">수신된 이벤트 메시지</param>
    /// <param name="ct">취소 토큰</param>
    Task HandleAsync(T message, CancellationToken ct = default);
}