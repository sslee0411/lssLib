// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · ViewModels/SettingsViewModel.cs
//  역할: 환경설정 탭 ViewModel — monitor.json Web 섹션(자체 SignalR Hub
//        Enabled/Port) 편집. Collectors[]/FavoriteTagKeys 는 각자
//        [Collector 관리]/태그 즐겨찾기 UI 에서 이미 편집 가능하므로 대상 아님.
//  C-SET-01 후속 (Monitor): Collector/Manager/Studio 환경설정 탭과 동일 트랙.
//        ★ InitializeAsync() 에서 자체적으로 MonitorSettingsLoader.LoadAsync()
//          를 호출한다 — CollectorManageView.Loaded 에서도 같은 로더로 로드를
//          수행하지만(monitor.json 공유), 두 Loaded 핸들러의 실행 순서를
//          가정하지 않기 위해 이 화면 스스로 다시 로드해 값을 확정한다
//          (동일 파일 재읽기라 안전 — 파일 I/O 외 부작용 없음).
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Monitor.Core.Config;
using System;
using System.Threading.Tasks;

namespace IIoT.Monitor.ViewModels;

/// <summary>환경설정 화면 ViewModel (DI 싱글턴).</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly MonitorSettingsLoader _loader;

    /// <summary>편집 대상 설정 객체 (MonitorSettingsLoader.Settings 와 동일 참조)</summary>
    [ObservableProperty]
    private MonitorSettings _settings = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public SettingsViewModel(MonitorSettingsLoader loader)
    {
        _loader = loader;
    }

    /// <summary>SettingsView.Loaded 에서 호출 — monitor.json 로드 후 화면에 반영.</summary>
    public async Task InitializeAsync()
    {
        await _loader.LoadAsync();
        Settings = _loader.Settings;
        StatusMessage = string.Empty;
        HasError = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Settings.Web.Port is < 1 or > 65535)
        {
            HasError = true;
            StatusMessage = "저장 실패 — 포트는 1~65535 범위여야 합니다";
            return;
        }

        await _loader.SaveAsync();
        HasError = false;
        StatusMessage = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — Monitor 를 재시작해야 적용됩니다.";
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await _loader.LoadAsync();
        Settings = _loader.Settings;
        StatusMessage = "monitor.json 을 다시 불러왔습니다 (편집 중이던 내용은 취소됨).";
        HasError = false;
    }
}
