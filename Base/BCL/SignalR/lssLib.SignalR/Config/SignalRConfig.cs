// ══════════════════════════════════════════════════════════════════════
//  lssLib.SignalR · Config/SignalRConfig.cs
//  역할: 호스트/클라이언트 설정 레코드
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.SignalR;

/// <summary>SignalR 허브 호스트(서버) 설정.</summary>
/// <param name="Port">수신 포트 (예: 7878)</param>
/// <param name="HubPath">허브 경로 (기본 "/hub")</param>
public sealed record SignalRHostConfig(int Port, string HubPath = "/hub")
{
    /// <summary>Kestrel 수신 URL (모든 인터페이스 바인딩)</summary>
    public string ListenUrl => $"http://*:{Port}";
}

/// <summary>SignalR 클라이언트 설정.</summary>
/// <param name="Host">서버 호스트명/IP (예: "localhost")</param>
/// <param name="Port">서버 포트</param>
/// <param name="HubPath">허브 경로 (서버와 동일해야 함, 기본 "/hub")</param>
public sealed record SignalRClientConfig(string Host, int Port, string HubPath = "/hub")
{
    /// <summary>허브 접속 URL</summary>
    public string HubUrl => $"http://{Host}:{Port}{HubPath}";
}
