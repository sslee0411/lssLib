// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/PlcEditorView.xaml.cs
//  역할: PLC 편집기 코드비하인드
//  S-14: OnApplyTemplate 핸들러
//  S-21B: OnBulkAddress 핸들러
//  S-28: CommEntryCombo 주입 + OnCommEntryChanged + OnCommEntryDetach
//  Studio-P03: OnDriverSelectionChanged + _RenderParameterForm()
//  Studio-P03 fix: ApplyTagTemplate 제거 → ApplyTemplateDialog 직접 호출
//                  BulkAddressDialog namespace 수정 (Views.DeviceTree 직접)
//  생성: 2026-06-18 / 수정: 2026-06-27
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;
using IIoT.Studio.Core.Plugin;
using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IIoT.Studio.Views.DeviceTree;

public partial class PlcEditorView : UserControl
{
    public PlcEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // §0 ─ DataContext 변경 ────────────────────────────────

    private void OnDataContextChanged(object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not PlcTreeNode plc) return;

        var mainVm = _FindAncestorVm<MainViewModel>(this);
        if (mainVm is null) return;

        // ★ S-28: CommEntryCombo ItemsSource 주입
        CommEntryCombo.ItemsSource = mainVm.CommLibrary.Entries;

        // ★ Studio-P03: 드라이버 드롭다운 채우기
        var pluginSvc = mainVm.PluginRegistry;
        if (pluginSvc is not null)
        {
            var items = new List<string> { "(드라이버 없음 — 레거시 방식)" };
            items.AddRange(pluginSvc.GetDriverIds());
            DriverCombo.ItemsSource = items;

            DriverCombo.SelectedItem = string.IsNullOrEmpty(plc.DriverId)
                ? items[0]
                : plc.DriverId;

            // 현재 DriverId 에 맞는 파라미터 폼 렌더링
            if (!string.IsNullOrEmpty(plc.DriverId))
                _RenderParameterForm(pluginSvc.GetSchema(plc.DriverId), plc);
        }
    }

    // §1 ─ ★ Studio-P03: 드라이버 선택 변경 ─────────────

    private void OnDriverSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;
        if (DriverCombo.SelectedItem is not string selected) return;

        var mainVm    = _FindAncestorVm<MainViewModel>(this);
        var pluginSvc = mainVm?.PluginRegistry;

        // "(드라이버 없음)" 선택 시 초기화
        if (selected.StartsWith("("))
        {
            plc.DriverId = string.Empty;
            plc.DriverParams.Clear();
            _ClearParameterForm();
            return;
        }

        plc.DriverId = selected;
        plc.DriverParams.Clear();

        if (pluginSvc is not null)
        {
            var schema = pluginSvc.GetSchema(selected);
            _RenderParameterForm(schema, plc);

            foreach (var def in schema)
                if (!string.IsNullOrEmpty(def.DefaultValue))
                    plc.DriverParams[def.Key] = def.DefaultValue;
        }
    }

    // §2 ─ ★ Studio-P03: 파라미터 폼 동적 렌더링 ─────────

    private void _RenderParameterForm(
        IReadOnlyList<ParameterDefinition> schema,
        PlcTreeNode plc)
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
                ParameterType.Bool     => _CreateCheckBox(def, plc),
                ParameterType.Enum     => _CreateEnumCombo(def, plc),
                ParameterType.Password => _CreatePasswordBox(def, plc),
                _                      => _CreateTextBox(def, plc)
            };
            ParamPanel.Children.Add(input);

            if (!string.IsNullOrEmpty(def.Description))
            {
                ParamPanel.Children.Add(new TextBlock
                {
                    Text         = def.Description,
                    FontSize     = 10,
                    Foreground   = (Brush)FindResource("Text2Brush"),
                    Margin       = new Thickness(0, 2, 0, 12),
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                ParamPanel.Children.Add(
                    new FrameworkElement { Height = 12 });
            }
        }
    }

    private void _ClearParameterForm()
    {
        ParamPanel.Children.Clear();
        ParamBorder.Visibility = Visibility.Collapsed;
    }

    // §2-1 ─ 컨트롤 팩토리 ────────────────────────────────

    private TextBox _CreateTextBox(ParameterDefinition def, PlcTreeNode plc)
    {
        var tb = new TextBox
        {
            Style = (Style)FindResource("PropInput"),
            Text  = plc.DriverParams.TryGetValue(def.Key, out var v)
                    ? v : def.DefaultValue ?? string.Empty
        };
        tb.TextChanged += (_, _) => plc.DriverParams[def.Key] = tb.Text;
        return tb;
    }

    private CheckBox _CreateCheckBox(ParameterDefinition def, PlcTreeNode plc)
    {
        var current = plc.DriverParams.TryGetValue(def.Key, out var v)
                      ? v : def.DefaultValue ?? "false";
        var cb = new CheckBox
        {
            IsChecked  = current is "true" or "True" or "1",
            Foreground = (Brush)FindResource("TextBrush")
        };
        cb.Checked   += (_, _) => plc.DriverParams[def.Key] = "true";
        cb.Unchecked += (_, _) => plc.DriverParams[def.Key] = "false";
        return cb;
    }

    private ComboBox _CreateEnumCombo(ParameterDefinition def, PlcTreeNode plc)
    {
        var current = plc.DriverParams.TryGetValue(def.Key, out var v)
                      ? v : def.DefaultValue ?? string.Empty;
        var combo = new ComboBox
        {
            Style        = (Style)FindResource("PropCombo"),
            ItemsSource  = def.EnumValues ?? Array.Empty<string>(),
            SelectedItem = current
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string sel)
                plc.DriverParams[def.Key] = sel;
        };
        return combo;
    }

    private PasswordBox _CreatePasswordBox(ParameterDefinition def, PlcTreeNode plc)
    {
        var pb = new PasswordBox
        {
            Style    = (Style)FindResource("PropInput"),
            Password = plc.DriverParams.TryGetValue(def.Key, out var v)
                       ? v : def.DefaultValue ?? string.Empty
        };
        pb.PasswordChanged += (_, _) => plc.DriverParams[def.Key] = pb.Password;
        return pb;
    }

    // §3 ─ S-28: CommEntry 이벤트 ─────────────────────────

    private void OnCommEntryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;
        if (CommEntryCombo.SelectedItem is not CommEntry entry) return;
        plc.Host   = entry.Host;
        plc.Port   = entry.Port;
        plc.PollMs = entry.PollMs;
    }

    private void OnCommEntryDetach(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;
        plc.CommEntryId = null;
        CommEntryCombo.SelectedItem = null;
    }

    // §4 ─ S-14: 템플릿 적용 ─────────────────────────────
    // ★ ApplyTagTemplate(DeviceTreeViewModel) 없음
    //   → ApplyTemplateDialog 직접 호출 (DeviceEditorView.xaml.cs 동일 패턴)

    private void OnApplyTemplate(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;

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

        // ★ ApplyTemplateDialog: namespace IIoT.Studio.Views.DeviceTree (동일 namespace)
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
            plc.Children.Add(tag);
        }
    }

    // §5 ─ S-21B: 일괄 주소 ──────────────────────────────
    // ★ BulkAddressDialog: namespace IIoT.Studio.Views.DeviceTree (동일 namespace)
    //   생성자: (PlcTreeNode plc, Window owner)

    private void OnBulkAddress(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlcTreeNode plc) return;

        var tags = plc.Children.OfType<TagTreeNode>().ToList();
        if (tags.Count == 0)
        {
            MessageBox.Show(
                "이 PLC 하위에 Tag가 없습니다.\n먼저 Tag를 추가하세요.",
                "Tag 없음",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // ★ Views.BulkAddressDialog 아님 — 동일 namespace이므로 직접 참조
        var dlg = new BulkAddressDialog(plc, Window.GetWindow(this));

        if (dlg.ShowDialog() != true) return;
        if (dlg.Result.Count == 0) return;

        foreach (var (tag, address) in dlg.Result)
            tag.Address = address;
    }

    // §6 ─ 헬퍼: 비주얼 트리 탐색 ───────────────────────

    private static T? _FindAncestorVm<T>(DependencyObject start)
        where T : class
    {
        var cur = VisualTreeHelper.GetParent(start);
        while (cur is not null)
        {
            if (cur is FrameworkElement fe && fe.DataContext is T vm)
                return vm;
            cur = VisualTreeHelper.GetParent(cur);
        }
        return null;
    }
}
