// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Models/CollectorEndpoint.cs
//  역할: 등록된 Collector 1개의 접속 정보 (hmi.json Collectors[] 1항목)
//        UI(DataGrid) 편집 + JSON 직렬화를 동일 모델로 겸용한다.
//        (IIoT.Monitor Models/CollectorEndpoint.cs — MN-01/MN-01B 이식)
//  HM-01: 신규
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace IIoT.HMI.Models;

/// <summary>
/// 등록된 Collector 1개의 접속 정보.
/// <para>
/// 이 정보를 기반으로 CollectorId(Id) → HubConnection 1:1 매핑을 생성한다.
/// Id 는 최초 등록 시 비워두거나 임의값으로 두어도 무방 — 자동 동기화 로직이
/// 최초 연결 성공 시 Collector 측 실제 CollectorId(C-EX-10)로 자동 교정한다.
/// </para>
/// </summary>
public partial class CollectorEndpoint : ObservableObject
{
    /// <summary>
    /// Collector 고유 식별자.
    /// ★ 최초 연결 성공 시 Collector 응답의 실제 CollectorId로 자동 갱신됨.
    ///   (수동 일치 불필요 — 틀리거나 비어 있어도 자동 교정)
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>표시 이름 (예: "공장1-1호기")</summary>
    [ObservableProperty]
    private string _name = "새 Collector";

    /// <summary>Collector Host/IP (예: localhost, 192.168.0.10)</summary>
    [ObservableProperty]
    private string _host = "localhost";

    /// <summary>Collector SignalR 포트 (기본 7878 — Collector settings.json SignalR.Port 와 일치)</summary>
    [ObservableProperty]
    private int _port = 7878;

    /// <summary>연결 시도 활성화 여부. false 면 연결 관리자가 이 항목을 건너뜀</summary>
    [ObservableProperty]
    private bool _enabled = true;

    /// <summary>
    /// ★ 현재 연결 상태 표시 텍스트 (예: "연결됨", "재연결 중...", "미연결").
    /// CollectorConnection 이 실시간으로 갱신하며, hmi.json 에는 저장하지 않는다.
    /// </summary>
    [JsonIgnore]
    [ObservableProperty]
    private string _statusText = "미연결";

    /// <summary>
    /// Hub 연결 URL을 조립합니다.
    /// 예: http://localhost:7878/iiot
    /// </summary>
    [JsonIgnore]
    public string HubUrl => $"http://{Host}:{Port}/iiot";
}
