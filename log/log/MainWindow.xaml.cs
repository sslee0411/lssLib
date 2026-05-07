using System;
using System.Threading.Tasks;
using System.Windows;
using lssLib.Log;

namespace log
{
    /// <summary>
    /// ■ MainWindow 역할
    ///   - LogManager(싱글톤) 초기화 및 종료 담당
    ///   - LogConfig 를 구성하여 LogManager.Start() 호출
    ///   - 레벨별·연속·AOP 테스트 버튼 제공
    ///   - XAML 에 LogViewerControl 을 배치하고 별도 코드는 없음
    ///     (LogViewerControl 이 스스로 LogAdded 이벤트 구독·해제)
    ///
    /// ■ 앱 시작~종료 흐름
    ///   MainWindow() → InitializeComponent() → LogManager.Start()
    ///        ↓
    ///   [사용자 동작] → AddLog() → Channel 큐 → ProcessQueueAsync
    ///        ↓
    ///   Window_Closing → StopAsync() → 큐 소진 후 Task 종료
    /// </summary>
    public partial class MainWindow : Window
    {
        // ════════════════════════════════════════════════════
        //  테스트용 샘플 데이터
        // ════════════════════════════════════════════════════
        #region Sample Data
        private static readonly Random _rnd = new Random();

        /// <summary>
        /// 로그 출처(Source) 샘플.
        /// 실제 앱에서는 "Network", "Database" 처럼 모듈명을 사용하며,
        /// 같은 Source 명칭은 동일한 파일에 분류 저장된다.
        /// 예) Source="Network" → Log\2025_03\22\Network.txt
        /// </summary>
        private static readonly string[] SampleSources =
            { "Network", "Database", "UI", "Auth", "Scheduler", "Cache", "FileIO" };

        /// <summary>로그 내용 샘플 메시지</summary>
        private static readonly string[] SampleMessages =
        {
            "서비스 연결 시도 중...",
            "데이터 조회 완료 (rows=128)",
            "타임아웃 발생 (5000ms 초과)",
            "메모리 사용량: 412MB / 1024MB",
            "파일 저장 성공: report_2025.xlsx",
            "인증 토큰 갱신 완료",
            "캐시 히트율: 92.4%",
            "요청 처리 지연 감지 (latency=850ms)"
        };
        #endregion

        // ════════════════════════════════════════════════════
        //  생성자
        // ════════════════════════════════════════════════════
        public MainWindow()
        {
            InitializeComponent();

            // ── LogManager 초기화 ──────────────────────────
            // LogConfig 를 구성하여 LogManager.Start() 에 전달.
            // Start() 내부에서:
            //   ① Channel<LogData> 생성 (큐)
            //   ② ProcessQueueAsync Task 시작 (파일저장·콘솔·UI 이벤트 처리)
            //   ③ ValidDayManagerAsync Task 시작 (만료 로그 폴더 삭제)
            var config = new LogConfig
            {
                // 로그 파일 저장 루트 경로 (실행파일 폴더\Log)
                LogRootPath = System.IO.Path.Combine(
                                          AppDomain.CurrentDomain.BaseDirectory, "Log"),

                // 7일 보관 (매일 자정 검사하여 만료 폴더 삭제)
                ValidDays = 7,
                CheckHour = 0,

                // 파일 하나가 5MB 초과 시 _2, _3 ... 번호 롤링
                MaxFileSizeBytes = 5L * 1024 * 1024,

                // TXT 파일 저장 (Csv / Both 로 변경 가능)
                FileFormat = LogFileFormat.Txt,

                // 파일 저장 활성화
                EnableFileOutput = true,

                // VS 출력창: 모든 레벨 출력 (운영 시 Warn 이상으로 변경 권장)
                EnableConsoleOutput = true,
                MinimumConsoleLevel = LogLevel.Debug,

                // 채널 큐: Debug 이상 전부 처리 (운영 시 Info 이상 권장)
                MinimumLevel = LogLevel.Debug,

                // 비동기 큐 무제한 (로그 유실 없음)
                ChannelCapacity = 0,

                // UI 화면 최대 1000건 (초과 시 오래된 항목 자동 제거)
                MaxDisplayCount = 1000
            };

            LogManager.Instance.Start(config);
            LogManager.Instance.Info("MainWindow", "애플리케이션 시작");

            // 상단 설정 정보 바에 표시
            TxtConfigInfo.Text =
                $"저장경로: {config.LogRootPath}  |  " +
                $"최소레벨: {config.MinimumLevel}  |  " +
                $"보관일수: {config.ValidDays}일  |  " +
                $"롤링크기: {config.MaxFileSizeBytes / 1024 / 1024}MB";
        }

        // ════════════════════════════════════════════════════
        //  Window 종료
        // ════════════════════════════════════════════════════

        /// <summary>
        /// 앱 종료 시 LogManager 를 비동기로 정지한다.
        ///
        /// ■ StopAsync() 동작 순서
        ///   1. Channel.Writer.Complete()  → 새 항목 추가 불가, 큐 마감 신호
        ///   2. ProcessQueueAsync          → 남은 항목 모두 소비(파일 저장) 후 루프 종료
        ///   3. _cts.Cancel()              → ValidDayManagerAsync 즉시 종료
        ///   4. Task.WhenAll()             → 두 Task 모두 종료 확인 후 반환
        ///
        /// ※ await 없이 종료하면 큐에 남은 로그가 파일에 기록되지 않으므로
        ///   반드시 await 로 완료를 기다려야 한다.
        /// </summary>
        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            LogManager.Instance.Info("MainWindow", "애플리케이션 종료 중...");
            await LogManager.Instance.StopAsync();
        }

        // ════════════════════════════════════════════════════
        //  테스트 버튼 - 레벨별 단건 로그
        // ════════════════════════════════════════════════════
        #region Level Buttons
        /// <summary>상세 디버그 정보. 개발 중에만 사용 권장.</summary>
        private void BtnDebug_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Debug("MainWindow", $"Debug 테스트 - tick={Environment.TickCount}");

        /// <summary>일반 정보. 운영 환경 기본 레벨.</summary>
        private void BtnInfo_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Info("MainWindow", $"Info 테스트 - {DateTime.Now:HH:mm:ss.fff}");

        /// <summary>주의 필요, 즉각 조치 불필요.</summary>
        private void BtnWarn_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Warn("MainWindow", "Warn 테스트 - 응답 지연 감지 (800ms)");

        /// <summary>오류 발생. 기능 일부 정상 동작 안 함.</summary>
        private void BtnError_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Error("MainWindow", "Error 테스트 - NullReferenceException 발생");

        /// <summary>치명적 오류. 서비스 중단 수준.</summary>
        private void BtnFatal_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Fatal("MainWindow", "Fatal 테스트 - 서비스 비정상 종료 감지!");
        #endregion

        // ════════════════════════════════════════════════════
        //  테스트 버튼 - 연속 50건 (랜덤)
        // ════════════════════════════════════════════════════

        /// <summary>
        /// 50건의 로그를 20ms 간격으로 연속 추가한다.
        ///
        /// AddLog() 는 Channel.Writer.TryWrite() 만 호출하고 즉시 반환 (UI 블로킹 없음).
        /// 실제 파일 저장·UI 업데이트는 백그라운드 ProcessQueueAsync Task 가 처리한다.
        ///
        /// await Task.Delay(20) 로 호출 간격을 두어 실제 운영 로그 발생 패턴을 시뮬레이션.
        /// </summary>
        private async void BtnBurst_Click(object sender, RoutedEventArgs e)
        {
            // Info 비중을 높여 현실적인 레벨 분포 구성
            LogLevel[] levels =
            {
                LogLevel.Debug,
                LogLevel.Info, LogLevel.Info, LogLevel.Info,
                LogLevel.Warn,
                LogLevel.Error,
                LogLevel.Fatal
            };

            for (int i = 0; i < 50; i++)
            {
                var level = levels[_rnd.Next(levels.Length)];
                var source = SampleSources[_rnd.Next(SampleSources.Length)];
                var msg = SampleMessages[_rnd.Next(SampleMessages.Length)];

                LogManager.Instance.AddLog(level, source, $"[{i + 1:D2}] {msg}");
                await Task.Delay(20);
            }
        }

        // ════════════════════════════════════════════════════
        //  테스트 버튼 - AOP 래퍼
        // ════════════════════════════════════════════════════

        /// <summary>
        /// AOP(Aspect-Oriented Programming) 래퍼 4가지 패턴 테스트.
        ///
        /// ■ AOP 래퍼를 사용하면
        ///   - Start / End / Error 로그를 매 함수마다 직접 작성하지 않아도 됨
        ///   - [CallerMemberName] 으로 호출 함수명이 자동 주입 (오타 방지)
        ///   - 핵심 비즈니스 로직과 로그 코드가 깔끔하게 분리됨
        ///
        /// ■ 내부 구조
        ///   public 래퍼 4개 → private 코어 2개 (ExecuteCore / ExecuteCoreAsync)
        ///   try/catch/finally + 로그 패턴은 코어에만 존재 → 변경 시 한 곳만 수정
        /// </summary>
        private async void BtnAop_Click(object sender, RoutedEventArgs e)
        {
            // ① 반환값 없음 · 동기
            //    source(함수명)는 [CallerMemberName]이 "BtnAop_Click" 자동 주입
            LogManager.Execute(
                tryAction: () => LogManager.Instance.Info("AOP", "동기 작업 실행"),
                catchAction: () => LogManager.Instance.Error("AOP", "동기 작업 실패"),
                category: "DEMO"
            );

            // ② 반환값 없음 · 비동기
            await LogManager.ExecuteAsync(
                tryAction: async () =>
                {
                    await Task.Delay(100);
                    LogManager.Instance.Info("AOP", "비동기 작업 완료 (100ms)");
                },
                catchAction: () => LogManager.Instance.Error("AOP", "비동기 작업 실패"),
                category: "DEMO"
            );

            // ③ 반환값 있음 · 동기 (성공: 42 반환, 예외: -1 반환)
            int result = LogManager.Execute(
                tryFunc: () => 42,
                catchFunc: () => -1,
                category: "DEMO"
            );
            LogManager.Instance.Info("AOP", $"반환값 결과: {result}");

            // ④ 예외 발생 시나리오 → catchAction 에서 복구 처리 확인
            LogManager.Execute(
                tryAction: () => throw new InvalidOperationException("의도적 예외 발생"),
                catchAction: () => LogManager.Instance.Warn("AOP", "예외 처리 후 복구됨"),
                category: "DEMO"
            );
        }
    }
}