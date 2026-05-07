using System;
using System.Threading.Tasks;
using System.Windows;
using lssLib.Log;                  // LogManager 클래스 라이브러리 네임스페이스

namespace logMVVM
{
    /// <summary>
    /// ■ MainWindow 역할
    ///   - LogManager(싱글톤) 초기화 및 종료
    ///   - LogConfig 설정 (저장경로·레벨·롤링 크기 등)
    ///   - 테스트 버튼 (레벨별 로그 / 연속 로그 / AOP 래퍼)
    ///   - LogViewerControl 은 XAML에 배치만 하고 별도 코드 없음
    ///     → LogViewerControl 이 스스로 ViewModel 생성 및 LogAdded 구독
    ///
    /// ■ MVVM 관점
    ///   - MainWindow 는 앱 진입점(껍데기) 역할
    ///   - 비즈니스 로직(필터·상태·커맨드)은 LogViewerViewModel 이 담당
    ///   - MainWindow 코드비하인드에는 LogManager 초기화 / 테스트 코드만 존재
    /// </summary>
    public partial class MainWindow : Window
    {
        // ════════════════════════════════════════════════════
        //  테스트용 샘플 데이터
        // ════════════════════════════════════════════════════
        #region Sample Data
        private static readonly Random _rnd = new Random();

        /// <summary>로그 출처(Source) 샘플 - 모듈별 분류 파일명이 됨</summary>
        private static readonly string[] SampleSources =
            { "Network", "Database", "UI", "Auth", "Scheduler", "Cache", "FileIO" };

        /// <summary>로그 내용 샘플</summary>
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
        //  생성자 - LogManager 초기화
        // ════════════════════════════════════════════════════
        public MainWindow()
        {
            InitializeComponent();
            InitializeLogManager();
        }

        // ════════════════════════════════════════════════════
        //  LogManager 초기화
        // ════════════════════════════════════════════════════
        #region Initialize
        /// <summary>
        /// LogConfig 설정 후 LogManager 를 시작한다.
        ///
        /// ■ LogConfig 주요 설정 항목
        ///   LogRootPath         : 로그 파일 저장 루트 경로
        ///   ValidDays           : 로그 보관 일수 (1 ~ 1095일)
        ///   CheckHour           : 만료 로그 삭제 기준 시각 (0~23시)
        ///   MaxFileSizeBytes    : 파일 롤링 기준 크기 (초과 시 _2, _3 ... 생성)
        ///   FileFormat          : Txt / Csv / Both
        ///   EnableFileOutput    : 파일 저장 여부
        ///   EnableConsoleOutput : VS 출력창 출력 여부
        ///   MinimumConsoleLevel : 출력창 최소 레벨 (해당 레벨 미만 미출력)
        ///   MinimumLevel        : 큐에 진입하는 최소 레벨 (미만은 완전 무시)
        ///   ChannelCapacity     : 비동기 큐 최대 용량 (0 = 무제한)
        ///   MaxDisplayCount     : UI 화면 최대 표시 건수
        /// </summary>
        private void InitializeLogManager()
        {
            var config = new LogConfig
            {
                // 로그 파일 저장 경로: 실행파일 폴더\Log
                LogRootPath = System.IO.Path.Combine(
                                          AppDomain.CurrentDomain.BaseDirectory, "Log"),

                // 7일 지난 로그 폴더 자동 삭제 (매일 자정 검사)
                ValidDays = 7,
                CheckHour = 0,

                // 파일 하나가 5MB 초과 시 새 파일로 롤링
                MaxFileSizeBytes = 5L * 1024 * 1024,

                // TXT 파일 저장 (Csv / Both 로 변경 가능)
                FileFormat = LogFileFormat.Txt,

                // 파일 저장 활성화
                EnableFileOutput = true,

                // VS 출력창: Warn 이상만 표시 (Debug·Info 는 출력창 미출력)
                EnableConsoleOutput = true,
                MinimumConsoleLevel = LogLevel.Warn,

                // 큐에 쌓이는 최소 레벨 (Debug 이상 전부 처리)
                MinimumLevel = LogLevel.Debug,

                // 비동기 큐 무제한 (로그 유실 없음)
                ChannelCapacity = 0,

                // UI 화면에 최대 1000건 유지 (초과 시 오래된 항목 자동 제거)
                MaxDisplayCount = 1000
            };

            // LogManager 시작
            // → 내부적으로 두 개의 Task 구동
            //   ① ProcessQueueAsync  : Channel 에서 LogData 를 꺼내 파일/콘솔/UI 출력
            //   ② ValidDayManagerAsync : 1시간마다 만료 로그 폴더 삭제 검사
            LogManager.Instance.Start(config);

            // 앱 시작 로그
            LogManager.Instance.Info("MainWindow", "애플리케이션 시작");

            // 상단 설정 정보 표시
            TxtConfigInfo.Text =
                $"저장경로: {config.LogRootPath}  |  " +
                $"형식: {config.FileFormat}  |  " +
                $"최소레벨: {config.MinimumLevel}  |  " +
                $"보관일수: {config.ValidDays}일  |  " +
                $"롤링크기: {config.MaxFileSizeBytes / 1024 / 1024}MB  |  " +
                $"화면최대: {config.MaxDisplayCount:N0}건";
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  Window 종료
        // ════════════════════════════════════════════════════
        #region Closing
        /// <summary>
        /// 앱 종료 시 LogManager 를 비동기로 정지한다.
        ///
        /// StopAsync() 동작 순서:
        ///   1. Channel.Writer.Complete() 로 큐 마감 신호 전송
        ///   2. ProcessQueueAsync 가 남은 항목을 모두 소비(파일 저장)
        ///   3. ValidDayManagerAsync 취소
        ///   4. 두 Task 모두 종료 확인 후 반환
        ///
        /// await 없이 종료하면 큐에 남은 로그가 파일에 기록되지 않으므로
        /// 반드시 await 로 완료를 기다려야 한다.
        /// </summary>
        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            LogManager.Instance.Info("MainWindow", "애플리케이션 종료 중...");
            await LogManager.Instance.StopAsync();
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  테스트 버튼 - 레벨별 단건 로그
        // ════════════════════════════════════════════════════
        #region Level Buttons
        /// <summary>가장 상세한 디버그 정보. 개발 중에만 사용 권장.</summary>
        private void BtnDebug_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Debug("MainWindow", $"Debug 테스트 - tick={Environment.TickCount}");

        /// <summary>일반적인 동작 정보. 운영 환경 기본 레벨.</summary>
        private void BtnInfo_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Info("MainWindow", $"Info 테스트 - {DateTime.Now:HH:mm:ss.fff}");

        /// <summary>주의가 필요하지만 즉각 조치 불필요한 상황.</summary>
        private void BtnWarn_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Warn("MainWindow", "Warn 테스트 - 응답 지연 감지 (800ms)");

        /// <summary>오류 발생. 기능 일부가 정상 동작하지 않는 상태.</summary>
        private void BtnError_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Error("MainWindow", "Error 테스트 - NullReferenceException 발생");

        /// <summary>치명적 오류. 서비스 중단 수준.</summary>
        private void BtnFatal_Click(object sender, RoutedEventArgs e)
            => LogManager.Instance.Fatal("MainWindow", "Fatal 테스트 - 서비스 비정상 종료 감지!");
        #endregion

        // ════════════════════════════════════════════════════
        //  테스트 버튼 - 연속 50건 (랜덤)
        // ════════════════════════════════════════════════════
        #region Burst Button
        /// <summary>
        /// 50건의 로그를 20ms 간격으로 연속 추가한다.
        ///
        /// AddLog 는 Channel.Writer.TryWrite 만 하고 즉시 반환 (UI 블로킹 없음).
        /// 실제 파일 저장·UI 업데이트는 백그라운드 ProcessQueueAsync Task 가 처리.
        /// </summary>
        private async void BtnBurst_Click(object sender, RoutedEventArgs e)
        {
            // Info 비중을 높여 현실적인 레벨 분포
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
        #endregion

        // ════════════════════════════════════════════════════
        //  테스트 버튼 - AOP 래퍼
        // ════════════════════════════════════════════════════
        #region AOP Button
        /// <summary>
        /// AOP(Aspect-Oriented Programming) 래퍼 4가지 패턴 테스트.
        ///
        /// ■ AOP 래퍼를 쓰면
        ///   - Start / End / Error 로그를 매 함수마다 직접 작성하지 않아도 됨
        ///   - [CallerMemberName] 으로 호출 함수명이 자동 주입 (오타 방지)
        ///   - 핵심 비즈니스 로직과 로그 코드가 깔끔하게 분리됨
        ///
        /// ■ 4가지 오버로드
        ///   ① Execute(Action)          - 반환값 없음 · 동기
        ///   ② ExecuteAsync(Func<Task>) - 반환값 없음 · 비동기
        ///   ③ Execute<T>(Func<T>)      - 반환값 있음 · 동기
        ///   ④ ExecuteAsync<T>(...)     - 반환값 있음 · 비동기
        /// </summary>
        private async void BtnAop_Click(object sender, RoutedEventArgs e)
        {
            // ① 반환값 없음 · 동기
            LogManager.Execute(
                tryAction: () => LogManager.Instance.Info("AOP", "동기 작업 실행 완료"),
                catchAction: () => LogManager.Instance.Error("AOP", "동기 작업 실패 - 복구 처리"),
                finallyAction: () => { /* 정리 코드 */ },
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

            // ③ 반환값 있음 · 동기 (성공 시 42, 예외 시 -1 반환)
            int result = LogManager.Execute(
                tryFunc: () => 42,
                catchFunc: () => -1,
                category: "DEMO"
            );
            LogManager.Instance.Info("AOP", $"반환값 결과: {result}");

            // ④ 예외 발생 → catchAction 에서 복구 처리 확인
            LogManager.Execute(
                tryAction: () => throw new InvalidOperationException("의도적 예외 발생"),
                catchAction: () => LogManager.Instance.Warn("AOP", "예외 처리 후 복구됨"),
                category: "DEMO"
            );
        }
        #endregion
    }
}