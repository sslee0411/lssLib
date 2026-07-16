// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/Config/ManagerSettings.cs
//  역할: Manager 런타임 설정 (manager.json) DTO + 로더/세이버
//        경로: {Manager 실행파일}\Config\manager.json
//  MG-02: 신규 — Processes[] (관리 대상 프로그램 + 실행 경로)
//         MonitorSettingsLoader(monitor.json) 와 동일한 패턴
//  MG-06: Deploy 섹션 추가 — 설정 배포 (소스 Config 폴더 + 배포 파일 목록)
//  MG-07: Schedules[] 추가 — 스케줄 관리 (지정 시각 자동 시작/정지/재시작)
//  MG-EX-05: Resource 섹션 추가 — CPU/메모리 임계값 (초과 시 경고 이벤트)
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-EX-05)
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

    /// <summary>★ MG-06: 설정 배포 설정 (구버전 manager.json 에 없으면 기본값 사용)</summary>
    public DeploySettings Deploy { get; set; } = new();

    /// <summary>★ MG-07: 스케줄 목록 (구버전 manager.json 에 없으면 빈 목록)</summary>
    public List<ScheduleEntry> Schedules { get; set; } = new();

    /// <summary>★ MG-EX-05: 리소스 임계값 (구버전 manager.json 에 없으면 기본값)</summary>
    public ResourceSettings Resource { get; set; } = new();
}

/// <summary>
/// ★ MG-EX-05: 리소스 모니터링 임계값 — 초과 시 경고 이벤트(트레이 알림 대상).
/// 전 프로그램 공통 적용. 경고는 프로그램·항목별 5분 쿨다운 (반복 알림 방지).
/// </summary>
public sealed class ResourceSettings
{
    /// <summary>CPU 사용률 경고 임계 (%). 0 이하 = 검사 안 함.</summary>
    public double CpuWarnPercent { get; set; } = 80;

    /// <summary>메모리(WorkingSet) 경고 임계 (MB). 0 이하 = 검사 안 함.</summary>
    public double MemoryWarnMb { get; set; } = 1024;
}

/// <summary>
/// ★ MG-06: 설정 배포 설정.
/// 소스(마스터) = Studio 의 Config 폴더 — Studio 에서 [저장]한 설정이 원본.
/// 대상 = 각 프로그램의 {exe 폴더}\Config.
/// </summary>
public sealed class DeploySettings
{
    /// <summary>
    /// 마스터 설정 폴더. 절대 경로 또는 Manager 실행 폴더 기준 상대 경로.
    /// 기본값: Studio Debug 출력의 Config 폴더.
    /// </summary>
    public string SourceConfigDir { get; set; } =
        @"..\..\..\..\..\Studio\IIoT.Studio\bin\Debug\net8.0-windows\Config";

    /// <summary>배포 대상 파일 목록 (소스 폴더 기준 파일명)</summary>
    public List<string> Files { get; set; } = ["device.json", "collect.json"];
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
        WriteIndented               = true,
        // ★ MG-07: enum 을 문자열로 직렬화 (Schedules[].Action = "Restart" 등 —
        //   사람이 manager.json 직접 편집 가능하도록. 숫자도 역직렬화 허용됨)
        Converters                  = { new JsonStringEnumConverter() }
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
