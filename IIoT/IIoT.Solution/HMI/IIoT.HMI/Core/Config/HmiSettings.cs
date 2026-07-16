// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Config/HmiSettings.cs
//  역할: HMI 런타임 설정 (hmi.json) DTO + 로더/세이버
//        경로: {HMI 실행파일}\Config\hmi.json
//        (IIoT.Monitor Core/Config/MonitorSettings.cs — MN-01 이식)
//  HM-01: 신규 — Collectors[] (등록된 Collector 목록) 저장
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Models;
using lssLib.Log;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.HMI.Core.Config;

// ── hmi.json 최상위 ───────────────────────────────────────

public sealed class HmiSettings
{
    /// <summary>등록된 Collector 목록. [Collector 관리] 탭에서 CRUD.</summary>
    public List<CollectorEndpoint> Collectors { get; set; } = new();
}

// ── 로더/세이버 ────────────────────────────────────────────

/// <summary>
/// hmi.json 로더/세이버 (DI 싱글턴).
/// 파일 없으면 빈 목록으로 생성 후 반환.
/// Collector 의 CollectorSettingsLoader / Monitor 의 MonitorSettingsLoader 와
/// 동일한 패턴을 따른다.
/// </summary>
public sealed class HmiSettingsLoader
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Encoder                     = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented               = true
    };

    public static string SettingsPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "hmi.json");

    public HmiSettings Settings { get; private set; } = new();

    /// <summary>
    /// hmi.json 을 로드합니다. 파일이 없으면 빈 설정을 저장 후 반환합니다.
    /// </summary>
    public async Task LoadAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            Settings = new HmiSettings();
            await SaveAsync();
            LogManager.Instance.Info("HmiSettings",
                $"hmi.json 없음 — 기본값 생성: {path}");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            Settings = JsonSerializer.Deserialize<HmiSettings>(json, _opts) ?? new HmiSettings();
            LogManager.Instance.Info("HmiSettings",
                $"hmi.json 로드 완료 — Collector {Settings.Collectors.Count}개 등록됨");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("HmiSettings", $"hmi.json 로드 실패: {ex.Message}");
            Settings = new HmiSettings();
        }
    }

    /// <summary>현재 Settings 를 hmi.json 에 저장합니다.</summary>
    public async Task SaveAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(Settings, _opts);
        await File.WriteAllTextAsync(path, json);

        LogManager.Instance.Info("HmiSettings",
            $"hmi.json 저장 완료 — Collector {Settings.Collectors.Count}개");
    }
}
