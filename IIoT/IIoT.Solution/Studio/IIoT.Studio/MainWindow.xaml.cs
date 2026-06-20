// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  Base-0: DI 생성자 + DataContext 주입
//  S-15B: HasUnsavedChanges 연동 (MainViewModel에서 처리)
//  S-22: OnClosing — 미저장 변경사항 있을 때 저장 여부 확인 팝업
//  생성: 2026-06-15 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

using System.ComponentModel;
using System.Windows;

namespace IIoT.Studio;

public partial class MainWindow : Window
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly MainViewModel _vm;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    /// <summary>
    /// DI 컨테이너에서 ViewModel 주입받아 DataContext 설정.
    /// ★ 기본 생성자(매개변수 없음) 절대 사용 금지
    ///    — App.xaml.cs 의 AddSingleton 팩토리와 충돌
    /// </summary>
    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
    }

    // §3 ─ 창 닫기 — 미저장 확인 (★ S-22) ───────────────────

    /// <summary>
    /// 창 닫기 시 미저장 변경사항 확인.
    /// 예  → SaveAsync() 후 종료
    /// 아니요 → 미저장 상태로 바로 종료
    /// 취소 → 종료 취소 (창 유지)
    /// </summary>
    protected override async void OnClosing(CancelEventArgs e)
    {
        // 미저장 변경사항 없으면 바로 종료
        if (!_vm.HasUnsavedChanges)
        {
            base.OnClosing(e);
            return;
        }

        // ★ 먼저 종료를 취소하고 팝업 결과에 따라 처리
        //   async void OnClosing에서 await 사용 시 이미 닫혀버리는 것을 방지
        e.Cancel = true;

        var result = MessageBox.Show(
            "저장하지 않은 변경사항이 있습니다.\n저장하시겠습니까?",
            "IIoT Studio — 종료 확인",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        switch (result)
        {
            case MessageBoxResult.Yes:
                // ★ SaveAsync()는 private — SaveCommand 통해 실행
                await _vm.SaveCommand.ExecuteAsync(null);
                _vm.HasUnsavedChanges = false;
                Application.Current.Shutdown();
                break;

            case MessageBoxResult.No:
                // 미저장 상태로 바로 종료
                _vm.HasUnsavedChanges = false;
                Application.Current.Shutdown();
                break;

            case MessageBoxResult.Cancel:
            default:
                // 종료 취소 — e.Cancel=true 상태 유지 (아무것도 안 함)
                break;
        }
    }
}
