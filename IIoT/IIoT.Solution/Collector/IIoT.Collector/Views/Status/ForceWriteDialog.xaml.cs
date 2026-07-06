// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Views/Status/ForceWriteDialog.xaml.cs
//  역할: 강제쓰기 입력 다이얼로그 코드비하인드
//        값 입력 확인 후 ResultValue 에 담아 DialogResult=true 반환
//  C-15: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using System.Windows;

namespace IIoT.Collector.Views.Status;

public partial class ForceWriteDialog : Window
{
    /// <summary>[쓰기] 클릭 시 입력된 값. 취소 시 null.</summary>
    public string? ResultValue { get; private set; }

    /// <param name="tagName">Tag 표시 이름</param>
    /// <param name="plcName">소속 PLC/Device 표시 이름</param>
    public ForceWriteDialog(string tagName, string plcName)
    {
        InitializeComponent();

        TxtTagName.Text = tagName;
        TxtTagInfo.Text = plcName;
        TxtWarning.Text = "⚠ 실제 PLC 에 즉시 반영됩니다. Raw 값 기준으로 입력하세요 (스케일 역변환 없음).";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtValue.Text))
        {
            MessageBox.Show(this, "값을 입력하세요.", "확인",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultValue = TxtValue.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
