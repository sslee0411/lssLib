// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/TagTemplateService.cs
//  역할: tag-templates.json 저장/로드
//  S-13B: 초기 구현
//  생성: 2026-06-18
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using System.IO;
using System.Text;
using System.Text.Json;

namespace IIoT.Studio.Core.Config;

public sealed class TagTemplateService
{
    // §1 ─ 경로 ───────────────────────────────────────────────

    public static string ConfigDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

    public static string TemplatePath =>
        Path.Combine(ConfigDir, "tag-templates.json");

    private static readonly JsonSerializerOptions _opt = new()
    {
        WriteIndented = true
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
