// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainViewModel.cs
//  역할: Studio 메인 ViewModel
//  Base-2: 탭 전환 기능 추가
//          SwitchTabCommand + ActiveTabIndex + IsXxxTab 5개
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IIoT.Studio;

public partial class MainViewModel : ObservableObject
{
    // §1 ─ 저장 상태 ──────────────────────────────────────────

    /// <summary>헤더 저장 상태 표시 텍스트</summary>
    [ObservableProperty]
    private string _saveStatus = "준비됨";

    // §2 ─ 탭 전환 ────────────────────────────────────────────

    /// <summary>
    /// 활성 탭 인덱스 (0=장비관리 / 1=수집흐름 / 2=스케일 / 3=알람규칙 / 4=통신설정)
    /// ★ [NotifyPropertyChangedFor] 필수
    ///    [ObservableProperty] 소스 생성 속성을 nameof() 로
    ///    PropertyChanged 핸들러에서 참조 시 컴파일 실패 → 이 방식 사용
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceTab))]
    [NotifyPropertyChangedFor(nameof(IsCanvasTab))]
    [NotifyPropertyChangedFor(nameof(IsScaleTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsCommTab))]
    private int _activeTabIndex;

    // §3 ─ 탭 가시성 프로퍼티 ────────────────────────────────

    public bool IsDeviceTab => ActiveTabIndex == 0;
    public bool IsCanvasTab => ActiveTabIndex == 1;
    public bool IsScaleTab => ActiveTabIndex == 2;
    public bool IsAlarmTab => ActiveTabIndex == 3;
    public bool IsCommTab => ActiveTabIndex == 4;

    // §4 ─ 커맨드 ─────────────────────────────────────────────

    /// <summary>
    /// 탭 전환 커맨드
    /// ★ CommandParameter 는 XAML 에서 항상 string 으로 전달됨
    ///    RelayCommand&lt;int&gt; 사용 시 ArgumentException 발생
    ///    → 반드시 string 파라미터 + int.TryParse 패턴
    /// </summary>
    [RelayCommand]
    private void SwitchTab(string tabParam)
    {
        if (int.TryParse(tabParam, out var idx))
            ActiveTabIndex = idx;
    }
}