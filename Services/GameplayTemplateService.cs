using System.Text.Json.Nodes;

namespace SoulMaskServerManager.Services;

/// <summary>
/// One preset slot from a gameplay preset file shipped with the game.
/// A single file can contribute more than one entry: the full templates carry
/// slots "0"/"1"/"2" and those slots hold genuinely different configurations
/// (Dashi differs by 75 settings between slot 0 and slot 1).
/// </summary>
public sealed record GameplayTemplate(
    string FilePath,
    string FileName,
    string SlotKey,
    string DisplayName,
    IReadOnlyDictionary<string, double> Values)
{
    /// <summary>How many settings this slot actually defines.</summary>
    public int DefinedCount => Values.Count;
}

/// <summary>
/// Reads the preset library the game ships under WS\Config\GameplaySettings.
///
/// Two shapes exist and they must be handled differently:
///   • Full presets   — ~276 settings, usually in every slot. Safe to use as-is.
///   • Server configs — only a handful (e.g. 42 of 276) in ONE slot, the rest
///                      empty. These are overlays meant to sit on top of a base;
///                      applying one wholesale would blank everything else.
/// This class reports what each slot defines; the caller decides replace vs merge.
/// </summary>
public static class GameplayTemplateService
{
    // GameXishuConfig_* files look similar but hold setting METADATA (descriptions,
    // min/max ranges) in a completely different shape. The ValuePrefix test below
    // already excludes them; this check is kept as an explicit statement of intent
    // so the distinction isn't lost if the prefixes ever change.
    private const string ValuePrefix  = "GameXishu_Template";
    private const string ConfigPrefix = "GameXishuConfig";

    public static List<GameplayTemplate> Scan(string templatesDir)
    {
        var found = new List<GameplayTemplate>();
        if (!Directory.Exists(templatesDir)) return found;

        foreach (var path in Directory.GetFiles(templatesDir, "*.json"))
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith(ConfigPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.StartsWith(ValuePrefix,  StringComparison.OrdinalIgnoreCase)) continue;

            found.AddRange(LoadAll(path));
        }

        return found.OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Every distinct populated slot in one file. Slots holding identical values
    /// are collapsed (slot 2 is commonly a copy of slot 0), and the slot number is
    /// only shown when a file actually offers more than one configuration.
    /// </summary>
    public static List<GameplayTemplate> LoadAll(string path)
    {
        var result = new List<GameplayTemplate>();
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
            if (root == null) return result;

            var slots = new List<(string Slot, Dictionary<string, double> Values)>();
            foreach (var slot in root)
            {
                if (slot.Value is not JsonObject obj) continue;

                var values = new Dictionary<string, double>();
                foreach (var kv in obj)
                    if (kv.Value is JsonValue jv && jv.TryGetValue<double>(out double d))
                        values[kv.Key] = d;

                if (values.Count == 0) continue;                        // empty slot
                if (slots.Any(s => SameValues(s.Values, values))) continue; // duplicate slot
                slots.Add((slot.Key, values));
            }

            string fileName = Path.GetFileName(path);
            string baseName = ToDisplayName(fileName);

            foreach (var (slot, values) in slots)
                result.Add(new GameplayTemplate(
                    path, fileName, slot,
                    slots.Count > 1 ? $"{baseName} — preset {slot}" : baseName,
                    values));
        }
        catch
        {
            // unreadable or not a preset file
        }
        return result;
    }

    /// <summary>Convenience for callers wanting a single slot (the largest).</summary>
    public static GameplayTemplate? Load(string path) =>
        LoadAll(path).OrderByDescending(t => t.DefinedCount).FirstOrDefault();

    private static bool SameValues(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
            if (!b.TryGetValue(kv.Key, out double v) || v != kv.Value) return false;
        return true;
    }

    /// <summary>"GameXishu_Template_PvE_10P_Hardcore.json" → "PvE 10P Hardcore".</summary>
    public static string ToDisplayName(string fileName)
    {
        string s = Path.GetFileNameWithoutExtension(fileName);
        if (s.StartsWith(ValuePrefix, StringComparison.OrdinalIgnoreCase))
            s = s[ValuePrefix.Length..];
        s = s.TrimStart('_').Replace('_', ' ').Trim();
        return s.Length == 0 ? "Default" : s;
    }
}
