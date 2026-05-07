// ══════════════════════════════════════════════════════════
//  lssLib.Messaging.Demo · EventBusView.xaml.cs
//  역할: EventBus Pub/Sub 데모 코드비하인드
// ══════════════════════════════════════════════════════════

using lssLib.messaging.demo;
using lssLib.Messaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace lssLib.Messaging.Demo.Views;

public partial class EventBusView : UserControl
{
    #region §1 ─ 구독 핸들 필드

    private IDisposable? _subSensor;
    private IDisposable? _subNetwork;
    private IDisposable? _subAlarm;

    private int _totalReceived;
    private readonly Random _rng = new();

    #endregion

    #region §2 ─ 생성자

    public EventBusView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AppendLog("── EventBus 데모 준비 완료. 구독 버튼을 눌러 시작하세요. ──", "#94A3B8");
        RefreshSubCount();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 모든 구독 해제 (메모리 누수 방지)
        _subSensor?.Dispose();
        _subNetwork?.Dispose();
        _subAlarm?.Dispose();
    }

    #endregion

    #region §3 ─ 구독 버튼

    private void BtnSubSensor_Click(object sender, RoutedEventArgs e)
    {
        if (_subSensor is not null) return;

        _subSensor = EventBus.Instance.Subscribe<SensorDataEvent>(OnSensorDataReceived);

        BtnSubSensor.IsEnabled = false;
        BtnUnsubSensor.IsEnabled = true;
        AppendLog("✅ SensorDataEvent 구독 시작", "#4ADE80");
        RefreshSubCount();
    }

    private void BtnUnsubSensor_Click(object sender, RoutedEventArgs e)
    {
        _subSensor?.Dispose();
        _subSensor = null;

        BtnSubSensor.IsEnabled = true;
        BtnUnsubSensor.IsEnabled = false;
        AppendLog("✖ SensorDataEvent 구독 해제", "#F87171");
        RefreshSubCount();
    }

    private void BtnSubNetwork_Click(object sender, RoutedEventArgs e)
    {
        if (_subNetwork is not null) return;

        _subNetwork = EventBus.Instance.Subscribe<NetworkStatusEvent>(OnNetworkStatusReceived);

        BtnSubNetwork.IsEnabled = false;
        BtnUnsubNetwork.IsEnabled = true;
        AppendLog("✅ NetworkStatusEvent 구독 시작", "#4ADE80");
        RefreshSubCount();
    }

    private void BtnUnsubNetwork_Click(object sender, RoutedEventArgs e)
    {
        _subNetwork?.Dispose();
        _subNetwork = null;

        BtnSubNetwork.IsEnabled = true;
        BtnUnsubNetwork.IsEnabled = false;
        AppendLog("✖ NetworkStatusEvent 구독 해제", "#F87171");
        RefreshSubCount();
    }

    private void BtnSubAlarm_Click(object sender, RoutedEventArgs e)
    {
        if (_subAlarm is not null) return;

        _subAlarm = EventBus.Instance.Subscribe<AlarmEvent>(OnAlarmReceived);

        BtnSubAlarm.IsEnabled = false;
        BtnUnsubAlarm.IsEnabled = true;
        AppendLog("✅ AlarmEvent 구독 시작", "#4ADE80");
        RefreshSubCount();
    }

    private void BtnUnsubAlarm_Click(object sender, RoutedEventArgs e)
    {
        _subAlarm?.Dispose();
        _subAlarm = null;

        BtnSubAlarm.IsEnabled = true;
        BtnUnsubAlarm.IsEnabled = false;
        AppendLog("✖ AlarmEvent 구독 해제", "#F87171");
        RefreshSubCount();
    }

    #endregion

    #region §4 ─ 발행 버튼

    private void BtnPublishSensor_Click(object sender, RoutedEventArgs e)
    {
        var evt = MakeSensorEvent();
        EventBus.Instance.Publish(evt);
        AppendLog($"▶ Publish  SensorDataEvent  id={evt.MessageId}", "#7C6AF7");
    }

    private async void BtnPublishSensorBurst_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("▶ 연속 발행 시작 (5회)...", "#7C6AF7");
        for (int i = 1; i <= 5; i++)
        {
            var evt = MakeSensorEvent(deviceId: i);
            EventBus.Instance.Publish(evt);
            await Task.Delay(200);
        }
        AppendLog("▶ 연속 발행 완료", "#7C6AF7");
    }

    private void BtnPublishConnected_Click(object sender, RoutedEventArgs e)
    {
        var hosts = new[] { "192.168.0.10", "sensor.local", "gateway.plant" };
        var latency = _rng.Next(5, 50);
        var evt = new NetworkStatusEvent(true, hosts[_rng.Next(hosts.Length)], latency);
        EventBus.Instance.Publish(evt);
        AppendLog($"▶ Publish  NetworkStatusEvent  연결됨  id={evt.MessageId}", "#4ADE80");
    }

    private void BtnPublishDisconnected_Click(object sender, RoutedEventArgs e)
    {
        var evt = new NetworkStatusEvent(false, "192.168.0.10");
        EventBus.Instance.Publish(evt);
        AppendLog($"▶ Publish  NetworkStatusEvent  연결 끊김  id={evt.MessageId}", "#F87171");
    }

    private void BtnPublishWarn_Click(object sender, RoutedEventArgs e)
    {
        var sources = new[] { "Sensor#1", "Network", "Database", "Scheduler" };
        var msgs = new[] { "응답 지연 감지", "온도 경계값 근접", "재시도 발생", "CPU 사용률 높음" };
        var idx = _rng.Next(sources.Length);
        var evt = new AlarmEvent(sources[idx], msgs[idx], IsCritical: false);
        EventBus.Instance.Publish(evt);
        AppendLog($"▶ Publish  AlarmEvent  경고  id={evt.MessageId}", "#FBBF24");
    }

    private void BtnPublishCritical_Click(object sender, RoutedEventArgs e)
    {
        var evt = new AlarmEvent("EmergencySystem", "비상 정지 신호 감지", IsCritical: true);
        EventBus.Instance.Publish(evt);
        AppendLog($"▶ Publish  AlarmEvent  긴급  id={evt.MessageId}", "#FF6B6B");
    }

    private async void BtnPublishAsync_Click(object sender, RoutedEventArgs e)
    {
        var evt = MakeSensorEvent();
        AppendLog($"▶ PublishAsync  SensorDataEvent  id={evt.MessageId}", "#56CFE1");
        await EventBus.Instance.PublishAsync(evt);
        AppendLog($"   → 모든 핸들러 완료", "#56CFE1");
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        EventBus.Instance.UnsubscribeAll<SensorDataEvent>();
        EventBus.Instance.UnsubscribeAll<NetworkStatusEvent>();
        EventBus.Instance.UnsubscribeAll<AlarmEvent>();

        _subSensor = null;
        _subNetwork = null;
        _subAlarm = null;

        BtnSubSensor.IsEnabled = true; BtnUnsubSensor.IsEnabled = false;
        BtnSubNetwork.IsEnabled = true; BtnUnsubNetwork.IsEnabled = false;
        BtnSubAlarm.IsEnabled = true; BtnUnsubAlarm.IsEnabled = false;

        AppendLog("🗑️ 전체 구독 해제 완료", "#94A3B8");
        RefreshSubCount();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        LstEvents.Items.Clear();
        _totalReceived = 0;
        TxtTotalReceived.Text = "0";
    }

    #endregion

    #region §5 ─ 수신 핸들러

    private void OnSensorDataReceived(SensorDataEvent e)
    {
        // EventBus 핸들러는 백그라운드 스레드에서 호출될 수 있음
        // → Dispatcher.InvokeAsync 로 UI 접근
        Dispatcher.InvokeAsync(() =>
        {
            _totalReceived++;
            TxtTotalReceived.Text = _totalReceived.ToString();

            // 상태 카드 갱신
            TxtLastTemp.Text = $"{e.Temperature:F1} °C";
            TxtLastHum.Text = $"{e.Humidity:F1} %";
            TxtLastBattery.Text = $"{e.Battery} %";

            // 온도에 따른 색상 변경
            TxtLastTemp.Foreground = e.Temperature switch
            {
                > 80 => new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)),
                > 60 => new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)),
                _ => new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))
            };

            AppendLog(
                $"◀ SensorData  device={e.DeviceId}" +
                $"  temp={e.Temperature:F1}°C  hum={e.Humidity:F1}%  bat={e.Battery}%",
                "#4ADE80");

        //    LogManager.Instance.Debug("EventBus",
        //        $"SensorDataEvent 수신: device={e.DeviceId}  temp={e.Temperature:F1}");
        });
    }

    private void OnNetworkStatusReceived(NetworkStatusEvent e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _totalReceived++;
            TxtTotalReceived.Text = _totalReceived.ToString();

            // 상태 카드 갱신
            TxtNetStatus.Text = e.IsConnected ? "CONNECTED" : "DISCONNECTED";
            TxtNetStatus.Foreground = e.IsConnected
                ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))
                : new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
            TxtNetHost.Text = e.Host;
            TxtNetLatency.Text = e.IsConnected ? $"{e.Latency} ms" : "—";

            var color = e.IsConnected ? "#4ADE80" : "#F87171";
            var icon = e.IsConnected ? "🟢" : "🔴";
            AppendLog(
                $"◀ NetworkStatus  {icon} {(e.IsConnected ? "CONNECTED" : "DISCONNECTED")}" +
                $"  host={e.Host}  latency={e.Latency}ms",
                color);
        });
    }

    private void OnAlarmReceived(AlarmEvent e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _totalReceived++;
            TxtTotalReceived.Text = _totalReceived.ToString();

            // 상태 카드 갱신
            TxtAlarmSeverity.Text = e.IsCritical ? "CRITICAL" : "WARNING";
            TxtAlarmSeverity.Foreground = e.IsCritical
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
                : new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
            TxtAlarmSource.Text = e.Source;
            TxtAlarmMsg.Text = e.Message;

            var color = e.IsCritical ? "#FF6B6B" : "#FBBF24";
            var icon = e.IsCritical ? "🚨" : "⚠️";
            AppendLog(
                $"◀ Alarm  {icon} [{(e.IsCritical ? "CRITICAL" : "WARNING")}]" +
                $"  source={e.Source}  msg={e.Message}",
                color);

            if (e.IsCritical)
            {
                //        LogManager.Instance.Fatal("EventBus",
                //            $"긴급 알람: {e.Source} — {e.Message}");
            }
        });
    }

    #endregion

    #region §6 ─ 유틸

    private SensorDataEvent MakeSensorEvent(int deviceId = 1)
        => new(
            DeviceId: deviceId,
            Temperature: (float)(_rng.NextDouble() * 90 + 10),   // 10~100°C
            Humidity: (float)(_rng.NextDouble() * 60 + 30),   // 30~90%
            Battery: _rng.Next(10, 100));

    private void RefreshSubCount()
    {
        int total = EventBus.Instance.GetSubscriberCount<SensorDataEvent>()
                  + EventBus.Instance.GetSubscriberCount<NetworkStatusEvent>()
                  + EventBus.Instance.GetSubscriberCount<AlarmEvent>();
        TxtSubCount.Text = total.ToString();
    }

    private void AppendLog(string text, string hexColor = "#E2E8F0")
    {
        var color = (Color)ColorConverter.ConvertFromString(hexColor);
        var item = new TextBlock
        {
            Text = $"[{DateTime.Now:HH:mm:ss.fff}]  {text}",
            Foreground = new SolidColorBrush(color),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Padding = new Thickness(4, 2, 4, 2)
        };

        LstEvents.Items.Insert(0, item);

        // 최대 200줄 유지
        while (LstEvents.Items.Count > 200)
            LstEvents.Items.RemoveAt(LstEvents.Items.Count - 1);
    }

    #endregion
}