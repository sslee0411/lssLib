// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Models/CollectorEndpoint.cs
//  역할: 등록된 Collector 1개의 접속 정보 (monitor.json Collectors[] 1항목)
//        UI(DataGrid) 편집 + JSON 직렬화를 동일 모델로 겸용한다.
//  MN-01: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Monitor.Models;

/// <summary>
/// 등록된 Collector 1개의 접속 정보.
/// <para>
/// MN-01B 이후 이 정보를 기반으로 CollectorId(Id) → HubConnection 1:1 매핑을 생성한다.
/// Id 는 Collector 측 settings.json 의 CollectorId(C-EX-10)와 문자열로 일치시켜야
/// REST 스냅샷/실시간 이벤트의 출처를 올바르게 병합할 수 있다.
/// </para>
/// </summary>
public partial class CollectorEndpoint : ObservableObject
{
    /// <summary>
    /// Collector 고유 식별자.
    /// ★ Collector 측 settings.json CollectorId(C-EX-10)와 반드시 동일한 값으로 입력할 것.
    /// (자동 매칭 로직 없음 — MN-01 단계에서는 담당자가 직접 일치시켜 등록)
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

    /// <summary>연결 시도 활성화 여부. false 면 MN-01B 연결 관리자가 이 항목을 건너뜀</summary>
    [ObservableProperty]
    private bool _enabled = true;

    /// <summary>
    /// Hub 연결 URL을 조립합니다.
    /// 예: http://localhost:7878/iiot
    /// </summary>
    public string HubUrl => $"http://{Host}:{Port}/iiot";
}
