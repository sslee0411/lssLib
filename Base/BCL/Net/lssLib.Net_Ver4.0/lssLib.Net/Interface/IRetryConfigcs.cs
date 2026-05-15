// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Interface/IRetryConfig.cs
//  역할: 재시도 패턴 인터페이스 — Config Lego 브릭 2/4
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 재시도 패턴 인터페이스.
/// </summary>
/// <remarks>
/// <b>환경별 권장값:</b>
/// <list type="bullet">
///   <item><description>RS-485/Serial: <c>RetryTarget.All</c>, Backoff=false (고정 간격)</description></item>
///   <item><description>TCP: <c>RetryTarget.Connect | RetryTarget.Write</c>, Backoff=true</description></item>
///   <item><description>UDP: <c>RetryTarget.None</c>, IsRetryEnabled=false</description></item>
/// </list>
/// </remarks>
public interface IRetryConfig
{
    /// <summary>재시도 활성화 여부. false 이면 RetryTarget 무관하게 재시도 없음.</summary>
    bool IsRetryEnabled { get; }

    /// <summary>재시도 적용 동작 대상 (<see cref="RetryTarget"/> 플래그 조합).</summary>
    RetryTarget RetryTarget { get; }

    /// <summary>최대 재시도 횟수. 0=무제한.</summary>
    int MaxRetries { get; }

    /// <summary>재시도 기본 대기 시간. 
    /// ReconnectBackoff=true 이면 지수 증가.
    /// 재시도를 하면 할수록 대기 시간을 점점 더 길게 늘림 (예: 1s → 2s → 4s → 8s ...).
    /// </summary>
    TimeSpan RetryDelay { get; }
}

/// <summary>재시도를 적용할 동작 대상 플래그.
/// [Flags]
/// 1. 비트 조합 허용 (중복 선택)
/// 일반적인 Enum은 한 가지만 선택하는 '단일 선택'이지만, 
/// [Flags]가 붙으면 2진수 비트 자리수(1, 2, 4, 8...)를 사용하여 
/// 여러 값을 조합할 수 이 있습니다. 예를 들어, Connect(1) | Write(4) = 5 처럼 조합 가능.
/// 2. 가독성 있는 문자열 출력 (ToString)[Flags]가 있고 없고의 가장 큰 차이 중 하나는 출력을 할 때 나타냄.
/// [Flags]가 없을 때: (RetryTarget)3을 출력하면 숫자 3이 나옴.
/// [Flags]가 있을 때: (RetryTarget)3을 출력하면 "Connect, Read"라고 친절하게 문자열로 변환해줌.
/// summary>
[Flags]
public enum RetryTarget
{
    /// <summary>재시도 없음.</summary>
    None = 0,

    /// <summary>접속(Connect) 실패 시 재시도.</summary>
    Connect = 1 << 0,

    /// <summary>읽기(Read) 실패 플래그 (현재: CircuitBreaker 로 처리).</summary>
    Read = 1 << 1,

    /// <summary>쓰기(Write) 실패 시 재접속 후 Critical 로 재투입.</summary>
    Write = 1 << 2,

    /// <summary>모두 재시도. RS-485/Serial 권장.</summary>
    All = Connect | Read | Write
}