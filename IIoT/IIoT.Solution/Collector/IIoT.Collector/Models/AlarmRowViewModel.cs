// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Models/AlarmRowViewModel.cs
//  역할: AlarmView DataGrid/ListView 1행에 바인딩되는 알람 상태 모델
//  C-06: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Collector.Core.Events;

namespace IIoT.Collector.Models;

/// <summary>
/// 활성 알람 목록 및 이력 1행.
/// <para>
/// <c>Status</c> 가 변경될 때마다 <see cref="UpdateStatus"/> 를 호출하여 갱신한다.
/// </para>
/// </summary>
public partial class AlarmRowViewModel : ObservableObject
{
    // §1 ─ 불변 정보 ───────────────────────────────────────

    public string AlarmKey  { get; }
    public string TagId     { get; }
    public string TagName   { get; }
    public string PlcId     { get; }
    public string LevelText { get; }
    public string Message   { get; }

    /// <summary>알람 최초 발생 시각 (표시용)</summary>
    public string OccurredAtText { get; }

    // §2 ─ 갱신 가능 상태 ──────────────────────────────────

    [ObservableProperty]
    private AlarmStatus _status;

    [ObservableProperty]
    private string _statusText = "발생";

    [ObservableProperty]
    private double _currentEngValue;

    [ObservableProperty]
    private string _updatedAtText = string.Empty;

    // §3 ─ 파생 프로퍼티 ───────────────────────────────────

    /// <summary>레벨별 색상 키 (XAML DataTrigger 사용)</summary>
    public string LevelColor => LevelText switch
    {
        "HH" => "#E05050",
        "H"  => "#EF9F27",
        "L"  => "#4C9FD4",
        "LL" => "#AA66FF",
        _    => "#888888"
    };

    // §4 ─ 생성자 ──────────────────────────────────────────

    public AlarmRowViewModel(AlarmChangedEvent e)
    {
        AlarmKey       = e.AlarmKey;
        TagId          = e.TagId;
        TagName        = e.TagName;
        PlcId          = e.PlcId;
        LevelText      = e.Level.ToString();
        Message        = e.Message;
        OccurredAtText = e.OccurredAt.ToLocalTime().ToString("HH:mm:ss");
        Status         = e.Status;
        StatusText     = _ToStatusText(e.Status);
        CurrentEngValue = e.CurrentEngValue;
        UpdatedAtText   = OccurredAtText;
    }

    // §5 ─ 갱신 ────────────────────────────────────────────

    public void UpdateStatus(AlarmChangedEvent e)
    {
        Status          = e.Status;
        StatusText      = _ToStatusText(e.Status);
        CurrentEngValue = e.CurrentEngValue;
        UpdatedAtText   = e.OccurredAt.ToLocalTime().ToString("HH:mm:ss");
    }

    // §6 ─ 헬퍼 ───────────────────────────────────────────

    private static string _ToStatusText(AlarmStatus s) => s switch
    {
        AlarmStatus.Active    => "발생",
        AlarmStatus.Acked     => "확인됨",
        AlarmStatus.Recovered => "복귀",
        _                     => "알 수 없음"
    };
}
