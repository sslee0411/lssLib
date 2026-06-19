// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/TagTemplateService.cs
//  역할: tag-templates.json 저장/로드
//  S-13B: 초기 구현
//  S-14 fix2: [이슈4] 한글 깨짐 수정
//    System.Text.Json 기본값은 비ASCII 문자(한글 포함)를
//    \uXXXX 유니코드 이스케이프로 직렬화함
//    → Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping 추가
//      한글·일본어·특수문자 등을 그대로 저장
//  생성: 2026-06-18 / 수정: 2026-06-19
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace IIoT.Studio.Core.Config;

public sealed class TagTemplateService
{
    // §1 ─ 경로 ───────────────────────────────────────────────

    public static string ConfigDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

    public static string TemplatePath =>
        Path.Combine(ConfigDir, "tag-templates.json");

    // ★ 이슈4 fix: UnsafeRelaxedJsonEscaping → 한글을 그대로 저장
    //   JavaScriptEncoder.Default      → "온도" → "\uC628\uB3C4" (깨짐처럼 보임)
    //   UnsafeRelaxedJsonEscaping      → "온도" → "온도" (정상)
    //   Create(UnicodeRanges.All) 도 동일 효과이나 UnsafeRelaxed가 관용적
    private static readonly JsonSerializerOptions _opt = new()
    {
        WriteIndented = true,
        Encoder       = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // §2 ─ 저장 ───────────────────────────────────────────────

    public void Save(IEnumerable<TagTemplate> templates)
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        var json = JsonSerializer.Serialize(templates.ToList(), _opt);
        var tmp  = TemplatePath + ".tmp";
        var bak  = TemplatePath + ".bak";

        File.WriteAllText(tmp, json, Encoding.UTF8);
        if (File.Exists(TemplatePath))
            File.Replace(tmp, TemplatePath, bak);
        else
            File.Move(tmp, TemplatePath);
    }

    // §3 ─ 로드 ───────────────────────────────────────────────

    public List<TagTemplate> Load()
    {
        if (!File.Exists(TemplatePath)) return new();
        try
        {
            var json = File.ReadAllText(TemplatePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<TagTemplate>>(json, _opt)
                   ?? new();
        }
        catch { return new(); }
    }
}
