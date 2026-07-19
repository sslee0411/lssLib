// ══════════════════════════════════════════════════════════
//  IIoT.HMI · ViewModels/LayoutCanvasViewModel.cs
//  역할: 레이아웃 편집 캔버스 ViewModel — 아이콘 배치·선택·삭제·줌/팬
//        (IIoT.Studio ViewModels/CanvasViewModel.cs 중 포트/연결선 관련
//         로직(AddConnection/RefreshConnections/드래그 미리보기 등)을 전부
//         제외하고 이식 — HM-03 은 프리폼 배치 메커니즘만 다룬다)
//  HM-03: 신규
//  HM-04: PaletteItems 가 LayoutNodeFactory 확장(모터/컨베이어/탱크/밸브)을
//         그대로 반영 — 이 파일 자체는 로직 변경 없음(주석만 갱신)
//  HM-05: Tag 바인딩 기능 추가
//         ① CollectorConnectionManager 주입(DI) — Collector/Device/Tag
//           선택기 데이터 조회 + TagValueReceived 실시간 구독
//         ② 선택된 노드(SelectedNode)에 대한 Collector→Device→Tag 3단 계단식
//           선택 프로퍼티(PickedCollector/PickedDevice/PickedTag) + 목록
//         ③ ApplyBindingCommand/ClearBindingCommand — 노드에 바인딩 적용/해제
//         ④ TagValueReceived 이벤트 수신 시 일치하는 노드의 ValueText 갱신
//           (SignalR 콜백은 UI 스레드가 아니므로 Dispatcher.BeginInvoke 필수 —
//            CollectorConnection의 콜백 마샬링 규칙과 동일하게 적용)
//  HM-06: ApplyBinding()/_OnTagValueReceived() 에서 노드의 EngValue(숫자값)도
//         함께 갱신하도록 확장 — Views/DeviceControls 의 각 장비 컨트롤이 이
//         값을 구독해 회전/흐름/수위/개폐 애니메이션을 구동한다(로직 변경은
//         이 두 메서드에 한 줄씩 추가한 것뿐, 나머지는 HM-05와 동일).
//  HM-07: ① Z-레벨 우선순위 — BringToFront/SendToBack/BringForward/SendBackward
//           커맨드로 SelectedNode.ZIndex 를 조정한다(카드 겹침 순서 지정).
//         ② 레이아웃 저장·불러오기 + 다중 화면(페이지) — HmiLayoutLoader 주입(DI).
//           Pages(화면 목록)/ActivePage(현재 편집 중인 화면) 프로퍼티, 화면 전환 시
//           현재 Nodes 를 DTO 로 스냅샷해 메모리 캐시에 저장하고 대상 화면의
//           스냅샷을 복원한다(_pageNodeCache). AddPage/DeletePage/SaveLayout
//           커맨드 + InitializeAsync()(View.Loaded 에서 1회 호출, hmi-layout.json
//           로드 후 마지막 활성 화면 복원).
//           ★ 저장 대상은 배치 좌표·Z순서·Tag 바인딩 식별자뿐이며, 실시간 값
//           (ValueText/EngValue/ValueQuality)은 저장하지 않는다(재실행 시 Collector
//           재연결 후 다시 채워짐).
//  HM-08: 알람 오버레이 — CollectorConnectionManager.AlarmChanged 구독(HM-01에서
//         이미 재발행되고 있었으나 이때까지 구독자가 없었음). _OnAlarmChanged() 가
//         일치하는 노드(BoundCollectorId/PlcId/TagId)의 알람 필드(HasActiveAlarm/
//         AlarmKey/AlarmLevel/AlarmStatusText/AlarmMessage/AlarmTimeText)를 갱신.
//         AcknowledgeAlarmCommand — CollectorConnectionManager.AcknowledgeAlarmAsync()
//         (HM-01에서 이미 구현되어 있었으나 미사용이던 메서드)를 그대로 재사용해
//         ACK 요청을 전송한다("발생 출처로만 전송" 원칙 그대로 적용).
//  HM-09: ForceWriteAsync(node,value,apiKey) 추가 — 아이콘 더블클릭 시 View(코드
//         비하인드)가 ForceWriteDialog 입력을 받아 호출하는 공개 메서드. 커맨드가
//         아니라 일반 메서드인 이유: 다이얼로그 표시/결과 MessageBox 는 순수 View
//         관심사이므로 코드비하인드가 직접 소유하고, ViewModel은 Collector 위임
//         (CollectorConnectionManager.ForceWriteAsync)만 담당한다.
//  HM-10: 화면(페이지) 콤보박스+텍스트박스 UI를 탭 바로 교체(사용자 요청) —
//         LayoutPageViewModel에 IsActive(탭 강조)/IsEditingName(더블클릭 시
//         인라인 이름 편집) 2개 필드 추가. OnActivePageChanged 에서 Pages 전체를
//         순회하며 IsActive 를 갱신하고, SelectPage(page) 공개 메서드(SelectNode
//         와 동일 패턴)를 View 의 탭 클릭 핸들러가 호출해 ActivePage 를 전환한다.
//  HM-12: 보안 3종(사용자 확인 완료) — ① HmiSettingsLoader 주입 추가 +
//         IsForceWriteLocked(기본값=hmi.json ForceWriteSecurity.DefaultLocked,
//         세션 중 토글만 가능·파일 저장 없음) + ToggleForceWriteLockCommand —
//         View 의 더블클릭 핸들러가 잠금 여부를 확인해 ForceWriteDialog 오픈을
//         차단한다. ② _apiKeyCache(Dictionary, 메모리 전용) — ForceWriteAsync
//         성공 시에만 Collector 별 API Key 를 캐싱, GetCachedApiKey() 로 조회해
//         다이얼로그를 열 때 미리 채워 넣는다(디스크 저장 없음). ③ 활성 알람 중
//         강제쓰기 경고는 ForceWriteDialog(View) 쪽에서 처리 — ViewModel 변경 없음.
//  HM-19: InitializeAsync() 에 멱등(idempotent) 가드 추가(Pages.Count>0 이면 즉시
//         반환) — 다중 모니터 지원으로 같은 ViewModel 을 공유하는 두 번째
//         LayoutCanvasView(보조 창)가 생성되면 그 Loaded 핸들러도
//         InitializeAsync() 를 호출하는데, 기존 로직은 매번 Pages.Clear()+
//         파일 재로드를 했었다 — 그대로 두면 보조 창을 열 때마다 아직 저장하지
//         않은 편집 내용(레이아웃 저장 전 상태)이 통째로 사라지는 심각한
//         데이터 손실 버그가 발생했을 것. HmiSettingsLoader 등에 이미 있는
//         "여러 호출부가 각자 방어적으로 재확인" 패턴과 달리, 여기는 재확인이
//         아니라 완전 무시가 맞다(호출부가 이미 채운 상태를 덮어써서는 안 됨).
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.HMI.Core.Config;
using IIoT.HMI.Core.Connection;
using IIoT.HMI.Core.Layout;
using IIoT.HMI.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace IIoT.HMI.ViewModels;

public partial class LayoutCanvasViewModel : ObservableObject
{
    // §0 ─ 의존성 ─────────────────────────────────────────────

    private readonly CollectorConnectionManager _connectionManager;
    private readonly HmiLayoutLoader            _layoutLoader;
    private readonly HmiSettingsLoader          _settingsLoader;

    // ★ HM-05: 노드 선택 시 기존 바인딩을 선택기에 복원하기 위한 1회성 타깃값
    //   (AvailableDevices/AvailableTags 가 비동기로 채워진 뒤 일치 항목을 찾아 적용)
    private string? _restoreTargetPlcId;
    private string? _restoreTargetTagId;

    // ★ HM-07: 화면(페이지)별 노드 스냅샷 캐시(메모리) — Key=페이지 Id.
    //   활성 화면이 바뀔 때마다 이전 화면의 Nodes 를 여기 저장하고, 새 화면의
    //   저장분을 여기서 꺼내 Nodes 에 복원한다. SaveLayoutCommand 실행 시
    //   이 캐시 전체를 hmi-layout.json 으로 직렬화한다.
    private readonly Dictionary<string, List<LayoutNodeDto>> _pageNodeCache = new();

    // ★ HM-12: Collector 별 ForceWrite API Key 세션 캐시(메모리 전용, 디스크
    //   저장 없음) — 강제쓰기 성공 시에만 채워지며, 앱 재시작 시 초기화된다.
    private readonly Dictionary<string, string> _apiKeyCache = new();

    public LayoutCanvasViewModel(
        CollectorConnectionManager connectionManager,
        HmiLayoutLoader layoutLoader,
        HmiSettingsLoader settingsLoader)
    {
        _connectionManager = connectionManager;
        _connectionManager.TagValueReceived += _OnTagValueReceived;
        _connectionManager.AlarmChanged     += _OnAlarmChanged;   // ★ HM-08
        _layoutLoader   = layoutLoader;
        _settingsLoader = settingsLoader;
    }

    /// <summary>View.Loaded 에서 1회 호출 — hmi-layout.json 을 읽어 화면 목록을
    /// 구성하고 마지막 활성 화면을 복원한다. ★ HM-12: hmi.json 의 ForceWrite
    /// 잠금 기본값도 함께 적용한다(다른 View 의 Loaded 순서와 무관하게 안전하도록
    /// 여기서도 자체적으로 LoadAsync 호출 — HmiWebHostService 와 동일 방어 패턴).</summary>
    public async Task InitializeAsync()
    {
        // ★ HM-19: 멱등 가드 — 이미 초기화됨(보조 창 등 두 번째 호출)이면 무시.
        //   Layout.Pages 는 HmiLayoutLoader 가 항상 최소 1개를 보장하므로, 최초
        //   1회 InitializeAsync() 가 끝나면 Pages.Count 는 절대 0이 될 수 없다.
        if (Pages.Count > 0) return;

        await _layoutLoader.LoadAsync();
        await _settingsLoader.LoadAsync();

        IsForceWriteLocked = _settingsLoader.Settings.ForceWriteSecurity.DefaultLocked;

        Pages.Clear();
        _pageNodeCache.Clear();

        foreach (var p in _layoutLoader.Layout.Pages)
        {
            Pages.Add(new LayoutPageViewModel(p.Id, p.Name));
            _pageNodeCache[p.Id] = p.Nodes;
        }

        var target = Pages.FirstOrDefault(p => p.Id == _layoutLoader.Layout.ActivePageId) ?? Pages.FirstOrDefault();
        ActivePage = target; // → OnActivePageChanged 가 해당 화면의 노드를 Nodes 에 복원
    }

    // §1 ─ 컬렉션 ─────────────────────────────────────────────

    public ObservableCollection<AbstractLayoutNode> Nodes { get; } = new();

    /// <summary>아이콘 팔레트 (HM-04: 모터/컨베이어/탱크/밸브 추가 완료)</summary>
    public IReadOnlyList<LayoutPaletteItem> PaletteItems => LayoutNodeFactory.PaletteItems;

    // §2 ─ 선택 노드 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(ApplyBindingCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearBindingCommand))]
    [NotifyCanExecuteChangedFor(nameof(BringToFrontCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendToBackCommand))]
    [NotifyCanExecuteChangedFor(nameof(BringForwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendBackwardCommand))]
    private AbstractLayoutNode? _selectedNode;

    public bool HasSelection => SelectedNode is not null;

    // §3 ─ 줌·패닝 ────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePercent))]
    private double _scale = 1.0;

    [ObservableProperty] private double _offsetX;
    [ObservableProperty] private double _offsetY;

    public string ScalePercent => $"{Scale * 100:F0}%";

    // §4 ─ 노드 추가 ──────────────────────────────────────────

    [RelayCommand]
    private void AddNode(string nodeType)
    {
        var node = LayoutNodeFactory.Create(nodeType);
        if (node is null) return;
        _PlaceNode(node);
    }

    // §5 ─ 선택·삭제 ──────────────────────────────────────────

    public void SelectNode(AbstractLayoutNode? node)
    {
        if (SelectedNode is not null) SelectedNode.IsSelected = false;
        SelectedNode = node;
        if (SelectedNode is not null) SelectedNode.IsSelected = true;

        // ★ HM-05: 선택된 노드가 바뀔 때마다 속성 패널의 Collector/Device/Tag
        //   선택기를 새로 갱신하고, 기존 바인딩이 있으면 복원을 시도한다.
        _RefreshBindingPickers(node);
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedNode is null) return;
        Nodes.Remove(SelectedNode);
        SelectedNode = null;
    }

    // §5-1 ─ HM-07: Z-레벨(겹침 순서) 우선순위 ──────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BringToFront()
    {
        if (SelectedNode is null) return;
        SelectedNode.ZIndex = Nodes.Count == 0 ? 0 : Nodes.Max(n => n.ZIndex) + 1;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SendToBack()
    {
        if (SelectedNode is null) return;
        SelectedNode.ZIndex = Nodes.Count == 0 ? 0 : Nodes.Min(n => n.ZIndex) - 1;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BringForward()
    {
        if (SelectedNode is null) return;
        SelectedNode.ZIndex += 1;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SendBackward()
    {
        if (SelectedNode is null) return;
        SelectedNode.ZIndex -= 1;
    }

    // §6 ─ 줌 커맨드 ──────────────────────────────────────────

    [RelayCommand] private void ZoomIn()  => Scale = Math.Min(3.0, Scale + 0.1);
    [RelayCommand] private void ZoomOut() => Scale = Math.Max(0.3, Scale - 0.1);

    [RelayCommand]
    private void ZoomReset() { Scale = 1.0; OffsetX = 0; OffsetY = 0; }

    public void ApplyWheelZoom(double delta)
    {
        Scale = Math.Clamp(Scale * (delta > 0 ? 1.1 : 0.9), 0.3, 3.0);
        OnPropertyChanged(nameof(ScalePercent));
    }

    // §7 ─ 내부 헬퍼 ──────────────────────────────────────────

    private void _PlaceNode(AbstractLayoutNode node)
    {
        node.X = 200 + (Nodes.Count % 5) * 140;
        node.Y = 120 + (Nodes.Count / 5) * 130;
        node.ZIndex = Nodes.Count == 0 ? 0 : Nodes.Max(n => n.ZIndex) + 1; // ★ HM-07: 새 카드는 항상 맨 위
        Nodes.Add(node);
        SelectNode(node);
    }

    // §8 ─ HM-05: Tag 바인딩 선택기 ───────────────────────────

    /// <summary>[레이아웃 편집] 속성 패널의 Collector 콤보박스 항목.</summary>
    public sealed record CollectorPickItem(string Id, string Name);

    public ObservableCollection<CollectorPickItem>  AvailableCollectors { get; } = new();
    public ObservableCollection<DeviceSnapshotDto>  AvailableDevices    { get; } = new();
    public ObservableCollection<TagSnapshotDto>     AvailableTags       { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyBindingCommand))]
    private CollectorPickItem? _pickedCollector;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyBindingCommand))]
    private DeviceSnapshotDto? _pickedDevice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyBindingCommand))]
    private TagSnapshotDto? _pickedTag;

    /// <summary>선택 노드가 바뀔 때 호출 — 현재 연결된 Collector 목록을 새로고침하고,
    /// 이미 바인딩된 노드라면 Collector→Device→Tag 순으로 복원을 시도한다.</summary>
    private void _RefreshBindingPickers(AbstractLayoutNode? node)
    {
        AvailableCollectors.Clear();
        foreach (var ep in _connectionManager.GetConnectedEndpoints())
            AvailableCollectors.Add(new CollectorPickItem(ep.Id, ep.Name));

        _restoreTargetPlcId = node?.BoundPlcId;
        _restoreTargetTagId = node?.BoundTagId;

        AvailableDevices.Clear();
        AvailableTags.Clear();
        PickedTag = null;
        PickedDevice = null;

        // PickedCollector 설정 → OnPickedCollectorChanged 훅이 Device 목록을 비동기로 로드한다.
        PickedCollector = (node is { IsBound: true })
            ? AvailableCollectors.FirstOrDefault(c => c.Id == node.BoundCollectorId)
            : null;
    }

    partial void OnPickedCollectorChanged(CollectorPickItem? value)
    {
        AvailableDevices.Clear();
        AvailableTags.Clear();
        PickedDevice = null;
        PickedTag = null;

        if (value is not null)
            _ = _LoadDevicesAsync(value.Id);
    }

    private async Task _LoadDevicesAsync(string collectorId)
    {
        var snapshot = await _connectionManager.GetSnapshotAsync(collectorId);

        AvailableDevices.Clear();
        foreach (var d in snapshot)
            AvailableDevices.Add(d);

        // ★ 노드 선택 시점에 저장해 둔 복원 대상 PlcId 가 있으면 자동 선택
        if (_restoreTargetPlcId is not null)
            PickedDevice = AvailableDevices.FirstOrDefault(d => d.PlcId == _restoreTargetPlcId);
    }

    partial void OnPickedDeviceChanged(DeviceSnapshotDto? value)
    {
        AvailableTags.Clear();
        PickedTag = null;

        if (value is null) return;

        foreach (var t in value.Tags)
            AvailableTags.Add(t);

        // ★ 복원 대상 TagId 가 있으면 자동 선택 (1회성 — 사용 후 초기화)
        if (_restoreTargetTagId is not null)
            PickedTag = AvailableTags.FirstOrDefault(t => t.TagId == _restoreTargetTagId);

        _restoreTargetPlcId = null;
    }

    partial void OnPickedTagChanged(TagSnapshotDto? value)
    {
        _restoreTargetTagId = null;
    }

    private bool _CanApplyBinding() =>
        SelectedNode is not null && PickedCollector is not null &&
        PickedDevice is not null && PickedTag is not null;

    [RelayCommand(CanExecute = nameof(_CanApplyBinding))]
    private void ApplyBinding()
    {
        if (SelectedNode is null || PickedCollector is null ||
            PickedDevice is null || PickedTag is null)
            return;

        SelectedNode.BoundCollectorId = PickedCollector.Id;
        SelectedNode.BoundPlcId       = PickedDevice.PlcId;
        SelectedNode.BoundTagId       = PickedTag.TagId;
        SelectedNode.BoundTagName     = PickedTag.Name;

        // ★ 다음 SignalR TagValue Push 수신 전까지 스냅샷의 초기값으로 우선 표시
        var initial = PickedTag.EngValue ?? PickedTag.RawValue;
        SelectedNode.ValueText = initial is null
            ? "값 대기 중..."
            : $"{initial:F1}{(string.IsNullOrEmpty(PickedTag.Unit) ? "" : " " + PickedTag.Unit)}";
        SelectedNode.ValueQuality = string.Empty;
        SelectedNode.EngValue     = initial;   // ★ HM-06: 애니메이션 계산용 숫자값

        ClearBindingCommand.NotifyCanExecuteChanged();
    }

    private bool _CanClearBinding() => SelectedNode is { IsBound: true };

    [RelayCommand(CanExecute = nameof(_CanClearBinding))]
    private void ClearBinding()
    {
        if (SelectedNode is null) return;

        SelectedNode.BoundCollectorId = string.Empty;
        SelectedNode.BoundPlcId       = string.Empty;
        SelectedNode.BoundTagId       = string.Empty;
        SelectedNode.BoundTagName     = string.Empty;
        SelectedNode.ValueText        = "-";
        SelectedNode.ValueQuality     = string.Empty;
        SelectedNode.EngValue         = null;   // ★ HM-06

        PickedCollector = null;
        ApplyBindingCommand.NotifyCanExecuteChanged();
    }

    // §9 ─ HM-05: 실시간 TagValue 반영 ────────────────────────

    /// <summary>
    /// CollectorConnectionManager 로부터 "TagValue" 수신 시 호출됨.
    /// ★ SignalR HubConnection 콜백 스레드(비 UI 스레드)에서 호출되므로,
    /// 노드 프로퍼티(바인딩 대상) 변경은 반드시 Dispatcher.BeginInvoke 로
    /// UI 스레드에 마샬링한다(CollectorConnection 마샬링 규칙과 동일 원칙).
    /// </summary>
    private void _OnTagValueReceived(string collectorId, JsonElement payload)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!payload.TryGetProperty("tagId", out var tagIdProp)) return;
            var tagId = tagIdProp.GetString() ?? string.Empty;
            var plcId = payload.TryGetProperty("plcId", out var plcProp) ? plcProp.GetString() ?? string.Empty : string.Empty;

            var target = Nodes.FirstOrDefault(n =>
                n.IsBound &&
                n.BoundCollectorId == collectorId &&
                n.BoundPlcId       == plcId &&
                n.BoundTagId       == tagId);

            if (target is null) return;

            double? eng = payload.TryGetProperty("engValue", out var e) && e.ValueKind == JsonValueKind.Number
                ? e.GetDouble() : null;
            double? raw = payload.TryGetProperty("rawValue", out var r) && r.ValueKind == JsonValueKind.Number
                ? r.GetDouble() : null;
            var unit    = payload.TryGetProperty("unit", out var u) ? u.GetString() ?? string.Empty : string.Empty;
            var quality = payload.TryGetProperty("quality", out var q) ? q.GetString() ?? string.Empty : string.Empty;

            var value = eng ?? raw;
            target.ValueText    = value is null ? "-" : $"{value:F1}{(string.IsNullOrEmpty(unit) ? "" : " " + unit)}";
            target.ValueQuality = quality;
            target.EngValue     = value;   // ★ HM-06: 애니메이션 계산용 숫자값
        });
    }

    // §9-1 ─ HM-08: 실시간 AlarmChanged 반영 + ACK ────────────

    /// <summary>
    /// CollectorConnectionManager 로부터 "AlarmChanged" 수신 시 호출됨.
    /// ★ TagValueReceived 와 동일하게 SignalR 콜백(비 UI 스레드)이므로
    /// Dispatcher.BeginInvoke 로 마샬링한다.
    /// </summary>
    private void _OnAlarmChanged(string collectorId, JsonElement payload)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!payload.TryGetProperty("tagId", out var tagIdProp)) return;
            var tagId = tagIdProp.GetString() ?? string.Empty;
            var plcId = payload.TryGetProperty("plcId", out var plcProp) ? plcProp.GetString() ?? string.Empty : string.Empty;

            var target = Nodes.FirstOrDefault(n =>
                n.IsBound &&
                n.BoundCollectorId == collectorId &&
                n.BoundPlcId       == plcId &&
                n.BoundTagId       == tagId);

            if (target is null) return;

            var status = payload.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;

            // "Recovered" — 알람 해제, 배지/팝업 숨김
            if (status == "Recovered")
            {
                target.HasActiveAlarm  = false;
                target.AlarmKey        = string.Empty;
                target.AlarmLevel      = string.Empty;
                target.AlarmStatusText = string.Empty;
                target.AlarmMessage    = string.Empty;
                target.AlarmTimeText   = string.Empty;
                AcknowledgeAlarmCommand.NotifyCanExecuteChanged();
                return;
            }

            var alarmKey = payload.TryGetProperty("alarmKey", out var k) ? k.GetString() ?? string.Empty : string.Empty;
            var level    = payload.TryGetProperty("level",    out var l) ? l.GetString() ?? string.Empty : string.Empty;
            var message  = payload.TryGetProperty("message",  out var m) ? m.GetString() ?? string.Empty : string.Empty;
            var timeText = payload.TryGetProperty("ts", out var t)
                && DateTimeOffset.TryParse(t.GetString(), out var dto)
                    ? dto.LocalDateTime.ToString("HH:mm:ss")
                    : string.Empty;

            // "Active"(미확인) 또는 "Acked"(확인됨) — 배지 계속 표시(ACK 여부만 다름)
            target.HasActiveAlarm  = true;
            target.AlarmKey        = alarmKey;
            target.AlarmLevel      = level;
            target.AlarmStatusText = status;
            target.AlarmMessage    = message;
            target.AlarmTimeText   = timeText;

            AcknowledgeAlarmCommand.NotifyCanExecuteChanged();
        });
    }

    private bool _CanAcknowledgeAlarm(AbstractLayoutNode? node) =>
        node is { HasActiveAlarm: true, AlarmStatusText: "Active" } && !string.IsNullOrEmpty(node.AlarmKey);

    /// <summary>
    /// 알람 팝업의 [✓ 확인(ACK)] 버튼 커맨드. 발생 출처(BoundCollectorId)로만
    /// ACK 를 전송한다("발생 출처로만 전송" 원칙 — Monitor MN-03/Collector C-EX-12 준용).
    /// CollectorConnectionManager.AcknowledgeAlarmAsync() 는 HM-01에서 이미 구현되어
    /// 있었으나 이 Step 전까지 호출부가 없었다(HM-08에서 처음 사용).
    /// </summary>
    [RelayCommand(CanExecute = nameof(_CanAcknowledgeAlarm))]
    private async Task AcknowledgeAlarmAsync(AbstractLayoutNode? node)
    {
        if (node is null || string.IsNullOrEmpty(node.AlarmKey)) return;
        await _connectionManager.AcknowledgeAlarmAsync(node.BoundCollectorId, node.AlarmKey);
    }

    // §9-2 ─ HM-09: ForceWrite 원격 제어 ──────────────────────

    /// <summary>
    /// 아이콘 더블클릭 → ForceWriteDialog 입력 확인 후 View(코드비하인드)가 호출한다.
    /// CollectorConnectionManager.ForceWriteAsync() 로 위임하며, 검증(기능 활성화·
    /// API Key·Tag 존재/활성·값 형식)은 전부 Collector 측(ForceWriteService)이 수행한다.
    /// "발생 출처로만 전송" 원칙 그대로 — node.BoundCollectorId 로만 전송된다.
    /// </summary>
    public async Task<ForceWriteResult> ForceWriteAsync(AbstractLayoutNode node, string value, string apiKey)
    {
        var result = await _connectionManager.ForceWriteAsync(node.BoundCollectorId, node.BoundPlcId, node.BoundTagId, value, apiKey);

        // ★ HM-12: 성공한 API Key만 세션 캐시에 저장(실패한 값을 캐싱해 다음 시도까지
        //   방해하지 않도록). 빈 값은 캐싱하지 않는다(API Key 미사용 Collector 구분 불필요).
        if (result.IsSuccess && !string.IsNullOrEmpty(apiKey))
            _apiKeyCache[node.BoundCollectorId] = apiKey;

        return result;
    }

    /// <summary>★ HM-12: 지정된 Collector 에 대해 이번 세션 중 성공했던 API Key
    /// 를 반환한다(없으면 빈 문자열). ForceWriteDialog 를 열 때 미리 채워 넣어
    /// 매번 재입력하지 않아도 되게 한다 — 디스크에는 저장하지 않는다.</summary>
    public string GetCachedApiKey(string collectorId) =>
        _apiKeyCache.TryGetValue(collectorId, out var key) ? key : string.Empty;

    // §9-3 ─ HM-12: 화면 잠금 모드 ─────────────────────────────

    /// <summary>true(기본값=hmi.json ForceWriteSecurity.DefaultLocked) 인 동안은
    /// 아이콘 더블클릭 시 ForceWriteDialog 가 열리지 않는다(운영자 실수 방지).
    /// 세션 중 툴바 버튼으로만 토글되며, 토글 자체는 파일에 저장되지 않는다.</summary>
    [NotifyPropertyChangedFor(nameof(LockButtonLabel))]
    [ObservableProperty] private bool _isForceWriteLocked = true;

    public string LockButtonLabel => IsForceWriteLocked ? "🔒 잠금(더블클릭 차단)" : "🔓 해제(강제쓰기 가능)";

    [RelayCommand]
    private void ToggleForceWriteLock() => IsForceWriteLocked = !IsForceWriteLocked;

    // §10 ─ HM-07: 화면(페이지) 관리 + 저장/불러오기 ──────────

    public ObservableCollection<LayoutPageViewModel> Pages { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeletePageCommand))]
    private LayoutPageViewModel? _activePage;

    /// <summary>화면 전환 직전 — 이전 화면의 현재 Nodes 를 DTO 스냅샷으로 캐시에 저장.</summary>
    partial void OnActivePageChanging(LayoutPageViewModel? oldValue, LayoutPageViewModel? newValue)
    {
        if (oldValue is not null)
            _pageNodeCache[oldValue.Id] = Nodes.Select(_ToDto).ToList();
    }

    /// <summary>화면 전환 직후 — 새 화면의 캐시된 스냅샷을 Nodes 에 복원.</summary>
    partial void OnActivePageChanged(LayoutPageViewModel? value)
    {
        // ★ HM-10: 탭 바의 활성 강조 표시(IsActive)를 여기서 함께 갱신 — Pages 목록의
        //   다른 항목들과 비교해 정확히 1개만 true 가 되도록 매번 전체 재설정한다.
        foreach (var p in Pages) p.IsActive = ReferenceEquals(p, value);

        SelectNode(null);
        Nodes.Clear();

        if (value is null) return;

        if (!_pageNodeCache.TryGetValue(value.Id, out var dtos))
            return;

        foreach (var dto in dtos)
        {
            var node = _FromDto(dto);
            if (node is not null) Nodes.Add(node);
        }
    }

    /// <summary>
    /// ★ HM-10: 화면 탭 클릭 시 View(코드비하인드)가 호출 — SelectNode(node) 와
    /// 동일한 패턴(커맨드가 아닌 공개 메서드)으로 ActivePage 를 전환한다.
    /// </summary>
    public void SelectPage(LayoutPageViewModel? page)
    {
        if (page is not null) ActivePage = page;
    }

    private bool _CanDeletePage() => Pages.Count > 1 && ActivePage is not null;

    [RelayCommand]
    private void AddPage()
    {
        var page = new LayoutPageViewModel(Guid.NewGuid().ToString(), $"화면 {Pages.Count + 1}");
        Pages.Add(page);
        _pageNodeCache[page.Id] = new List<LayoutNodeDto>();
        ActivePage = page;
        DeletePageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(_CanDeletePage))]
    private void DeletePage()
    {
        if (ActivePage is null || Pages.Count <= 1) return;

        var removed = ActivePage;
        var next = Pages.FirstOrDefault(p => p.Id != removed.Id);

        // ★ 순서 중요: ActivePage 전환(OnActivePageChanging 이 removed 의 현재 Nodes 를
        //   캐시에 다시 써 넣으므로) → 전환이 끝난 뒤에 캐시/목록에서 제거해야 한다.
        ActivePage = next;
        _pageNodeCache.Remove(removed.Id);
        Pages.Remove(removed);

        DeletePageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveLayoutAsync()
    {
        // ★ 현재 활성 화면의 최신 상태를 먼저 캐시에 반영
        if (ActivePage is not null)
            _pageNodeCache[ActivePage.Id] = Nodes.Select(_ToDto).ToList();

        _layoutLoader.Layout.Pages = Pages.Select(p => new LayoutPageDto
        {
            Id    = p.Id,
            Name  = p.Name,
            Nodes = _pageNodeCache.TryGetValue(p.Id, out var nodes) ? nodes : new List<LayoutNodeDto>()
        }).ToList();
        _layoutLoader.Layout.ActivePageId = ActivePage?.Id ?? string.Empty;

        await _layoutLoader.SaveAsync();
    }

    private static LayoutNodeDto _ToDto(AbstractLayoutNode n) => new()
    {
        NodeType         = n.NodeType,
        Label             = n.Label,
        X                 = n.X,
        Y                 = n.Y,
        ZIndex            = n.ZIndex,
        BoundCollectorId  = n.BoundCollectorId,
        BoundPlcId        = n.BoundPlcId,
        BoundTagId        = n.BoundTagId,
        BoundTagName      = n.BoundTagName
    };

    private static AbstractLayoutNode? _FromDto(LayoutNodeDto dto)
    {
        var node = LayoutNodeFactory.Create(dto.NodeType);
        if (node is null) return null;

        node.Label            = dto.Label;
        node.X                = dto.X;
        node.Y                = dto.Y;
        node.ZIndex           = dto.ZIndex;
        node.BoundCollectorId = dto.BoundCollectorId;
        node.BoundPlcId       = dto.BoundPlcId;
        node.BoundTagId       = dto.BoundTagId;
        node.BoundTagName     = dto.BoundTagName;

        // ★ 실시간 값은 저장 대상이 아니므로 복원 직후에는 "대기" 상태로 표시.
        //   Collector 재연결 후 다음 SignalR TagValue Push 수신 시 자동 갱신된다.
        node.ValueText = node.IsBound ? "값 대기 중..." : "-";

        return node;
    }
}

/// <summary>
/// ★ HM-07: [레이아웃 편집] 화면(페이지) 선택기 항목 — Pages 콤보박스/이름 편집에 바인딩.
/// LayoutPageDto(직렬화 전용, 비-Observable)와 별도로 두어 이름 편집 시 UI가 즉시 갱신되게 한다.
/// ★ HM-10: 콤보박스+텍스트박스를 탭 바로 교체하며 IsActive(탭 강조 표시)/
/// IsEditingName(더블클릭 시 인라인 이름 편집 전환) 2개 필드 추가.
/// </summary>
public sealed partial class LayoutPageViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty] private string _name;

    /// <summary>이 화면이 현재 활성 화면인지 — 탭 바 강조 표시용(LayoutCanvasViewModel
    /// .OnActivePageChanged 가 ActivePage 전환 시마다 전체 Pages 를 순회하며 갱신).</summary>
    [ObservableProperty] private bool _isActive;

    /// <summary>탭을 더블클릭해 이름 편집 모드로 전환했는지 — true 인 동안 탭에
    /// TextBlock 대신 인라인 TextBox 가 표시된다(LayoutCanvasView.xaml PageTabTemplate).</summary>
    [ObservableProperty] private bool _isEditingName;

    public LayoutPageViewModel(string id, string name)
    {
        Id    = id;
        _name = name;
    }
}
