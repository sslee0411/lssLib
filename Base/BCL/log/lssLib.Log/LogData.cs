using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lssLib.Log
{
    /// <summary>
    /// 로그 데이터 단위 모델
    /// </summary>
    public class LogData
    {
        #region Fields
        private string strYearMonth;
        private string strDay;
        private string strTime;
        private string strSource;      // 로그 발생 출처 (파일 분류 기준)
        private string strContents;
        #endregion

        #region Properties
        /// <summary>로그 심각도 레벨</summary>
        public LogLevel Level { get; set; }

        /// <summary>레벨 문자열 표시 (DEBUG / INFO / WARN / ERROR / FATAL)</summary>
        public string LevelText => Level.ToString().ToUpper();

        /// <summary>날짜+시간 표시 (yyyy_MM_dd HH:mm:ss.fff)</summary>
        public string Date => $"{strYearMonth}_{strDay} {strTime}";

        /// <summary> 로그 발생 날짜 - 파일 분류 기준 (예: "2024_06" - 2024년 6월) </summary>
        public string YearMonth {
            get => strYearMonth;
        }

        /// <summary> 로그 발생 날짜 - 파일 분류 기준 (예: "15" - 15일) </summary>
        public string Day
        {
            get => strDay;
        }

        /// <summary> 로그 발생 시간 - 로그 내용 표시 기준 (예: "14:30:45.123" - 14시 30분 45초 123밀리초) </summary>
        public string Time{ 
            get => strTime; 
        }

        /// <summary>로그 발생 출처 - 파일 분류 기준 (예: Network, Database, UI)</summary>
        public string Source
        {
            get => strSource;
            set => strSource = value;
        }

        /// <summary>로그 내용 - 실제 로그 메시지 (예: "User logged in successfully", "Database connection failed") </summary>
        public string Contents
        {
            get => strContents;
            set => strContents = value;
        }
        #endregion

        #region Constructor

        public LogData(LogLevel level, string source, string contents)
        {
            DateTime dateTime = DateTime.Now;
            Level = level;
            strYearMonth = dateTime.ToString("yyyy_MM");
            strDay = dateTime.ToString("dd");
            strTime = dateTime.ToString("HH:mm:ss.fff");
            strSource = source ?? string.Empty;
            strContents = contents ?? string.Empty;
        }
        #endregion

        public override string ToString()
            => $"{Date}  [{LevelText,-5}]  {strSource,-16}  {strContents}";
    }

}
