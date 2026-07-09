// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Config/MonitorSettings.cs
//  역할: Monitor 런타임 설정 (monitor.json) DTO + 로더/세이버
//        경로: {Monitor 실행파일}\Config\monitor.json
//  MN-01: 신규 — Collectors[] (등록된 Collector 목록) 저장
//  MN-05: Web(WebHostSettings) 섹션 추가 — 자체 SignalR Hub 포트/활성화 설정
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-05)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;
using lssLib.Log;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Monitor.Core.Config;

// ── monitor.json 최상위 ───────────────────────────────────

public sealed class MonitorSettings
{
    /// <summary>등록된 Collector 목록. MN-01 [Collector 관리] 탭에서 CRUD.</summary>
    public List<CollectorEndpoint> Collectors { get; set; } = new();

    /// <summary>★ MN-05 신규: Monitor 자체 웹 Hub(브라우저 연동) 설정</summary>
    public WebHostSettings Web { get; set; } = new();
}

/// <summary>Monitor 자체 SignalR Hub(웹 브라우저 연동, MN-05) 설정.</summary>
public sealed class WebHostSettings
{
    /// <summary>true 면 Monitor 시작 시 자체 웹 서버를 함께 구동한다.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>웹 Hub 포트. Collector(7878)와 겹치지 않도록 기본 7879 사용.</summary>
    public int Port { get; set; } = 7879;
}

// ── 로더/세이버 ────────────────────────────────────────────

/// <summary>
/// monitor.json 로더/세이버 (DI 싱글턴).
/// 파일 없으면 빈 목록으로 생성 후 반환.
/// Collector 의 CollectorSettingsLoader 와 동일한 패턴을 따른다.
/// </summary>
public sealed class MonitorSettingsLoader
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Encoder                     = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented               = true
    };

    public static string SettingsPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "monitor.json");

    public MonitorSettings Settings { get; private set; } = new();

    /// <summary>
    /// monitor.json 을 로드합니다. 파일이 없으면 빈 설정을 저장 후 반환합니다.
    /// </summary>
    public async Task LoadAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            Settings = new MonitorSettings();
            await SaveAsync();
            LogManager.Instance.Info("MonitorSettings",
                $"monitor.json 없음 — 기본값 생성: {path}");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            Settings = JsonSerializer.Deserialize<MonitorSettings>(json, _opts) ?? new MonitorSettings();
            LogManager.Instance.Info("MonitorSettings",
                $"monitor.json 로드 완료 — Collector {Settings.Collectors.Count}개 등록됨");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("MonitorSettings", $"monitor.json 로드 실패: {ex.Message}");
            Settings = new MonitorSettings();
        }
    }

    /// <summary>현재 Settings 를 monitor.json 에 저장합니다.</summary>
    public async Task SaveAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(Settings, _opts);
        await File.WriteAllTextAsync(path, json);

        LogManager.Instance.Info("MonitorSettings",
            $"monitor.json 저장 완료 — Collector {Settings.Collectors.Count}개");
    }
}
