// ══════════════════════════════════════════════════════════════════════
//  lssLib.SignalR · Hubs/BroadcastHub.cs
//  역할: 범용 토픽 발행/구독(Pub/Sub) 허브 — 별도 허브 구현 없이
//        바로 쓸 수 있는 기본 제공 허브.
//  프로토콜 (클라이언트 수신 메서드명: "Receive"):
//    Subscribe(topic)        → 해당 토픽 그룹 가입
//    Unsubscribe(topic)      → 그룹 탈퇴
//    Publish(topic, payload) → 그룹 구독자 전원에게 Receive(topic, payload)
//    BroadcastAll(payload)   → 전체에게 Receive("*", payload)
//    Ping()                  → "pong" 반환 (왕복 시간 측정용)
//  수정(2026-07-09): TrafficLogged 정적 이벤트 추가 — 서버 측에서
//    접속/해제/구독/발행 내역을 관찰할 수 있는 훅 (데모·디버깅용.
//    미구독 시 부하 없음. 허브는 호출마다 새 인스턴스로 생성되므로
//    인스턴스 이벤트로는 불가 → 정적 이벤트 사용)
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.SignalR;

namespace lssLib.SignalR;

/// <summary>범용 토픽 Pub/Sub 허브 (기본 제공).</summary>
public class BroadcastHub : Hub
{
    /// <summary>클라이언트 수신 메서드명 상수 — 클라이언트 On() 등록 시 사용.</summary>
    public const string ReceiveMethod = "Receive";

    /// <summary>
    /// ★ 서버 측 트래픽 관찰 훅 (접속/해제/구독/발행 1줄 요약).
    /// 데모·디버깅용 — 예: <c>BroadcastHub.TrafficLogged += Console.WriteLine;</c>
    /// 핸들러는 SignalR 워커 스레드에서 호출됨 — UI 갱신 시 마샬링 필요.
    /// </summary>
    public static event Action<string>? TrafficLogged;

    private static void _Log(string msg) =>
        TrafficLogged?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");

    /// <summary>짧은 연결 ID (표시용 앞 8자)</summary>
    private string _Cid => Context.ConnectionId.Length > 8
        ? Context.ConnectionId[..8]
        : Context.ConnectionId;

    // ── 연결 수명 ─────────────────────────────────────────

    public override Task OnConnectedAsync()
    {
        _Log($"접속     {_Cid}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _Log($"해제     {_Cid}{(exception is null ? "" : $" ({exception.Message})")}");
        return base.OnDisconnectedAsync(exception);
    }

    // ── Pub/Sub ──────────────────────────────────────────

    /// <summary>토픽 구독 (그룹 가입).</summary>
    public Task Subscribe(string topic)
    {
        _Log($"구독     {_Cid} → \"{topic}\"");
        return Groups.AddToGroupAsync(Context.ConnectionId, topic);
    }

    /// <summary>토픽 구독 해제 (그룹 탈퇴).</summary>
    public Task Unsubscribe(string topic)
    {
        _Log($"구독해제 {_Cid} → \"{topic}\"");
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, topic);
    }

    /// <summary>토픽 구독자 전원에게 발행.</summary>
    public Task Publish(string topic, string payload)
    {
        _Log($"발행     {_Cid} → \"{topic}\" : \"{payload}\"");
        return Clients.Group(topic).SendAsync(ReceiveMethod, topic, payload);
    }

    /// <summary>접속자 전원에게 발행 (토픽 "*").</summary>
    public Task BroadcastAll(string payload)
    {
        _Log($"전체발행 {_Cid} : \"{payload}\"");
        return Clients.All.SendAsync(ReceiveMethod, "*", payload);
    }

    /// <summary>왕복 시간 측정용 핑.</summary>
    public string Ping()
    {
        _Log($"핑       {_Cid}");
        return "pong";
    }
}
