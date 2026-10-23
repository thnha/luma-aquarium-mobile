using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// The Cá – hình dáng / màu / bề mặt / vây tabs of the price sheet (DecorCatalogSheet): the "model" and "look"
// blocks of Art/Fish/<Name>/<Name>.json, one row per fish, after one row per template ("mẫu: <Name>", the
// files in PipelineFishConfig.TemplateDir). A fish's mẫu column is its "base". An empty cell takes the
// template's value, or the default in PipelineFishConfig: Pull removes that key from the file. A new "mẫu: …"
// row makes a new template. Colours are #RRGGBB (or "r, g, b" when a channel is above 1), ranges "start, end".
// Pull writes the changed files and rebuilds those fish and the fish of changed templates (PipelineFishImporter.Build).
public static class FishLookSheet
{
    // Not "id", so DecorCatalogSheet.ReadRows leaves these tabs to us.
    public const string KeyHeader = "cá";
    public const string DefaultRow = "(mặc định)";
    public const string TemplatePrefix = "mẫu: ";
    private static readonly string[] KeyHeaders = { KeyHeader, "ảnh", "tên" };

    public enum Kind { Number, Whole, Pattern, Colour, Pair, Template }

    // block "" is the file's top level.
    public sealed class Param
    {
        public string block, key, header; public Kind kind; public float min = float.NegativeInfinity, max = float.PositiveInfinity;
        public object fallback;
    }

    private static readonly Param Base = new Param { block = "", key = "base", header = "mẫu", kind = Kind.Template, fallback = "" };

    public static readonly (string tab, Param[] columns)[] Groups =
    {
        ("Cá – hình dáng", new[] { Base }.Concat(Params("model", "size", "bodySwimAmplitude", "strokeSwimAmplitude"))
            .Concat(Params("look", "pattern", "seed")).ToArray()),
        ("Cá – màu", Params("look", "primary", "secondary", "accent", "finEdge", "scaleEdgeDark", "scaleEdgeLight")),
        ("Cá – bề mặt", Params("look", "iridescence", "textureDetail", "silverSheen", "gloss", "blackGloss", "whiteScales",
            "pigmentDepth", "scaleRelief", "scaleEdge", "scaleEdgeWidth", "vibrance", "brightness", "baseTexture")),
        ("Cá – vây", Params("look", "finEdgeClear", "finRelief", "finTranslucency", "finBlack", "finBrightness",
            "finRimAlpha", "finRim", "caudalRim", "caudalStart")),
    };

    // Kind and default from PipelineFishConfig, limits from the [Range] of the BettaVariant field of the same name.
    private static Param[] Params(string block, params string[] keys) => keys.Select(key =>
    {
        object defaults = block == "model" ? new PipelineFishConfig.Model() : (object)new PipelineFishConfig.Look();
        var field = defaults.GetType().GetField(key) ?? throw new ArgumentException("PipelineFishConfig has no " + block + "." + key);
        var value = field.GetValue(defaults);
        var param = new Param { block = block, key = key, header = key, fallback = value, kind = value switch
        {
            int _ => Kind.Whole, string _ => Kind.Pattern, float[] v when v.Length == 3 => Kind.Colour, float[] _ => Kind.Pair, _ => Kind.Number
        } };
        var range = typeof(BettaVariant).GetField(key)?.GetCustomAttribute<RangeAttribute>();
        if (range != null) { param.min = range.min; param.max = range.max; }
        else if (param.kind == Kind.Pair) { param.min = 0f; param.max = 1f; }
        else if (block == "model") param.min = key == "size" ? 0.01f : 0f;
        return param;
    }).ToArray();

    // name is the row's cá cell: the fish's folder name, or TemplatePrefix + the template's name.
    public sealed class Fish { public string name, displayName, image, json; }

    public static bool IsTemplate(string name) => name.StartsWith(TemplatePrefix, StringComparison.Ordinal);

    public static List<DecorCatalogSheet.SheetTab> Tabs() => Tabs(Load());

    // Header, the grey defaults row, then the templates and the fish, each with only the keys its file sets.
    public static List<DecorCatalogSheet.SheetTab> Tabs(IReadOnlyList<Fish> fish) => Groups.Select(group =>
    {
        var table = new List<string[]> { KeyHeaders.Concat(group.columns.Select(p => p.header)).ToArray(),
            new[] { DefaultRow, "", "" }.Concat(group.columns.Select(p => Show(p, Raw(p.fallback)))).ToArray() };
        foreach (var f in fish.OrderBy(f => IsTemplate(f.name) ? 0 : 1))
            table.Add(new[] { f.name, f.image != null ? $"=IMAGE(\"{f.image}\")" : "", f.displayName }.Concat(group.columns.Select(p =>
                Current(f.json, p) is string raw ? Show(p, raw) : "")).ToArray());
        return new DecorCatalogSheet.SheetTab { name = group.tab, viewHeaders = new string[0], table = table,
            keyColumns = KeyHeaders.Length, greyRows = 1, swatches = true };
    }).ToList();

    // Applies every tab with a cá column to the files in json (row name -> text; templates under TemplatePrefix + name).
    // A new template row adds its file to json. Returns the rows whose file changed or is new.
    public static HashSet<string> Apply(IEnumerable<(string name, List<string[]> table)> tabs, Dictionary<string, string> json,
        List<string> errors, Func<string, bool> patternExists = null)
    {
        patternExists ??= PatternExists;
        var all = Groups.SelectMany(g => g.columns).ToDictionary(p => p.header);
        var cells = new Dictionary<string, Dictionary<Param, (string text, string place)>>();
        var unknown = new SortedSet<string>();
        var changed = new HashSet<string>();
        foreach (var (tabName, table) in tabs)
        {
            if (table.Count == 0) continue;
            int key = Array.FindIndex(table[0], h => Same(h, KeyHeader));
            if (key < 0) continue;
            var columns = new List<(int index, Param param)>();
            for (int c = 0; c < table[0].Length; c++)
                if (all.TryGetValue(table[0][c].Trim(), out var param)) columns.Add((c, param));
            for (int r = 1; r < table.Count; r++)
            {
                string fish = RowName(Cell(table[r], key));
                if (string.IsNullOrEmpty(fish) || fish == DefaultRow) continue;
                if (!json.ContainsKey(fish))
                {
                    if (!IsTemplate(fish)) { unknown.Add(fish); continue; }
                    if (!Regex.IsMatch(fish.Substring(TemplatePrefix.Length), @"^[\w][\w \-]*$"))
                    { errors.Add($"Tab {tabName}, hàng {r + 1}: tên mẫu \"{fish}\" chỉ được có chữ, số, dấu cách, - và _."); continue; }
                    json[fish] = "{\n}\n";
                    changed.Add(fish);
                }
                if (!cells.TryGetValue(fish, out var row)) cells[fish] = row = new Dictionary<Param, (string, string)>();
                foreach (var (index, param) in columns)
                {
                    string place = $"Tab {tabName}, hàng {r + 1}, {param.header}";
                    if (row.ContainsKey(param)) errors.Add($"{place}: \"{fish}\" có {param.header} ở hai chỗ, giữ giá trị cũ.");
                    else row[param] = (Cell(table[r], index) ?? "", place);
                }
            }
        }
        if (unknown.Count > 0) errors.Add("Không có file json cho cá (bỏ qua): " + string.Join(", ", unknown));

        bool TemplateExists(string name) => json.ContainsKey(TemplatePrefix + name);
        foreach (var (fish, row) in cells)
        {
            string text = json[fish];
            foreach (var block in new[] { "", "model", "look" })
            {
                var edits = new Dictionary<string, string>();
                foreach (var pair in row)
                {
                    var param = pair.Key; var (cell, place) = pair.Value;
                    if (param.block != block) continue;
                    string raw = Current(text, param);
                    if (cell.Length == 0) { if (raw != null) edits[param.key] = null; continue; }
                    if (param.kind == Kind.Template && IsTemplate(fish)) { errors.Add($"{place}: mẫu không dựa trên mẫu khác, bỏ qua."); continue; }
                    string value = Parse(param, cell, raw, param.kind == Kind.Template ? TemplateExists : patternExists, out string error);
                    if (error != null) errors.Add($"{place} ({fish}): {error}, giữ giá trị cũ.");
                    else if (value != null) edits[param.key] = value;
                }
                if (edits.Count > 0) text = block == "" ? SetRoot(text, edits) : SetBlock(text, block, edits);
            }
            if (text != json[fish]) { json[fish] = text; changed.Add(fish); }
        }
        return changed;
    }

    // "mẫu:Galaxy", "Mẫu: Galaxy" -> "mẫu: Galaxy".
    private static string RowName(string cell)
    {
        if (cell == null) return null;
        string prefix = TemplatePrefix.TrimEnd();
        return cell.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? TemplatePrefix + cell.Substring(prefix.Length).Trim() : cell;
    }

    // Writes the changed files and rebuilds their fish, and the fish of changed templates. Returns how many files changed.
    public static int Save(IEnumerable<(string name, List<string[]> table)> workbook, List<string> errors)
    {
        var fish = PipelineFishImporter.All().Where(f => File.Exists(JsonPath(f))).ToDictionary(f => f.name);
        var json = Load().ToDictionary(f => f.name, f => f.json);
        var changed = Apply(workbook, json, errors);
        if (changed.Count == 0) return 0;
        foreach (string name in changed)
        {
            string path = IsTemplate(name) ? PipelineFishConfig.TemplatePath(name.Substring(TemplatePrefix.Length)) : JsonPath(fish[name]);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json[name], new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
        }
        var rebuild = fish.Keys.Where(name => changed.Contains(name) ||
            Current(json[name], Base) is string template && changed.Contains(TemplatePrefix + Show(Base, template))).ToList();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("Đang Play: đã lưu json nhưng chưa dựng lại " + string.Join(", ", rebuild) + ". Thoát Play rồi chạy Lumaquarium > Fish > Import Pipeline Fish.");
            return changed.Count;
        }
        foreach (string name in rebuild)
        {
            try { PipelineFishImporter.Build(fish[name]); }
            catch (Exception e) { errors.Add($"Cá {name}: đã lưu json nhưng dựng lại lỗi: {e.Message}"); }
        }
        AssetDatabase.SaveAssets();
        return changed.Count;
    }

    private static List<Fish> Load() => (Directory.Exists(PipelineFishConfig.TemplateDir)
            ? Directory.GetFiles(PipelineFishConfig.TemplateDir, "*.json").OrderBy(p => p, StringComparer.Ordinal).Select(p => new Fish
                { name = TemplatePrefix + Path.GetFileNameWithoutExtension(p), displayName = "", json = File.ReadAllText(p) })
            : Enumerable.Empty<Fish>())
        .Concat(PipelineFishImporter.All().Where(f => File.Exists(JsonPath(f))).Select(f => new Fish
        {
            name = f.name, displayName = f.displayName, json = File.ReadAllText(JsonPath(f)),
            image = f.inShop ? FishCatalogSheet.PortraitUrl(f) : null
        })).ToList();

    private static string JsonPath(PipelineFish fish) =>
        PipelineFishConfig.PathFor(Path.GetDirectoryName(AssetDatabase.GetAssetPath(fish)).Replace('\\', '/'), fish.name);

    private static bool PatternExists(string name) =>
        File.Exists("Assets/_Project/Data/Betta/Patterns/" + name + ".asset");

    private static bool Same(string a, string b) => string.Equals(a?.Trim().Normalize(), b, StringComparison.OrdinalIgnoreCase);

    private static string Cell(string[] cells, int index) => index >= 0 && index < cells.Length ? cells[index].Trim().Normalize() : null;

    // ---- Values: JSON text <-> sheet cell ----

    private static string Raw(object value) => value switch
    {
        float f => Format(f), int i => i.ToString(CultureInfo.InvariantCulture), string s => Quote(s),
        float[] v => "[" + string.Join(", ", v.Select(Format)) + "]", _ => ""
    };

    private static string Format(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // A JSON value as the sheet shows it.
    public static string Show(Param param, string raw)
    {
        switch (param.kind)
        {
            case Kind.Pattern: case Kind.Template: return raw.Trim().Trim('"');
            case Kind.Colour:
                var c = Numbers(raw);
                if (c == null || c.Length != 3) return raw;
                return c.All(v => v >= 0f && v <= 1f) ? Hex(c) : string.Join(", ", c.Select(Format));
            case Kind.Pair:
                var p = Numbers(raw);
                return p == null ? raw : string.Join(", ", p.Select(Format));
            default: return raw.Trim();
        }
    }

    private static string Hex(float[] c) => "#" + string.Concat(c.Select(v => Mathf.RoundToInt(Mathf.Clamp01(v) * 255f).ToString("X2")));

    // The JSON text for a cell, or null when it means the value already in the file.
    private static string Parse(Param param, string cell, string raw, Func<string, bool> exists, out string error)
    {
        error = null;
        switch (param.kind)
        {
            case Kind.Pattern: case Kind.Template:
                if (!exists(cell))
                {
                    error = param.kind == Kind.Pattern ? $"không có pattern \"{cell}\" trong Data/Betta/Patterns" : $"không có hàng \"{TemplatePrefix}{cell}\"";
                    return null;
                }
                return raw != null && raw.Trim().Trim('"') == cell ? null : Quote(cell);
            case Kind.Whole:
                if (!int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out int whole)) { error = $"\"{cell}\" không phải số nguyên"; return null; }
                return raw != null && int.TryParse(raw.Trim(), out int old) && old == whole ? null : whole.ToString(CultureInfo.InvariantCulture);
            case Kind.Number:
                if (!TryNumber(cell, out float number)) { error = $"\"{cell}\" không phải số"; return null; }
                if (number < param.min || number > param.max) { error = $"{Format(number)} {Limits(param)}"; return null; }
                return raw != null && TryNumber(raw, out float was) && Mathf.Abs(was - number) < 1e-5f ? null : Format(number);
            default:
                bool hex = param.kind == Kind.Colour && Regex.IsMatch(cell, "^#?[0-9A-Fa-f]{6}$");
                var values = hex
                    ? Enumerable.Range(0, 3).Select(i => Convert.ToInt32(cell.TrimStart('#').Substring(i * 2, 2), 16) / 255f).ToArray()
                    : Numbers(cell);
                int count = param.kind == Kind.Colour ? 3 : 2;
                if (values == null || values.Length != count)
                {
                    error = param.kind == Kind.Colour ? $"\"{cell}\" phải là #RRGGBB hoặc \"r, g, b\"" : $"\"{cell}\" phải là \"đầu, cuối\"";
                    return null;
                }
                if (values.Any(v => v < param.min || v > param.max) || param.kind == Kind.Colour && values.Any(v => v < 0f))
                { error = $"\"{cell}\" {(param.kind == Kind.Colour ? "có kênh âm" : Limits(param))}"; return null; }
                var old3 = raw != null ? Numbers(raw) : null;
                // A hex cell that still shows the file's colour keeps the file's exact numbers.
                if (old3 != null && old3.Length == count && (hex ? Show(param, raw) == "#" + cell.TrimStart('#').ToUpperInvariant()
                        : old3.Zip(values, (a, b) => Mathf.Abs(a - b) < 1e-5f).All(x => x))) return null;
                return Raw(values);
        }
    }

    private static string Limits(Param param) => float.IsPositiveInfinity(param.max)
        ? $"phải từ {Format(param.min)} trở lên" : $"phải trong khoảng {Format(param.min)}–{Format(param.max)}";

    // Decimal commas ("0,55") are read as points.
    private static bool TryNumber(string text, out float value) =>
        float.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);

    // "[0.1, 0.2]", "0.1, 0.2" or "0.1; 0.2" -> numbers; null if any is not a number.
    private static float[] Numbers(string text)
    {
        var parts = text.Trim().TrimStart('[').TrimEnd(']').Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var values = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return null;
        return values;
    }

    // ---- The "model" / "look" blocks of the hand-written file ----

    private const string ValuePattern = "\\[[^\\]]*\\]|\"(?:[^\"\\\\]|\\\\.)*\"|[^,\\s}\\]]+";

    private static Match Block(string json, string block) => Regex.Match(json, $"([ \\t]*)\"{block}\"\\s*:\\s*\\{{[^{{}}]*\\}}");

    // The block's keys and their JSON text, in file order.
    public static Dictionary<string, string> Entries(string json, string block)
    {
        var entries = new Dictionary<string, string>();
        var match = Block(json, block);
        if (!match.Success) return entries;
        foreach (Match entry in Regex.Matches(match.Value.Substring(match.Value.IndexOf('{')), $"\"(\\w+)\"\\s*:\\s*({ValuePattern})"))
            entries[entry.Groups[1].Value] = entry.Groups[2].Value;
        return entries;
    }

    // The JSON text of a param in a file, or null when the file leaves it out.
    public static string Current(string json, Param param)
    {
        if (param.block != "") return Entries(json, param.block).TryGetValue(param.key, out string raw) ? raw : null;
        var match = Regex.Match(json, $"\"{param.key}\"\\s*:\\s*({ValuePattern})");
        return match.Success ? match.Groups[1].Value : null;
    }

    // Sets (or with null removes) top-level keys; a new one goes first in the file.
    public static string SetRoot(string json, IReadOnlyDictionary<string, string> edits)
    {
        foreach (var (key, value) in edits)
        {
            var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*(?:{ValuePattern})");
            if (value != null && match.Success)
                json = json.Substring(0, match.Index) + $"\"{key}\": {value}" + json.Substring(match.Index + match.Length);
            else if (value != null)
            {
                int open = json.IndexOf('{') + 1;
                string rest = json.Substring(open);
                json = json.Substring(0, open) + $"\n  \"{key}\": {value}" + (rest.TrimStart().StartsWith("}") ? "" : ",") + rest;
            }
            else if (match.Success)
            {
                json = Regex.Replace(json, $"[ \\t]*\"{key}\"\\s*:\\s*(?:{ValuePattern})\\s*,?[ \\t]*\\r?\\n?", "");
                // The key was the last one: drop the comma left before the closing brace.
                json = Regex.Replace(json, ",(\\s*\\})", "$1");
            }
        }
        return json;
    }

    // Sets (or with null removes) keys of one block, one key per line as the files are written; the rest of the
    // file stays as it is. A missing block is added at the end.
    public static string SetBlock(string json, string block, IReadOnlyDictionary<string, string> edits)
    {
        var match = Block(json, block);
        var entries = Entries(json, block).ToList();
        foreach (var (key, value) in edits)
        {
            int at = entries.FindIndex(e => e.Key == key);
            if (value == null) { if (at >= 0) entries.RemoveAt(at); }
            else if (at >= 0) entries[at] = new KeyValuePair<string, string>(key, value);
            else entries.Add(new KeyValuePair<string, string>(key, value));
        }
        string indent = match.Success ? match.Groups[1].Value : "  ";
        string body = entries.Count == 0 ? "{}"
            : "{\n" + string.Join(",\n", entries.Select(e => $"{indent}  \"{e.Key}\": {e.Value}")) + $"\n{indent}}}";
        string text = $"{indent}\"{block}\": {body}";
        if (match.Success) return json.Substring(0, match.Index) + text + json.Substring(match.Index + match.Length);
        int end = json.LastIndexOf('}');
        string before = json.Substring(0, end).TrimEnd();
        return before + (before.EndsWith("{") ? "\n" : ",\n") + text + "\n" + json.Substring(end);
    }
}
