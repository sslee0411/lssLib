// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Config/HmiSettings.cs
//  역할: HMI 런타임 설정 (hmi.json) DTO + 로더/세이버
//        경로: {HMI 실행파일}\Config\hmi.json
//        (IIoT.Monitor Core/Config/MonitorSettings.cs — MN-01 이식)
//  HM-01: 신규 — Collectors[] (등록된 Collector 목록) 저장
//  HM-11: Web(WebHostSettings) 추가 — 웹 브라우저 표시 확장(자체 Kestrel+
//         SignalR Hub+wwwroot) 활성화 여부/포트. Collector(7878)·Monitor(7879)
//         와 겹치지 않도록 기본 7880 사용(Monitor MonitorSettings.WebHostSettings
//         패턴 그대로 이식).
//  HM-12: ForceWriteSecurity(ForceWriteSecuritySettings) 추가 — [레이아웃 편집]
//         탭의 "화면 잠금 모드"(더블클릭 시 ForceWriteDialog 오픈 차단) 기본값을
//         앱 시작 시 어느 상태(잠김/해제)로 시작할지 설정. 기본 true(잠김) —
//         운영자 실수로 인한 오조작을 안전 우선으로 방지.
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

    /// <summary>★ HM-11: 웹 브라우저 표시 확장 설정.</summary>
    public WebHostSettings Web { get; set; } = new();

    /// <summary>★ HM-12: ForceWrite 화면 잠금 모드 관련 설정.</summary>
    public ForceWriteSecuritySettings ForceWriteSecurity { get; set; } = new();
}

/// <summary>
/// ★ HM-12: [레이아웃 편집] 탭의 "화면 잠금 모드"(더블클릭 시 ForceWriteDialog
/// 오픈 차단) 관련 설정. 잠금 자체는 세션 중 툴바 버튼으로 토글되는 런타임
/// 상태(LayoutCanvasViewModel.IsForceWriteLocked)이며, 여기서는 "앱 시작 시
/// 기본값"만 설정한다(토글할 때마다 파일에 저장하지 않음).
/// </summary>
public sealed class ForceWriteSecuritySettings
{
    /// <summary>앱 시작 시 화면 잠금 기본 상태. 기본 true(잠김) — 안전 우선.</summary>
    public bool DefaultLocked { get; set; } = true;
}

/// <summary>
/// ★ HM-11: 웹 표시용 자체 Kestrel+SignalR Hub 호스팅 설정
/// (IIoT.Monitor Core/Config/MonitorSettings.cs 의 WebHostSettings 와 동일 구조).
/// </summary>
public sealed class WebHostSettings
{
    /// <summary>웹 표시 서버 활성화 여부 (기본 true)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>웹 Hub/정적 페이지 포트. Collector(7878)·Monitor(7879)와
    /// 겹치지 않도록 기본 7880 사용.</summary>
    public int Port { get; set; } = 7880;
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
