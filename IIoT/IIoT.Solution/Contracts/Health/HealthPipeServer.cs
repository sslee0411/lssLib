// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Health/HealthPipeServer.cs
//  역할: 헬스체크 NamedPipe 응답 서버 (핑/퐁)
//        각 프로그램(Studio·Collector·Monitor)이 시작 시 1개 띄우고,
//        Manager 가 "ping" 을 보내면 "pong|{상태문구}" 로 응답한다.
//  MG-03: 신규
//  프로토콜:
//    파이프명: "IIoT.Health.{appName}"  (예: IIoT.Health.IIoT.Collector)
//    요청:  "ping\n"  →  응답: "pong|{statusProvider() 결과 또는 빈 문자열}\n"
//  설계 메모:
//    - 의존성 없음 (lssLib.Log 미참조) — 로그는 onLog 콜백으로 위임
//      (Contracts 는 플러그인 계약 레이어 — 외부 의존 최소화 원칙)
//    - statusProvider 로 내부 상태 문구 확장 가능
//      (예: Collector → "수집 루프 정상, Tag 1240개")
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using System.IO.Pipes;

namespace IIoT.Contracts.Health;

/// <summary>
/// 헬스체크 NamedPipe 응답 서버.
/// <para>
/// Start() 후 백그라운드 루프가 연결을 대기하며, DisposeAsync() 로 정리한다.
/// 앱 종료 시 반드시 DisposeAsync() 호출 (OnExit 정리 세트에 포함할 것).
/// </para>
/// </summary>
public sealed class HealthPipeServer : IAsyncDisposable
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly string                   _pipeName;
    private readonly Func<string>?            _statusProvider;
    private readonly Action<string>?          _onLog;
    private readonly CancellationTokenSource  _cts = new();
    private Task?                             _loopTask;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    /// <param name="appName">앱 식별 이름 — 실행 파일 이름과 동일하게 (예: "IIoT.Collector")</param>
    /// <param name="statusProvider">pong 에 실어 보낼 상태 문구 (null 이면 빈 문자열)</param>
    /// <param name="onLog">로그 콜백 (null 이면 무음)</param>
    public HealthPipeServer(string appName,
                            Func<string>?   statusProvider = null,
                            Action<string>? onLog          = null)
    {
        _pipeName       = GetPipeName(appName);
        _statusProvider = statusProvider;
        _onLog          = onLog;
    }

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>appName → 파이프 이름 규칙 (Manager 클라이언트와 공유)</summary>
    public static string GetPipeName(string appName) => $"IIoT.Health.{appName}";

    /// <summary>응답 루프를 백그라운드로 시작한다 (재호출 무시).</summary>
    public void Start()
    {
        if (_loopTask is not null) return;
        _loopTask = Task.Run(() => _ServeLoopAsync(_cts.Token));
        _onLog?.Invoke($"헬스체크 파이프 시작: {_pipeName}");
    }

    /// <summary>루프를 중지하고 파이프를 정리한다 (최대 2초 대기).</summary>
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loopTask is not null)
        {
            // 종료가 무한정 늘어지지 않도록 2초 한도 (Monitor 버그 #11 교훈)
            await Task.WhenAny(_loopTask, Task.Delay(2000));
        }
        _cts.Dispose();
        _onLog?.Invoke($"헬스체크 파이프 종료: {_pipeName}");
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>연결 대기 → ping 수신 → pong 응답 → 재대기 루프.</summary>
    private async Task _ServeLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token);

                using var reader = new StreamReader(server, leaveOpen: true);
                using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

                var line = await reader.ReadLineAsync(token);
                if (line == "ping")
                {
                    string status = "";
                    try   { status = _statusProvider?.Invoke() ?? ""; }
                    catch (Exception ex) { status = $"상태조회오류:{ex.Message}"; }

                    await writer.WriteLineAsync($"pong|{status}");
                }

                server.WaitForPipeDrain();
                server.Disconnect();
            }
            catch (OperationCanceledException)
            {
                break;   // 정상 종료
            }
            catch (Exception ex)
            {
                // 클라이언트 중도 이탈(IOException) 등 — 로그만 남기고 루프 유지
                _onLog?.Invoke($"헬스체크 파이프 오류(계속): {ex.Message}");
                try { await Task.Delay(500, token); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                server?.Dispose();
            }
        }
    }
}
