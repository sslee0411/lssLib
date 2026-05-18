using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lssLib.Log
{
    /// <summary>
    /// 로그 파일 출력 형식
    /// </summary>
    public enum LogFileFormat
    {
        /// <summary>텍스트 형식 (.txt) - 기본값</summary>
        Txt = 0,

        /// <summary>CSV 형식 (.csv) - Excel 등으로 바로 열기 가능</summary>
        Csv = 1,

        /// <summary>TXT + CSV 동시 출력</summary>
        Both = 2
    }
}
