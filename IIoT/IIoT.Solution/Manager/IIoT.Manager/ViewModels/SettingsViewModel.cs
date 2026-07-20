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
//  설정화면 통일(2026-07-20): Collector 환경설정 탭과 완전히 동일한 좌측
//        섹션 네비게이션 골격(ActiveSectionIndex/IsXSection/SwitchSectionCommand)
//        + 전체 오류 목록 표시(_ValidateAll) 패턴으로 통일. 섹션이 1개뿐이라
//        네비게이션에는 항목이 하나만 표시되지만, 5개 프로그램의 환경설정
//        화면 골격(배너 위치·하단 버튼 순서·좌측 네비 폭)을 동일하게 맞추기
//        위해 동일 인프라를 그대로 적용한다.
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Manager.Core.Config;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IIoT.Manager.ViewModels;

/// <summary>환경설정 화면 ViewModel (DI 싱글턴).</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ManagerSettingsLoader _loader;

    // §1 ─ 좌측 섹션 네비게이션 (★ 설정화면 통일 — Collector 와 동일 패턴) ──

    /// <summary>현재 선택된 섹션 인덱스. 0=리소스 임계값(유일한 섹션)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResourceSection))]
    private int _activeSectionIndex;

    public bool IsResourceSection => ActiveSectionIndex == 0;

    [RelayCommand]
    private void SwitchSection(string idx)
    {
        if (int.TryParse(idx, out var i)) ActiveSectionIndex = i;
    }

    // §2 ─ 설정 모델 ──────────────────────────────────────────

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
        StatusMessage = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — 이미 실행 중인 감시에도 즉시 반영됩니다.";
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await _loader.LoadAsync();
        Initialize();
        StatusMessage = "manager.json 을 다시 불러왔습니다 (편집 중이던 내용은 취소됨).";
    }

    // §4 ─ 유효성 검사 (★ 설정화면 통일 — Collector 와 동일하게 전체 오류를
    //      한 번에 모아 표시하는 _ValidateAll() 패턴) ──────────────────

    private List<string> _ValidateAll()
    {
        var errors = new List<string>();
        var s = Settings;

        if (s.Resource.CpuWarnPercent < 0)
            errors.Add("CPU 사용률 경고 임계값은 0 이상이어야 합니다 (0 이하 = 검사 안 함)");
        if (s.Resource.MemoryWarnMb < 0)
            errors.Add("메모리 사용량 경고 임계값은 0 이상이어야 합니다 (0 이하 = 검사 안 함)");

        return errors;
    }
}
