// ══════════════════════════════════════════════════════════
//  lssLib.Messaging.Demo · SchedulerView.xaml.cs
//  역할: AsyncScheduler 비동기 스케줄러 데모 코드비하인드
// ══════════════════════════════════════════════════════════

using lssLib.messaging.demo;
using lssLib.Messaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace lssLib.Messaging.Demo.Views;

public partial class SchedulerView : UserControl
{
    #region §1 ─ 필드

    private readonly Random _rng = new();
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    // 등록된 작업 핸들 관리
    private ScheduledTask? _sensorTask;
    private ScheduledTask? _heartbeatTask;

    #endregion

    #region §2 ─ 생성자

    public SchedulerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _uiTimer.Tick += OnUiTimerTick;
        _uiTimer.Start();

        AppendLog("── AsyncScheduler 데모 준비 완료. 작업을 등록해보세요. ──", "#94A3B8");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _uiTimer.Stop();
    }

    #endregion

    #region §3 ─ 작업 등록 버튼

    private void BtnStartSensor_Click(object sender, RoutedEventArgs e)
    {
        if (_sensorTask is { IsCancelled: false })
        {
            AppendLog("⚠️ 센서 폴링이 이미 실행 중입니다.", "#FBBF24");
            return;
        }

        if (!int.TryParse(TxtSensorInterval.Text, out int sec) || sec < 1) sec = 3;

        _sensorTask = AsyncScheduler.Instance.ScheduleRecurring(
            interval: TimeSpan.FromSeconds(sec),
            action: async ct =>
            {
                // 센서 데이터 시뮬레이션
                float temp = (float)(_rng.NextDouble() * 80 + 15);
                float hum = (float)(_rng.NextDouble() * 50 + 30);
                int bat = _rng.Next(20, 100);

                // EventBus로 발행 — 구독 중인 핸들러가 있으면 수신
                await EventBus.Instance.PublishAsync(
                    new SensorDataEvent(1, temp, hum, bat), ct);

                // UI 로그 업데이트
                Dispatcher.InvokeAsync(() =>
                    AppendLog($"📊 센서  temp={temp:F1}°C  hum={hum:F1}%  bat={bat}%", "#7C6AF7"));
            },
            name: "SensorPoll");

        BtnStartSensor.IsEnabled = false;
        AppendLog($"▶ SensorPoll 등록  id={_sensorTask.TaskId}  간격={sec}s", "#4ADE80");
        RefreshTaskPanel();
    }

    private void BtnStartHeartbeat_Click(object sender, RoutedEventArgs e)
    {
        if (_heartbeatTask is { IsCancelled: false })
        {
            AppendLog("⚠️ 하트비트가 이미 실행 중입니다.", "#FBBF24");
            return;
        }

        if (!int.TryParse(TxtHeartbeatInterval.Text, out int sec) || sec < 1) sec = 5;

        _heartbeatTask = AsyncScheduler.Instance.ScheduleRecurring(
            interval: TimeSpan.FromSeconds(sec),
            action: ct =>
            {
                // 연결 상태 시뮬레이션 (90% 확률로 연결됨)
                bool connected = _rng.NextDouble() > 0.10;
                int latency = _rng.Next(5, 80);

                EventBus.Instance.Publish(
                    new NetworkStatusEvent(connected, "192.168.0.10", latency));

                Dispatcher.InvokeAsync(() =>
                {
                    string icon = connected ? "💓" : "💔";
                    string color = connected ? "#4ADE80" : "#F87171";
                    AppendLog($"{icon} 하트비트  {(connected ? "OK" : "FAIL")}  latency={latency}ms", color);
                });

                return Task.CompletedTask;
            },
            name: "Heartbeat");

        BtnStartHeartbeat.IsEnabled = false;
        AppendLog($"▶ Heartbeat 등록  id={_heartbeatTask.TaskId}  간격={sec}s", "#4ADE80");
        RefreshTaskPanel();
    }

    private void BtnStartOnce_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtOnceDelay.Text, out int sec) || sec < 1) sec = 4;

        var task = AsyncScheduler.Instance.ScheduleOnce(
            delay: TimeSpan.FromSeconds(sec),
            action: ct =>
            {
                Dispatcher.InvokeAsync(() =>
                    AppendLog($"⏱️ 1회 작업 실행! (등록 후 {sec}초 경과)", "#FBBF24"));
                return Task.CompletedTask;
            },
            name: $"Once#{DateTime.Now:HHmmss}");

        AppendLog($"▶ Once 등록  id={task.TaskId}  {sec}초 후 실행", "#FBBF24");
        RefreshTaskPanel();
    }

    private void BtnStartLimited_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtLimitedInterval.Text, out int sec) || sec < 1) sec = 2;
        if (!int.TryParse(TxtLimitedMax.Text, out int maxRuns) || maxRuns < 1) maxRuns = 5;

        var task = AsyncScheduler.Instance.Schedule(
            action: ct =>
            {
                Dispatcher.InvokeAsync(() =>
                    AppendLog($"🔢 제한 작업 실행 (run #{AsyncScheduler.Instance.GetTask(ct.GetHashCode().ToString())?.RunCount})", "#94A3B8"));
                return Task.CompletedTask;
            },
            options: new ScheduleOptions
            {
                Name = $"Limited#{DateTime.Now:HHmmss}",
                Interval = TimeSpan.FromSeconds(sec),
                MaxRuns = maxRuns,
                ContinueOnError = true,
                Category = "Demo"
            });

        AppendLog($"▶ Limited 등록  id={task.TaskId}  {sec}초마다  최대 {maxRuns}회", "#94A3B8");
        RefreshTaskPanel();
    }

    #endregion

    #region §4 ─ 전체 제어 버튼

    private void BtnPauseAll_Click(object sender, RoutedEventArgs e)
    {
        AsyncScheduler.Instance.PauseAll();
        AppendLog("⏸ 전체 작업 일시 정지", "#FBBF24");
        RefreshTaskPanel();
    }

    private void BtnResumeAll_Click(object sender, RoutedEventArgs e)
    {
        AsyncScheduler.Instance.ResumeAll();
        AppendLog("▶ 전체 작업 재개", "#4ADE80");
        RefreshTaskPanel();
    }

    private async void BtnStopAll_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("⏹ 전체 종료 요청...", "#F87171");
        await AsyncScheduler.Instance.StopAsync(TimeSpan.FromSeconds(3));

        _sensorTask = null;
        _heartbeatTask = null;
        BtnStartSensor.IsEnabled = true;
        BtnStartHeartbeat.IsEnabled = true;

        AppendLog("⏹ 전체 작업 종료 완료", "#F87171");
        RefreshTaskPanel();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        => LstLog.Items.Clear();

    #endregion

    #region §5 ─ 작업 카드 동적 생성

    // 작업 카드에서 개별 Pause/Resume/Cancel 제어
    private void CreateTaskCard(ScheduledTask task)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x38)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5E)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Tag = task.TaskId
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 왼쪽: 작업 정보
        var infoPanel = new StackPanel();

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        nameRow.Children.Add(new TextBlock
        {
            Text = task.Name,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0xCF, 0xE1)),
            FontWeight = FontWeights.Bold,
            FontSize = 12
        });
        nameRow.Children.Add(new TextBlock
        {
            Text = $"  #{task.TaskId}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        });

        var statusText = new TextBlock
        {
            Tag = $"{task.TaskId}_status",
            Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            Margin = new Thickness(0, 3, 0, 0)
        };

        infoPanel.Children.Add(nameRow);
        infoPanel.Children.Add(statusText);
        Grid.SetColumn(infoPanel, 0);

        // 오른쪽: 제어 버튼
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var btnPause = MakeSmallButton("⏸", "#B45309");
        btnPause.Click += (_, _) =>
        {
            task.Pause();
            AppendLog($"⏸ [{task.Name}] 일시 정지", "#FBBF24");
        };

        var btnResume = MakeSmallButton("▶", "#22863A");
        btnResume.Click += (_, _) =>
        {
            task.Resume();
            AppendLog($"▶ [{task.Name}] 재개", "#4ADE80");
        };

        var btnCancel = MakeSmallButton("✖", "#991B1B");
        btnCancel.Click += (_, _) =>
        {
            task.Cancel();
            AppendLog($"✖ [{task.Name}] 취소됨", "#F87171");

            if (task.TaskId == _sensorTask?.TaskId)
            {
                _sensorTask = null;
                BtnStartSensor.IsEnabled = true;
            }
            if (task.TaskId == _heartbeatTask?.TaskId)
            {
                _heartbeatTask = null;
                BtnStartHeartbeat.IsEnabled = true;
            }
        };

        btnPanel.Children.Add(btnPause);
        btnPanel.Children.Add(btnResume);
        btnPanel.Children.Add(btnCancel);
        Grid.SetColumn(btnPanel, 1);

        grid.Children.Add(infoPanel);
        grid.Children.Add(btnPanel);
        card.Child = grid;

        PnlTasks.Children.Insert(0, card);
    }

    private static Button MakeSmallButton(string content, string hexBg)
    {
        var color = (Color)ColorConverter.ConvertFromString(hexBg);
        return new Button
        {
            Content = content,
            Background = new SolidColorBrush(color),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(4, 0, 0, 0),
            FontSize = 11,
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = CreateRoundButtonTemplate()
        };
    }

    private static ControlTemplate CreateRoundButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.PaddingProperty,
            new System.Windows.Data.Binding("Padding")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        return template;
    }

    private void RefreshTaskPanel()
    {
        var tasks = AsyncScheduler.Instance.GetTasks();
        var activeIds = tasks.Select(t => t.TaskId).ToHashSet();

        TxtTaskCount.Text = $"({tasks.Count}개)";

        // 종료된 카드 제거
        var toRemove = PnlTasks.Children
            .OfType<Border>()
            .Where(b => b.Tag is string id && !activeIds.Contains(id))
            .ToList();
        foreach (var b in toRemove) PnlTasks.Children.Remove(b);

        // 신규 작업 카드 추가
        var existingIds = PnlTasks.Children
            .OfType<Border>()
            .Select(b => b.Tag as string)
            .ToHashSet();

        foreach (var t in tasks)
            if (!existingIds.Contains(t.TaskId))
                CreateTaskCard(t);
    }

    #endregion

    #region §6 ─ UI 타이머 (상태 갱신)

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        var tasks = AsyncScheduler.Instance.GetTasks();
        TxtTaskCount.Text = $"({tasks.Count}개)";

        // 각 카드의 상태 텍스트 갱신
        foreach (var task in tasks)
        {
            foreach (var border in PnlTasks.Children.OfType<Border>())
            {
                if (border.Tag as string != task.TaskId) continue;

                // 카드 테두리 색상 — 상태 표시
                border.BorderBrush = task.IsPaused
                    ? new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24))
                    : task.IsCancelled
                        ? new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71))
                        : new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));

                // 상태 텍스트 갱신 (태그로 찾기)
                FindStatusText(border, $"{task.TaskId}_status", t =>
                {
                    string state = task.IsCancelled ? "CANCELLED"
                                 : task.IsPaused ? "PAUSED"
                                 : "RUNNING";
                    string next = task.NextRunAt.HasValue
                                 ? $"  next={task.NextRunAt:HH:mm:ss}"
                                 : "";
                    t.Text = $"runs={task.RunCount}  state={state}{next}";
                    t.Foreground = task.IsCancelled
                        ? new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71))
                        : task.IsPaused
                            ? new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24))
                            : new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
                });
            }
        }

        // 종료된 카드 제거
        RefreshTaskPanel();
    }

    private static void FindStatusText(DependencyObject parent, string tag, Action<TextBlock> action)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock tb && tb.Tag as string == tag)
            {
                action(tb);
                return;
            }
            FindStatusText(child, tag, action);
        }
    }

    #endregion

    #region §7 ─ 유틸

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

        LstLog.Items.Insert(0, item);
        while (LstLog.Items.Count > 300)
            LstLog.Items.Remove(LstLog.Items.Count - 1);
    }

    #endregion
}