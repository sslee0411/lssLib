// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainViewModel.cs
//  역할: Studio 메인 ViewModel
//  Base-0: 최소 구현 (SaveStatus 프로퍼티만)
//          이후 Step 마다 기능을 하나씩 추가
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Studio;

public partial class MainViewModel : ObservableObject
{
    // §1 ─ 상태 프로퍼티 ──────────────────────────────────────
    // Base-1 에서 탭 전환 프로퍼티 추가 예정

    /// <summary>헤더 저장 상태 표시 텍스트</summary>
    [ObservableProperty]
    private string _saveStatus = "준비됨";
}
