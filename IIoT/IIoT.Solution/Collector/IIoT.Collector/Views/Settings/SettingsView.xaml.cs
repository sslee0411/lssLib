// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Views/Settings/SettingsView.xaml.cs
//  역할: 환경설정 탭 코드비하인드
//        PasswordBox 는 보안상 Password 속성이 바인딩 불가하므로
//        코드비하인드에서 직접 ViewModel.Settings 하위 필드와 동기화한다.
//  C-SET-01: 신규
//  규칙:
//    ★ 기본 생성자 절대 금지 — App.xaml.cs AddSingleton 팩토리와 충돌
//    ★ _suppressSync: Initialize/Reload 로 Settings 참조가 교체될 때
//      PasswordBox.Password 를 코드로 다시 채우는 과정에서 PasswordChanged 가
//      또 발생해 ViewModel 에 값을 덮어쓰는 재귀를 막기 위한 가드
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Collector.ViewModels;
using System.ComponentModel;
using System.Windows.Controls;

namespace IIoT.Collector.Views.Settings;

public partial class SettingsView : UserControl
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly SettingsViewModel _vm;
    private bool _suppressSync;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public SettingsView(SettingsViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;

        Loaded += (_, _) => _RefreshPasswordBoxes();
        _vm.PropertyChanged += _OnVmPropertyChanged;
    }

    // §3 ─ Settings 교체 시 PasswordBox 재동기화 ─────────────

    private void _OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Settings))
            _RefreshPasswordBoxes();
    }

    private void _RefreshPasswordBoxes()
    {
        _suppressSync = true;
        try
        {
            InfluxTokenBox.Password      = _vm.Settings.Storage.InfluxDB.Token ?? string.Empty;
            MqttPasswordBox.Password     = _vm.Settings.Storage.Mqtt.Password ?? string.Empty;
            SmtpPasswordBox.Password     = _vm.Settings.Notification.Smtp.Password ?? string.Empty;
            ForceWriteApiKeyBox.Password = _vm.Settings.Security.ForceWriteApiKey ?? string.Empty;
            ApiKeyBox.Password           = _vm.Settings.Security.ApiKey ?? string.Empty;
        }
        finally
        {
            _suppressSync = false;
        }
    }

    // §4 ─ PasswordChanged 핸들러 (ViewModel.Settings 로 즉시 반영) ──

    private void InfluxTokenBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_suppressSync) return;
        _vm.Settings.Storage.InfluxDB.Token = InfluxTokenBox.Password;
    }

    private void MqttPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_suppressSync) return;
        _vm.Settings.Storage.Mqtt.Password = MqttPasswordBox.Password;
    }

    private void SmtpPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_suppressSync) return;
        _vm.Settings.Notification.Smtp.Password = SmtpPasswordBox.Password;
    }

    private void ForceWriteApiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_suppressSync) return;
        _vm.Settings.Security.ForceWriteApiKey = ForceWriteApiKeyBox.Password;
    }

    private void ApiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_suppressSync) return;
        _vm.Settings.Security.ApiKey = ApiKeyBox.Password;
    }
}
