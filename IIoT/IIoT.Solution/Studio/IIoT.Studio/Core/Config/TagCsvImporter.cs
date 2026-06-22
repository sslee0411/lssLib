// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/TagCsvImporter.cs
//  역할: Tag 목록 CSV 파일을 읽어 장비 트리에 일괄 추가
//  S-20B: 신규
//
//  CSV 형식 (헤더 필수):
//    PLC명,Tag명,주소,자료형,단위,설명
//    PLC-01,토출압력,40001,FloatLE,bar,1번 펌프 토출 압력
//    PLC-01,유량,40003,FloatLE,m3/h,1번 펌프 유량
//
//  동작 규칙:
//    - PLC명 일치 → 해당 PLC 하위에 Tag 추가
//    - PLC명 없음 → 새 PlcTreeNode 생성 후 RootNodes에 추가
//    - 오류 행 → 건너뛰고 계속 처리
//    - 헤더 행(첫 번째 행) → 자동 스킵
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace IIoT.Studio.Core.Config;

// §1 ─ 가져오기 결과 ──────────────────────────────────────

public sealed record ImportResult(
    int    AddedCount,
    int    SkippedCount,
    int    NewPlcCount,
    List<string> Errors
)
{
    public bool IsSuccess => Errors.Count == 0 || AddedCount > 0;

    public string Summary =>
        $"Tag {AddedCount}개 추가" +
        (NewPlcCount > 0 ? $" / 새 PLC {NewPlcCount}개 생성" : "") +
        (SkippedCount > 0 ? $" / {SkippedCount}행 건너뜀" : "");
}

// §2 ─ 임포터 ─────────────────────────────────────────────

public sealed class TagCsvImporter
{
    // §2-1 ─ 컬럼 인덱스 상수 ───────────────────────────────

    private const int COL_PLC      = 0;
    private const int COL_TAG      = 1;
    private const int COL_ADDRESS  = 2;
    private const int COL_DATATYPE = 3;
    private const int COL_UNIT     = 4;
    private const int COL_DESC     = 5;
    private const int MIN_COLS     = 4; // PLC명, Tag명, 주소, 자료형 최소 필수

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// CSV 파일을 읽어 RootNodes에 Tag를 추가한다.
    /// </summary>
    /// <param name="filePath">CSV 파일 경로</param>
    /// <param name="rootNodes">장비 트리 루트 컬렉션</param>
    public ImportResult Import(
        string                                  filePath,
        ObservableCollection<AbstractTreeNode>  rootNodes)
    {
        var errors      = new List<string>();
        int added       = 0;
        int skipped     = 0;
        int newPlcCount = 0;

        if (!File.Exists(filePath))
        {
            errors.Add($"파일을 찾을 수 없습니다: {filePath}");
            return new ImportResult(0, 0, 0, errors);
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            errors.Add($"파일 읽기 오류: {ex.Message}");
            return new ImportResult(0, 0, 0, errors);
        }

        if (lines.Length < 2)
        {
            errors.Add("데이터 행이 없습니다. (헤더 포함 최소 2행 필요)");
            return new ImportResult(0, 0, 0, errors);
        }

        // 첫 번째 행은 헤더 — 스킵
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var cols = _ParseCsvLine(line);

            if (cols.Count < MIN_COLS)
            {
                errors.Add($"{i + 1}행: 컬럼 수 부족 (최소 {MIN_COLS}개 필요) → 건너뜀");
                skipped++;
                continue;
            }

            var plcName  = cols[COL_PLC].Trim();
            var tagName  = cols[COL_TAG].Trim();
            var address  = cols[COL_ADDRESS].Trim();
            var dataType = cols[COL_DATATYPE].Trim();
            var unit     = cols.Count > COL_UNIT ? cols[COL_UNIT].Trim() : string.Empty;
            var desc     = cols.Count > COL_DESC ? cols[COL_DESC].Trim() : string.Empty;

            // 필수 필드 검사
            if (string.IsNullOrEmpty(plcName))
            {
                errors.Add($"{i + 1}행: PLC명이 비어 있음 → 건너뜀");
                skipped++;
                continue;
            }
            if (string.IsNullOrEmpty(tagName))
            {
                errors.Add($"{i + 1}행: Tag명이 비어 있음 → 건너뜀");
                skipped++;
                continue;
            }
            if (string.IsNullOrEmpty(address))
            {
                errors.Add($"{i + 1}행: 주소가 비어 있음 → 건너뜀");
                skipped++;
                continue;
            }

            // PLC 찾기 or 생성
            var plcNode = _FindOrCreatePlc(plcName, rootNodes, ref newPlcCount);

            // Tag 추가
            var tag = new TagTreeNode(tagName)
            {
                Address     = address,
                DataType    = string.IsNullOrEmpty(dataType) ? "UInt16" : dataType,
                Unit        = unit,
                Description = desc
            };
            plcNode.Children.Add(tag);
            added++;
        }

        return new ImportResult(added, skipped, newPlcCount, errors);
    }

    // §4 ─ 헬퍼 ──────────────────────────────────────────────

    /// <summary>
    /// 이름으로 PlcTreeNode를 찾거나 없으면 새로 생성.
    /// 재귀 탐색: 루트 → 그룹 → 장비 → PLC
    /// </summary>
    private static PlcTreeNode _FindOrCreatePlc(
        string                                  plcName,
        ObservableCollection<AbstractTreeNode>  rootNodes,
        ref int                                 newPlcCount)
    {
        // 이름 일치 PlcTreeNode 재귀 탐색
        var found = _FindPlc(rootNodes, plcName);
        if (found is not null) return found;

        // 없으면 RootNodes에 새 PlcTreeNode 생성
        var newPlc = new PlcTreeNode(plcName);
        rootNodes.Add(newPlc);
        newPlcCount++;
        return newPlc;
    }

    private static PlcTreeNode? _FindPlc(
        IEnumerable<AbstractTreeNode> nodes, string name)
    {
        foreach (var node in nodes)
        {
            if (node is PlcTreeNode p
                && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p;

            var found = _FindPlc(node.Children, name);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>
    /// CSV 한 행 파싱 — 쉼표 구분, 큰따옴표 내 쉼표 허용.
    /// </summary>
    private static List<string> _ParseCsvLine(string line)
    {
        var result  = new List<string>();
        var current = new StringBuilder();
        bool inQuote = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (c == ',' && !inQuote)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
