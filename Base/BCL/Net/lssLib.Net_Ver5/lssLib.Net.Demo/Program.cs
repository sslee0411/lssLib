// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net.Demo · Program.cs
//  역할: 9가지 Transport 예제 메뉴 (시퀀스 제어 제외)
//  시퀀스 제어 예제 → lssLib.NetSequence.Demo 참고
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;
using lssLib.Net.Demo;

Console.WriteLine("lssLib.Net v5.1 — Transport 데모");
Console.WriteLine("────────────────────────────────────────────────");
Console.WriteLine("  1   TCP Passive              (Push 서버 필요)");
Console.WriteLine("  2   TCP RequestResponse      (Echo 서버 필요)");
Console.WriteLine("  3   Serial Modbus RTU        (COM3 필요)");
Console.WriteLine("  4   UDP Passive");
Console.WriteLine("  5   Named Pipe IPC");
Console.WriteLine("  6   HTTP REST API            (서버 필요)");
Console.WriteLine("  7   WebSocket                (서버 필요)");
Console.WriteLine("  8   MQTT                     (브로커 필요)");
Console.WriteLine("  9   Virtual ★ 하드웨어 불필요");
Console.WriteLine(" 10   Multi-Transport Registry");
Console.WriteLine(" 11   SharedMemory IPC");
Console.WriteLine("────────────────────────────────────────────────");
Console.WriteLine("  시퀀스 제어 예제는 lssLib.NetSequence.Demo 를 사용하세요.");
Console.Write("입력 (기본=9): ");

string? input = Console.ReadLine()?.Trim();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

try
{
    await (input switch
    {
        "1" => Ex01_TcpPassive.RunAsync(cts.Token),
        "2" => Ex02_TcpRequestResponse.RunAsync(cts.Token),
        "3" => Ex03_Serial.RunAsync(cts.Token),
        "4" => Ex04_Udp.RunAsync(cts.Token),
        "5" => Ex05_NamedPipe.RunAsync(cts.Token),
        "6" => Ex06_Http.RunAsync(cts.Token),
        "7" => Ex07_WebSocket.RunAsync(cts.Token),
        "8" => Ex08_Mqtt.RunAsync(cts.Token),
        "9" => Ex09_Virtual.RunAsync(cts.Token),
        "10" => Ex10_MultiTransport.RunAsync(cts.Token),
        "11" => Ex11_SharedMemory.RunAsync(cts.Token),
        _ => Ex09_Virtual.RunAsync(cts.Token)
    });
}
catch (OperationCanceledException) { Console.WriteLine("타임아웃"); }
catch (Exception ex) { Console.WriteLine($"오류: {ex.Message}"); }

Console.WriteLine("\n데모 완료");