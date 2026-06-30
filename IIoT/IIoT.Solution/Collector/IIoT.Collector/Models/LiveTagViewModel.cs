// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Models/LiveTagViewModel.cs
//  역할: 수집 현황 DataGrid 1행에 바인딩되는 실시간 Tag 값 모델
//        TagValueUpdatedEvent 수신 시 Update() 호출로 값 갱신
//  C-04: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Collector.Core.Events;
using IIoT.Contracts;

namespace IIoT.Collector.Models;

/// <summary>
/// 수집 현황 화면(StatusView) DataGrid 1행에 해당하는 실시간 Tag 상태.
/// <para>
/// PlcId/TagId 는 불변, 나머지 값은 TagValueUpdatedEvent 수신마다 Update() 로 갱신된다.
/// </para>
/// </summary>
public partial class LiveTagViewModel : ObservableObject
{
    // §1 ─ 불변 식별 정보 ──────────────────────────────────

    /// <summary>Tag 고유 ID (device.json Tag 노드 Id)</summary>
    public string TagId { get; }

    /// <summary>Tag 표시 이름</summary>
    public string Name { get; }

    /// <summary>이 Tag 가 속한 PLC/Device ID</summary>
    public string PlcId { get; }

    /// <summary>이 Tag 가 속한 PLC/Device 표시 이름 (DataGrid 그룹핑·필터용)</summary>
    public string PlcName { get; }

    /// <summary>공학 단위 (예: bar, °C) — C-05 ScaleEngine 적용 전까지는 빈 문자열</summary>
    public string Unit { get; }

    // §2 ─ 실시간 갱신 값 ──────────────────────────────────

    /// <summary>현재 표시값 (문자열 — RawValue 를 보기 좋게 포맷)</summary>
    [ObservableProperty]
    private string _displayValue = "—";

    /// <summary>품질 코드 — DataGrid 색상 표시에 사용</summary>
    [ObservableProperty]
    private TagQuality _quality = TagQuality.Disconnected;

    /// <summary>마지막 갱신 시각 (로컬 시간, 표시용 문자열)</summary>
    [ObservableProperty]
    private string _updatedAtText = "—";

    /// <summary>
    /// 값 변경 플래시 효과 트리거.
    /// Update() 호출마다 true → false 토글하여 DataGrid 행 애니메이션 트리거.
    /// (XAML DataTrigger 에서 바인딩하여 짧은 배경색 플래시 구현 — C-04 UI 연동)
    /// </summary>
    [ObservableProperty]
    private bool _isFlashing;

    // §3 ─ 생성자 ──────────────────────────────────────────

    public LiveTagViewModel(string tagId, string name, string plcId, string plcName, string unit)
    {
        TagId   = tagId;
        Name    = name;
        PlcId   = plcId;
        PlcName = plcName;
        Unit    = unit;
    }

    // §4 ─ 갱신 ────────────────────────────────────────────

    /// <summary>
    /// TagValueUpdatedEvent 수신 시 호출하여 표시값을 갱신합니다.
    /// 반드시 UI 스레드에서 호출할 것 (구독측에서 Dispatcher.InvokeAsync 처리).
    /// </summary>
    public void Update(TagValue value)
    {
        DisplayValue  = _FormatValue(value.RawValue);
        Quality       = value.Quality;
        UpdatedAtText = value.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

        // 플래시 효과: true → (UI 렌더 후) false 로 되돌리는 처리는
        // View 코드비하인드에서 짧은 딜레이 후 수행 (C-04 LiveTagView.xaml.cs)
        IsFlashing = true;
    }

    private static string _FormatValue(object? raw) => raw switch
    {
        null         => "—",
        double d     => d.ToString("F2"),
        float f      => f.ToString("F2"),
        bool b       => b ? "ON" : "OFF",
        _            => raw.ToString() ?? "—"
    };
}
