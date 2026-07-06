// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Config/CollectorSettings.cs
//  역할: Collector 런타임 설정 (settings.json) DTO + 로더
//        Storage.Provider: "SQLite" | "InfluxDB"
//        SDT ExcDevPercent: 스케일 범위 대비 허용 오차 비율
//  C-07: 신규
//  C-14: Notification 섹션 추가 (알람 에스컬레이션 이메일/Webhook 알림)
//  C-14 버그 수정: NotificationSettings.Enabled 필드 → 프로퍼티
//                  (System.Text.Json 은 기본적으로 필드를 직렬화하지 않으므로
//                   필드 상태로는 settings.json 에 저장/로드가 반영되지 않는 버그였음)
//  생성: 2026-06-29 / 수정: 2026-07-05
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
    public StorageSettings  Storage  { get; set; } = new();
    public SignalRSettings  SignalR  { get; set; } = new();
    public RetrySettings    Retry    { get; set; } = new();
    // ★ C-14 신규
    public NotificationSettings Notification { get; set; } = new();
    // ★ C-15 신규
    public ForceWriteSettings   ForceWrite   { get; set; } = new();
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

    /// <summary>
    /// .signal 파일 감시 폴더 경로.
    /// null 또는 빈 문자열이면 Collector 자신의 Config 폴더를 감시한다.
    /// Studio 와 Config 폴더를 공유하려면 Studio 실행파일의 Config 폴더 경로를 지정.
    /// 예: "D:\lssLib\IIoT\IIoT.Solution\Studio\IIoT.Studio\bin\Debug\net8.0-windows\Config"
    /// </summary>
    public string? WatchPath { get; set; } = null;

    public MqttPublishSettings Mqtt { get; set; } = new();

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

// ── 재연결 설정 ───────────────────────────────────────────

/// <summary>
/// 드라이버 자동 재연결 설정 (C-12).
/// settings.json 의 Retry 섹션.
/// </summary>
public sealed class RetrySettings
{
    /// <summary>자동 재연결 활성화 (기본 true)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 재연결 시도 간격(초) 목록 — 지수 백오프.
    /// 마지막 값에 도달하면 이후는 마지막 값으로 고정.
    /// 기본: 5 → 15 → 30 → 60초
    /// </summary>
    public int[] IntervalsSec { get; set; } = [5, 15, 30, 60];

    /// <summary>최대 재시도 횟수 (0 = 무제한)</summary>
    public int MaxRetries { get; set; } = 0;
}

// ── SignalR Hub 설정 ──────────────────────────────────────

/// <summary>SignalR Hub 설정. settings.json 의 SignalR 섹션.</summary>
public sealed class SignalRSettings
{
    /// <summary>SignalR Hub 활성화 여부 (기본 true)</summary>
    public bool     Enabled        { get; set; } = true;

    /// <summary>수신 포트 (기본 7878). 방화벽 허용 필요.</summary>
    public int      Port           { get; set; } = 7878;

    /// <summary>
    /// 허용할 CORS Origin 목록.
    /// 빈 배열(기본) = 개발 중 전체 허용.
    /// 운영 환경: ["http://myserver.com", "http://192.168.0.100:3000"]
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}

// ── MQTT 발행 설정 ───────────────────────────────────────

/// <summary>
/// MQTT 발행 설정.
/// settings.json 의 Storage.Mqtt 섹션.
/// 브로커가 없으면 Enabled=false 로 발행 비활성화.
/// </summary>
public sealed class MqttPublishSettings
{
    /// <summary>MQTT 발행 활성화 여부 (기본 false — 브로커 없어도 동작)</summary>
    public bool   Enabled    { get; set; } = false;

    /// <summary>브로커 호스트 (기본 localhost)</summary>
    public string BrokerHost { get; set; } = "localhost";

    /// <summary>브로커 포트 (기본 1883)</summary>
    public int    BrokerPort { get; set; } = 1883;

    /// <summary>클라이언트 ID (null = 자동 생성)</summary>
    public string? ClientId  { get; set; } = null;

    /// <summary>
    /// Tag 값 발행 토픽 접두사.
    /// 실제 토픽: {TopicPrefix}/{PlcId}/{TagId}
    /// 기본: "iiot"
    /// </summary>
    public string TopicPrefix { get; set; } = "iiot";

    /// <summary>QoS 레벨 (0=최대1회, 1=최소1회, 기본 1)</summary>
    public byte   QoS        { get; set; } = 1;

    /// <summary>브로커 인증 사용자명 (없으면 null)</summary>
    public string? Username  { get; set; } = null;

    /// <summary>브로커 인증 비밀번호 (없으면 null)</summary>
    public string? Password  { get; set; } = null;
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

// ── 알림 섹션 (C-14 신규) ─────────────────────────────────

/// <summary>
/// 알람 에스컬레이션 알림 설정.
/// Enabled=false 이면 이메일/Webhook 발송을 전부 생략한다 (개발 중 안전장치).
/// </summary>
public sealed class NotificationSettings
{
    // ★ C-14 버그 수정: 필드(bool Enabled = false;) → 프로퍼티로 변경.
    //   System.Text.Json 은 IncludeFields=true 옵션이 없으면 필드를 무시하므로
    //   필드 상태에서는 settings.json 에 저장/로드가 전혀 반영되지 않았음.
    public bool             Enabled { get; set; } = false;
    public SmtpSettings     Smtp    { get; set; } = new();
    public WebhookSettings  Webhook { get; set; } = new();
}

public sealed class SmtpSettings
{
    /// <summary>SMTP 서버 호스트 (예: smtp.gmail.com, smtp.office365.com)</summary>
    public string Host        { get; set; } = "smtp.gmail.com";
    public int    Port        { get; set; } = 587;
    public bool   UseSsl      { get; set; } = true;
    public string User        { get; set; } = string.Empty;
    public string Password    { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName    { get; set; } = "IIoT Collector";
}

public sealed class WebhookSettings
{
    /// <summary>true 여야 실제 발송 (SMS 등 외부 REST 알림용, 알리고/솔라피/커스텀 등)</summary>
    public bool   Enabled { get; set; } = false;
    public string Url     { get; set; } = string.Empty;
}

// ── 강제쓰기 안전 설정 (C-15 신규) ────────────────────────

/// <summary>
/// Tag 강제값 쓰기(Force Write) 안전 설정.
/// <para>
/// 실 PLC 에 값을 직접 쓰는 기능이므로 기본값은 Enabled=false (비활성)이다.
/// 현장 적용 시 의도적으로 true 로 전환해야 한다 (안전장치).
/// </para>
/// </summary>
public sealed class ForceWriteSettings
{
    /// <summary>강제쓰기 기능 활성화 여부 (기본 false — 안전을 위해 명시적 활성화 필요)</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// true 이면 Studio 알람 라이브러리 등에서 이미 알람(HH/LL) 발생 중인 Tag 에 대한
    /// 쓰기 요청을 추가로 로그에 경고 표시한다 (쓰기 자체를 막지는 않음).
    /// </summary>
    public bool WarnOnActiveAlarm { get; set; } = true;
}
