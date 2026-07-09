// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Export/SnapshotCsvExportService.cs
//  역할: 현재 [태그현황] 화면의 전체 Tag 값 스냅샷을 CSV 파일로 저장.
//        Collector CsvExportService.cs / Studio ConfigImportExportService.cs
//        와 동일한 패턴(StringBuilder + CSV 셀 이스케이프 + UTF8 저장).
//  MN-EX-07: 신규
//  생성: 2026-07-08
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;
using System.Globalization;
using System.IO;
using System.Text;

namespace IIoT.Monitor.Core.Export;

/// <summary>Tag 현재값 스냅샷 CSV 내보내기 유틸리티 (DI 싱글턴).</summary>
public sealed class SnapshotCsvExportService
{
    /// <summary>
    /// 전달된 Tag 목록을 CSV 로 저장한다.
    /// 컬럼: Collector, PLC, TagId, Raw, 공학값, 단위, 품질, 갱신시각
    /// </summary>
    public async Task ExportAsync(IEnumerable<LiveTagRow> rows, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Collector,PLC,TagId,Raw,공학값,단위,품질,갱신시각");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                _CsvCell(r.CollectorName),
                _CsvCell(r.PlcId),
                _CsvCell(r.TagId),
                r.RawValue.ToString(CultureInfo.InvariantCulture),
                r.EngValue.ToString(CultureInfo.InvariantCulture),
                _CsvCell(r.Unit),
                _CsvCell(r.Quality),
                r.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string _CsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
