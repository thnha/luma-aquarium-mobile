using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

// Names, descriptions and prices of the decoration shop, kept in a table (decor_catalog.csv) that opens in
// Numbers, Excel or Google Sheets. Rows are matched by id; the nhóm column is for reading only. The catalog
// builder applies the same table, so a rebuild keeps the edits. The Google Sheet has one tab per group
// (decor_catalog.xlsx), plus a Cá tab for the fish (FishCatalogSheet) and Thành hồ, Cát nền and Hồ tabs
// (SurfaceCatalogSheet) and a Gói quà tab for the gift packs (GiftSheet), and is pulled as a whole workbook.
public static class DecorCatalogSheet
{
    public const string CsvPath = AquariumDecorCatalogBuilder.Root + "/decor_catalog.csv";
    public const string XlsxPath = AquariumDecorCatalogBuilder.Root + "/decor_catalog.xlsx";
    private static readonly AquariumDecorCategory[] TabOrder = { AquariumDecorCategory.Wood, AquariumDecorCategory.Rocks,
        AquariumDecorCategory.Coral, AquariumDecorCategory.Plants, AquariumDecorCategory.Moss, AquariumDecorCategory.Floating };
    private const string CatalogPath = AquariumDecorCatalogBuilder.Root + "/AquariumDecorCatalog.asset";
    private const string UrlKey = "Lumaquarium.DecorCatalogSheetUrl";
    private static readonly string[] Header = { "id", "nhóm", "tên", "mô tả", "giá" };

    public sealed class Row { public string place, id, name, description, price; }

    // An editable shop item, decoration or fish. Fish may cost 0 (a gift).
    public sealed class SheetItem { public string id, name, description; public int price; public bool freeAllowed, changed; }

    // One tab of the Excel file: a picture, then the editable columns, then this tab's read-only columns.
    // A tab with a table instead (Gói quà, the Cá – … tabs) is written as it is, its first row as the header:
    // its first keyColumns columns stay in view (grey after the first), the greyRows rows below the header are
    // for reading only, "=IMAGE(...)" is a picture and with swatches a #RRGGBB cell is filled with its colour.
    public sealed class SheetRow { public SheetItem item; public string image; public string[] view; }
    public sealed class SheetTab
    {
        public string name; public string[] viewHeaders; public List<string[]> table;
        public int keyColumns, greyRows; public bool swatches;
        public readonly List<SheetRow> rows = new List<SheetRow>();
    }

    public sealed class Report
    {
        public int changed;
        public readonly List<string> errors = new List<string>();
        public readonly List<string> unknown = new List<string>();
        public override string ToString()
        {
            var text = new StringBuilder($"Đã cập nhật {changed} món.");
            if (unknown.Count > 0) text.Append($"\n\nKhông có trong catalog (bỏ qua): {string.Join(", ", unknown)}");
            if (errors.Count > 0) text.Append("\n\nLỗi (giữ giá trị cũ):\n").Append(string.Join("\n", errors));
            return text.ToString();
        }
    }

    public static string SheetUrl
    {
        get => EditorUserSettings.GetConfigValue(UrlKey) ?? "";
        set => EditorUserSettings.SetConfigValue(UrlKey, value?.Trim());
    }

    public static string CategoryLabel(AquariumDecorCategory category) => category switch
    {
        AquariumDecorCategory.Wood => "Lũa",
        AquariumDecorCategory.Rocks => "Đá",
        AquariumDecorCategory.Coral => "San hô",
        AquariumDecorCategory.Moss => "Rêu",
        AquariumDecorCategory.Floating => "Bèo",
        _ => "Cây"
    };

    public static string Export(IReadOnlyList<AquariumDecorOffer> offers)
    {
        var text = new StringBuilder();
        text.Append(string.Join(",", Header)).Append("\r\n");
        foreach (var offer in offers)
            if (offer != null)
                text.Append(string.Join(",", Quote(offer.id), Quote(CategoryLabel(offer.category)), Quote(offer.displayName),
                    Quote(offer.description), offer.price.ToString())).Append("\r\n");
        return text.ToString();
    }

    private static string Quote(string field)
    {
        field ??= "";
        return field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? field : "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    // RFC 4180: quoted fields may hold commas, doubled quotes and line breaks.
    public static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        text = text.TrimStart('﻿');
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c != '"') field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = false;
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                rows.Add(row.ToArray()); row.Clear();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }

    // Columns are found by their header, so they may be reordered or have extra columns beside them.
    // Null when the table has no id column (a notes tab in the sheet) or is the Gói quà tab.
    public static List<Row> ReadRows(List<string[]> table, string tab = null)
    {
        if (table.Count == 0) return null;
        int Column(string name) => Array.FindIndex(table[0], h => string.Equals(h.Trim().Normalize(), name, StringComparison.OrdinalIgnoreCase));
        int id = Column(Header[0]), name = Column(Header[2]), description = Column(Header[3]), price = Column(Header[4]);
        if (id < 0 || GiftSheet.IsGiftTable(table)) return null;
        string Cell(string[] cells, int index) => index >= 0 && index < cells.Length ? cells[index].Trim().Normalize() : null;
        var rows = new List<Row>();
        for (int i = 1; i < table.Count; i++)
        {
            string rowId = Cell(table[i], id);
            if (string.IsNullOrEmpty(rowId)) continue;
            rows.Add(new Row { place = tab == null ? $"Hàng {i + 1}" : $"Tab {tab}, hàng {i + 1}", id = rowId, name = Cell(table[i], name),
                description = Cell(table[i], description), price = Cell(table[i], price) });
        }
        return rows;
    }

    public static Report Apply(IReadOnlyList<AquariumDecorOffer> offers, string csv) =>
        Apply(offers, new[] { ("", ParseCsv(csv)) });

    public static Report Apply(IReadOnlyList<AquariumDecorOffer> offers, IEnumerable<(string name, List<string[]> table)> tabs)
    {
        var items = DecorItems(offers);
        var report = Apply(items, tabs);
        WriteBack(offers, items);
        return report;
    }

    public static List<SheetItem> DecorItems(IReadOnlyList<AquariumDecorOffer> offers) => offers.Where(o => o != null)
        .Select(o => new SheetItem { id = o.id, name = o.displayName, description = o.description, price = o.price }).ToList();

    public static void WriteBack(IReadOnlyList<AquariumDecorOffer> offers, IEnumerable<SheetItem> items)
    {
        foreach (var item in items)
        {
            if (!item.changed) continue;
            var offer = offers.First(o => o != null && o.id == item.id);
            offer.displayName = item.name; offer.description = item.description; offer.price = item.price;
        }
    }

    // Every tab with an id column counts, so items can sit in any tab. An empty cell keeps the current value.
    // Prices may carry thousands separators ("1.200", "1,200").
    public static Report Apply(IReadOnlyList<SheetItem> items, IEnumerable<(string name, List<string[]> table)> tabs)
    {
        var report = new Report();
        var all = new List<Row>();
        foreach (var (tabName, table) in tabs)
        {
            var rows = ReadRows(table, string.IsNullOrEmpty(tabName) ? null : tabName);
            if (rows != null) all.AddRange(rows);
        }
        if (all.Count == 0) { report.errors.Add($"Không có bảng nào có cột \"{Header[0]}\" ở hàng đầu tiên."); return report; }
        var seen = new HashSet<string>();
        foreach (var row in all)
        {
            if (!seen.Add(row.id)) { report.errors.Add($"{row.place}: id \"{row.id}\" bị lặp."); continue; }
            var item = items.FirstOrDefault(x => x.id == row.id);
            if (item == null) { report.unknown.Add(row.id); continue; }
            bool changed = false;
            if (!string.IsNullOrEmpty(row.name) && row.name != item.name) { item.name = row.name; changed = true; }
            if (!string.IsNullOrEmpty(row.description) && row.description != item.description) { item.description = row.description; changed = true; }
            if (!string.IsNullOrEmpty(row.price))
            {
                string digits = row.price.Replace(".", "").Replace(",", "").Replace(" ", "");
                if (!int.TryParse(digits, out int price) || price < 0 || price == 0 && !item.freeAllowed)
                    report.errors.Add($"{row.place} ({row.id}): giá \"{row.price}\" không hợp lệ, phải là số " +
                        (item.freeAllowed ? "từ 0 trở lên (0 = tặng miễn phí)." : "lớn hơn 0."));
                else if (price != item.price) { item.price = price; changed = true; }
            }
            if (changed) { item.changed = true; report.changed++; }
        }
        return report;
    }

    // Called by the catalog builder after it rebuilds the offers.
    public static void ApplyIfPresent(IReadOnlyList<AquariumDecorOffer> offers)
    {
        if (!File.Exists(CsvPath)) return;
        var report = Apply(offers, File.ReadAllText(CsvPath));
        Debug.Log("[Decor Catalog] " + CsvPath + ": " + report);
    }

    // Read-only facts shown beside the editable columns; Pull ignores their columns.
    public sealed class Details { public string image, size; public int points; }
    private const string ImageHeader = "ảnh";
    private static readonly string[] DecorViewHeaders = { "kích thước (R × C × S)", "điểm" };

    // A portrait uploaded by Tools/upload_sheet_images_r2.sh to sheet-images/<folder>/; ?v= changes with the picture.
    public static string ImageUrl(string path, string folder)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        using var md5 = System.Security.Cryptography.MD5.Create();
        string version = BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").Substring(0, 8).ToLowerInvariant();
        return $"{RemoteContent.BaseUrl}/sheet-images/{folder}/{Path.GetFileName(path)}?v={version}";
    }

    // Portrait on R2 (Tools/upload_sheet_images_r2.sh), size from the prefab's meshes in game units, capacity points.
    public static Details DetailsOf(AquariumDecorOffer offer)
    {
        var details = new Details();
        if (offer.portrait != null)
        {
            string path = AssetDatabase.GetAssetPath(offer.portrait);
            if (Path.GetDirectoryName(path)?.Replace('\\', '/') == AquariumDecorCatalogBuilder.Root) details.image = ImageUrl(path, "decor");
        }
        if (offer.prefab != null)
        {
            // As AquariumDecoration counts it: at least 1, carpets free.
            details.points = offer.prefab.carpet > 0 ? 0 : Mathf.Max(1, offer.prefab.points);
            var root = offer.prefab.transform.worldToLocalMatrix;
            Bounds? bounds = null;
            foreach (var filter in offer.prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var local = filter.sharedMesh.bounds; var matrix = root * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (bounds == null) bounds = new Bounds(point, Vector3.zero);
                    else { var grown = bounds.Value; grown.Encapsulate(point); bounds = grown; }
                }
            }
            if (bounds != null)
            {
                var size = bounds.Value.size;
                details.size = string.Format(CultureInfo.InvariantCulture, "{0:0.00} × {1:0.00} × {2:0.00}", size.x, size.y, size.z);
            }
        }
        return details;
    }

    // One tab per group in shop tab order, with the catalog's order inside each tab.
    public static List<SheetTab> DecorTabs(IReadOnlyList<AquariumDecorOffer> offers, Func<AquariumDecorOffer, Details> details = null)
    {
        var tabs = new List<SheetTab>();
        foreach (var category in TabOrder)
        {
            var tab = new SheetTab { name = CategoryLabel(category), viewHeaders = DecorViewHeaders };
            foreach (var offer in offers)
            {
                if (offer == null || offer.category != category) continue;
                var info = details?.Invoke(offer);
                tab.rows.Add(new SheetRow { image = info?.image, view = new[] { info?.size, info?.points.ToString() },
                    item = new SheetItem { id = offer.id, name = offer.displayName, description = offer.description, price = offer.price } });
            }
            if (tab.rows.Count > 0) tabs.Add(tab);
        }
        return tabs;
    }

    public static byte[] ExportXlsx(IReadOnlyList<AquariumDecorOffer> offers, Func<AquariumDecorOffer, Details> details = null) =>
        ExportXlsx(DecorTabs(offers, details));

    // Editable columns follow the id and picture; the grey ones after giá are for reading only.
    public static byte[] ExportXlsx(IReadOnlyList<SheetTab> groups)
    {
        // Styles (styles.xml): 1 header, 2 grey header, 3 grey read-only cell, 4 editable cell.
        string Cell(string column, int row, string text, int style) =>
            $"<c r=\"{column}{row}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(text ?? "")}</t></is></c>";
        string NumberCell(string column, int row, int value, int style) => $"<c r=\"{column}{row}\" s=\"{style}\"><v>{value}</v></c>";
        // Read-only columns start at F, one per header of the tab.
        string ViewColumn(int index) => ((char)('F' + index)).ToString();
        string ViewCell(string column, int row, string text) =>
            int.TryParse(text, out int number) ? NumberCell(column, row, number, 3) : Cell(column, row, text, 3);
        const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        // Fill colours of swatch cells, each its own style after the five fixed ones.
        var swatches = new List<string>();
        int Swatch(string hex)
        {
            int index = swatches.IndexOf(hex);
            if (index < 0) { index = swatches.Count; swatches.Add(hex); }
            return 5 + index;
        }
        // A plain table: bold header, editable cells, numbers as numbers.
        string PlainSheet(SheetTab tab)
        {
            var table = tab.table;
            var rows = new StringBuilder();
            bool pictures = table.Any(row => row.Any(text => text != null && text.StartsWith("=IMAGE(")));
            for (int n = 0; n < table.Count; n++)
                rows.Append($"<row r=\"{n + 1}\"{(n > 0 && pictures ? " ht=\"66\" customHeight=\"1\"" : "")}>" + string.Concat(table[n].Select((text, k) =>
                {
                    string column = ((char)('A' + k)).ToString();
                    bool grey = k > 0 && k < tab.keyColumns || n > 0 && n <= tab.greyRows;
                    if (n == 0) return Cell(column, 1, text, grey ? 2 : 1);
                    int style = grey ? 3 : 4;
                    if (text != null && text.StartsWith("=IMAGE(")) return $"<c r=\"{column}{n + 1}\" s=\"3\"><f>{SecurityElement.Escape(text.Substring(1))}</f></c>";
                    if (tab.swatches && text != null && Regex.IsMatch(text, "^#[0-9A-F]{6}$")) style = Swatch(text.Substring(1));
                    return text != null && Regex.IsMatch(text, @"^-?\d+(\.\d+)?$") ? $"<c r=\"{column}{n + 1}\" s=\"{style}\"><v>{text}</v></c>"
                        : Cell(column, n + 1, text, style);
                })) + "</row>");
            int columns = table.Count > 0 ? table[0].Length : 0;
            string pane = tab.keyColumns > 0
                ? $"<pane xSplit=\"{tab.keyColumns}\" ySplit=\"1\" topLeftCell=\"{(char)('A' + tab.keyColumns)}2\" activePane=\"bottomRight\" state=\"frozen\"/>"
                : "<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>";
            return $"<worksheet xmlns=\"{Main}\"><sheetViews><sheetView workbookViewId=\"0\">{pane}</sheetView></sheetViews><cols>" +
                string.Concat(Enumerable.Range(1, columns).Select(c => $"<col min=\"{c}\" max=\"{c}\" width=\"18\" customWidth=\"1\"/>")) +
                $"</cols><sheetData>{rows}</sheetData></worksheet>";
        }
        const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Entry(string path, string xml)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + xml);
            }
            var types = new StringBuilder("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            var sheets = new StringBuilder(); var rels = new StringBuilder();
            for (int i = 1; i <= groups.Count; i++)
            {
                types.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                sheets.Append($"<sheet name=\"{SecurityElement.Escape(groups[i - 1].name)}\" sheetId=\"{i}\" r:id=\"rId{i}\"/>");
                rels.Append($"<Relationship Id=\"rId{i}\" Type=\"{Rel}/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
                var group = groups[i - 1];
                if (group.table != null) { Entry($"xl/worksheets/sheet{i}.xml", PlainSheet(group)); continue; }
                var rows = new StringBuilder($"<row r=\"1\">{Cell("A", 1, Header[0], 1)}{Cell("B", 1, ImageHeader, 2)}" +
                    $"{Cell("C", 1, Header[2], 1)}{Cell("D", 1, Header[3], 1)}{Cell("E", 1, Header[4], 1)}" +
                    string.Concat(group.viewHeaders.Select((h, k) => Cell(ViewColumn(k), 1, h, 2))) + "</row>");
                for (int n = 0; n < group.rows.Count; n++)
                {
                    var row = group.rows[n]; var item = row.item; int r = n + 2;
                    // IMAGE() fits the picture inside its cell, so it follows its row when the sheet is sorted or filtered.
                    string image = string.IsNullOrEmpty(row.image) ? $"<c r=\"B{r}\" s=\"3\"/>"
                        : $"<c r=\"B{r}\" s=\"3\"><f>IMAGE(\"{SecurityElement.Escape(row.image)}\")</f></c>";
                    rows.Append($"<row r=\"{r}\" ht=\"66\" customHeight=\"1\">{Cell("A", r, item.id, 4)}{image}" +
                        $"{Cell("C", r, item.name, 4)}{Cell("D", r, item.description, 4)}{NumberCell("E", r, item.price, 4)}" +
                        string.Concat(group.viewHeaders.Select((_, k) => ViewCell(ViewColumn(k), r, row.view != null && k < row.view.Length ? row.view[k] : null))) + "</row>");
                }
                // The header row and the id column stay in view while scrolling.
                Entry($"xl/worksheets/sheet{i}.xml", $"<worksheet xmlns=\"{Main}\"><sheetViews><sheetView workbookViewId=\"0\">" +
                    "<pane xSplit=\"1\" ySplit=\"1\" topLeftCell=\"B2\" activePane=\"bottomRight\" state=\"frozen\"/></sheetView></sheetViews><cols>" +
                    "<col min=\"1\" max=\"1\" width=\"22\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"16\" customWidth=\"1\"/>" +
                    "<col min=\"3\" max=\"3\" width=\"28\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"60\" customWidth=\"1\"/>" +
                    "<col min=\"5\" max=\"5\" width=\"9\" customWidth=\"1\"/>" +
                    string.Concat(group.viewHeaders.Select((h, k) => $"<col min=\"{6 + k}\" max=\"{6 + k}\" width=\"{Math.Max(8, h.Length + 4)}\" customWidth=\"1\"/>")) + "</cols>" +
                    $"<sheetData>{rows}</sheetData></worksheet>");
            }
            Entry("[Content_Types].xml", types + "</Types>");
            Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                $"<Relationship Id=\"rId1\" Type=\"{Rel}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Entry("xl/workbook.xml", $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Rel}\"><sheets>{sheets}</sheets></workbook>");
            rels.Append($"<Relationship Id=\"rIdStyles\" Type=\"{Rel}/styles\" Target=\"styles.xml\"/>");
            Entry("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{rels}</Relationships>");
            // A swatch's text is white on dark colours.
            bool Dark(string hex) => Enumerable.Range(0, 3).Select(k => Convert.ToInt32(hex.Substring(k * 2, 2), 16))
                .Zip(new[] { 0.299, 0.587, 0.114 }, (c, w) => c * w).Sum() < 128;
            Entry("xl/styles.xml", $"<styleSheet xmlns=\"{Main}\">" +
                "<fonts count=\"4\"><font><sz val=\"11\"/><name val=\"Arial\"/></font><font><b/><sz val=\"11\"/><name val=\"Arial\"/></font>" +
                "<font><sz val=\"11\"/><color rgb=\"FF707070\"/><name val=\"Arial\"/></font>" +
                "<font><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Arial\"/></font></fonts>" +
                $"<fills count=\"{3 + swatches.Count}\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE3E3E3\"/></patternFill></fill>" +
                string.Concat(swatches.Select(hex => $"<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF{hex}\"/></patternFill></fill>")) + "</fills>" +
                $"<borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"{5 + swatches.Count}\"><xf/>" +
                "<xf fontId=\"1\" applyFont=\"1\"/><xf fontId=\"1\" fillId=\"2\" applyFont=\"1\" applyFill=\"1\"/>" +
                "<xf fontId=\"2\" fillId=\"2\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\"/></xf>" +
                "<xf applyAlignment=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" +
                string.Concat(swatches.Select((hex, k) => $"<xf fontId=\"{(Dark(hex) ? 3 : 0)}\" fillId=\"{3 + k}\" applyFont=\"1\" applyFill=\"1\" " +
                    "applyAlignment=\"1\"><alignment vertical=\"center\"/></xf>")) + "</cellXfs></styleSheet>");
        }
        return stream.ToArray();
    }

    // Every tab of a workbook as text rows. Google writes numbers as "60.0"; whole numbers come back as "60".
    public static List<(string name, List<string[]> table)> ReadXlsx(byte[] data)
    {
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        XDocument Load(string path)
        {
            var entry = zip.GetEntry(path);
            if (entry == null) return null;
            using var stream = entry.Open();
            return XDocument.Load(stream);
        }
        var strings = new List<string>();
        var shared = Load("xl/sharedStrings.xml");
        if (shared != null)
            foreach (var item in shared.Root.Elements(main + "si"))
                strings.Add(string.Concat(item.Descendants(main + "t").Select(t => t.Value)));
        var targets = Load("xl/_rels/workbook.xml.rels").Root.Elements(packageRel + "Relationship")
            .ToDictionary(r => (string)r.Attribute("Id"), r => ((string)r.Attribute("Target")).TrimStart('/'));
        var tabs = new List<(string, List<string[]>)>();
        foreach (var sheet in Load("xl/workbook.xml").Root.Element(main + "sheets").Elements(main + "sheet"))
        {
            string target = targets[(string)sheet.Attribute(rel + "id")];
            var document = Load(target.StartsWith("xl/") ? target : "xl/" + target);
            var table = new List<string[]>();
            foreach (var row in document.Root.Element(main + "sheetData").Elements(main + "row"))
            {
                int number = (int?)row.Attribute("r") ?? table.Count + 1;
                while (table.Count < number - 1) table.Add(new string[0]);
                var cells = new List<string>();
                foreach (var cell in row.Elements(main + "c"))
                {
                    string reference = (string)cell.Attribute("r");
                    int column = cells.Count;
                    if (reference != null) { column = 0; foreach (char c in reference) if (char.IsLetter(c)) column = column * 26 + (c - 'A' + 1); column--; }
                    while (cells.Count < column) cells.Add("");
                    string type = (string)cell.Attribute("t"), value = (string)cell.Element(main + "v");
                    cells.Add(type == "s" ? strings[int.Parse(value)]
                        : type == "inlineStr" ? string.Concat(cell.Descendants(main + "t").Select(t => t.Value))
                        : type == null || type == "n" ? Number(value)
                        : value ?? "");
                }
                table.Add(cells.ToArray());
            }
            tabs.Add(((string)sheet.Attribute("name"), table));
        }
        return tabs;
    }

    private static string Number(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && number == Math.Floor(number) && Math.Abs(number) < 1e15
            ? ((long)number).ToString(CultureInfo.InvariantCulture) : value ?? "";

    private static AquariumDecorCatalog LoadCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>(CatalogPath);
        if (catalog == null) EditorUtility.DisplayDialog("Decor Catalog", "Chưa có " + CatalogPath + ". Chạy Lumaquarium > UI > Build Decoration Shop trước.", "OK");
        return catalog;
    }

    [MenuItem("Lumaquarium/Decor Catalog/Export Excel (tab theo nhóm)")]
    public static void ExportExcel()
    {
        // Importing the file replaces the whole Google Sheet, so edits made there must come into the catalog first.
        if (ExportUrl(SheetUrl) != null)
        {
            int choice = EditorUtility.DisplayDialogComplex("Decor Catalog",
                "Import file Excel với Replace spreadsheet sẽ ghi đè toàn bộ Google Sheet.\n\nPull các chỉnh sửa trên sheet về trước khi Export?",
                "Pull rồi Export", "Huỷ", "Export luôn");
            if (choice == 1 || choice == 0 && !TryPull()) return;
        }
        var catalog = LoadCatalog();
        if (catalog == null) return;
        var surfaces = SurfaceCatalogSheet.Load();
        var tabs = DecorTabs(catalog.offers, DetailsOf);
        tabs.InsertRange(0, FishLookSheet.Tabs());
        tabs.Insert(0, FishCatalogSheet.Tab());
        GiftSheet.EnsureStarter(catalog);
        tabs.Insert(0, GiftSheet.Tab(catalog.giftPacks));
        if (surfaces != null) { SurfaceCatalogSheet.EnsureTanks(surfaces); tabs.AddRange(SurfaceCatalogSheet.Tabs(surfaces)); }
        File.WriteAllBytes(XlsxPath, ExportXlsx(tabs));
        AssetDatabase.ImportAsset(XlsxPath);
        Debug.Log($"[Decor Catalog] Exported {catalog.offers.Length} items to {XlsxPath}");
        EditorUtility.RevealInFinder(XlsxPath);
    }

    [MenuItem("Lumaquarium/Decor Catalog/Export CSV")]
    public static void ExportCsv()
    {
        var catalog = LoadCatalog();
        if (catalog == null) return;
        if (File.Exists(CsvPath) && !EditorUtility.DisplayDialog("Decor Catalog",
                "Ghi đè " + CsvPath + " bằng dữ liệu hiện có trong catalog?", "Ghi đè", "Huỷ")) return;
        WriteCsv(catalog);
        Debug.Log($"[Decor Catalog] Exported {catalog.offers.Length} items to {CsvPath}");
        EditorUtility.RevealInFinder(CsvPath);
    }

    // The BOM lets Excel read the Vietnamese text as UTF-8.
    private static void WriteCsv(AquariumDecorCatalog catalog)
    {
        File.WriteAllText(CsvPath, Export(catalog.offers), new UTF8Encoding(true));
        AssetDatabase.ImportAsset(CsvPath);
    }

    [MenuItem("Lumaquarium/Decor Catalog/Open CSV")]
    public static void OpenCsv()
    {
        if (!File.Exists(CsvPath)) ExportCsv();
        if (File.Exists(CsvPath)) EditorUtility.OpenWithDefaultApp(CsvPath);
    }

    [MenuItem("Lumaquarium/Decor Catalog/Apply CSV")]
    public static void ApplyCsv()
    {
        if (!File.Exists(CsvPath)) { EditorUtility.DisplayDialog("Decor Catalog", "Chưa có " + CsvPath + ". Chạy Export CSV trước.", "OK"); return; }
        ApplyTabs(new[] { ("", ParseCsv(File.ReadAllText(CsvPath))) });
    }

    private static AquariumDecorCatalog ApplyTabs(IEnumerable<(string, List<string[]>)> tabs)
    {
        var catalog = LoadCatalog();
        if (catalog == null) return null;
        Undo.RecordObject(catalog, "Apply Decor Catalog");
        var report = Apply(catalog.offers, tabs);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[Decor Catalog] " + report);
        EditorUtility.DisplayDialog("Decor Catalog", report.ToString(), "OK");
        return catalog;
    }

    // The whole workbook (every tab) as .xlsx. Accepts the sheet's normal link (shared "Anyone with the link")
    // or a "Publish to web" link.
    public static string ExportUrl(string url)
    {
        url = (url ?? "").Trim();
        var published = Regex.Match(url, @"^(https://docs\.google\.com/spreadsheets/d/e/[\w-]+)/");
        if (published.Success) return published.Groups[1].Value + "/pub?output=xlsx";
        var sheet = Regex.Match(url, @"^https://docs\.google\.com/spreadsheets/d/([\w-]+)");
        return sheet.Success ? $"https://docs.google.com/spreadsheets/d/{sheet.Groups[1].Value}/export?format=xlsx" : null;
    }

    [MenuItem("Lumaquarium/Decor Catalog/Pull from Google Sheet")]
    public static void PullFromSheet() => TryPull();

    // False when nothing was applied: no link, cancelled, or the download failed.
    public static bool TryPull()
    {
        string url = ExportUrl(SheetUrl);
        if (url == null) { DecorCatalogSheetWindow.Open(); return false; }
        using var request = UnityWebRequest.Get(url);
        var operation = request.SendWebRequest();
        try
        {
            while (!operation.isDone)
                if (EditorUtility.DisplayCancelableProgressBar("Decor Catalog", "Đang tải Google Sheet…", request.downloadProgress))
                { request.Abort(); return false; }
        }
        finally { EditorUtility.ClearProgressBar(); }
        byte[] data = request.downloadHandler.data;
        // An .xlsx is a zip file ("PK"); a sign-in page comes back as HTML.
        if (request.result != UnityWebRequest.Result.Success || data == null || data.Length < 2 || data[0] != 'P' || data[1] != 'K')
        {
            EditorUtility.DisplayDialog("Decor Catalog", "Không tải được Google Sheet (" + (request.error ?? "nhận về trang web thay vì bảng tính") +
                ").\n\nKiểm tra Share > General access = \"Anyone with the link\" (Viewer), hoặc dùng File > Share > Publish to web.", "OK");
            return false;
        }
        var catalog = LoadCatalog();
        if (catalog == null) return false;
        var decor = DecorItems(catalog.offers); var fish = FishCatalogSheet.Items();
        var surfaces = SurfaceCatalogSheet.Load();
        if (surfaces != null) SurfaceCatalogSheet.EnsureTanks(surfaces);
        var surfaceItems = surfaces != null ? SurfaceCatalogSheet.Items(surfaces) : new List<SheetItem>();
        var workbook = ReadXlsx(data);
        var report = Apply(decor.Concat(fish).Concat(surfaceItems).ToList(), workbook);
        Undo.RecordObject(catalog, "Apply Decor Catalog");
        WriteBack(catalog.offers, decor);
        // A sheet without the Gói quà tab (made before it) leaves the packs as they are.
        var giftTable = workbook.FirstOrDefault(t => GiftSheet.IsGiftTable(t.table)).table;
        if (giftTable != null)
        {
            var packs = GiftSheet.Read(giftTable, GiftSheet.Slots(catalog, surfaces, fish), report.errors);
            if (packs != null) catalog.giftPacks = packs;
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        report.errors.AddRange(FishCatalogSheet.Save(fish));
        // After the shop text, which is written into the same json files.
        report.changed += FishLookSheet.Save(workbook, report.errors);
        if (surfaces != null) SurfaceCatalogSheet.Save(surfaces, surfaceItems);
        Debug.Log("[Decor Catalog] " + report);
        EditorUtility.DisplayDialog("Decor Catalog", report.ToString(), "OK");
        // Keep a local copy so the builder and Apply CSV use the same values.
        WriteCsv(catalog);
        // The server sells at the new prices once deployed (Tools/upload_content_r2.sh does it before the upload).
        ServerCatalogWriter.Write();
        return true;
    }
}

public sealed class DecorCatalogSheetWindow : EditorWindow
{
    private string url;

    [MenuItem("Lumaquarium/Decor Catalog/Google Sheet Settings…")]
    public static void Open() => GetWindow<DecorCatalogSheetWindow>(true, "Decor Catalog", true).minSize = new Vector2(480, 250);

    private void OnEnable() => url = DecorCatalogSheet.SheetUrl;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bảng giá đồ trang trí", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("1. Export Excel, rồi trong Google Sheets: File > Import > Upload file .xlsx > Replace spreadsheet.\n" +
            "2. Share > General access: Anyone with the link (Viewer).\n3. Dán link sheet vào đây, bấm Pull.\n" +
            "Mỗi nhóm một tab; món nằm ở tab nào cũng được, miễn còn cột id. Cột xám chỉ để xem. Tab Cá: giá 0 = tặng miễn phí.\n" +
            "Tab Cá – hình dáng/màu/bề mặt/vây: mỗi hàng một con cá; ô trống = mặc định (hàng xám); màu dạng #RRGGBB. Pull dựng lại các cá đã đổi.\n" +
            "Tab Gói quà: mỗi người chơi nhận mỗi gói một lần; starter_v1 là quà của người chơi mới, id xu = số xu.\n" +
            "Ảnh đổi thì chạy Tools/upload_sheet_images_r2.sh trước khi Export.", MessageType.Info);
        url = EditorGUILayout.TextField("Link Google Sheet", url);
        if (!string.IsNullOrWhiteSpace(url) && DecorCatalogSheet.ExportUrl(url) == null)
            EditorGUILayout.HelpBox("Link không phải Google Sheet.", MessageType.Warning);
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Export Excel (tab theo nhóm)")) DecorCatalogSheet.ExportExcel();
            if (GUILayout.Button("Export CSV")) DecorCatalogSheet.ExportCsv();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Mở CSV")) DecorCatalogSheet.OpenCsv();
            if (GUILayout.Button("Apply CSV")) DecorCatalogSheet.ApplyCsv();
        }
        using (new EditorGUI.DisabledScope(DecorCatalogSheet.ExportUrl(url) == null))
            if (GUILayout.Button("Pull from Google Sheet", GUILayout.Height(28)))
            {
                DecorCatalogSheet.SheetUrl = url;
                DecorCatalogSheet.PullFromSheet();
            }
    }

    private void OnDisable() => DecorCatalogSheet.SheetUrl = url;
}
