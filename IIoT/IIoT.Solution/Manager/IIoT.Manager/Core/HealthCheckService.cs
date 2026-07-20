// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/HealthCheckService.cs
//  역할: NamedPipe 헬스체크 클라이언트 — "ping" 송신 → "pong|{상태}" 수신,
//        왕복 시간(ms) 측정
//  MG-03: 신규
//  HM-22: 원격 설정 조회/저장 클라이언트 메서드 추가(GetSettingsAsync/
//         SaveSettingsAsync) — HealthPipeServer(HM-22 확장)와 쌍을 이루며,
//         같은 채널·같은 연결-요청-응답 패턴을 그대로 재사용한다(신규 파이프 없음).
//  프로토콜: HealthPipeServer(IIoT.Contracts) 와 쌍
//    파이프명: "IIoT.Health.{processName}"
//    ping           / 응답 "pong|{상태}"
//    get-settings   / 응답 "settings|{Base64 JSON}" 또는 "error|{메시지}"
//    save-settings|{Base64 JSON} / 응답 "ok" 또는 "error|{메시지}"
//  생성: 2026-07-09 / 수정: 2026-07-09 (FIX: using System.IO 누락 — CS0246)
// ══════════════════════════════════════════════════════════

using IIoT.Contracts.Health;
using System.Diagnostics;
// ★ FIX(2026-07-09): StreamReader/StreamWriter 는 System.IO 소속 —
//   System.IO.Pipes 만으로는 CS0246 발생 (HealthPipeServer 와 동일 수정)
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace IIoT.Manager.Core;

/// <summary>헬스체크 1회 결과.</summary>
public readonly record struct HealthResult(bool Ok, long ElapsedMs, string Status)
{
    public static HealthResult Fail => new(false, 0, "");
}

/// <summary>★ HM-22: 원격 설정 조회/저장 1회 결과.</summary>
public readonly record struct RemoteSettingsResult(bool Ok, string Json, string Error)
{
    public static RemoteSettingsResult Fail(string error) => new(false, "", error);
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

    /// <summary>
    /// ★ HM-22: 대상 프로그램의 settings.json 원문을 원격으로 조회한다.
    /// 대상이 실행 중이 아니거나 원격 설정을 지원하지 않으면 실패로 반환.
    /// </summary>
    public async Task<RemoteSettingsResult> GetSettingsAsync(string processName)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", HealthPipeServer.GetPipeName(processName),
                PipeDirection.InOut, PipeOptions.Asynchronous);

            using var cts = new CancellationTokenSource(_timeout);
            await client.ConnectAsync(cts.Token);

            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);

            await writer.WriteLineAsync("get-settings".AsMemory(), cts.Token);
            var resp = await reader.ReadLineAsync(cts.Token);

            if (resp is null) return RemoteSettingsResult.Fail("응답 없음(프로그램이 실행 중인지 확인하세요)");

            if (resp.StartsWith("settings|"))
            {
                var b64  = resp["settings|".Length..];
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                return new RemoteSettingsResult(true, json, "");
            }
            if (resp.StartsWith("error|"))
                return RemoteSettingsResult.Fail(resp["error|".Length..]);

            return RemoteSettingsResult.Fail($"알 수 없는 응답: {resp}");
        }
        catch (Exception ex)
        {
            return RemoteSettingsResult.Fail($"연결 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// ★ HM-22: 대상 프로그램의 settings.json 원문을 원격으로 저장한다.
    /// 대상 프로그램은 파일만 갱신하고 즉시 반영하지 않으므로(다른 프로그램의
    /// 로컬 환경설정 탭과 동일하게), 저장 후 재시작이 필요하다는 안내는 호출부(UI) 책임.
    /// </summary>
    public async Task<RemoteSettingsResult> SaveSettingsAsync(string processName, string json)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", HealthPipeServer.GetPipeName(processName),
                PipeDirection.InOut, PipeOptions.Asynchronous);

            using var cts = new CancellationTokenSource(_timeout);
            await client.ConnectAsync(cts.Token);

            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);

            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            await writer.WriteLineAsync($"save-settings|{b64}".AsMemory(), cts.Token);
            var resp = await reader.ReadLineAsync(cts.Token);

            if (resp is null) return RemoteSettingsResult.Fail("응답 없음(프로그램이 실행 중인지 확인하세요)");
            if (resp == "ok") return new RemoteSettingsResult(true, json, "");
            if (resp.StartsWith("error|")) return RemoteSettingsResult.Fail(resp["error|".Length..]);

            return RemoteSettingsResult.Fail($"알 수 없는 응답: {resp}");
        }
        catch (Exception ex)
        {
            return RemoteSettingsResult.Fail($"연결 실패: {ex.Message}");
        }
    }
}
