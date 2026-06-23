// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/DeviceTree/BulkAddressDialog.xaml.cs
//  역할: Tag 일괄 주소 부여 다이얼로그 코드비하인드
//  S-21B: 신규
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using System.Windows;
using System.Windows.Controls;

namespace IIoT.Studio.Views.DeviceTree;

// §1 ─ 미리보기 항목 ──────────────────────────────────────

public sealed record PreviewItem(string TagName, string Address);

// §2 ─ 다이얼로그 ─────────────────────────────────────────

public partial class BulkAddressDialog : Window
{
    // §2-1 ─ 필드 ─────────────────────────────────────────────

    private readonly PlcTreeNode         _plc;
    private readonly List<TagTreeNode>   _tags;
    private readonly PlcVendor           _vendor;
    private readonly RegisterType        _defaultRegister;

    private int _defaultStep;

    /// <summary>적용 결과: Tag → 주소 문자열 매핑</summary>
    public List<(TagTreeNode Tag, string Address)> Result { get; } = new();

    // §2-2 ─ 생성자 ───────────────────────────────────────────

    public BulkAddressDialog(PlcTreeNode plc, Window owner)
    {
        InitializeComponent();
        Owner  = owner;
        _plc   = plc;
        _tags  = plc.Children.OfType<TagTreeNode>().ToList();
        _vendor = plc.PlcVendor;

        // 첫 번째 Tag의 RegisterType을 기본 레지스터 종류로 사용
        _defaultRegister = _tags.FirstOrDefault()?.RegisterType
                           ?? RegisterTypeExtensions.ForVendor(_vendor).FirstOrDefault();

        // 기본 간격 계산
        _defaultStep = _defaultRegister.DefaultStep(_vendor);

        // UI 초기화
        _InitUi();
    }

    // §2-3 ─ UI 초기화 ────────────────────────────────────────

    private void _InitUi()
    {
        // 안내 텍스트
        TxtInfo.Text = $"PLC: {_plc.Name}  |  제조사: {_vendor.ToLabel()}  |  " +
                       $"Tag {_tags.Count}개에 순서대로 주소를 부여합니다.";

        // 기본 시작 주소
        var defaultAddr = TagAddress.Default(_vendor, _defaultRegister);
        TxtStartAddress.Text = defaultAddr.ToString();

        // 기본 간격
        TxtStep.Text = _defaultStep.ToString();

        // 힌트
        TxtStartHint.Text = TagAddress.GetHint(_vendor, _defaultRegister);
        _UpdateStepHint();

        // 태그 없으면 적용 버튼 비활성
        BtnApply.IsEnabled = _tags.Count > 0;

        // 미리보기 초기 계산
        _RefreshPreview();
    }

    // §2-4 ─ 미리보기 갱신 ───────────────────────────────────

    private void _RefreshPreview()
    {
        if (!int.TryParse(TxtStep.Text, out var step) || step < 1) step = 1;

        // 시작 주소 파싱
        var startText = TxtStartAddress.Text?.Trim() ?? string.Empty;
        var current   = TagAddress.Parse(startText, _vendor, _defaultRegister);

        var items = new List<PreviewItem>();
        foreach (var tag in _tags)
        {
            items.Add(new PreviewItem(tag.Name, current.ToString()));
            current = current.Next(step);
        }

        PreviewList.ItemsSource = items;
    }

    // §2-5 ─ 이벤트 핸들러 ───────────────────────────────────

    private void OnStartAddressChanged(object sender, TextChangedEventArgs e)
        => _RefreshPreview();

    private void OnStepChanged(object sender, TextChangedEventArgs e)
    {
        _UpdateStepHint();
        _RefreshPreview();
    }

    private void _UpdateStepHint()
    {
        if (!int.TryParse(TxtStep.Text, out var step) || step < 1)
        {
            TxtStepHint.Text = "간격은 1 이상의 정수를 입력하세요.";
            return;
        }

        // 비트 주소인 경우 추가 안내
        if (_defaultRegister.IsBit())
            TxtStepHint.Text = $"비트 단위 {step} 증가  " +
                               $"(0~7 순환, 예: X0.7+{step} → X1.{(step-1)%8})";
        else
            TxtStepHint.Text = $"주소 {step} 씩 증가  " +
                               $"(기본값: {_defaultStep} — " +
                               (_defaultStep == 2 ? "FloatLE 2레지스터" : "1 워드") + ")";
    }

    private void OnResetStep(object sender, RoutedEventArgs e)
    {
        TxtStep.Text = _defaultStep.ToString();
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtStep.Text, out var step) || step < 1)
        {
            MessageBox.Show("간격은 1 이상의 정수를 입력하세요.",
                "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var startText = TxtStartAddress.Text?.Trim() ?? string.Empty;
        var current   = TagAddress.Parse(startText, _vendor, _defaultRegister);

        // 결과 목록 생성
        Result.Clear();
        foreach (var tag in _tags)
        {
            Result.Add((tag, current.ToString()));
            current = current.Next(step);
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
