// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/CollectorManageViewModel.cs
//  역할: [Collector 관리] 탭 ViewModel
//        monitor.json 로드 → Collectors 목록 표시 → 추가/삭제/저장
//  MN-01: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Models;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.Monitor.ViewModels;

/// <summary>
/// [Collector 관리] 탭의 ViewModel.
/// <para>
/// monitor.json 에 등록된 Collector 목록을 표시하고, 추가/삭제/저장을 담당한다.
/// MN-01B(다중 HubConnection 연결 관리자)가 이 목록(Collectors)을 구독하여
/// Collector 별 연결을 생성/해제한다.
/// </para>
/// </summary>
public partial class CollectorManageViewModel : ObservableObject
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly MonitorSettingsLoader _settingsLoader;

    // §2 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>등록된 Collector 목록 (DataGrid ItemsSource 바인딩)</summary>
    public ObservableCollection<CollectorEndpoint> Collectors { get; } = new();

    /// <summary>DataGrid 에서 선택된 항목 (삭제 대상 판단용)</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCollectorCommand))]
    private CollectorEndpoint? _selected;

    /// <summary>마지막 저장 상태 표시 텍스트 (하단 상태바용)</summary>
    [ObservableProperty]
    private string _statusText = "monitor.json 로드 대기 중";

    // §3 ─ 생성자 ──────────────────────────────────────────

    public CollectorManageViewModel(MonitorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    // §4 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// monitor.json 을 로드하여 목록을 채웁니다.
    /// View 의 Loaded 이벤트에서 1회 호출됩니다.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _settingsLoader.LoadAsync();

        Collectors.Clear();
        foreach (var c in _settingsLoader.Settings.Collectors)
            Collectors.Add(c);

        StatusText = Collectors.Count == 0
            ? "등록된 Collector가 없습니다 — [+ 추가] 로 등록하세요"
            : $"Collector {Collectors.Count}개 등록됨";
    }

    // §5 ─ 명령 ────────────────────────────────────────────

    /// <summary>새 Collector 항목을 추가합니다 (기본값으로 생성 후 즉시 저장).</summary>
    [RelayCommand]
    private async Task AddCollectorAsync()
    {
        var c = new CollectorEndpoint
        {
            Name = $"Collector-{Collectors.Count + 1}"
        };
        Collectors.Add(c);
        Selected = c;

        await _SaveAsync();
    }

    /// <summary>선택된 Collector 항목을 삭제합니다.</summary>
    [RelayCommand(CanExecute = nameof(_CanRemove))]
    private async Task RemoveCollectorAsync()
    {
        if (Selected is null) return;

        Collectors.Remove(Selected);
        Selected = Collectors.Count > 0 ? Collectors[0] : null;

        await _SaveAsync();
    }

    private bool _CanRemove() => Selected is not null;

    /// <summary>
    /// 현재 목록(Name/Host/Port/Enabled 편집값 포함)을 monitor.json 에 저장합니다.
    /// DataGrid 인라인 편집은 즉시 모델에 반영되므로, 이 명령은 파일 저장만 수행합니다.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync() => await _SaveAsync();

    // §6 ─ 내부 저장 헬퍼 ──────────────────────────────────

    private async Task _SaveAsync()
    {
        _settingsLoader.Settings.Collectors = Collectors.ToList();
        await _settingsLoader.SaveAsync();

        StatusText = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — Collector {Collectors.Count}개";
        LogManager.Instance.Info("CollectorManage", StatusText);
    }
}
