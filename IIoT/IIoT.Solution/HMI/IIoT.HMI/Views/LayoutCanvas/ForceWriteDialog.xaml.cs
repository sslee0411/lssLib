// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/LayoutCanvas/ForceWriteDialog.xaml.cs
//  역할: 강제쓰기 입력 다이얼로그 코드비하인드
//        값 입력 확인 후 ResultValue 에 담아 DialogResult=true 반환
//        (IIoT.Collector Views/Status/ForceWriteDialog.xaml.cs 이식 — 동일 동작)
//  HM-09: 신규
//  HM-12: ① 생성자에 hasActiveAlarm/alarmMessage 매개변수 추가 — true 이면
//         AlarmWarningPanel 을 표시하고 OkButton 을 기본 비활성화, "위험을
//         인지했으며 계속 진행합니다" 체크박스를 체크해야만 [쓰기]가 활성화된다
//         (활성 알람 중 강제쓰기 경고).
//         ② PrefillApiKey(apiKey) 공개 메서드 추가 — 세션 캐시된 API Key 를
//         호출부(LayoutCanvasView.xaml.cs)가 미리 채워 넣을 때 사용
//         (PasswordBox.Password 는 보안상 XAML Binding 이 불가능해 코드비하인드
//         에서만 설정 가능 — WPF 표준 제약).
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

using System.Windows;

namespace IIoT.HMI.Views.LayoutCanvas;

public partial class ForceWriteDialog : Window
{
    /// <summary>[쓰기] 클릭 시 입력된 값. 취소 시 null.</summary>
    public string? ResultValue { get; private set; }

    /// <summary>입력된 API Key (미입력 시 빈 문자열)</summary>
    public string ResultApiKey { get; private set; } = string.Empty;

    /// <param name="tagName">Tag 표시 이름</param>
    /// <param name="plcInfo">소속 PLC/Device 표시 정보</param>
    /// <param name="hasActiveAlarm">★ HM-12: 대상 Tag 에 현재 활성 알람이 있는지 —
    /// true 이면 경고 패널을 표시하고 체크박스 확인 전까지 [쓰기]를 막는다.</param>
    /// <param name="alarmMessage">★ HM-12: 경고 문구에 함께 표시할 알람 메시지(선택)</param>
    public ForceWriteDialog(string tagName, string plcInfo, bool hasActiveAlarm = false, string alarmMessage = "")
    {
        InitializeComponent();

        TxtTagName.Text = tagName;
        TxtTagInfo.Text = plcInfo;
        TxtWarning.Text = "⚠ 실제 PLC 에 즉시 반영됩니다. Raw 값 기준으로 입력하세요 (스케일 역변환 없음).";

        if (hasActiveAlarm)
        {
            AlarmWarningPanel.Visibility = Visibility.Visible;
            TxtAlarmWarning.Text = string.IsNullOrEmpty(alarmMessage)
                ? "⚠ 이 Tag는 현재 활성 알람 상태입니다. 강제쓰기는 위험할 수 있으니 신중히 진행하세요."
                : $"⚠ 이 Tag는 현재 활성 알람 상태입니다 ({alarmMessage}). 강제쓰기는 위험할 수 있으니 신중히 진행하세요.";
            OkButton.IsEnabled = false; // ChkAlarmAck 체크 전까지 [쓰기] 비활성화
        }
    }

    /// <summary>★ HM-12: 세션 중 같은 Collector 에 성공했던 API Key 를 미리 채워
    /// 넣는다. 빈 값이면 아무것도 하지 않는다(기존 동작 그대로 유지).</summary>
    public void PrefillApiKey(string apiKey)
    {
        if (!string.IsNullOrEmpty(apiKey))
            PwdApiKey.Password = apiKey;
    }

    /// <summary>★ HM-12: 활성 알람 경고 체크박스 상태에 따라 [쓰기] 버튼 활성화 여부 결정.</summary>
    private void ChkAlarmAck_CheckedChanged(object sender, RoutedEventArgs e)
    {
        OkButton.IsEnabled = ChkAlarmAck.IsChecked == true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtValue.Text))
        {
            MessageBox.Show(this, "값을 입력하세요.", "확인",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultValue  = TxtValue.Text.Trim();
        ResultApiKey = PwdApiKey.Password;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
