// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Models/LogRow.cs
//  역할: 통합 로그 뷰어 1행 모델 + lssLib.Log TXT 라인 파서
//  MG-04: 신규
//  개선(2026-07-09, 사용자 요청): 표준 LogPanelView UI 정렬 —
//        시각/레벨/Source 컬럼 분리를 위해 lssLib.Log AppendTxt 형식
//        "yyyy_MM_dd HH:mm:ss.fff  [LEVEL]  Source(-16)  내용" 파싱 추가
//  생성: 2026-07-09 / 수정: 2026-07-09
// ══════════════════════════════════════════════════════════

namespace IIoT.Manager.Models;

/// <summary>통합 로그 뷰어에 표시되는 1개 라인.</summary>
/// <param name="Program">프로그램 이름 (예: IIoT.Collector — 어느 Log 폴더에서 왔는지)</param>
/// <param name="TimeText">시각 (HH:mm:ss.fff)</param>
/// <param name="LevelText">레벨 (DEBUG/INFO/WARN/ERROR/FATAL — 파싱 실패 시 빈 문자열)</param>
/// <param name="Source">로그 출처 태그 (예: App, ProcessManager)</param>
/// <param name="Message">내용</param>
public sealed record LogRow(string Program, string TimeText, string LevelText,
                            string Source, string Message)
{
    /// <summary>
    /// lssLib.Log AppendTxt 라인을 파싱한다.
    /// 형식: "yyyy_MM_dd HH:mm:ss.fff  [LEVEL]  Source(패딩16)  내용"
    /// 형식이 다르면(외부 기록 등) 전체를 Message 로 담아 반환한다.
    /// </summary>
    public static LogRow Parse(string program, string line)
    {
        try
        {
            // ① 시각: "yyyy_MM_dd HH:mm:ss.fff" — 앞 23자
            //    (yyyy_MM_dd=10) + 공백 + (HH:mm:ss.fff=12)
            if (line.Length > 25 && line[10] == ' ')
            {
                var time = line.Substring(11, 12);

                // ② 레벨: 대괄호 사이
                int lb = line.IndexOf('[', 23);
                int rb = lb >= 0 ? line.IndexOf(']', lb + 1) : -1;
                if (rb > lb)
                {
                    var level = line[(lb + 1)..rb].Trim();

                    // ③ Source: "]  " 이후 ~ 연속 공백 2개 전까지
                    //    ({Source,-16} 패딩 → 뒤에 공백 2개 후 내용)
                    var rest = line[(rb + 1)..].TrimStart();
                    int sep  = rest.IndexOf("  ", StringComparison.Ordinal);
                    if (sep > 0)
                    {
                        var source  = rest[..sep].Trim();
                        var message = rest[(sep + 2)..].TrimStart();
                        return new LogRow(program, time, level, source, message);
                    }

                    // Source 뒤 내용이 없는 라인
                    return new LogRow(program, time, level, rest.Trim(), "");
                }
            }
        }
        catch
        {
            // 파싱 실패 → 아래 원문 폴백
        }

        // 형식 불일치 — 원문 그대로 표시
        return new LogRow(program, "", "", "", line);
    }
}
