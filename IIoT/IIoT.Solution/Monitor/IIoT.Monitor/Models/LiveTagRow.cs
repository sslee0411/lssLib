// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Models/LiveTagRow.cs
//  역할: [태그현황] 그리드 1행 — Collector→PLC→Tag 계층 그룹핑의 최하위 단위
//  MN-02: 신규
//  MN-EX-05: IsFavorite 속성 추가 (즐겨찾기/핀 고정)
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-EX-05)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Monitor.Models;

/// <summary>
/// 실시간 Tag 값 1건.
/// <para>
/// Collector 의 "TagValue" SignalR 이벤트를 받을 때마다 이 행이 생성/갱신된다.
/// CollectorId 는 이벤트 payload 에 없으므로(C-EX-11 미완료) 발신 HubConnection의
/// 출처(연결 자체)를 기준으로 태깅한다 — MN-01B 설계와 동일한 전제.
/// </para>
/// </summary>
public partial class LiveTagRow : ObservableObject
{
    /// <summary>발신 Collector ID (연결 기준 태깅) — 그룹핑 키이므로 생성 후 불변</summary>
    public required string CollectorId { get; init; }

    /// <summary>발신 Collector 표시 이름 (그룹 헤더용). [Collector 관리] 탭에서 이름 변경 시 실시간 갱신됨</summary>
    [ObservableProperty] private string _collectorName = string.Empty;

    public required string PlcId { get; init; }

    public required string TagId { get; init; }

    [ObservableProperty] private double _rawValue;

    [ObservableProperty] private double _engValue;

    [ObservableProperty] private string _unit = string.Empty;

    /// <summary>Good / Bad / Timeout / Disconnected</summary>
    [ObservableProperty] private string _quality = "Good";

    [ObservableProperty] private DateTimeOffset _updatedAt;

    /// <summary>★ MN-EX-05 신규: 즐겨찾기(핀 고정) 여부. FavoriteTagService 가 관리한다.</summary>
    [ObservableProperty] private bool _isFavorite;

    /// <summary>내부 인덱싱용 고유 키 (CollectorId:PlcId:TagId)</summary>
    public string RowKey => $"{CollectorId}:{PlcId}:{TagId}";
}
