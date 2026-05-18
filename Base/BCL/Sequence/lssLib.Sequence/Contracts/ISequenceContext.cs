// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Contracts/ISequenceContext.cs
//  역할: 시퀀스 실행 컨텍스트 인터페이스
//
//  ┌─ 설계 의도 ────────────────────────────────────────────────────┐
//  │                                                                 │
//  │  ISequenceContext 는 ISequenceStep 이 실행 중에 필요한          │
//  │  모든 외부 자원에 접근하는 "관문" 역할을 합니다.                │
//  │                                                                 │
//  │  lssLib.Net 에서는:                                             │
//  │    GetDevice(id) → NetChannelBase 반환                          │
//  │                                                                 │
//  │  Node-RED 스타일에서는:                                         │
//  │    GetDevice(id) → 노드 연결 정보 반환                         │
//  │                                                                 │
//  │  DB 작업 시퀀스에서는:                                          │
//  │    GetDevice(id) → DbConnection 반환                           │
//  │                                                                 │
//  │  → 구현체가 바뀌어도 ISequenceStep 코드는 변경 없음             │
//  └─────────────────────────────────────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Sequence;

/// <summary>
/// 시퀀스 실행 컨텍스트 인터페이스.
/// <see cref="ISequenceStep"/> 이 실행 중에 외부 자원에 접근하는 관문입니다.
/// </summary>
/// <remarks>
/// <para>
/// 구현체는 실행 환경에 따라 달라집니다:
/// <list type="table">
///   <item><term>lssLib.Net</term><description>NetSequenceContext → NetDeviceRegistry 를 통해 NetChannelBase 반환</description></item>
///   <item><term>Node-RED 스타일</term><description>FlowContext → 노드 연결 정보 반환</description></item>
///   <item><term>DB 시퀀스</term><description>DbSequenceContext → DbConnection 반환</description></item>
/// </list>
/// </para>
///
/// <b>구현 예시 (lssLib.Net):</b>
/// <code>
/// public class NetSequenceContext : ISequenceContext
/// {
///     public object? GetDevice(int deviceId)
///         => NetDeviceRegistry.Instance.Get(deviceId);
///
///     public void Log(string message) =>
///         LogManager.Instance.Info("Sequence", message);
/// }
/// </code>
/// </remarks>
public interface ISequenceContext
{
    #region §1 ─ 장비 접근

    /// <summary>
    /// DeviceId 로 장치/자원 객체를 반환합니다.
    /// <para>lssLib.Net: NetChannelBase 반환.</para>
    /// <para>없으면 null 반환.</para>
    /// </summary>
    object? GetDevice(int deviceId);

    /// <summary>
    /// DeviceId 로 연결 상태를 확인합니다.
    /// <para>lssLib.Net: channel.IsConnected 반환.</para>
    /// </summary>
    bool IsDeviceConnected(int deviceId);

    #endregion

    #region §2 ─ 변수 저장소 (스텝 간 데이터 전달)

    /// <summary>
    /// 변수를 저장합니다. 동일 key 는 덮어씁니다.
    /// <para>스텝 간 데이터 전달, 조건 분기 등에 활용합니다.</para>
    /// <example><code>
    /// // RequestStep 에서 응답 값 저장
    /// context.SetVariable("motor_speed", 1500);
    ///
    /// // 다음 스텝에서 읽기
    /// int speed = context.GetVariable<int>("motor_speed");
    /// </code></example>
    /// </summary>
    void SetVariable(string key, object? value);

    /// <summary>
    /// 저장된 변수를 반환합니다. 없으면 default(T) 반환.
    /// </summary>
    T? GetVariable<T>(string key);

    /// <summary>
    /// 저장된 변수가 있는지 확인합니다.
    /// </summary>
    bool HasVariable(string key);

    /// <summary>
    /// 모든 변수를 삭제합니다.
    /// </summary>
    void ClearVariables();

    #endregion

    #region §3 ─ 로그

    /// <summary>
    /// 정보성 로그를 기록합니다.
    /// <para>lssLib.Log 연동 시: LogManager.Instance.Info("Sequence", message).</para>
    /// </summary>
    void Log(string message);

    /// <summary>
    /// 오류 로그를 기록합니다.
    /// </summary>
    void LogError(string message);

    #endregion
}