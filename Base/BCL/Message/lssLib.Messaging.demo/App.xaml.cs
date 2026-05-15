

// ══════════════════════════════════════════════════════════
//  lssLib.messaging.Demo · App.xaml.cs
//  역할: 전역 초기화 및 종료 처리
// ══════════════════════════════════════════════════════════


using lssLib.Messaging;
using System.Windows;

namespace lssLib.Messaging.demo;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // §1 LogManager 가장 먼저 시작
    //    LogManager.Instance.Start(new LogConfig
    //    {
    //        LogRootPath = System.IO.Path.Combine(
    //                                  AppDomain.CurrentDomain.BaseDirectory, "Log"),
    //        ValidDays = 7,
    //        FileFormat = LogFileFormat.Txt,
    //        MinimumLevel = LogLevel.Debug,
    //        MinimumConsoleLevel = LogLevel.Debug,
    //        MaxDisplayCount = 2000
    //    });

        // §2 CommandQueue 시작
        CommandQueue.Instance.Start();

    //    LogManager.Instance.Info("App", "▶ lssLib.Messaging Demo 시작");
    }

    protected override async void OnExit(ExitEventArgs e)
    {
    //    LogManager.Instance.Info("App", "■ 종료 처리 중...");

        // 종료 순서: Scheduler → CommandQueue → LogManager
        await AsyncScheduler.Instance.StopAsync(TimeSpan.FromSeconds(3));
        await CommandQueue.Instance.StopAsync();
    //    await LogManager.Instance.StopAsync();

        base.OnExit(e);
    }
}