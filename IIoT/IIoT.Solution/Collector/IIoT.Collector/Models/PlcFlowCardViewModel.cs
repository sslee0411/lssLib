// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Models/PlcFlowCardViewModel.cs
//  역할: 수집 흐름 탭 PLC 카드 1개 상태 ViewModel
//        FlowEngine.Stats 에서 1초 주기로 갱신됨
//  C-09: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Collector.Core.Engine;

namespace IIoT.Collector.Models;

/// <summary>
/// 수집 흐름 탭에서 PLC 1개의 상태를 표시하는 카드 ViewModel.
/// <para>
/// FlowViewModel 이 1초 주기로 FlowEngine.Stats 를 읽어 갱신한다.
/// </para>
/// </summary>
public partial class PlcFlowCardViewModel : ObservableObject
{
    // §1 ─ 불변 정보 ───────────────────────────────────────

    public string PlcId    { get; }
    public string PlcName  { get; }
    public string DriverId { get; }
    public int    TagCount { get; }
    public int    PollMs   { get; }

    // §2 ─ 실시간 상태 ─────────────────────────────────────

    /// <summary>드라이버 연결 여부 (LED 색상 결정)</summary>
    [ObservableProperty] private bool   _isConnected;

    /// <summary>누적 폴링 횟수</summary>
    [ObservableProperty] private long   _pollCount;

    /// <summary>누적 오류 횟수</summary>
    [ObservableProperty] private long   _errorCount;

    /// <summary>오류율 표시 문자열 (예: "0.0%")</summary>
    [ObservableProperty] private string _errorRateText = "0.0%";

    /// <summary>마지막 폴링 시각 (표시용)</summary>
    [ObservableProperty] private string _lastPollAtText = "—";

    /// <summary>마지막 오류 메시지 (null=정상)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _lastError;

    /// <summary>오류 존재 여부 (XAML Visibility 바인딩용)</summary>
    public bool HasError => LastError is not null;

    /// <summary>재연결 시도 중 여부 (C-12)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRetryStatus))]
    private bool _isRetrying;

    /// <summary>재연결 시도 횟수 (C-12)</summary>
    [ObservableProperty] private int    _retryCount;

    /// <summary>재연결 상태 텍스트 (C-12) — XAML Visibility 바인딩용</summary>
    [ObservableProperty] private string _retryStatusText = string.Empty;

    /// <summary>재연결 상태 표시 여부 (C-12)</summary>
    public bool HasRetryStatus => IsRetrying;

    /// <summary>연결 상태 표시 텍스트</summary>
    [ObservableProperty] private string _statusText = "대기 중";

    /// <summary>연결 상태 색상 (DynamicResource 브러시 키)</summary>
    [ObservableProperty] private string _statusColor = "#888888";

    /// <summary>초당 수집 건수 (최근 1초 기준 — Tag수 × 1회 폴링)</summary>
    [ObservableProperty] private string _tpsText = "—";

    // §3 ─ 생성자 ──────────────────────────────────────────

    public PlcFlowCardViewModel(
        string plcId, string plcName, string driverId,
        int tagCount, int pollMs)
    {
        PlcId    = plcId;
        PlcName  = plcName;
        DriverId = driverId;
        TagCount = tagCount;
        PollMs   = pollMs;
    }

    // §4 ─ 갱신 ────────────────────────────────────────────

    public void Update(FlowEngine.PlcPollStat stat)
    {
        IsConnected = stat.IsConnected;
        PollCount   = stat.PollCount;
        ErrorCount  = stat.ErrorCount;
        LastError   = stat.LastError;

        // 오류율
        var errorRate = PollCount > 0
            ? (ErrorCount * 100.0 / PollCount)
            : 0.0;
        ErrorRateText = $"{errorRate:F1}%";

        // 마지막 폴링 시각
        LastPollAtText = stat.LastPollAt == default
            ? "—"
            : stat.LastPollAt.ToLocalTime().ToString("HH:mm:ss");

        // 상태 텍스트 + 색상
        if (IsConnected)
        {
            StatusText  = "● 수집 중";
            StatusColor = "#3FB950";
        }
        else if (LastError is not null)
        {
            StatusText  = "● 오류";
            StatusColor = "#E05050";
        }
        else
        {
            StatusText  = "○ 대기";
            StatusColor = "#888888";
        }

        // TPS (이론값: TagCount / (PollMs / 1000))
        var tps = TagCount / Math.Max(PollMs / 1000.0, 0.1);
        TpsText = $"{tps:F1} tag/s";

        // ★ C-12: 재연결 상태
        IsRetrying      = stat.IsRetrying;
        RetryCount      = stat.RetryCount;
        RetryStatusText = stat.RetryStatusText;

        // 재연결 중이면 상태 텍스트 덮어쓰기
        if (stat.IsRetrying)
        {
            StatusText  = $"↻ 재연결 중 ({stat.RetryCount}회)";
            StatusColor = "#EF9F27";
        }
    }
}
