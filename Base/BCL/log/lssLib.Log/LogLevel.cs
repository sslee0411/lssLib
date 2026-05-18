
namespace lssLib.Log
{
    /// <summary>
    /// 로그 심각도 레벨
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 상세 디버그 정보 - 개발 및 디버깅 목적으로 사용되는 상세한 정보
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 일반 정보 =- 시스템이나 애플리케이션이 정상적으로 작동하고 있는 상황
        /// </summary>
        Info = 1,

        /// <summary>
        /// 경고 - 시스템이나 애플리케이션이 예상치 못한 상황에 직면했지만, 여전히 정상적으로 작동할 수 있는 상황
        /// </summary>
        Warn = 2,

        /// <summary>
        /// 오류 - 시스템이나 애플리케이션이 정상적으로 작동하지 않는 상황
        /// </summary>
        Error = 3,

        /// <summary>
        /// 치명적 오류 - 시스템이나 애플리케이션이 심각한 문제에 직면하여 정상적으로 작동할 수 없는 상황
        /// </summary>
        Fatal = 4 
    }


}
