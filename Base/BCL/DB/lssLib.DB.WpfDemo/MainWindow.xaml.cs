// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.WpfDemo · MainWindow.xaml.cs
//  역할: 탭 컨테이너 — 상태바 공통 메시지 중계
// ══════════════════════════════════════════════════════════════════════
namespace lssLib.DB.WpfDemo;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 각 페이지에서 상태바 메시지를 올릴 수 있도록 핸들러 전달
        PageInflux.SetStatusCallback(SetStatus);
        PageMsSql.SetStatusCallback(SetStatus);
        PageOracle.SetStatusCallback(SetStatus);
        PageMySql.SetStatusCallback(SetStatus);
        PageSqlite.SetStatusCallback(SetStatus);
    }

    /// <summary>하단 상태바 메시지 설정.</summary>
    public void SetStatus(string message)
        => Dispatcher.InvokeAsync(() => TxtStatusBar.Text = message);
}
