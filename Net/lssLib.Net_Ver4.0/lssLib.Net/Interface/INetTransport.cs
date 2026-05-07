// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Interface/INetTransport.cs
//  역할: 전송 계층 인터페이스
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 전송 계층 인터페이스.
/// TCP / UDP / Serial / 공유 메모리 등 모든 통신 리소스는 이 인터페이스를 구현합니다.
/// </summary>
/// <remarks>
/// 직접 구현보다 <see cref="NetTransportBase"/> 상속을 권장합니다.
/// 상태 관리·이벤트·로그가 자동으로 처리됩니다.
/// </remarks>
public interface INetTransport : IAsyncDisposable
{
    /// <summary>현재 연결 상태.</summary>
    NetState State { get; }

    /// <summary>
    /// 연결 상태 변경 시 발생.
    /// <para>⚠ 백그라운드 스레드 — WPF UI 접근 시 Dispatcher.InvokeAsync 필요.</para>
    /// </summary>
    event Action<NetState>? StateChanged;

    /// <summary>
    /// 원시 데이터 수신 시 발생 (Passive 수신 모드).
    /// <para>프레임 조립은 <see cref="INetProtocol"/> 에서 담당합니다.</para>
    /// </summary>
    event Action<byte[]>? DataReceived;

    /// <summary>비동기 연결을 시도합니다. 실패 시 예외 throw.</summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>연결을 해제합니다. 이미 끊어진 상태에서도 예외 없음.</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>인코딩 완료된 바이트 배열을 전송합니다.</summary>
    Task WriteAsync(byte[] data, CancellationToken ct = default);

    /// <summary>고정 길이 바이트를 수신합니다.</summary>
    Task<byte[]> ReadAsync(int length, CancellationToken ct = default);

    /// <summary>프레임 1개를 수신합니다 (기본: 내부 버퍼 크기만큼 읽기).</summary>
    Task<byte[]> ReadAsync(CancellationToken ct = default);
}