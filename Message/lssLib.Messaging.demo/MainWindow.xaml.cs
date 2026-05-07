// ══════════════════════════════════════════════════════════
//  lssLib.Messaging.Demo · MainWindow.xaml.cs
//  역할: 메인 윈도우 — 전역 상태 갱신 타이머
// ══════════════════════════════════════════════════════════

using lssLib.Messaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace lssLib.messaging.Demo;

public partial class MainWindow : Window
{
    #region §1 ─ 필드

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    #endregion

    #region §2 ─ 생성자

    public MainWindow()
    {
        InitializeComponent();

        _timer.Tick += OnTimerTick;
        _timer.Start();

        // §2 CommandQueue 시작
        CommandQueue.Instance.Start();
        //    LogManager.Instance.Info("MainWindow", "UI 초기화 완료");
    }

    #endregion

    #region §3 ─ 타이머 (헤더 상태 갱신)

    private void OnTimerTick(object? sender, EventArgs e)
    {
        TxtClock.Text = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
        TxtQueuePending.Text = CommandQueue.Instance.PendingCount.ToString();
        TxtSchedulerCount.Text = AsyncScheduler.Instance.TaskCount.ToString();
        TxtSubCount.Text = EventBus.Instance.TotalSubscriptions.ToString();
    }

    #endregion

    #region §4 ─ 종료

    protected override async void OnClosed(EventArgs e)
    {
        await AsyncScheduler.Instance.StopAsync(TimeSpan.FromSeconds(3));
        await CommandQueue.Instance.StopAsync();

        _timer.Stop();
    //    LogManager.Instance.Info("MainWindow", "윈도우 닫힘");
        base.OnClosed(e);
    }

    #endregion

    // 하단 상태 바 텍스트를 외부에서 설정할 수 있도록 노출
    public void SetStatus(string text)
        => Dispatcher.InvokeAsync(() => TxtStatusBar.Text = text);
}