// ══════════════════════════════════════════════════════════
//  IIoT.Collector · ViewModels/DeviceTreeNodeViewModel.cs
//  역할: DeviceTreeView 표시 전용 노드 — DeviceInstance/TagInstance 스냅샷을
//        화면 바인딩용으로 감싼다 (POCO 원본은 변경하지 않음)
//
//  ★ 설계 변경 (버그 수정, 2026-07-06):
//    기존에는 1초마다 DeviceTreeNodeViewModel/TagTreeNodeViewModel 을
//    완전히 새로 생성했는데, WPF TreeViewItem 은 바인딩된 객체의
//    "동일성(identity)" 으로 펼침(IsExpanded) 상태를 추적하기 때문에
//    새 객체로 교체될 때마다 사용자가 접은 트리가 매초 다시 펼쳐지는
//    버그가 발생했다.
//    → 이제 객체를 새로 만들지 않고 Apply() 로 값만 갱신한다.
//      (ObservableObject 로 전환하여 변경분만 바인딩에 반영)
//
//  C-EX-01-6: 신규
//  버그 수정: 2026-07-06 (트리 재확장 문제, 폭 미충족 문제)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Models;
using IIoT.Contracts;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace IIoT.Collector.ViewModels;

/// <summary>Device(PLC) 1개의 트리 표시 노드 — 객체를 유지한 채 값만 갱신된다</summary>
public sealed partial class DeviceTreeNodeViewModel : ObservableObject
{
    /// <summary>PlcId — 갱신 시 동일 항목 식별용 (불변)</summary>
    public string PlcId { get; }

    /// <summary>표시 이름 (불변 — Studio 에서 이름 변경 시에는 재시작으로 전체 재구성됨)</summary>
    public string Name { get; }

    [ObservableProperty] private string _statusIcon  = string.Empty;
    [ObservableProperty] private string _statusColor = string.Empty;
    [ObservableProperty] private string _subTitle    = string.Empty;

    /// <summary>연결 상태 텍스트 (연결됨/일시정지/재연결 중/연결 끊김)</summary>
    [ObservableProperty] private string _connectionStatusText = string.Empty;

    /// <summary>하위 Tag 목록 — Clear/재생성 대신 Apply() 로 항목별 diff 갱신됨</summary>
    public ObservableCollection<TagTreeNodeViewModel> Tags { get; } = new();

    public DeviceTreeNodeViewModel(DeviceInstance d)
    {
        PlcId = d.PlcId;
        Name  = d.Name;
        Apply(d);
    }

    /// <summary>
    /// 기존 인스턴스(및 하위 Tag 인스턴스)를 그대로 유지한 채 최신 값으로 갱신합니다.
    /// TreeViewItem 의 펼침/선택 상태가 보존되는 핵심 포인트입니다.
    /// </summary>
    public void Apply(DeviceInstance d)
    {
        SubTitle = $"{d.DriverId} · Tag {d.TagCount}개 · 알람 {d.ActiveAlarmCount}건";

        (StatusIcon, StatusColor) = d.HealthStatus switch
        {
            DeviceHealthStatus.Alarm        => ("🔴", "#E05050"),
            DeviceHealthStatus.Warning      => ("🟡", "#D4A72C"),
            DeviceHealthStatus.Disconnected => ("⚪", "#888888"),
            _                                => ("🟢", "#3FB950"),
        };

        // ── 통신 상태 라벨 (요청사항: PLC 통신 상태 라벨 추가) ──
        ConnectionStatusText = d.IsPaused
            ? "일시정지"
            : d.IsRetrying
                ? "재연결 중"
                : d.IsConnected
                    ? "연결됨"
                    : "연결 끊김";

        // ── Tag 목록 diff 갱신 (삭제된 Tag 제거) ──
        var latestIds = d.Tags.Select(t => t.TagId).ToHashSet();
        for (var i = Tags.Count - 1; i >= 0; i--)
        {
            if (!latestIds.Contains(Tags[i].TagId))
                Tags.RemoveAt(i);
        }

        // ── 추가/갱신 ──
        foreach (var t in d.Tags)
        {
            var existing = Tags.FirstOrDefault(x => x.TagId == t.TagId);
            if (existing is null)
                Tags.Add(new TagTreeNodeViewModel(t));
            else
                existing.Apply(t);
        }
    }
}

/// <summary>Tag 1개의 트리 표시 노드 — 객체를 유지한 채 값만 갱신된다</summary>
public sealed partial class TagTreeNodeViewModel : ObservableObject
{
    public string TagId { get; }
    public string Name  { get; }

    [ObservableProperty] private string _valueText = "—";

    /// <summary>비활성/품질이상 상태 배지 텍스트 (이름과 값 사이에 표시, 정상이면 빈 문자열)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusIssue))]
    private string _statusBadgeText = string.Empty;

    /// <summary>상태 배지 색상 (헥스 문자열)</summary>
    [ObservableProperty] private string _statusBadgeColor = string.Empty;

    /// <summary>상태 배지 표시 여부 (Visibility 바인딩용)</summary>
    public bool HasStatusIssue => !string.IsNullOrEmpty(StatusBadgeText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlarm))]
    private string _alarmBadge = string.Empty;

    [ObservableProperty] private string _scaleText  = "미설정";
    [ObservableProperty] private string _alarmText  = "미설정";
    [ObservableProperty] private string _notifyText = "미설정";
    [ObservableProperty] private string _extraBadge = string.Empty;

    /// <summary>주소·데이터타입 요약 (예: "40001 · UInt16")</summary>
    [ObservableProperty] private string _addressText = string.Empty;

    /// <summary>가상 Tag 계산식 (예: "수식: [T001] + [T002]"). 가상 Tag 아니면 빈 문자열</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExpression))]
    private string _expressionText = string.Empty;

    /// <summary>가상 Tag 수식 표시 여부 (Visibility 바인딩용)</summary>
    public bool HasExpression => !string.IsNullOrEmpty(ExpressionText);

    /// <summary>알람 배지 표시 여부 (Visibility 바인딩용 — 문자열을 직접 컨버터에 넣지 않음)</summary>
    public bool HasAlarm => !string.IsNullOrEmpty(AlarmBadge);

    public TagTreeNodeViewModel(TagInstance t)
    {
        TagId = t.TagId;
        Name  = t.Name;
        Apply(t);
    }

    /// <summary>기존 인스턴스를 유지한 채 최신 값으로 갱신합니다.</summary>
    public void Apply(TagInstance t)
    {
        ValueText  = t.EngValue is double v ? $"{v:F2} {t.Unit}".Trim() : "—";
        AlarmBadge = t.ActiveAlarmLevel ?? string.Empty;
        ScaleText  = t.Scale is { } s ? $"{s.EngMin}~{s.EngMax}" : "미설정";
        AlarmText  = _BuildAlarmSummary(t.AlarmRule);
        NotifyText = _BuildNotifySummary(t.AlarmRule);

        // ── 요청사항: 비활성/품질이상 상태를 이름-값 사이 배지로 표시 ──
        //   우선순위: 비활성 > 품질 이상(Bad/Timeout/Disconnected) > 정상(배지 없음)
        (StatusBadgeText, StatusBadgeColor) = !t.IsEnabled
            ? ("비활성", "#888888")
            : t.Quality switch
            {
                TagQuality.Bad          => ("품질불량", "#E05050"),
                TagQuality.Timeout      => ("응답없음", "#D4A72C"),
                TagQuality.Disconnected => ("연결끊김", "#888888"),
                _                       => (string.Empty, string.Empty),
            };

        // ExtraBadge 는 "가상" 여부만 표시 (비활성은 위 StatusBadge 로 이동 — 중복 방지)
        ExtraBadge = t.IsVirtual ? "가상" : string.Empty;

        // ── 주소/DataType (요청사항: Tag 주소/DataType 추가) ──
        AddressText = t.IsVirtual
            ? "계산값"
            : $"{t.Address} · {t.DataType}";

        // ── 가상 Tag 수식 (요청사항: 가상 Tag 수식 추가) ──
        ExpressionText = t.IsVirtual && !string.IsNullOrWhiteSpace(t.Expression)
            ? $"수식: {t.Expression}"
            : string.Empty;
    }

    /// <summary>HH/H/L/LL 중 활성화된 레벨만 요약 (예: "HH:90 L:20"). 없으면 "미설정"</summary>
    private static string _BuildAlarmSummary(AlarmEntryDto? a)
    {
        if (a is null) return "미설정";

        var parts = new List<string>();
        if (a.HhEnabled) parts.Add($"HH:{a.HhValue}");
        if (a.HEnabled)  parts.Add($"H:{a.HValue}");
        if (a.LEnabled)  parts.Add($"L:{a.LValue}");
        if (a.LlEnabled) parts.Add($"LL:{a.LlValue}");

        return parts.Count > 0 ? string.Join(" ", parts) : "미설정";
    }

    /// <summary>이메일/Webhook/에스컬레이션 설정 요약. 아무것도 없으면 "미설정"</summary>
    private static string _BuildNotifySummary(AlarmEntryDto? a)
    {
        if (a is null) return "미설정";

        var hasEmail = !string.IsNullOrWhiteSpace(a.NotifyEmail);
        var hasPhone = !string.IsNullOrWhiteSpace(a.NotifyPhone);
        if (!hasEmail && !hasPhone) return "미설정";

        var parts = new List<string>();
        if (hasEmail) parts.Add("이메일");
        if (hasPhone) parts.Add("SMS/Webhook");
        if (a.EscalateMinutes > 0) parts.Add($"{a.EscalateMinutes}분 에스컬레이션");

        return string.Join(", ", parts);
    }
}
