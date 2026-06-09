// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/ConfigWatcher.cs
//  역할: device.json 저장 완료 후 변경 신호 발행
//        CollectorRuntime 이 이 신호를 받아 자동 재시작
//  Phase 6: 신규
//
//  신호 전달 방법 (우선순위):
//    1순위: .signal 파일 생성 (Config/device.json.signal)
//           → CollectorRuntime 의 FileSystemWatcher 가 감지
//    2순위: lssLib EventBus (같은 프로세스 내 알림용)
//
//  단방향 원칙 유지:
//    DeviceManager → device.json 쓰기 + .signal 파일 생성
//    CollectorRuntime → .signal 감지 → device.json 읽기 → 재시작
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Messaging;
using System.IO;
using System.Text;

namespace IIoT.DeviceManager.Core.Config;

/// <summary>
/// device.json 저장 완료를 CollectorRuntime 에 알리는 신호 서비스.
///
/// 신호 파일 방식 (.signal):
///   DeviceManager 가 저장 완료 후 device.json.signal 파일을 생성.
///   CollectorRuntime 의 FileSystemWatcher 가 *.signal 파일을 감지.
///   CollectorRuntime 은 .signal 파일을 삭제하고 설정을 다시 로드.
///
/// 이 방식을 선택한 이유:
///   · Named Pipe / MQTT 없이도 동작 (프로세스 독립)
///   · .signal 파일은 극히 작음 (JSON 타임스탬프만 포함)
///   · CollectorRuntime 이 꺼져 있어도 다음 시작 시 신호 감지 가능
/// </summary>
public sealed class ConfigWatcher : IDisposable
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc   = "ConfigWatcher";
    private const string SignalExt = ".signal";

    private readonly string _configDirectory;
    private bool _disposed;

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public ConfigWatcher(string configDirectory)
    {
        _configDirectory = configDirectory;
    }

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// device.json 저장 완료 신호를 발행합니다.
    ///
    /// JsonWriteService.SaveDeviceTree() 완료 직후 호출하세요.
    /// 신호 파일 생성 + EventBus 발행을 동시 수행합니다.
    /// </summary>
    /// <param name="reason">변경 사유 (로그/신호 파일에 기록)</param>
    public void NotifyDeviceConfigChanged(string reason = "manual-save")
    {
        _WriteSignalFile("device.json", reason);
        _PublishEvent(reason);

        LogManager.Instance.Info(LogSrc,
            $"device.json 변경 신호 발행 — 사유: {reason}");
    }

    /// <summary>
    /// scale/alarm/comm 라이브러리 변경 신호를 발행합니다.
    /// CollectorRuntime 은 라이브러리 변경 시에도 설정을 재로드합니다.
    /// </summary>
    public void NotifyLibraryChanged(string libraryFileName, string reason = "manual-save")
    {
        _WriteSignalFile(libraryFileName, reason);
        LogManager.Instance.Info(LogSrc,
            $"{libraryFileName} 변경 신호 발행 — 사유: {reason}");
    }

    /// <summary>
    /// 남아있는 .signal 파일을 모두 정리합니다.
    /// 앱 시작 시 호출하여 이전 세션의 미처리 신호를 제거합니다.
    /// </summary>
    public void CleanupSignalFiles()
    {
        try
        {
            var signals = Directory.GetFiles(_configDirectory, $"*{SignalExt}");
            foreach (var f in signals)
                File.Delete(f);

            if (signals.Length > 0)
                LogManager.Instance.Info(LogSrc,
                    $"이전 신호 파일 {signals.Length}개 정리 완료");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn(LogSrc, $"신호 파일 정리 오류: {ex.Message}");
        }
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>
    /// [configDir]/[targetFile].signal 파일을 생성합니다.
    ///
    /// 파일 내용 예시:
    /// {
    ///   "source": "DeviceManager",
    ///   "target": "device.json",
    ///   "reason": "manual-save",
    ///   "timestamp": "2025-06-01T09:30:00Z"
    /// }
    /// </summary>
    private void _WriteSignalFile(string targetFileName, string reason)
    {
        try
        {
            var signalPath = Path.Combine(
                _configDirectory,
                targetFileName + SignalExt);

            var content = $$"""
                {
                  "source":    "DeviceManager",
                  "target":    "{{targetFileName}}",
                  "reason":    "{{reason}}",
                  "timestamp": "{{DateTime.UtcNow:O}}"
                }
                """;

            File.WriteAllText(signalPath, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // 신호 발행 실패는 치명적이지 않음 — 경고만 로그
            LogManager.Instance.Warn(LogSrc,
                $"신호 파일 생성 실패 ({targetFileName}): {ex.Message}");
        }
    }

    /// <summary>
    /// lssLib EventBus 로 같은 프로세스 내 구독자에게 알립니다.
    /// (같은 AppDomain 내 모니터링 패널 등에 실시간 알림)
    /// </summary>
    private static void _PublishEvent(string reason)
    {
        try
        {
            EventBus.Instance.Publish(
                new DeviceConfigChangedEvent(reason, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            LogManager.Instance.Debug(LogSrc, $"EventBus 발행 오류: {ex.Message}");
        }
    }

    // §5 ─ IDisposable ────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

// ── 이벤트 페이로드 ──────────────────────────────────────
/// <summary>
/// device.json 변경 알림 이벤트 (lssLib EventBus 용)
/// ★ EventBus.Publish&lt;T&gt; 제약 조건: T : EventMessage 필수
/// </summary>
public sealed record DeviceConfigChangedEvent(
    string   Reason,
    DateTime Timestamp) : EventMessage;
