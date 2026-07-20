// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/SettingsViewModel.cs
//  역할: 환경설정 탭 ViewModel — studio-settings.json 편집
//        (로그 / 편집기 히스토리 — device.json/collect.json 은 대상 아님,
//         이미 [장비 관리]/[수집 흐름] 등 자기 탭에서 편집됨)
//  C-SET-01 후속 (Studio)
//        ★ StudioSettingsLoader.LoadSync() 가 OnStartup 맨 앞에서 이미 동기
//          완료되므로(다른 프로그램과 달리 별도 Initialize() 불필요), 생성자에서
//          바로 Settings = loader.Settings 로 초기화한다.
//        ★ 로그 섹션·Undo 히스토리 단계 수는 프로그램 시작 시 1회만 적용되므로
//          저장 후 재시작 안내 배너를 표시한다.
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Config;
using System;
using System.Threading.Tasks;

namespace IIoT.Studio.ViewModels;

/// <summary>환경설정 화면 ViewModel (DI 싱글턴, MainViewModel.Settings 로 노출).</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly StudioSettingsLoader _loader;

    /// <summary>편집 대상 설정 객체 (StudioSettingsLoader.Settings 와 동일 참조)</summary>
    [ObservableProperty]
    private StudioSettings _settings;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public SettingsViewModel(StudioSettingsLoader loader)
    {
        _loader  = loader;
        _settings = loader.Settings;   // ★ LoadSync() 가 이미 완료된 상태
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
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
        if (Settings.Editor.UndoHistoryMaxSize < 1)
        {
            HasError = true;
            StatusMessage = "저장 실패 — 실행취소 최대 단계 수는 1 이상이어야 합니다";
            return;
        }
        if (Settings.Editor.SaveHistoryMaxCount < 1)
        {
            HasError = true;
            StatusMessage = "저장 실패 — 저장 이력 최대 개수는 1 이상이어야 합니다";
            return;
        }

        await _loader.SaveAsync();
        HasError = false;
        StatusMessage = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — Studio 를 재시작해야 적용됩니다.";
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await _loader.LoadAsync();
        Settings = _loader.Settings;
        StatusMessage = "studio-settings.json 을 다시 불러왔습니다 (편집 중이던 내용은 취소됨).";
        HasError = false;
    }
}
