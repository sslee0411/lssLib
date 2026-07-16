// ══════════════════════════════════════════════════════════════════════
//  lssLib.SignalR.Demo · Program.cs
//  역할: lssLib.SignalR 데모 메뉴 (lssLib.Net.Demo 와 동일한 콘솔 메뉴 형식)
//    1  셀프 테스트 ★ 외부 서버 불필요 — 호스트+클라 2개를 한 프로세스에서
//       기동해 구독/발행/핑 왕복을 검증
//    2  서버만 실행 (BroadcastHub 호스팅 — 다른 PC/프로세스의 클라 테스트용)
//    3  클라이언트만 실행 (실행 중인 서버에 접속 — 구독 수신 + 콘솔 입력 발행)
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════════════════

using lssLib.SignalR;
// ★ IHubContext.Clients.All.SendAsync 확장 메서드용 (셀프 테스트 ③)
//   — FrameworkReference 는 ProjectReference 를 통해 전이되므로 참조 가능
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;

Console.WriteLine("lssLib.SignalR v1.0 — Hub 호스트/클라이언트 데모");
Console.WriteLine("────────────────────────────────────────────────");
Console.WriteLine("  1   셀프 테스트 ★ 외부 서버 불필요 (호스트+클라 2개)");
Console.WriteLine("  2   서버만 실행   (BroadcastHub 호스팅)");
Console.WriteLine("  3   클라이언트만  (서버 접속 → 구독 수신 + 발행)");
Console.WriteLine("────────────────────────────────────────────────");
Console.Write("입력 (기본=1): ");

var input = Console.ReadLine()?.Trim();

try
{
    await (input switch
    {
        "2" => RunServerAsync(),
        "3" => RunClientAsync(),
        _   => RunSelfTestAsync(),
    });
}
catch (Exception ex)
{
    Console.WriteLine($"[오류] {ex.Message}");
}

// ══════════════════════════════════════════════════════════════════════
//  1. 셀프 테스트 — 호스트 + 클라이언트 A(구독)/B(발행)
// ══════════════════════════════════════════════════════════════════════
static async Task RunSelfTestAsync()
{
    const int    port  = 7890;
    const string topic = "demo/tag";

    Console.WriteLine($"\n[호스트] BroadcastHub 기동 — 포트 {port}");
    await using var host = new SignalRHostService<BroadcastHub>(new SignalRHostConfig(port));
    await host.StartAsync();

    // ── 클라이언트 A: 구독자 ────────────────────────────────
    await using var clientA = new SignalRClientConnection(
        new SignalRClientConfig("localhost", port));

    var received = new TaskCompletionSource<(string Topic, string Payload)>();
    clientA.On<string, string>(BroadcastHub.ReceiveMethod,
        (t, p) => received.TrySetResult((t, p)));

    await clientA.StartAsync(retryCount: 3);
    await clientA.InvokeAsync("Subscribe", topic);
    Console.WriteLine($"[클라A] 접속 + \"{topic}\" 구독 완료");

    // ── 클라이언트 B: 발행자 ────────────────────────────────
    await using var clientB = new SignalRClientConnection(
        new SignalRClientConfig("localhost", port));
    await clientB.StartAsync(retryCount: 3);
    Console.WriteLine("[클라B] 접속 완료");

    // ① 핑 왕복 시간
    var sw   = Stopwatch.StartNew();
    var pong = await clientB.InvokeAsync<string>("Ping");
    sw.Stop();
    Console.WriteLine($"[클라B] Ping → {pong} ({sw.ElapsedMilliseconds} ms)");

    // ② 토픽 발행 → A 수신 검증
    var payload = $"value=42.5 @{DateTime.Now:HH:mm:ss.fff}";
    await clientB.InvokeAsync("Publish", topic, payload);
    Console.WriteLine($"[클라B] Publish(\"{topic}\") → \"{payload}\"");

    var done = await Task.WhenAny(received.Task, Task.Delay(3000));
    if (done == received.Task)
    {
        var (t, p) = received.Task.Result;
        Console.WriteLine($"[클라A] 수신 ✅ topic=\"{t}\" payload=\"{p}\"");
        Console.WriteLine("\n★ 셀프 테스트 성공 — 호스트/구독/발행/핑 모두 정상");
    }
    else
    {
        Console.WriteLine("[클라A] 수신 실패 ❌ (3초 타임아웃)");
    }

    // ③ 서버 → 클라이언트 직접 발행 (IHubContext 사용 예)
    if (host.HubContext is not null)
    {
        await host.HubContext.Clients.All.SendAsync(
            BroadcastHub.ReceiveMethod, "*", "서버가 직접 보낸 메시지");
        Console.WriteLine("[호스트] IHubContext 로 전체 발행 완료");
    }

    await Task.Delay(300);   // 마지막 수신 여유
    Console.WriteLine("정리 후 종료합니다.");
}

// ══════════════════════════════════════════════════════════════════════
//  2. 서버만 실행
// ══════════════════════════════════════════════════════════════════════
static async Task RunServerAsync()
{
    Console.Write("포트 (기본 7890): ");
    var port = int.TryParse(Console.ReadLine(), out var p) ? p : 7890;

    // ★ 서버 콘솔에 트래픽 표시 — 접속/해제/구독/발행/핑 전부 관찰
    BroadcastHub.TrafficLogged += Console.WriteLine;

    await using var host = new SignalRHostService<BroadcastHub>(new SignalRHostConfig(port));
    await host.StartAsync();

    Console.WriteLine($"\n[호스트] BroadcastHub 실행 중 — http://*:{port}/hub");
    Console.WriteLine("클라이언트 접속을 기다립니다. 트래픽이 아래에 표시됩니다. 종료: Enter");
    Console.ReadLine();

    // ★ 정적 이벤트 구독 해제 (규칙: static 이벤트 미해제 = 구독 누수)
    BroadcastHub.TrafficLogged -= Console.WriteLine;
}

// ══════════════════════════════════════════════════════════════════════
//  3. 클라이언트만 실행
// ══════════════════════════════════════════════════════════════════════
static async Task RunClientAsync()
{
    Console.Write("서버 호스트 (기본 localhost): ");
    var hostName = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(hostName)) hostName = "localhost";

    Console.Write("포트 (기본 7890): ");
    var port = int.TryParse(Console.ReadLine(), out var p) ? p : 7890;

    Console.Write("구독 토픽 (기본 demo/tag): ");
    var topic = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(topic)) topic = "demo/tag";

    await using var client = new SignalRClientConnection(
        new SignalRClientConfig(hostName, port));

    client.Connected    += ()  => Console.WriteLine("[상태] 연결됨");
    client.Reconnecting += msg => Console.WriteLine($"[상태] 재연결 중… ({msg})");
    client.Reconnected  += ()  => Console.WriteLine("[상태] 재연결 성공");
    client.Closed       += msg => Console.WriteLine($"[상태] 연결 종료 ({msg})");

    client.On<string, string>(BroadcastHub.ReceiveMethod,
        (t, payload) => Console.WriteLine($"[수신] {t} ← \"{payload}\""));

    Console.WriteLine($"\n{new SignalRClientConfig(hostName, port).HubUrl} 접속 시도…");
    await client.StartAsync(retryCount: 5, retrySec: 2);
    await client.InvokeAsync("Subscribe", topic);
    Console.WriteLine($"\"{topic}\" 구독 완료 — 입력한 내용이 이 토픽으로 발행됩니다. 종료: 빈 줄 Enter");

    while (true)
    {
        var line = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(line)) break;
        await client.InvokeAsync("Publish", topic, line);
    }
}
