// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/ApplyTemplateDialog.xaml.cs
//  역할: 태그 템플릿 선택 + Modbus 시작주소 입력 다이얼로그
//  S-13B: Views/Canvas/ → Views/DeviceTree/ 로 이동
//         (TagTemplate은 장비 관리 영역)
//  생성: 2026-06-18
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace IIoT.Studio.Views.DeviceTree;

// §1 ─ 다이얼로그 ViewModel ───────────────────────────────

public sealed partial class ApplyTemplateDialogVm : ObservableObject
{
    public ObservableCollection<TagTemplate> Templates { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTemplate))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyPropertyChangedFor(nameof(PreviewFirstAddress))]
    private TagTemplate? _selectedTemplate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyPropertyChangedFor(nameof(PreviewFirstAddress))]
    private string _startAddress = "40001";

    public bool HasSelectedTemplate => SelectedTemplate is not null;

    public bool CanApply =>
        SelectedTemplate is not null
        && int.TryParse(StartAddress, out var n)
        && n > 0;

    public string PreviewFirstAddress
    {
        get
        {
            if (SelectedTemplate?.Items.FirstOrDefault() is not { } first) return "-";
            if (!int.TryParse(StartAddress, out var n)) return "-";
            return first.CalcAddress(n).ToString();
        }
    }

    public ApplyTemplateDialogVm(IEnumerable<TagTemplate> templates)
    {
        Templates        = new(templates);
        SelectedTemplate = Templates.FirstOrDefault();
    }
}

// §2 ─ 다이얼로그 코드비하인드 ────────────────────────────

public partial class ApplyTemplateDialog : Window
{
    public TagTemplate? ResultTemplate     { get; private set; }
    public int          ResultStartAddress { get; private set; }

    public ApplyTemplateDialog(
        IEnumerable<TagTemplate> templates,
        Window owner)
    {
        InitializeComponent();
        Owner       = owner;
        DataContext = new ApplyTemplateDialogVm(templates);
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        var vm = (ApplyTemplateDialogVm)DataContext;
        if (!vm.CanApply) return;
        ResultTemplate     = vm.SelectedTemplate;
        ResultStartAddress = int.Parse(vm.StartAddress);
        DialogResult       = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
