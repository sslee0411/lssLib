// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/DeviceEditorView.xaml.cs
//  역할: 장비 편집기 코드비하인드
//  S-03: 초기 구현
//  S-14 fix6: OnApplyTemplate 핸들러
//  Studio-P03b: OnDriverSelectionChanged + _RenderParameterForm()
//               OnCommEntryChanged + OnCommEntryDetach
//               (PLC 노드와 동일 수준 — 단독 통신 장비 지원)
//  S-프로토콜01: ProtocolEntryCombo 주입 + OnProtocolEntryDetach 추가
//  생성: 2026-06-15 / 수정: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;
using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IIoT.Studio.Views.DeviceTree;

public partial class DeviceEditorView : UserControl
{
    public DeviceEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // §0 ─ DataContext 변경 ────────────────────────────────

    private void OnDataContextChanged(object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not DeviceTreeNode dev) return;

        var mainVm = _FindAncestorVm<MainViewModel>(this);
        if (mainVm is null) return;

        // ★ Studio-P03b: CommEntryCombo ItemsSource 주입
        CommEntryCombo.ItemsSource = mainVm.CommLibrary.Entries;

        // ★ S-프로토콜01: ProtocolEntryCombo ItemsSource 주입
        ProtocolEntryCombo.ItemsSource = mainVm.ProtocolLibrary.Entries;

        // ★ Studio-P03b: 드라이버 드롭다운 채우기
        var pluginSvc = mainVm.PluginRegistry;
        if (pluginSvc is not null)
        {
            var items = new List<string> { "(드라이버 없음 — 레거시 방식)" };
            items.AddRange(pluginSvc.GetDriverIds());
            DriverCombo.ItemsSource = items;

            DriverCombo.SelectedItem = string.IsNullOrEmpty(dev.DriverId)
                ? items[0]
                : dev.DriverId;

            if (!string.IsNullOrEmpty(dev.DriverId))
                _RenderParameterForm(pluginSvc.GetSchema(dev.DriverId), dev);
        }
    }

    // §1 ─ ★ Studio-P03b: 드라이버 선택 변경 ────────────

    private void OnDriverSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not DeviceTreeNode dev) return;
        if (DriverCombo.SelectedItem is not string selected) return;

        var mainVm    = _FindAncestorVm<MainViewModel>(this);
        var pluginSvc = mainVm?.PluginRegistry;

        if (selected.StartsWith("("))
        {
            dev.DriverId = string.Empty;
            dev.DriverParams.Clear();
            _ClearParameterForm();
            return;
        }

        dev.DriverId = selected;
        dev.DriverParams.Clear();

        if (pluginSvc is not null)
        {
            var schema = pluginSvc.GetSchema(selected);
            _RenderParameterForm(schema, dev);
            foreach (var def in schema)
                if (!string.IsNullOrEmpty(def.DefaultValue))
                    dev.DriverParams[def.Key] = def.DefaultValue;
        }
    }

    // §2 ─ ★ Studio-P03b: 파라미터 폼 동적 렌더링 ───────

    private void _RenderParameterForm(
        IReadOnlyList<ParameterDefinition> schema,
        DeviceTreeNode dev)
    {
        ParamPanel.Children.Clear();

        if (schema.Count == 0)
        {
            _ClearParameterForm();
            return;
        }

        ParamBorder.Visibility = Visibility.Visible;

        foreach (var def in schema)
        {
            ParamPanel.Children.Add(new TextBlock
            {
                Text       = def.IsRequired ? $"{def.DisplayName} *" : def.DisplayName,
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Text2Brush"),
                Margin     = new Thickness(0, 0, 0, 4),
                ToolTip    = def.Description
            });

            UIElement input = def.Type switch
            {
                ParameterType.Bool     => _CreateCheckBox(def, dev),
                ParameterType.Enum     => _CreateEnumCombo(def, dev),
                ParameterType.Password => _CreatePasswordBox(def, dev),
                _                      => _CreateTextBox(def, dev)
            };
            ParamPanel.Children.Add(input);

            ParamPanel.Children.Add(string.IsNullOrEmpty(def.Description)
                ? (UIElement)new FrameworkElement { Height = 12 }
                : new TextBlock
                {
                    Text         = def.Description,
                    FontSize     = 10,
                    Foreground   = (Brush)FindResource("Text2Brush"),
                    Margin       = new Thickness(0, 2, 0, 12),
                    TextWrapping = TextWrapping.Wrap
                });
        }
    }

    private void _ClearParameterForm()
    {
        ParamPanel.Children.Clear();
        ParamBorder.Visibility = Visibility.Collapsed;
    }

    // §2-1 ─ 컨트롤 팩토리 ────────────────────────────────

    private TextBox _CreateTextBox(ParameterDefinition def, DeviceTreeNode dev)
    {
        var tb = new TextBox
        {
            Style = (Style)FindResource("PropInput"),
            Text  = dev.DriverParams.TryGetValue(def.Key, out var v)
                    ? v : def.DefaultValue ?? string.Empty
        };
        tb.TextChanged += (_, _) => dev.DriverParams[def.Key] = tb.Text;
        return tb;
    }

    private CheckBox _CreateCheckBox(ParameterDefinition def, DeviceTreeNode dev)
    {
        var cur = dev.DriverParams.TryGetValue(def.Key, out var v)
                  ? v : def.DefaultValue ?? "false";
        var cb  = new CheckBox
        {
            IsChecked  = cur is "true" or "True" or "1",
            Foreground = (Brush)FindResource("TextBrush")
        };
        cb.Checked   += (_, _) => dev.DriverParams[def.Key] = "true";
        cb.Unchecked += (_, _) => dev.DriverParams[def.Key] = "false";
        return cb;
    }

    private ComboBox _CreateEnumCombo(ParameterDefinition def, DeviceTreeNode dev)
    {
        var cur   = dev.DriverParams.TryGetValue(def.Key, out var v)
                    ? v : def.DefaultValue ?? string.Empty;
        var combo = new ComboBox
        {
            Style        = (Style)FindResource("PropCombo"),
            ItemsSource  = def.EnumValues ?? Array.Empty<string>(),
            SelectedItem = cur
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string sel)
                dev.DriverParams[def.Key] = sel;
        };
        return combo;
    }

    private PasswordBox _CreatePasswordBox(ParameterDefinition def, DeviceTreeNode dev)
    {
        var pb = new PasswordBox
        {
            Style    = (Style)FindResource("PropInput"),
            Password = dev.DriverParams.TryGetValue(def.Key, out var v)
                       ? v : def.DefaultValue ?? string.Empty
        };
        pb.PasswordChanged += (_, _) => dev.DriverParams[def.Key] = pb.Password;
        return pb;
    }

    // §3 ─ ★ Studio-P03b: CommEntry 이벤트 ───────────────

    private void OnCommEntryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not DeviceTreeNode dev) return;
        if (CommEntryCombo.SelectedItem is not CommEntry entry) return;
        dev.Host   = entry.Host;
        dev.Port   = entry.Port;
        dev.PollMs = entry.PollMs;
    }

    private void OnCommEntryDetach(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceTreeNode dev) return;
        dev.CommEntryId = null;
        CommEntryCombo.SelectedItem = null;
    }

    // §3-1 ─ ★ S-프로토콜01: ProtocolEntry 참조 해제 ─────

    private void OnProtocolEntryDetach(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceTreeNode dev) return;
        dev.ProtocolEntryId = null;
        ProtocolEntryCombo.SelectedItem = null;
    }

    // §4 ─ S-14: 템플릿 적용 ─────────────────────────────

    private void OnApplyTemplate(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceTreeNode device) return;

        var treeVm = _FindAncestorVm<DeviceTreeViewModel>(this);
        if (treeVm?.TagTemplateVm is null) return;

        if (!treeVm.TagTemplateVm.Templates.Any())
        {
            MessageBox.Show(
                "저장된 템플릿이 없습니다.\n툴바 [📋 템플릿 관리] 버튼으로 먼저 작성하세요.",
                "IIoT Studio",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dlg = new ApplyTemplateDialog(
            treeVm.TagTemplateVm.Templates,
            Window.GetWindow(this));

        if (dlg.ShowDialog() != true) return;
        if (dlg.ResultTemplate is null) return;

        foreach (var item in dlg.ResultTemplate.Items)
        {
            var tag = new TagTreeNode(item.Name)
            {
                Address  = item.CalcAddress(dlg.ResultStartAddress).ToString(),
                DataType = item.BufType,
                Unit     = item.Unit
            };
            device.Children.Add(tag);
        }
    }

    // §5 ─ 헬퍼 ──────────────────────────────────────────

    private static T? _FindAncestorVm<T>(DependencyObject child)
        where T : class
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is FrameworkElement fe && fe.DataContext is T vm)
                return vm;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
