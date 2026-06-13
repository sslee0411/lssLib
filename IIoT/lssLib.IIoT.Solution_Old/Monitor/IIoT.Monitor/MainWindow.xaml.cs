// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · MainWindow.xaml.cs
//  Phase 10: 신규
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using IIoT.UI.Themes;
using System.Windows;
using System.Windows.Input;

namespace IIoT.Monitor;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        ThemeManager.ThemeChanged += _ => { };
        Closed += (_, _) => ThemeManager.ThemeChanged -= _ => { };
    }

    // Ctrl+T / Ctrl+Shift+T 테마 순환
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.T && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            int dir = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : +1;
            var all = ThemeManager.AllThemes;
            int idx = all.Select((t, i) => (t, i))
                          .FirstOrDefault(x => x.t.Kind == ThemeManager.Current).i;
            int next = ((idx + dir) % all.Count + all.Count) % all.Count;
            ThemeManager.Apply(all[next].Kind);
            e.Handled = true;
        }
    }
}
