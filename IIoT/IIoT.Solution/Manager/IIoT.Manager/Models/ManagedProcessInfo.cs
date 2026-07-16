// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Models/ManagedProcessInfo.cs
//  역할: 관리 대상 프로그램 정의 모델 + 프로세스 상태 enum
//  MG-01: 신규 — Studio·Collector·Monitor 3개 기본 정의
//  MG-02: ExePath 추가 (manager.json 직렬화 DTO 겸용 —
//         Manager 실행 폴더 기준 상대 경로 허용)
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-02)
// ══════════════════════════════════════════════════════════

namespace IIoT.Manager.Models;

/// <summary>프로세스 실행 상태.</summary>
public enum ProcessState
{
    /// <summary>정지 (프로세스 없음)</summary>
    Stopped,

    /// <summary>실행 중</summary>
    Running,

    /// <summary>오류 — 프로세스는 있으나 응답 없음 (MG-03 헬스체크에서 판정 예정)</summary>
    Error
}

/// <summary>
/// Manager 가 관리하는 대상 프로그램 1개의 정의.
/// <para>
/// MG-01: 프로세스 이름 기반 상태 감지에 사용.
/// MG-02: manager.json 직렬화 DTO 겸용 — ExePath 추가.
/// </para>
/// </summary>
public sealed class ManagedProcessInfo
{
    // §1 ─ 속성 ──────────────────────────────────────────────

    /// <summary>고유 ID (예: "studio")</summary>
    public required string Id { get; init; }

    /// <summary>표시 이름 (예: "IIoT.Studio")</summary>
    public required string Name { get; init; }

    /// <summary>한 줄 설명 (카드에 표시)</summary>
    public required string Description { get; init; }

    /// <summary>
    /// 프로세스 이름 — Process.GetProcessesByName() 인자.
    /// 확장자(.exe) 없이 지정 (예: "IIoT.Studio")
    /// </summary>
    public required string ProcessName { get; init; }

    /// <summary>
    /// ★ MG-02: 실행 파일 경로. 절대 경로 또는 Manager 실행 폴더 기준 상대 경로.
    /// 기본값은 개발(Debug) 빌드 출력 위치 — 배포 시 manager.json 에서 수정.
    /// </summary>
    public string ExePath { get; set; } = "";

    /// <summary>
    /// ★ MG-03: 자동복구 — 헬스체크 연속 3회 실패 시 자동 재시작.
    /// 기본 false (안전) — manager.json 에서 프로그램별로 활성화.
    /// (Studio 같은 편집기는 미저장 작업 손실 위험이 있어 기본 비활성 권장)
    /// </summary>
    public bool AutoRestart { get; set; } = false;

    /// <summary>
    /// ★ MG-EX-03: 자동 기동 — Manager 시작 시 이 프로그램을 자동 시작.
    /// 기본 false — manager.json 에서 프로그램별로 활성화 (Collector·Monitor 권장).
    /// 시작 순서 = Processes[] 배열 순서.
    /// </summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>
    /// ★ MG-EX-03: 자동 기동 후 다음 프로그램 시작까지 대기 초.
    /// (예: Collector 가 Hub 를 먼저 열도록 5초 뒤 Monitor 시작)
    /// </summary>
    public int AutoStartDelaySec { get; set; } = 3;

    // §2 ─ 기본 정의 ─────────────────────────────────────────

    /// <summary>
    /// 관리 대상 기본 3개 프로그램 (Studio·Collector·Monitor).
    /// ExePath 기본값: Manager 실행 폴더(bin\Debug\net8.0-windows) 기준
    /// IIoT.Solution 루트까지 5단계 상위 → 각 프로그램의 Debug 출력 경로.
    /// </summary>
    public static IReadOnlyList<ManagedProcessInfo> Defaults { get; } =
    [
        new()
        {
            Id = "studio", Name = "IIoT.Studio", Description = "설정 편집기",
            ProcessName = "IIoT.Studio",
            ExePath = @"..\..\..\..\..\Studio\IIoT.Studio\bin\Debug\net8.0-windows\IIoT.Studio.exe"
        },
        new()
        {
            Id = "collector", Name = "IIoT.Collector", Description = "수집·감지·저장",
            ProcessName = "IIoT.Collector",
            ExePath = @"..\..\..\..\..\Collector\IIoT.Collector\bin\Debug\net8.0-windows\IIoT.Collector.exe"
        },
        new()
        {
            Id = "monitor", Name = "IIoT.Monitor", Description = "실시간 모니터링",
            ProcessName = "IIoT.Monitor",
            ExePath = @"..\..\..\..\..\Monitor\IIoT.Monitor\bin\Debug\net8.0-windows\IIoT.Monitor.exe"
        },
    ];
}
