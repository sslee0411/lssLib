// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Models/DeviceInstance.cs
//  역할: PLC/Device 1개 + 하위 Tag 전체를 하나의 트리로 통합한 조회 전용 모델
//        (통신 설정 + 실시간 값 + 스케일 + 알람 상태를 한 번에 노출)
//
//  ★ 설계 원칙: 이 모델은 순수 "조합(읽기 전용 뷰)"이다.
//    CollectorConfigLoader/StatusViewModel/AlarmStateManager 등 원본 데이터
//    소유자의 내부 로직에는 전혀 관여하지 않으며, DeviceInstanceService 가
//    이벤트 구독을 통해 이 모델의 필드만 갱신한다.
//    → IIoT.Monitor 가 SignalR/API 로 그대로 소비할 데이터 계약(Contract) 역할.
//
//  C-EX-01: 신규 (Collector 실무강화 이후, Monitor 착수 전 사전 작업)
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Contracts;
using System.Linq;

namespace IIoT.Collector.Core.Models;

/// <summary>
/// DeviceInstance 종합 상태 (계산값 — HealthStatus 프로퍼티에서 사용).
/// </summary>
public enum DeviceHealthStatus
{
    /// <summary>정상 — 연결됨, 활성 알람 없음</summary>
    Normal,
    /// <summary>경고 — 연결됨, L/H 등급 알람 발생 중</summary>
    Warning,
    /// <summary>위험 — 연결됨, HH/LL 등급 알람 발생 중</summary>
    Alarm,
    /// <summary>연결 끊김 (일시정지 포함)</summary>
    Disconnected
}

/// <summary>
/// PLC/Device 1개의 통합 인스턴스.
/// <para>
/// 통신·설정 정보(<see cref="PlcRuntimeConfig"/> 기반)와 하위 Tag 전체
/// (<see cref="Tags"/>)를 하나의 트리로 묶는다.
/// </para>
/// </summary>
public sealed class DeviceInstance
{
    // §1 ─ 불변 식별·설정 정보 ─────────────────────────────

    /// <summary>PLC/Device 고유 ID</summary>
    public string PlcId { get; init; } = string.Empty;

    /// <summary>표시 이름</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>원본 노드 타입 ("PLC" 또는 "Device")</summary>
    public string NodeType { get; init; } = string.Empty;

    /// <summary>플러그인 드라이버 ID (예: "modbus-tcp")</summary>
    public string DriverId { get; init; } = string.Empty;

    /// <summary>폴링 주기 (ms)</summary>
    public int PollMs { get; init; }

    /// <summary>이 Device 하위 Tag 전체 (실제 + 가상 Tag 모두 포함)</summary>
    public IReadOnlyList<TagInstance> Tags { get; init; } = Array.Empty<TagInstance>();

    // §2 ─ 실시간 상태 (DeviceInstanceService 가 갱신) ─────

    /// <summary>드라이버 연결 여부</summary>
    public bool IsConnected { get; set; }

    /// <summary>수집 일시정지 여부 (C-19)</summary>
    public bool IsPaused { get; set; }

    /// <summary>재연결 시도 중 여부 (C-12)</summary>
    public bool IsRetrying { get; set; }

    // §3 ─ 계산 프로퍼티 (C-EX-01-4 신규) ──────────────────

    /// <summary>하위 Tag 총 개수</summary>
    public int TagCount => Tags.Count;

    /// <summary>품질=Good 인 Tag 개수</summary>
    public int GoodCount => Tags.Count(t => t.Quality == TagQuality.Good);

    /// <summary>품질≠Good 인 Tag 개수 (Bad/Timeout/Disconnected 합)</summary>
    public int BadCount => Tags.Count(t => t.Quality != TagQuality.Good);

    /// <summary>현재 활성 알람이 걸려 있는 Tag 개수</summary>
    public int ActiveAlarmCount => Tags.Count(t => t.ActiveAlarmLevel is not null);

    /// <summary>
    /// 종합 상태 요약.
    /// 우선순위: 연결끊김/일시정지 &gt; HH·LL 알람 &gt; H·L 알람 &gt; 정상
    /// </summary>
    public DeviceHealthStatus HealthStatus
    {
        get
        {
            if (!IsConnected || IsPaused) return DeviceHealthStatus.Disconnected;
            if (Tags.Any(t => t.ActiveAlarmLevel is "HH" or "LL")) return DeviceHealthStatus.Alarm;
            if (ActiveAlarmCount > 0) return DeviceHealthStatus.Warning;
            return DeviceHealthStatus.Normal;
        }
    }
}

/// <summary>
/// Tag 1개의 통합 인스턴스.
/// <para>
/// 정적 설정(주소·타입) + 실시간 값 + 해석된 스케일 규칙 + 해석된 알람 규칙과
/// 현재 알람 상태를 모두 담는다. Scale/Alarm 은 ID 참조가 아니라 실제 값이다.
/// </para>
/// </summary>
public sealed class TagInstance
{
    // §1 ─ 불변 식별·설정 정보 ─────────────────────────────

    public string PlcId    { get; init; } = string.Empty;
    public string TagId    { get; init; } = string.Empty;
    public string Name     { get; init; } = string.Empty;
    public string Address  { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public string Memo     { get; init; } = string.Empty;
    public bool   IsEnabled  { get; init; } = true;
    public bool   IsVirtual  { get; init; } = false;

    /// <summary>가상(계산) Tag 수식 (예: "[T001] + [T002]"). IsVirtual=false 면 null</summary>
    public string? Expression { get; init; }

    /// <summary>해석된 스케일 규칙 (null = 스케일 미설정, Raw 값 그대로 사용)</summary>
    public ScaleEntryDto? Scale { get; init; }

    /// <summary>해석된 알람 규칙 (null = 알람 미설정)</summary>
    public AlarmEntryDto? AlarmRule { get; init; }

    // §2 ─ 실시간 값 (DeviceInstanceService 가 TagValueUpdatedEvent 로 갱신) ─

    public double?         RawValue  { get; set; }
    public double?         EngValue  { get; set; }
    public string          Unit      { get; set; } = string.Empty;
    public TagQuality      Quality   { get; set; } = TagQuality.Disconnected;
    public DateTimeOffset? UpdatedAt { get; set; }

    // §3 ─ 실시간 알람 상태 (DeviceInstanceService 가 AlarmChangedEvent 로 갱신) ─

    /// <summary>현재 발생 중인 알람 레벨 ("HH"/"H"/"L"/"LL", 없으면 null)</summary>
    public string? ActiveAlarmLevel { get; set; }

    /// <summary>현재 알람 상태 문자열 ("Active"/"Acked"/"Recovered", 알람 없으면 null)</summary>
    public string? AlarmStatusText { get; set; }
}
