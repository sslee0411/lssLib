// ══════════════════════════════════════════════════════════
//  lssLib.Messaging.Demo · CommandQueueView.xaml.cs
//  역할: CommandQueue 우선순위 큐 데모 코드비하인드
// ══════════════════════════════════════════════════════════

using lssLib.messaging.demo;
using lssLib.Messaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace lssLib.Messaging.Demo.Views;

public partial class CommandQueueView : UserControl
{
    #region §1 ─ 필드

    private readonly Random _rng = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private int _seqId;

    #endregion

    #region §2 ─ 생성자

    public CommandQueueView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // CommandCompleted 이벤트 구독 — 실행 결과 수신
        CommandQueue.Instance.CommandCompleted += OnCommandCompleted;

        // 통계 갱신 타이머 시작
        _timer.Tick += OnTimerTick;
        _timer.Start();

        AppendLog("── CommandQueue 데모 준비 완료. 커맨드를 등록해보세요. ──", "#94A3B8");
        AppendLog($"── 큐 상태: 실행 중={CommandQueue.Instance.IsRunning}", "#94A3B8");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        CommandQueue.Instance.CommandCompleted -= OnCommandCompleted;
    }

    #endregion

    #region §3 ─ 버튼 핸들러

    private void BtnEnqueueCritical_Click(object sender, RoutedEventArgs e)
    {
        var cmd = new EmergencyStopCommand($"데모 테스트 #{++_seqId}");
        CommandQueue.Instance.Enqueue(cmd);
        AppendLog($"▶ Enqueue  [CRITICAL] EmergencyStopCommand  id={cmd.CommandId}", "#FF6B6B");
    }

    private void BtnEnqueueHigh_Click(object sender, RoutedEventArgs e)
    {
        var msgs = new[] { "과열 감지 알림", "연결 오류 알림", "배터리 부족 알림" };
        var cmd = new SendNotificationCommand(msgs[_rng.Next(msgs.Length)],
                                               delayMs: _rng.Next(100, 300));
        CommandQueue.Instance.Enqueue(cmd);
        AppendLog($"▶ Enqueue  [HIGH] SendNotificationCommand  id={cmd.CommandId}", "#FBBF24");
    }

    private void BtnEnqueueNormal_Click(object sender, RoutedEventArgs e)
    {
        var value = (float)(_rng.NextDouble() * 100);
        var cmd = new ProcessDataCommand(++_seqId, value,
                                           delayMs: _rng.Next(200, 600));
        CommandQueue.Instance.Enqueue(cmd);
        AppendLog($"▶ Enqueue  [NORMAL] ProcessDataCommand  id={cmd.CommandId}  val={value:F2}", "#7C6AF7");
    }

    private void BtnEnqueueLow_Click(object sender, RoutedEventArgs e)
    {
        var cmd = new SaveFileCommand($"data_{DateTime.Now:HHmmss}.bin",
                                      delayMs: _rng.Next(400, 800));
        CommandQueue.Instance.Enqueue(cmd);
        AppendLog($"▶ Enqueue  [LOW] SaveFileCommand  id={cmd.CommandId}", "#94A3B8");
    }

    private void BtnEnqueueAll_Click(object sender, RoutedEventArgs e)
    {
        // 역순(Low → Critical)으로 등록해도 처리 순서는 Critical이 가장 먼저
        AppendLog("── 4종 동시 등록 (등록 순서: Low → Normal → High → Critical) ──", "#56CFE1");

        var cmdLow = new SaveFileCommand($"bulk_{DateTime.Now:HHmmss}.bin", 500);
        var cmdNorm = new ProcessDataCommand(++_seqId, 42.0f, 300);
        var cmdHigh = new SendNotificationCommand("일괄 등록 테스트", 150);
        var cmdCrit = new EmergencyStopCommand("일괄 등록 Critical");

        CommandQueue.Instance.Enqueue(cmdLow);
        CommandQueue.Instance.Enqueue(cmdNorm);
        CommandQueue.Instance.Enqueue(cmdHigh);
        CommandQueue.Instance.Enqueue(cmdCrit);

        AppendLog($"   Low:      {cmdLow.CommandId}", "#94A3B8");
        AppendLog($"   Normal:   {cmdNorm.CommandId}", "#7C6AF7");
        AppendLog($"   High:     {cmdHigh.CommandId}", "#FBBF24");
        AppendLog($"   Critical: {cmdCrit.CommandId} ← 가장 먼저 처리됨", "#FF6B6B");
    }

    private void BtnEnqueueLambda_Click(object sender, RoutedEventArgs e)
    {
        // 람다 커맨드 — 별도 클래스 없이 인라인으로 생성
        var seq = ++_seqId;
        var cmd = LambdaCommand.Create(async ct =>
        {
            await Task.Delay(_rng.Next(100, 400), ct);
        //    LogManager.Instance.Info("LambdaCmd", $"람다 커맨드 실행 완료 #{seq}");
        }, CommandPriority.Normal);

        CommandQueue.Instance.Enqueue(cmd);
        AppendLog($"▶ Enqueue  [NORMAL] LambdaCommand  id={cmd.CommandId}  seq=#{seq}", "#56CFE1");
    }

    private void BtnClearQueue_Click(object sender, RoutedEventArgs e)
    {
        int before = CommandQueue.Instance.PendingCount;
        CommandQueue.Instance.Clear();
        AppendLog($"🗑️ 대기 커맨드 {before}개 삭제 완료", "#F87171");
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        => LstResults.Items.Clear();

    #endregion

    #region §4 ─ 완료 이벤트 수신

    private void OnCommandCompleted(CommandResult result)
    {
        // 백그라운드 스레드 → UI 스레드 전환
        Dispatcher.InvokeAsync(() =>
        {
            TxtLastElapsed.Text = result.ElapsedMs.ToString();

            string icon = result.IsSuccess ? "✅" : "❌";
            string color = result.IsSuccess ? "#4ADE80" : "#F87171";

            // 우선순위에 따른 커맨드 타입별 색상
            string typeColor = result.CommandType switch
            {
                nameof(EmergencyStopCommand) => "#FF6B6B",
                nameof(SendNotificationCommand) => "#FBBF24",
                nameof(ProcessDataCommand) => "#7C6AF7",
                nameof(SaveFileCommand) => "#94A3B8",
                _ => "#56CFE1",
            };

            AppendLog(
                $"{icon} Done  [{result.CommandType}]  " +
                $"id={result.CommandId}  {result.ElapsedMs}ms" +
                (result.IsError ? $"  → {result.Error?.Message}" : ""),
                color);
        });
    }

    #endregion

    #region §5 ─ 타이머 (통계 갱신)

    private void OnTimerTick(object? sender, EventArgs e)
    {
        TxtPending.Text = CommandQueue.Instance.PendingCount.ToString();
        TxtProcessed.Text = CommandQueue.Instance.ProcessedCount.ToString();
        TxtFailed.Text = CommandQueue.Instance.FailedCount.ToString();
    }

    #endregion

    #region §6 ─ 유틸

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

        LstResults.Items.Insert(0, item);
        while (LstResults.Items.Count > 300)
            LstResults.Items.RemoveAt(LstResults.Items.Count - 1);
    }

    #endregion
}