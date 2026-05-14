// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config.Demo · MainWindow.xaml.cs
// ══════════════════════════════════════════════════════════════════════════
using System.Windows;

namespace lssLib.Config.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>하단 상태바 메시지 갱신 (다른 View 에서 호출 가능).</summary>
    public static void SetStatus(string msg)
    {
        if (Application.Current.MainWindow is MainWindow mw)
            mw.TbStatus.Text = $"[{DateTime.Now:HH:mm:ss}]  {msg}";
    }
}