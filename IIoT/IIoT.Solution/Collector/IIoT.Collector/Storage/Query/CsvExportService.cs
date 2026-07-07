// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/Query/CsvExportService.cs
//  역할: TrendQueryService.QueryAsync() 결과(TrendPoint 목록)를 CSV 파일로 저장
//  C-EX-07: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using System.Globalization;
using System.Text;

namespace IIoT.Collector.Storage.Query;

/// <summary>CSV 내보내기 유틸리티 서비스 (DI 싱글턴).</summary>
public sealed class CsvExportService
{
    /// <summary>
    /// TrendPoint 목록을 CSV 로 저장합니다.
    /// </summary>
    /// <param name="points">내보낼 데이터 (Timestamp/Value 등을 가진 레코드)</param>
    /// <param name="filePath">저장 경로 (.csv)</param>
    /// <param name="tagName">헤더에 표시할 Tag 이름</param>
    public async Task ExportAsync(
        IReadOnlyList<TrendPoint> points, string filePath, string tagName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tag,{tagName}");
        sb.AppendLine("Timestamp,Value");

        foreach (var p in points)
        {
            sb.AppendLine(string.Join(',',
                p.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                p.Value.ToString(CultureInfo.InvariantCulture)));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }
}
