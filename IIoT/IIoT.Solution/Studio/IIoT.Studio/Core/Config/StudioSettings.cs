// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/StudioSettings.cs
//  역할: Studio 앱 자체 동작 설정 (studio-settings.json) DTO + 로더/세이버
//        경로: {Studio 실행파일}\Config\studio-settings.json
//        ★ device.json/collect.json(장비·수집흐름 설정)과는 별개 파일 —
//          이 파일은 Studio 프로그램 자신의 동작(로그·편집기 히스토리)만 다룬다.
//  C-SET-01 후속 (Studio): Collector/Manager 환경설정 탭과 동일한 트랙.
//        이전까지 App.xaml.cs/DeviceTreeViewModel/MainViewModel 에 하드코딩돼
//        있던 값(로그 레벨·보존일수, Undo 히스토리 단계 수, 저장 이력 개수)을
//        이 설정으로 옮겨 편집 가능하게 한다.
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Studio.Core.Config;

// ── studio-settings.json 최상위 ───────────────────────────

public sealed class StudioSettings
{
    /// <summary>로그 설정 — App.xaml.cs 의 LogManager.Instance.Start() 인자로 사용</summary>
    public LogSettings Log { get; set; } = new();

    /// <summary>편집기 동작 설정 — Undo 히스토리 단계 수 / 저장 이력 개수</summary>
    public EditorSettings Editor { get; set; } = new();
}

// ── 로그 섹션 ──────────────────────────────────────────────

public sealed class LogSettings
{
    /// <summary>파일 로그 최소 레벨 (기본 Debug — 개발 중 상세 기록)</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    /// <summary>콘솔/로그 패널 표시 최소 레벨 (기본 Info)</summary>
    public LogLevel MinimumConsoleLevel { get; set; } = LogLevel.Info;

    /// <summary>로그 파일 보존 일수 (기본 30일)</summary>
    public int ValidDays { get; set; } = 30;

    /// <summary>로그 패널 최대 표시 건수 (기본 2000)</summary>
    public int MaxDisplayCount { get; set; } = 2000;
}

// ── 편집기 섹션 ────────────────────────────────────────────

public sealed class EditorSettings
{
    /// <summary>실행취소/다시실행(Ctrl+Z/Y) 최대 단계 수 (S-29 기본 50)</summary>
    public int UndoHistoryMaxSize { get; set; } = 50;

    /// <summary>저장 시 변경 메모 이력 최대 보관 개수 (S-27 기본 10)</summary>
    public int SaveHistoryMaxCount { get; set; } = 10;
}

// ── 로더/세이버 ────────────────────────────────────────────

/// <summary>
/// studio-settings.json 로더/세이버 (DI 싱글턴).
/// <para>
/// ★ 주의: LogManager.Instance.Start() 및 DeviceTreeViewModel(Undo 히스토리)·
/// MainViewModel(저장 이력)의 생성자가 이 값을 필요로 하며, 이들은 모두
/// App.xaml.cs 의 DI 그래프 구성(동기, OnStartup 중) 시점에 생성된다.
/// 따라서 <see cref="LoadSync"/> 로 OnStartup 맨 앞(테마 적용 직후,
/// LogManager.Start() 호출 전)에서 동기적으로 먼저 읽어야 한다 —
/// 다른 프로그램의 비동기 LoadAsync()(창 표시 후 호출) 패턴과 다르다.
/// </para>
/// </summary>
public sealed class StudioSettingsLoader
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Encoder                     = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented               = true,
        Converters                  = { new JsonStringEnumConverter() }   // LogLevel 문자열 저장
    };

    public static string SettingsPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "studio-settings.json");

    public StudioSettings Settings { get; private set; } = new();

    /// <summary>
    /// 동기 로드 — OnStartup 맨 앞 전용(비동기 컨텍스트 진입 전).
    /// 파일이 없으면 기본값을 저장한다.
    /// </summary>
    public void LoadSync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            Settings = new StudioSettings();
            File.WriteAllText(path, JsonSerializer.Serialize(Settings, _opts), Encoding.UTF8);
            return;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            Settings = JsonSerializer.Deserialize<StudioSettings>(json, _opts) ?? new StudioSettings();
        }
        catch (Exception ex)
        {
            // ★ 이 시점은 LogManager.Start() 호출 전이라 로그 기록 불가 — 콘솔 출력만
            System.Diagnostics.Debug.WriteLine($"studio-settings.json 파싱 실패 → 기본값 사용: {ex.Message}");
            Settings = new StudioSettings();
        }
    }

    /// <summary>환경설정 화면의 [다시 불러오기] 전용 — 창 표시 후 호출되는 비동기 재로드.</summary>
    public async Task LoadAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            Settings = new StudioSettings();
            await SaveAsync();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            Settings = JsonSerializer.Deserialize<StudioSettings>(json, _opts) ?? new StudioSettings();
            LogManager.Instance.Info("Settings", "studio-settings.json 다시 불러오기 완료");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("Settings", $"studio-settings.json 로드 실패: {ex.Message}");
            Settings = new StudioSettings();
        }
    }

    /// <summary>환경설정 화면의 [저장] 전용.</summary>
    public async Task SaveAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(Settings, _opts);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8);
        LogManager.Instance.Info("Settings", $"studio-settings.json 저장 완료: {path}");
    }
}
