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
//  설정화면 통일(2026-07-20): Collector 환경설정 탭과 완전히 동일한 좌측
//        섹션 네비게이션 골격(ActiveSectionIndex/IsXSection/SwitchSectionCommand)
//        + 전체 오류 목록 표시(_ValidateAll) 패턴으로 통일(기존에는 개별
//        early-return 검증이라 첫 오류만 표시되었음).
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.HMI.Core.Config;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IIoT.HMI.ViewModels;

/// <summary>환경설정 화면 ViewModel (DI 싱글턴).</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly HmiSettingsLoader _loader;

    // §1 ─ 좌측 섹션 네비게이션 (★ 설정화면 통일 — Collector 와 동일 패턴) ──

    /// <summary>현재 선택된 섹션 인덱스. 0=웹Hub 1=화면잠금 2=로그</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebSection))]
    [NotifyPropertyChangedFor(nameof(IsSecuritySection))]
    [NotifyPropertyChangedFor(nameof(IsLogSection))]
    private int _activeSectionIndex;

    public bool IsWebSection      => ActiveSectionIndex == 0;
    public bool IsSecuritySection => ActiveSectionIndex == 1;
    public bool IsLogSection      => ActiveSectionIndex == 2;

    [RelayCommand]
    private void SwitchSection(string idx)
    {
        if (int.TryParse(idx, out var i)) ActiveSectionIndex = i;
    }

    // §2 ─ 설정 모델 ──────────────────────────────────────────

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

    // §3 ─ 커맨드 ─────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        var errors = _ValidateAll();
        if (errors.Count > 0)
        {
            HasError = true;
            StatusMessage = "저장 실패 — " + string.Join(" / ", errors);
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

    // §4 ─ 유효성 검사 (★ 설정화면 통일 — Collector 와 동일하게 전체 오류를
    //      한 번에 모아 표시하는 _ValidateAll() 패턴) ──────────────────

    private List<string> _ValidateAll()
    {
        var errors = new List<string>();

        if (Settings.Web.Port is < 1 or > 65535)
            errors.Add("웹 Hub 포트는 1~65535 범위여야 합니다");
        if (Settings.Log.ValidDays < 1)
            errors.Add("로그 보존 일수는 1일 이상이어야 합니다");
        if (Settings.Log.MaxDisplayCount < 100)
            errors.Add("로그 패널 최대 표시 건수는 100 이상이어야 합니다");

        return errors;
    }
}
