// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/StartupRegistrationService.cs
//  역할: Windows 시작 시 Manager 자동 실행 등록/해제
//        레지스트리 HKCU\Software\Microsoft\Windows\CurrentVersion\Run 사용
//        (현재 사용자 전용 — 관리자 권한 불필요)
//  MG-EX-03: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using Microsoft.Win32;

namespace IIoT.Manager.Core;

/// <summary>Windows 시작 프로그램 등록 서비스 (DI 싱글턴).</summary>
public sealed class StartupRegistrationService
{
    // §1 ─ 상수 ──────────────────────────────────────────────

    private const string _runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string _valueName  = "IIoT.Manager";

    // §2 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>현재 자동 실행 등록 여부.</summary>
    public bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
            return key?.GetValue(_valueName) is not null;
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("Startup", $"등록 상태 확인 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>자동 실행 등록 (현재 실행 파일 경로 기준).</summary>
    /// <returns>성공 여부 — 실패 사유는 로그 기록</returns>
    public bool Register()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                LogManager.Instance.Warn("Startup", "실행 파일 경로를 확인할 수 없어 등록 실패");
                return false;
            }

            using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath);
            key.SetValue(_valueName, $"\"{exePath}\"");
            LogManager.Instance.Info("Startup", $"Windows 시작 시 자동 실행 등록: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("Startup", $"자동 실행 등록 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>자동 실행 해제.</summary>
    /// <returns>성공 여부 — 실패 사유는 로그 기록</returns>
    public bool Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
            key?.DeleteValue(_valueName, throwOnMissingValue: false);
            LogManager.Instance.Info("Startup", "Windows 시작 시 자동 실행 해제");
            return true;
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("Startup", $"자동 실행 해제 실패: {ex.Message}");
            return false;
        }
    }
}
