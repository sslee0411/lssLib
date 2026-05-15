// ══════════════════════════════════════════════════════════
//  lssLib.Messaging · EventMessage.cs
//  역할: 이벤트 메시지 기반 추상 레코드
// ══════════════════════════════════════════════════════════

namespace lssLib.Messaging;

/// <summary>
/// 모든 이벤트 메시지의 기반 추상 레코드.
/// EventBus 를 통해 발행·구독되는 모든 메시지가 공통으로 갖는
/// 메타데이터(<see cref="MessageId"/>, <see cref="Timestamp"/>)를 자동으로 부여합니다.
/// </summary>
/// <remarks>
/// record 상속 구조를 사용하므로 사용자 정의 메시지는 위치 매개변수 또는
/// 프로퍼티로 데이터를 선언하기만 하면 불변성·구조적 동등성·분해가 자동 제공됩니다.
/// </remarks>
/// <example><code>
/// // 1. 사용자 정의 이벤트 메시지 선언
/// public record SensorDataEvent(int DeviceId, float Temperature) : EventMessage;
/// public record NetworkStatusEvent(bool IsConnected, string Host) : EventMessage;
/// public record FrameErrorEvent(string Source, string Reason) : EventMessage;
///
/// // 2. 메시지 생성
/// var evt = new SensorDataEvent(DeviceId: 1, Temperature: 42.5f);
/// Console.WriteLine(evt.MessageId);   // "A3F2B1C0"  — 8자리 고유 ID
/// Console.WriteLine(evt.Timestamp);   // 생성 시각 자동 기록
/// </code></example>
public abstract record EventMessage
{
    // §1 ─ 자동 메타데이터

    /// <summary>메시지 고유 ID (8자리 대문자 16진수, 생성 시 자동 부여)</summary>
    public string MessageId { get; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>메시지 생성 시각 (로컬 시간)</summary>
    public DateTime Timestamp { get; } = DateTime.Now;
}