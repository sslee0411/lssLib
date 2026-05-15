// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net.TcpTestServer · MainWindow.xaml.cs
//  역할: WPF 서버 UI 코드비하인드
// ══════════════════════════════════════════════════════════════════════

using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace lssLib.Net.TcpTestServer;

public partial class MainWindow : Window
{
    #region §1 ─ 필드

    private TcpServerCore? _server;
    private const int MAX_LOG_LINES = 500;

    #endregion

    #region §2 ─ 생성자

    public MainWindow()
    {
        // ComboBox IsSelected 발화 전 상태 초기화
        InitializeComponent();
        UpdateIntervalVisibility();
    }

    #endregion

    #region §3 ─ 시작 / 정지

    private async void BtnStartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_server?.IsRunning == true)
        {
            // 정지
            BtnStartStop.IsEnabled = false;
            await _server.StopAsync();
            _server = null;
            SetStopped();
            BtnStartStop.IsEnabled = true;
        }
        else
        {
            // 시작
            if (!int.TryParse(TxtPort.Text, out int port) || port < 1024 || port > 65535)
            {
                MessageBox.Show("포트 번호가 올바르지 않습니다. (1024~65535)", "입력 오류",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtInterval.Text, out int interval) || interval < 50)
                interval = 500;

            var mode = (CmbMode.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() == "Echo"
                ? ServerMode.Echo
                : ServerMode.Push;

            _server = new TcpServerCore();
            _server.Log += OnServerLog;
            _server.ClientsChanged += OnClientsChanged;
            _server.StatsChanged += OnStatsChanged;

            try
            {
                BtnStartStop.IsEnabled = false;
                await _server.StartAsync(port, mode, interval);
                SetRunning(port, mode);
            }
            catch (Exception ex)
            {
                AppendLog($"[오류] 서버 시작 실패: {ex.Message}");
                _server = null;
                SetStopped();
            }
            finally
            {
                BtnStartStop.IsEnabled = true;
            }
        }
    }

    #endregion

    #region §4 ─ 이벤트 핸들러

    private void OnServerLog(string msg)
        => Dispatcher.InvokeAsync(() => AppendLog(msg));

    private void OnClientsChanged()
        => Dispatcher.InvokeAsync(() =>
            LblClients.Text = $"{_server?.ConnectedClients.Count ?? 0}개");

    private void OnStatsChanged()
        => Dispatcher.InvokeAsync(() =>
        {
            LblSent.Text = (_server?.TotalSent ?? 0).ToString("N0");
            LblReceived.Text = (_server?.TotalReceived ?? 0).ToString("N0");
        });

    private void CmbMode_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdateIntervalVisibility();

    private void BtnClear_Click(object sender, RoutedEventArgs e)
        => TxtLog.Clear();

    #endregion

    #region §5 ─ UI 헬퍼

    private void SetRunning(int port, ServerMode mode)
    {
        BtnStartStop.Content = "■ 정지";
        BtnStartStop.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        LblStatus.Text = $"실행 중 — 포트 {port} / {mode} 모드";
        TxtPort.IsEnabled = false;
        CmbMode.IsEnabled = false;
        TxtInterval.IsEnabled = false;
    }

    private void SetStopped()
    {
        BtnStartStop.Content = "▶ 시작";
        BtnStartStop.Background = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
        LblStatus.Text = "정지";
        LblClients.Text = "0개";
        LblSent.Text = "0";
        LblReceived.Text = "0";
        TxtPort.IsEnabled = true;
        CmbMode.IsEnabled = true;
        TxtInterval.IsEnabled = true;
    }

    private void UpdateIntervalVisibility()
    {
        if (LblInterval is null || TxtInterval is null) return;

        bool isPush = (CmbMode?.SelectedItem as System.Windows.Controls.ComboBoxItem)?
                      .Tag?.ToString() != "Echo";

        LblInterval.Visibility = isPush ? Visibility.Visible : Visibility.Collapsed;
        TxtInterval.Visibility = isPush ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>로그 추가 + 최대 줄 수 제한 (Consolas 폰트).</summary>
    private void AppendLog(string msg)
    {
        TxtLog.AppendText(msg + "\n");

        // 최대 MAX_LOG_LINES 유지
        var lines = TxtLog.Text.Split('\n');
        if (lines.Length > MAX_LOG_LINES)
        {
            TxtLog.Text = string.Join("\n", lines[^MAX_LOG_LINES..]);
        }

        TxtLog.ScrollToEnd();
    }

    #endregion

    #region §6 ─ 윈도우 종료

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_server?.IsRunning == true)
        {
            e.Cancel = true;
            await _server.StopAsync();
            Application.Current.Shutdown();
        }

        base.OnClosing(e);
    }

    #endregion
}