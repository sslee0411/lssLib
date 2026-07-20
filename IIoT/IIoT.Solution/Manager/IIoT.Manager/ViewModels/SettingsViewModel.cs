// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/SettingsViewModel.cs
//  역할: 환경설정 탭 ViewModel — manager.json Resource 섹션 편집
//        (CpuWarnPercent / MemoryWarnMb — 유일하게 UI 가 없던 섹션.
//         Processes/Deploy/Schedules 는 이미 각자 탭에서 편집 가능하므로 대상 아님)
//  C-SET-01 후속 (Manager): Collector 환경설정 탭과 동일 패턴 이식
//        ★ Settings 는 ManagerSettingsLoader.Settings 와 동일 참조이므로,
//          ResourceSettings 는 이미 실행 중인 ProcessCardViewModel 들이
//          생성자에서 참조로 들고 있는 바로 그 객체다 — 즉 저장(파일 기록) 전에도
//          화면에서 값을 바꾸는 즉시 실행 중인 경고 임계 감시에 반영된다
//          (Collector 와 달리 재시작 불필요 — 사용 설명에 명시).
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Manager.Core.Config;
using System;
using System.Threading.Tasks;

namespace IIoT.Manager.ViewModels;

/// <summary>환경설정 화면 ViewModel (DI 싱글턴).</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ManagerSettingsLoader _loader;

    /// <summary>편집 대상 설정 객체 (ManagerSettingsLoader.Settings 와 동일 참조)</summary>
    [ObservableProperty]
    private ManagerSettings _settings = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public SettingsViewModel(ManagerSettingsLoader loader)
    {
        _loader = loader;
    }

    /// <summary>ManagerMainViewModel.InitializeAsync() 에서 manager.json 로드 완료 후 호출.</summary>
    public void Initialize()
    {
        Settings = _loader.Settings;
        StatusMessage = string.Empty;
        HasError = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Settings.Resource.CpuWarnPercent < 0 || Settings.Resource.MemoryWarnMb < 0)
        {
            HasError = true;
            StatusMessage = "저장 실패 — 임계값은 0 이상이어야 합니다 (0 이하 = 검사 안 함)";
            return;
        }

        await _loader.SaveAsync();
        HasError = false;
        StatusMessage = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — 이미 실행 중인 감시에도 즉시 반영됩니다.";
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await _loader.LoadAsync();
        Initialize();
        StatusMessage = "manager.json 을 다시 불러왔습니다 (편집 중이던 내용은 취소됨).";
    }
}
