// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Models/LiveTagViewModel.cs
//  역할: 수집 현황 DataGrid 1행에 바인딩되는 실시간 Tag 값 모델
//        TagValueUpdatedEvent 수신 시 Update() 호출로 값 갱신
//  C-04: 신규
//  C-05-fix: Raw 값과 스케일된 값을 별도 컬럼으로 분리 표시
//            (기존엔 ScaleEngine 적용값만 보이고 Raw 가 버려짐 — 사용자 요청으로 추가)
//  생성: 2026-06-29 / 수정: 2026-06-29
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Collector.Core.Events;
using IIoT.Contracts;

namespace IIoT.Collector.Models;

/// <summary>
/// 수집 현황 화면(StatusView) DataGrid 1행에 해당하는 실시간 Tag 상태.
/// <para>
/// PlcId/TagId 는 불변, 나머지 값은 TagValueUpdatedEvent 수신마다 Update() 로 갱신된다.
/// Raw 값(드라이버 원시값)과 스케일 변환값(ScaleEngine 적용 후 공학값)을 모두 보관하여
/// DataGrid 에서 두 컬럼으로 동시에 비교 표시할 수 있다.
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

    /// <summary>device.json Tag.Unit 원본값 (참고용 — 실제 표시는 DisplayUnit 사용)</summary>
    public string Unit { get; }

    // §2 ─ 실시간 갱신 값 ──────────────────────────────────

    /// <summary>
    /// Raw 표시값 — 드라이버가 반환한 원시값 그대로 (스케일 미적용).
    /// 예: PLC 레지스터 원시값 0~4000 그대로.
    /// </summary>
    [ObservableProperty]
    private string _rawDisplayValue = "—";

    /// <summary>
    /// 스케일 적용 표시값 — ScaleEngine.Apply() 결과 (Tag 에 ScaleEntryId 가 없으면 Raw 와 동일).
    /// 예: 0~4000 → 0~10 bar 로 변환된 값.
    /// </summary>
    [ObservableProperty]
    private string _scaledDisplayValue = "—";

    /// <summary>
    /// 스케일이 실제로 적용되었는지 여부.
    /// false 면 ScaledDisplayValue 가 RawDisplayValue 와 사실상 동일한 값(단위 포맷만 다름)임을 의미.
    /// DataGrid 에서 스케일 미설정 Tag 를 시각적으로 구분할 때 사용 가능.
    /// </summary>
    [ObservableProperty]
    private bool _wasScaled;

    /// <summary>품질 코드 — DataGrid 색상 표시에 사용</summary>
    [ObservableProperty]
    private TagQuality _quality = TagQuality.Disconnected;

    /// <summary>마지막 갱신 시각 (로컬 시간, 표시용 문자열)</summary>
    [ObservableProperty]
    private string _updatedAtText = "—";

    /// <summary>
    /// 실제 표시 단위.
    /// 초기값은 device.json Tag.Unit, ScaleEngine 적용 후에는 ScaleEntry.Unit 으로 갱신됨.
    /// </summary>
    [ObservableProperty]
    private string _displayUnit;

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
        DisplayUnit = unit;
    }

    // §4 ─ 갱신 ────────────────────────────────────────────

    /// <summary>
    /// TagValueUpdatedEvent 수신 시 호출하여 표시값을 갱신합니다.
    /// Raw 값(e.Value.RawValue)과 스케일 변환값(e.EngValue)을 각각 별도 프로퍼티에 반영한다.
    /// 반드시 UI 스레드에서 호출할 것 (구독측에서 Dispatcher.InvokeAsync 처리).
    /// </summary>
    public void Update(TagValueUpdatedEvent e)
    {
        var isGood = e.Value.Quality == TagQuality.Good;

        RawDisplayValue = isGood
            ? _FormatRaw(e.Value.RawValue)
            : "—";

        ScaledDisplayValue = isGood
            ? e.EngValue.ToString("F" + Math.Clamp(e.DecimalPlaces, 0, 6))
            : "—";

        WasScaled     = e.WasScaled;
        Quality       = e.Value.Quality;
        UpdatedAtText = e.Value.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        DisplayUnit   = e.Unit;

        // 플래시 효과: true → (UI 렌더 후) false 로 되돌리는 처리는
        // View 코드비하인드에서 짧은 딜레이 후 수행 (C-04 LiveTagView.xaml.cs)
        IsFlashing = true;
    }

    // §5 ─ Raw 값 포맷 헬퍼 ─────────────────────────────────

    private static string _FormatRaw(object? raw) => raw switch
    {
        null     => "—",
        double d => d.ToString("F2"),
        float f  => f.ToString("F2"),
        bool b   => b ? "ON" : "OFF",
        _        => raw.ToString() ?? "—"
    };
}
