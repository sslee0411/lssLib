// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Health/HealthPipeServer.cs
//  역할: 헬스체크 NamedPipe 응답 서버 (핑/퐁)
//        각 프로그램(Studio·Collector·Monitor)이 시작 시 1개 띄우고,
//        Manager 가 "ping" 을 보내면 "pong|{상태문구}" 로 응답한다.
//  MG-03: 신규
//  HM-22: 원격 설정 조회/저장 커맨드 추가(get-settings/save-settings) —
//         Manager 가 이미 열려있는 이 채널을 그대로 확장해 Studio/Collector/
//         Monitor/HMI 의 settings.json 원문을 원격으로 조회·저장할 수 있게
//         한다(신규 파이프 없이 기존 헬스체크 채널 재사용, 사용자 확인
//         2026-07-20). settingsProvider/settingsSaver 는 둘 다 선택 인자이며,
//         null 이면 해당 프로그램은 원격 설정 기능 미지원으로 처리된다
//         (예: Manager 자신은 로컬이므로 등록하지 않음).
//         페이로드는 개행을 포함할 수 있는 JSON 원문이므로 Base64 로 감싸
//         본 프로토콜의 "줄 단위(ReadLine/WriteLine)" 특성을 그대로 유지한다.
//  프로토콜:
//    파이프명: "IIoT.Health.{appName}"  (예: IIoT.Health.IIoT.Collector)
//    ping           → "pong|{statusProvider() 결과 또는 빈 문자열}"
//    get-settings   → "settings|{Base64(UTF8 JSON)}" 또는 "error|{메시지}"
//                     (settingsProvider 가 null 이면 "error|not-supported")
//    save-settings|{Base64(UTF8 JSON)} → "ok" 또는 "error|{메시지}"
//                     (settingsSaver 가 null 이면 "error|not-supported")
//  설계 메모:
//    - 의존성 없음 (lssLib.Log 미참조) — 로그는 onLog 콜백으로 위임
//      (Contracts 는 플러그인 계약 레이어 — 외부 의존 최소화 원칙)
//    - statusProvider 로 내부 상태 문구 확장 가능
//      (예: Collector → "수집 루프 정상, Tag 1240개")
//  생성: 2026-07-09 / 수정: 2026-07-09 (FIX: using System.IO 누락 — CS0246)
// ══════════════════════════════════════════════════════════

// ★ FIX(2026-07-09): StreamReader/StreamWriter 는 System.IO 소속 —
//   System.IO.Pipes 만으로는 CS0246 발생 (Monitor 버그 #3 HttpClient 와 동일 패턴)
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

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
    private readonly Func<string>?            _settingsProvider;
    private readonly Func<string, string>?    _settingsSaver;
    private readonly Action<string>?          _onLog;
    private readonly CancellationTokenSource  _cts = new();
    private Task?                             _loopTask;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    /// <param name="appName">앱 식별 이름 — 실행 파일 이름과 동일하게 (예: "IIoT.Collector")</param>
    /// <param name="statusProvider">pong 에 실어 보낼 상태 문구 (null 이면 빈 문자열)</param>
    /// <param name="onLog">로그 콜백 (null 이면 무음)</param>
    /// <param name="settingsProvider">★ HM-22: 현재 settings.json 원문(UTF8 JSON 문자열)을
    /// 반환하는 콜백. null 이면 이 프로그램은 원격 설정 조회를 지원하지 않음.</param>
    /// <param name="settingsSaver">★ HM-22: 전달받은 JSON 문자열을 파일에 저장하는 콜백.
    /// 성공 시 빈 문자열, 실패 시 오류 메시지를 반환해야 한다. null 이면 원격 저장 미지원.</param>
    public HealthPipeServer(string appName,
                            Func<string>?         statusProvider   = null,
                            Action<string>?       onLog            = null,
                            Func<string>?         settingsProvider = null,
                            Func<string, string>? settingsSaver    = null)
    {
        _pipeName         = GetPipeName(appName);
        _statusProvider   = statusProvider;
        _onLog            = onLog;
        _settingsProvider = settingsProvider;
        _settingsSaver    = settingsSaver;
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
                var response = _HandleRequest(line);
                if (response is not null)
                    await writer.WriteLineAsync(response);

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

    /// <summary>
    /// ★ HM-22: 요청 1줄을 해석해 응답 1줄을 만든다. null 을 반환하면 응답 없이
    /// 연결을 닫는다(기존 ping 이외 미인식 커맨드는 무응답 — 하위 호환 유지).
    /// </summary>
    private string? _HandleRequest(string? line)
    {
        if (line == "ping")
        {
            string status = "";
            try   { status = _statusProvider?.Invoke() ?? ""; }
            catch (Exception ex) { status = $"상태조회오류:{ex.Message}"; }
            return $"pong|{status}";
        }

        if (line == "get-settings")
        {
            if (_settingsProvider is null) return "error|not-supported";
            try
            {
                var json = _settingsProvider.Invoke();
                return $"settings|{Convert.ToBase64String(Encoding.UTF8.GetBytes(json))}";
            }
            catch (Exception ex)
            {
                return $"error|{ex.Message}";
            }
        }

        if (line is not null && line.StartsWith("save-settings|"))
        {
            if (_settingsSaver is null) return "error|not-supported";
            try
            {
                var b64  = line["save-settings|".Length..];
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));

                // 저장 전 최소 검증 — 문법이 깨진 JSON 이 파일에 그대로 쓰이는 것을 방지
                using var _ = JsonDocument.Parse(json);

                var error = _settingsSaver.Invoke(json);
                return string.IsNullOrEmpty(error) ? "ok" : $"error|{error}";
            }
            catch (Exception ex)
            {
                return $"error|{ex.Message}";
            }
        }

        return null;   // 미인식 커맨드 — 무응답(기존 동작 유지)
    }
}
