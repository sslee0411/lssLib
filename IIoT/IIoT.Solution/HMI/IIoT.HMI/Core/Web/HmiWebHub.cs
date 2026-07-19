// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Web/HmiWebHub.cs
//  역할: 웹 브라우저 표시용 SignalR Hub.
//        읽기 전용 표시 전용 — 클라이언트→서버 호출 메서드는 없음(ACK/ForceWrite
//        는 이번 Step 범위 밖. "🔧 후속·보류 항목" 참조). HmiWebHostService 가
//        IHubContext<HmiWebHub> 를 통해 "NodesChanged" 이벤트만 Push 한다.
//        (IIoT.Collector SignalR/IIoTHub.cs 패턴 준용 — 다만 HMI 웹 화면은
//        1차로 "표시 전용"만 구현하므로 Hub 자체는 빈 클래스)
//  HM-11: 신규
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

using Microsoft.AspNetCore.SignalR;

namespace IIoT.HMI.Core.Web;

/// <summary>웹 브라우저 표시 전용 Hub — 클라이언트 호출 메서드 없음(Push 전용).</summary>
public sealed class HmiWebHub : Hub
{
}
