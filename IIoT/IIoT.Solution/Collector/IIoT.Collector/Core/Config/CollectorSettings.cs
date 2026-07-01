// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Config/CollectorSettings.cs
//  역할: Collector 런타임 설정 (settings.json) DTO + 로더
//        Storage.Provider: "SQLite" | "InfluxDB"
//        SDT ExcDevPercent: 스케일 범위 대비 허용 오차 비율
//  C-07: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Collector.Core.Config;

// ── settings.json 최상위 ──────────────────────────────────

public sealed class CollectorSettings
{
    public StorageSettings Storage { get; set; } = new();
}

// ── Storage 섹션 ──────────────────────────────────────────

public sealed class StorageSettings
{
    /// <summary>
    /// 저장 공급자.
    /// "SQLite" (기본) 또는 "InfluxDB"
    /// </summary>
    public string Provider { get; set; } = "SQLite";

    /// <summary>
    /// SDT 허용 오차 비율 (스케일 EngMax-EngMin 기준, 기본 0.5%).
    /// 예) EngMax-EngMin=100, ExcDevPercent=0.5 → ExcDev=0.5 (공학단위)
    /// </summary>
    public double SdtExcDevPercent { get; set; } = 0.5;

    /// <summary>수집 통계 저장 주기 (초, 기본 60초)</summary>
    public int StatIntervalSec { get; set; } = 60;

    public SqliteSettings   SQLite   { get; set; } = new();
    public InfluxDbSettings InfluxDB { get; set; } = new();
}

// ── SQLite 설정 ───────────────────────────────────────────

public sealed class SqliteSettings
{
    /// <summary>
    /// DB 파일 경로.
    /// 상대 경로 → 실행파일 옆 기준.
    /// 기본: Data\collector.db
    /// </summary>
    public string DbPath { get; set; } = @"Data\collector.db";
}

// ── InfluxDB 설정 ─────────────────────────────────────────

public sealed class InfluxDbSettings
{
    /// <summary>InfluxDB v2 URL (예: http://localhost:8086)</summary>
    public string Url    { get; set; } = "http://localhost:8086";

    /// <summary>API 토큰 (InfluxDB UI → Data → Tokens 에서 생성)</summary>
    public string Token  { get; set; } = string.Empty;

    /// <summary>조직 이름 (InfluxDB 가입 시 설정한 org)</summary>
    public string Org    { get; set; } = "my-org";

    /// <summary>버킷 이름 (데이터를 저장할 버킷)</summary>
    public string Bucket { get; set; } = "iiot";

    /// <summary>
    /// 배치 쓰기 최대 건수 (기본 500).
    /// 이 수치에 도달하거나 FlushIntervalMs 가 경과하면 HTTP POST 전송.
    /// </summary>
    public int BatchSize     { get; set; } = 500;

    /// <summary>배치 쓰기 최대 대기 시간 (ms, 기본 5000)</summary>
    public int FlushIntervalMs { get; set; } = 5000;
}

// ── 로더 ──────────────────────────────────────────────────

/// <summary>
/// settings.json 로더 (DI 싱글턴).
/// 파일 없으면 기본값으로 생성 후 반환.
/// </summary>
public sealed class CollectorSettingsLoader
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive  = true,
        DefaultIgnoreCondition       = JsonIgnoreCondition.WhenWritingNull,
        Encoder                      = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented                = true
    };

    public static string SettingsPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "settings.json");

    public CollectorSettings Settings { get; private set; } = new();

    /// <summary>
    /// settings.json 을 로드합니다.
    /// 파일 없으면 기본값을 저장 후 반환합니다.
    /// </summary>
    public async Task LoadAsync()
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            Settings = new CollectorSettings();
            var json = JsonSerializer.Serialize(Settings, _opts);
            await File.WriteAllTextAsync(path, json, Encoding.UTF8);
            LogManager.Instance.Info("Settings",
                $"settings.json 없음 → 기본값으로 생성: {path}");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            Settings = JsonSerializer.Deserialize<CollectorSettings>(json, _opts)
                       ?? new CollectorSettings();
            LogManager.Instance.Info("Settings",
                $"settings.json 로드 완료 — Provider={Settings.Storage.Provider}, " +
                $"SdtExcDev={Settings.Storage.SdtExcDevPercent}%");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("Settings",
                $"settings.json 파싱 실패 → 기본값 사용: {ex.Message}");
            Settings = new CollectorSettings();
        }
    }
}
