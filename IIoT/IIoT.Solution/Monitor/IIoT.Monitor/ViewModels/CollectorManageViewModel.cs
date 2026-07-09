// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/CollectorManageViewModel.cs
//  역할: [Collector 관리] 탭 ViewModel
//        monitor.json 로드 → Collectors 목록 표시 → 추가/삭제/저장
//  MN-01: 신규
//  MN-01B: CollectorConnectionManager 연동 추가
//          목록 로드/추가/삭제/저장 시마다 연결 상태를 동기화한다.
//  FIX(2026-07-07): SyncFromEndpointsAsync() 가 List<string> 오류 목록을
//                   반환하도록 시그니처 변경됨(중복 CollectorId 등으로 동기화가
//                   조용히 실패하던 문제 대응) — 반환된 오류를 StatusText 에
//                   그대로 노출하여 "저장해도 아무 반응 없음" 상황을 없앤다.
//  생성: 2026-07-07 / 수정: 2026-07-07 (동기화 오류 노출)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Core.Connection;
using IIoT.Monitor.Models;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.Monitor.ViewModels;

/// <summary>
/// [Collector 관리] 탭의 ViewModel.
/// <para>
/// monitor.json 에 등록된 Collector 목록을 표시하고, 추가/삭제/저장을 담당한다.
/// 목록이 바뀔 때마다 <see cref="CollectorConnectionManager"/> 에 동기화를 요청하여
/// 실제 HubConnection 생성/해제가 이어지도록 한다.
/// </para>
/// </summary>
public partial class CollectorManageViewModel : ObservableObject
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly MonitorSettingsLoader       _settingsLoader;
    private readonly CollectorConnectionManager  _connectionManager;

    // §2 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>등록된 Collector 목록 (DataGrid ItemsSource 바인딩)</summary>
    public ObservableCollection<CollectorEndpoint> Collectors { get; } = new();

    /// <summary>DataGrid 에서 선택된 항목 (삭제 대상 판단용)</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCollectorCommand))]
    private CollectorEndpoint? _selected;

    /// <summary>마지막 저장 상태 표시 텍스트 (하단 상태바용). 동기화 오류도 여기에 표시된다.</summary>
    [ObservableProperty]
    private string _statusText = "monitor.json 로드 대기 중";

    // §3 ─ 생성자 ──────────────────────────────────────────

    public CollectorManageViewModel(
        MonitorSettingsLoader      settingsLoader,
        CollectorConnectionManager connectionManager)
    {
        _settingsLoader    = settingsLoader;
        _connectionManager = connectionManager;
    }

    // §4 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// monitor.json 을 로드하여 목록을 채우고, 등록된 Collector 전체에 대해
    /// 연결을 시작합니다. View 의 Loaded 이벤트에서 1회 호출됩니다.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _settingsLoader.LoadAsync();

        Collectors.Clear();
        foreach (var c in _settingsLoader.Settings.Collectors)
            Collectors.Add(c);

        StatusText = Collectors.Count == 0
            ? "등록된 Collector가 없습니다 — [+ 추가] 로 등록하세요"
            : $"Collector {Collectors.Count}개 등록됨 — 연결 시도 중...";

        // ★ MN-01B: 로드 직후 전체 Collector 연결 시작
        var errors = await _connectionManager.SyncFromEndpointsAsync(Collectors);
        _ApplySyncErrors(errors);
    }

    // §5 ─ 명령 ────────────────────────────────────────────

    /// <summary>새 Collector 항목을 추가합니다 (기본값으로 생성 후 즉시 저장 + 연결 시작).</summary>
    [RelayCommand]
    private async Task AddCollectorAsync()
    {
        var c = new CollectorEndpoint
        {
            Name = $"Collector-{Collectors.Count + 1}"
        };
        Collectors.Add(c);
        Selected = c;

        await _SaveAndSyncAsync();
    }

    /// <summary>선택된 Collector 항목을 삭제합니다 (연결도 함께 종료).</summary>
    [RelayCommand(CanExecute = nameof(_CanRemove))]
    private async Task RemoveCollectorAsync()
    {
        if (Selected is null) return;

        Collectors.Remove(Selected);
        Selected = Collectors.Count > 0 ? Collectors[0] : null;

        await _SaveAndSyncAsync();
    }

    private bool _CanRemove() => Selected is not null;

    /// <summary>
    /// 현재 목록(Name/Host/Port/Enabled 편집값 포함)을 monitor.json 에 저장하고
    /// 연결 상태를 동기화합니다. (예: Enabled 체크 해제 → 저장 시 해당 연결 종료)
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync() => await _SaveAndSyncAsync();

    // §6 ─ 내부 저장+동기화 헬퍼 ───────────────────────────

    private async Task _SaveAndSyncAsync()
    {
        _settingsLoader.Settings.Collectors = Collectors.ToList();
        await _settingsLoader.SaveAsync();

        var savedAt = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — Collector {Collectors.Count}개";
        LogManager.Instance.Info("CollectorManage", savedAt);

        // ★ MN-01B: 저장 직후 연결 상태 재동기화
        //   (신규 추가 → 연결 시작 / 삭제·비활성화 → 연결 종료)
        var errors = await _connectionManager.SyncFromEndpointsAsync(Collectors);

        // ★ FIX: 동기화 중 오류(예: 중복 CollectorId)가 있으면 저장 완료 문구 대신
        //   오류 내용을 StatusText 에 표시 — 이전에는 예외가 조용히 삼켜져
        //   "저장을 눌러도 미연결에서 전혀 안 바뀌는" 원인 파악이 불가능했다.
        StatusText = errors.Count == 0 ? savedAt : $"⚠ {string.Join(" / ", errors)}";
    }

    /// <summary>InitializeAsync() 경로의 동기화 오류를 StatusText 에 반영한다.</summary>
    private void _ApplySyncErrors(List<string> errors)
    {
        if (errors.Count > 0)
            StatusText = $"⚠ {string.Join(" / ", errors)}";
    }
}
