// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/RetryTarget.cs
//  역할: 재시도 동작 대상 플래그 열거형
//
//  v5 변경사항:
//    · IRetryConfig 인터페이스 제거 → NetDeviceConfig 에 직접 포함
//    · RetryTarget.Read 제거 (내부 구현에서 미사용)
//    · ConnectAndWrite 복합 프리셋 추가
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 재시도를 적용할 동작 대상 플래그.
/// <para><c>|</c> 연산으로 복수 대상을 조합할 수 있습니다.</para>
/// </summary>
/// <example><code>
/// // TCP 환경 (기본 권장)
/// cfg.RetryTarget = RetryTarget.ConnectAndWrite;
///
/// // Connect 만 (Write 실패는 무시)
/// cfg.RetryTarget = RetryTarget.Connect;
///
/// // 재시도 완전 비활성
/// cfg.IsRetryEnabled = false;   // RetryTarget 무관
/// </code></example>
[Flags]
public enum RetryTarget
{
    /// <summary>재시도 없음.</summary>
    None = 0,

    /// <summary>접속(Connect) 실패 시 재시도.</summary>
    Connect = 1 << 0,

    /// <summary>쓰기(Write) 실패 시 재접속 후 Critical 로 재투입.</summary>
    Write = 1 << 1,

    /// <summary>
    /// Connect + Write 모두 재시도 (기본 권장값).
    /// <para>TCP / Serial 환경에서 일반적으로 사용합니다.</para>
    /// </summary>
    ConnectAndWrite = Connect | Write,

    /// <summary>
    /// ConnectAndWrite 와 동일. RS-485 / Serial 환경 명시적 표현에 사용.
    /// </summary>
    All = ConnectAndWrite
}