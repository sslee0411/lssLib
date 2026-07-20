// ══════════════════════════════════════════════════════════
//  IIoT.HMI · ViewModels/SettingsViewModel.cs
//  역할: 환경설정 탭 ViewModel — hmi.json 의 Web/ForceWriteSecurity/Log
//        3개 섹션 편집. Collectors[] 는 [Collector 관리] 탭에서 이미
//        편집 가능하므로 대상 아님.
//  C-SET-01 후속 (HMI): Collector/Manager/Studio/Monitor 환경설정 탭과
//        동일 트랙(범위는 사용자 확인, 2026-07-20 — Web+ForceWriteSecurity+Log).
//        ★ InitializeAsync() 에서 자체적으로 HmiSettingsLoader.LoadAsync() 를
//          호출한다 — CollectorManageView.Loaded 에서도 같은 로더로 로드를
//          수행하지만(hmi.json 공유), 두 Loaded 핸들러의 실행 순서를 가정하지
//          않기 위해 이 화면 스스로 다시 로드해 값을 확정한다(Monitor
//          SettingsViewModel.cs 와 동일 방어 패턴).
//        ★ Log 섹션은 App.xaml.cs OnStartup 맨 앞에서 LoadSync() 로 이미 1회
//          적용된 뒤이므로, 여기서 값을 바꿔 저장해도 재시작해야 반영된다.
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.HMI.Core.Config;
using System;
using System.Threading.Tasks;

namespace IIoT.HMI.ViewModels;

/// <summary>환경설정 화면 ViewModel (DI 싱글턴).</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly HmiSettingsLoader _loader;

    /// <summary>편집 대상 설정 객체 (HmiSettingsLoader.Settings 와 동일 참조)</summary>
    [ObservableProperty]
    private HmiSettings _settings = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public SettingsViewModel(HmiSettingsLoader loader)
    {
        _loader = loader;
    }

    /// <summary>SettingsView.Loaded 에서 호출 — hmi.json 로드 후 화면에 반영.</summary>
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
            StatusMessage = "저장 실패 — 웹 Hub 포트는 1~65535 범위여야 합니다";
            return;
        }
        if (Settings.Log.ValidDays < 1)
        {
            HasError = true;
            StatusMessage = "저장 실패 — 로그 보존 일수는 1일 이상이어야 합니다";
            return;
        }
        if (Settings.Log.MaxDisplayCount < 100)
        {
            HasError = true;
            StatusMessage = "저장 실패 — 로그 패널 최대 표시 건수는 100 이상이어야 합니다";
            return;
        }

        await _loader.SaveAsync();
        HasError = false;
        StatusMessage = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — Web/Log 설정은 HMI 를 재시작해야 적용됩니다.";
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await _loader.LoadAsync();
        Settings = _loader.Settings;
        StatusMessage = "hmi.json 을 다시 불러왔습니다 (편집 중이던 내용은 취소됨).";
        HasError = false;
    }
}
