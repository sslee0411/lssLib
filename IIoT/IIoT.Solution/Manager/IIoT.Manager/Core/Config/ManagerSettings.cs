// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/Config/ManagerSettings.cs
//  역할: Manager 런타임 설정 (manager.json) DTO + 로더/세이버
//        경로: {Manager 실행파일}\Config\manager.json
//  MG-02: 신규 — Processes[] (관리 대상 프로그램 + 실행 경로)
//         MonitorSettingsLoader(monitor.json) 와 동일한 패턴
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Models;
using lssLib.Log;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Manager.Core.Config;

// ── manager.json 최상위 ───────────────────────────────────

public sealed class ManagerSettings
{
    /// <summary>관리 대상 프로그램 목록. 파일이 없으면 기본 3개(Studio·Collector·Monitor)로 생성.</summary>
    public List<ManagedProcessInfo> Processes { get; set; } = new();
}

// ── 로더/세이버 ────────────────────────────────────────────

/// <summary>
/// manager.json 로더/세이버 (DI 싱글턴).
/// 파일 없으면 ManagedProcessInfo.Defaults 기반 기본값을 저장 후 반환.
/// <para>
/// ExePath 는 Manager 실행 폴더 기준 상대 경로 허용 — 배포 위치가 바뀌면
/// manager.json 만 수정하면 된다 (프로그램 수정 불필요).
/// </para>
/// </summary>
public sealed class ManagerSettingsLoader
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Encoder                     = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented               = true
    };

    public static string SettingsPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "manager.json");

    public ManagerSettings Settings { get; private set; } = new();

    /// <summary>
    /// manager.json 을 로드합니다. 파일이 없으면 기본 3개 프로그램으로 생성 후 반환합니다.
    /// </summary>
    public async Task LoadAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            Settings = new ManagerSettings
            {
                Processes = ManagedProcessInfo.Defaults.ToList()
            };
            await SaveAsync();
            LogManager.Instance.Info("ManagerSettings",
                $"manager.json 없음 — 기본값 생성: {path}");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            Settings = JsonSerializer.Deserialize<ManagerSettings>(json, _opts) ?? new ManagerSettings();
            LogManager.Instance.Info("ManagerSettings",
                $"manager.json 로드 완료 — 프로그램 {Settings.Processes.Count}개 등록됨");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("ManagerSettings", $"manager.json 로드 실패: {ex.Message}");
            Settings = new ManagerSettings();
        }
    }

    /// <summary>현재 Settings 를 manager.json 에 저장합니다.</summary>
    public async Task SaveAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(Settings, _opts);
        await File.WriteAllTextAsync(path, json);

        LogManager.Instance.Info("ManagerSettings",
            $"manager.json 저장 완료 — 프로그램 {Settings.Processes.Count}개");
    }
}
