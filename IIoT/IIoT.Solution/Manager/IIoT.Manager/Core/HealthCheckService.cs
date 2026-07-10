// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/HealthCheckService.cs
//  역할: NamedPipe 헬스체크 클라이언트 — "ping" 송신 → "pong|{상태}" 수신,
//        왕복 시간(ms) 측정
//  MG-03: 신규
//  프로토콜: HealthPipeServer(IIoT.Contracts) 와 쌍
//    파이프명: "IIoT.Health.{processName}" / 요청 "ping" / 응답 "pong|{상태}"
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Contracts.Health;
using System.Diagnostics;
using System.IO.Pipes;

namespace IIoT.Manager.Core;

/// <summary>헬스체크 1회 결과.</summary>
public readonly record struct HealthResult(bool Ok, long ElapsedMs, string Status)
{
    public static HealthResult Fail => new(false, 0, "");
}

/// <summary>
/// NamedPipe 헬스체크 클라이언트 (DI 싱글턴).
/// <para>
/// 연결·응답 각 1초 한도 — 초과 시 실패로 판정한다.
/// 대상 프로그램에 HealthPipeServer 가 없으면(구버전 빌드) 연결 자체가
/// 실패하므로 동일하게 "응답 없음" 으로 표시된다.
/// </para>
/// </summary>
public sealed class HealthCheckService
{
    // §1 ─ 상수 ──────────────────────────────────────────────

    /// <summary>연결 + 응답 대기 한도</summary>
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);

    // §2 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// 대상 프로그램에 핑을 보내고 왕복 시간을 측정한다.
    /// 실패(타임아웃·파이프 없음·프로토콜 불일치)는 예외 없이 Fail 반환.
    /// </summary>
    public async Task<HealthResult> PingAsync(string processName)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",                                       // 로컬 PC
                HealthPipeServer.GetPipeName(processName), // 파이프명 규칙 공유
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var cts = new CancellationTokenSource(_timeout);
            var sw = Stopwatch.StartNew();

            await client.ConnectAsync(cts.Token);

            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);

            await writer.WriteLineAsync("ping".AsMemory(), cts.Token);
            var resp = await reader.ReadLineAsync(cts.Token);

            sw.Stop();

            if (resp is null || !resp.StartsWith("pong"))
                return HealthResult.Fail;

            // "pong|{상태문구}" — 상태문구는 선택
            var sep    = resp.IndexOf('|');
            var status = sep >= 0 ? resp[(sep + 1)..] : "";

            return new HealthResult(true, sw.ElapsedMilliseconds, status);
        }
        catch
        {
            // 타임아웃/파이프 없음 — 호출부에서 "응답 없음" 처리 (로그는 호출부 판단)
            return HealthResult.Fail;
        }
    }
}
