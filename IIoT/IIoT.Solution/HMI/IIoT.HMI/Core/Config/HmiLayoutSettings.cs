// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Config/HmiLayoutSettings.cs
//  역할: [레이아웃 편집] 탭의 화면(페이지) 저장 데이터 (hmi-layout.json) DTO + 로더/세이버
//        경로: {HMI 실행파일}\Config\hmi-layout.json
//        (HmiSettingsLoader/hmi.json 과 동일한 로더 패턴을 따름 — Monitor
//         MonitorSettingsLoader 계열)
//  HM-07: 신규 — 여러 화면(페이지)을 하나의 파일에 저장한다. 각 화면은 노드
//         목록(배치 좌표+Z순서+Tag 바인딩 식별자)을 갖는다.
//         ★ 노드의 실시간 값(ValueText/EngValue/ValueQuality)은 저장하지 않는다
//         — 재실행 시 Collector 재연결 후 SignalR TagValue Push로 다시 채워지는
//         값이기 때문이다(여기서는 순수 "배치+바인딩 식별자"만 영속화).
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.HMI.Core.Config;

// ── 노드 1개 저장 단위 ─────────────────────────────────────

/// <summary>레이아웃에 배치된 노드 1개의 저장 데이터 — 위치·Z순서·Tag 바인딩 식별자만 보관.</summary>
public sealed class LayoutNodeDto
{
    public string NodeType         { get; set; } = string.Empty;
    public string Label            { get; set; } = string.Empty;
    public double X                { get; set; }
    public double Y                { get; set; }
    public int    ZIndex           { get; set; }
    public string BoundCollectorId { get; set; } = string.Empty;
    public string BoundPlcId       { get; set; } = string.Empty;
    public string BoundTagId       { get; set; } = string.Empty;
    public string BoundTagName     { get; set; } = string.Empty;
}

// ── 화면(페이지) 1개 ───────────────────────────────────────

/// <summary>레이아웃 화면(페이지) 1개 — 이름 + 소속 노드 목록.</summary>
public sealed class LayoutPageDto
{
    public string Id   { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "화면";
    public List<LayoutNodeDto> Nodes { get; set; } = new();
}

// ── hmi-layout.json 최상위 ─────────────────────────────────

public sealed class HmiLayoutFile
{
    /// <summary>마지막으로 편집 중이던 화면 Id (다음 실행 시 자동 복원)</summary>
    public string ActivePageId { get; set; } = string.Empty;

    public List<LayoutPageDto> Pages { get; set; } = new();
}

// ── 로더/세이버 ────────────────────────────────────────────

/// <summary>
/// hmi-layout.json 로더/세이버 (DI 싱글턴).
/// 파일이 없거나 화면이 0개면 "기본 화면" 1개짜리 빈 레이아웃으로 생성 후 반환한다.
/// HmiSettingsLoader(hmi.json)와 동일한 패턴을 따른다.
/// </summary>
public sealed class HmiLayoutLoader
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Encoder                     = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented               = true
    };

    public static string SettingsPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "hmi-layout.json");

    public HmiLayoutFile Layout { get; private set; } = new();

    /// <summary>hmi-layout.json 을 로드합니다. 파일이 없으면 "기본 화면" 1개로 생성 후 저장·반환합니다.</summary>
    public async Task LoadAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            Layout = _CreateDefault();
            await SaveAsync();
            LogManager.Instance.Info("HmiLayout", $"hmi-layout.json 없음 — 기본 화면 생성: {path}");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            Layout = JsonSerializer.Deserialize<HmiLayoutFile>(json, _opts) ?? _CreateDefault();

            if (Layout.Pages.Count == 0)
            {
                var page = new LayoutPageDto { Name = "기본 화면" };
                Layout.Pages.Add(page);
                Layout.ActivePageId = page.Id;
            }

            LogManager.Instance.Info("HmiLayout",
                $"hmi-layout.json 로드 완료 — 화면 {Layout.Pages.Count}개");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("HmiLayout", $"hmi-layout.json 로드 실패: {ex.Message}");
            Layout = _CreateDefault();
        }
    }

    /// <summary>현재 Layout 을 hmi-layout.json 에 저장합니다.</summary>
    public async Task SaveAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(Layout, _opts);
        await File.WriteAllTextAsync(path, json);

        LogManager.Instance.Info("HmiLayout",
            $"hmi-layout.json 저장 완료 — 화면 {Layout.Pages.Count}개");
    }

    private static HmiLayoutFile _CreateDefault()
    {
        var file = new HmiLayoutFile();
        var page = new LayoutPageDto { Name = "기본 화면" };
        file.Pages.Add(page);
        file.ActivePageId = page.Id;
        return file;
    }
}
