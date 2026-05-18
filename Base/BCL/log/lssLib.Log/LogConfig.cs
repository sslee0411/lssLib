using System;
using System.IO;

namespace lssLib.Log
{
    /// <summary>
    /// 로그 매니저 설정 클래스
    ///
    /// ■ 사용 예시
    ///   var config = new LogConfig
    ///   {
    ///       LogRootPath         = @"C:\MyApp\Log",
    ///       ValidDays           = 14,
    ///       FileFormat          = LogFileFormat.Both,
    ///       MinimumLevel        = LogLevel.Info,
    ///       MinimumConsoleLevel = LogLevel.Warn,
    ///       MaxDisplayCount     = 500
    ///   };
    ///   LogManager.Instance.Start(config);
    /// </summary>
    public class LogConfig
    {
        #region Constants
        /// <summary>최소 보관 1일</summary>
        public const int VALID_DAYS_MIN = 1;

        /// <summary>최대 보관 3년 (1095일)</summary>
        public const int VALID_DAYS_MAX = 365 * 3;
        #endregion

        #region Fields
        private int _validDays = 7;
        #endregion

        #region Properties

        /// <summary>
        /// 로그 파일 저장 루트 경로 (기본: 실행파일 경로\Log)
        /// 하위 폴더 구조: LogRootPath \ yyyy_MM \ dd \ All.txt
        ///                                                \ {Source}.txt
        /// </summary>
        public string LogRootPath { get; set; }
            = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");

        /// <summary>
        /// 로그 보관 일수 (기본: 7일 / 최소: 1일 / 최대: 3년(1095일))
        /// 유효 범위를 벗어난 값 입력 시 ArgumentOutOfRangeException 발생.
        /// ValidDayManagerAsync 가 매 1시간마다 검사하여
        /// 현재 날짜 기준으로 ValidDays 이상 지난 폴더를 자동 삭제한다.
        /// </summary>
        public int ValidDays
        {
            get => _validDays;
            set
            {
                if (value < VALID_DAYS_MIN || value > VALID_DAYS_MAX)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(ValidDays),
                        value,
                        $"보관 일수는 {VALID_DAYS_MIN}일 이상 {VALID_DAYS_MAX}일({VALID_DAYS_MAX / 365}년) 이하여야 합니다. (입력값: {value})");
                }
                _validDays = value;
            }
        }

        /// <summary>
        /// 만료 로그 삭제를 실행할 기준 시각 (0~23, 기본: 0 = 자정)
        /// 예) CheckHour = 3  →  매일 새벽 3시에 ValidDays 가 지난 폴더 자동 삭제
        /// ※ ValidDayManagerAsync 가 1시간마다 루프를 돌면서
        ///    DateTime.Now.Hour == CheckHour 인 순간 하루 1번만 실행
        /// </summary>
        public int CheckHour { get; set; } = 0;

        /// <summary>
        /// 파일 저장 여부 (기본: true)
        /// false 로 설정 시 WriteToFile() 이 호출되지 않아 파일에 기록되지 않음.
        /// </summary>
        public bool EnableFileOutput { get; set; } = true;

        /// <summary>
        /// 파일 롤링 최대 크기 (기본: 10MB)
        /// 하나의 로그 파일이 이 크기를 초과하면 자동으로 다음 번호 파일로 넘어간다.
        /// 예) All.txt (10MB 초과) → All_2.txt → All_3.txt → ...
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 10L * 1024 * 1024;

        /// <summary>
        /// Debug 출력창(Visual Studio Output) 출력 여부 (기본: true)
        /// false 로 설정 시 WriteToConsole() 이 즉시 반환되어 출력창에 아무것도 표시되지 않음.
        /// </summary>
        public bool EnableConsoleOutput { get; set; } = true;

        /// <summary>
        /// 콘솔(Debug 출력창) 출력 최소 레벨 (기본: Debug = 모든 레벨 출력)
        /// 예) MinimumConsoleLevel = LogLevel.Warn
        ///   → Warn / Error / Fatal 만 출력창에 표시 (Debug·Info 는 파일에만 저장)
        /// ※ MinimumLevel 보다 낮게 설정해도 MinimumLevel 미만은
        ///    채널 큐에 들어오지 않으므로 실질적으로 무의미함.
        /// </summary>
        public LogLevel MinimumConsoleLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// UI 화면에 표시할 최대 로그 건수 (기본: 1000건)
        /// 최신 데이터 기준으로 해당 건수만큼만 화면에 유지되며,
        /// 초과 시 TrimDisplayList() 가 가장 오래된 항목부터 자동 제거한다.
        /// SetMaxDisplayCount() 로 런타임 중 동적 변경도 가능.
        /// </summary>
        public int MaxDisplayCount { get; set; } = 1000;

        /// <summary>
        /// 파일 출력 형식 (기본: Txt)
        ///  • Txt  : .txt 파일로 저장 (사람이 읽기 좋은 고정폭 텍스트)
        ///  • Csv  : .csv 파일로 저장 (Excel·DB Import 등 분석 목적)
        ///  • Both : .txt + .csv 동시 저장
        /// </summary>
        public LogFileFormat FileFormat { get; set; } = LogFileFormat.Txt;

        /// <summary>
        /// 채널 큐에 진입하는 최소 레벨 (기본: Debug = 모든 레벨 처리)
        /// 이 레벨 미만의 로그는 AddLog() 에서 즉시 버려지며 파일·화면 어디에도 남지 않음.
        /// 운영 환경에서는 LogLevel.Info 또는 LogLevel.Warn 으로 설정하여
        /// Debug 로그를 완전히 차단하면 성능 향상 효과가 있음.
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// 비동기 로그 큐 최대 용량 (기본: 0 = 무제한)
        ///  • 0        : 메모리가 허용하는 만큼 무제한. 로그 유실 없음.
        ///               단, 폭발적인 로그 발생 시 메모리 부담 가능.
        ///  • 양수(예: 1000) : 큐가 꽉 찼을 때 가장 오래된 항목부터 제거하고 새 항목 수용.
        ///                    메모리는 안전하지만 과부하 시 일부 로그 유실 가능.
        /// 일반적인 업무 앱은 0(무제한) 권장.
        /// </summary>
        public int ChannelCapacity { get; set; } = 0;

        #endregion
    }
}