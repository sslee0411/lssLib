// ══════════════════════════════════════════════════════════
//  IIoT.HMI · MainWindow.xaml.cs
//  역할: 메인 창 코드비하인드
//  HM-Base-0: 생성자만
//  HM-Base-1~2: HmiMainViewModel(DataContext) 주입
//  HM-02: CollectorManageView 주입 (ContentControl 호스트, DI 필요 View 패턴)
//  HM-03: LayoutCanvasView 주입 (레이아웃 편집 탭 호스트)
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Views.CollectorManage;
using IIoT.HMI.Views.LayoutCanvas;
using System.Windows;

namespace IIoT.HMI;

public partial class MainWindow : Window
{
    // §1 ─ 생성자 ─────────────────────────────────────────────

    public MainWindow(HmiMainViewModel vm,
                      CollectorManageView collectorManageView,
                      LayoutCanvasView    layoutCanvasView)
    {
        InitializeComponent();

        DataContext = vm;

        // ★ HM-02: DI 필요 View → ContentControl + 코드 주입 패턴
        //   (CollectorManageView 자체가 Loaded 이벤트에서 hmi.json 로드를 수행 —
        //   Monitor CollectorManageView.Loaded 패턴과 동일)
        CollectorManageHost.Content = collectorManageView;

        // ★ HM-03: 레이아웃 편집 캔버스 주입 (초기 데이터 로드 불필요 — 메모리 상태만)
        LayoutCanvasHost.Content = layoutCanvasView;
    }
}
