using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class FishLookSheetTests
{
    private const string Json = "{\n  \"build\": {\"fins\": {\"pectoral\": 1.1}},\n  \"shop\": {\n    \"price\": 0\n  },\n" +
        "  \"model\": {\n    \"size\": 2.4\n  },\n  \"look\": {\n    \"pattern\": \"Koi\",\n    \"primary\": [0.1, 0.25, 0.85],\n" +
        "    \"gloss\": 1,\n    \"finRim\": [0.55, 1.0],\n    \"seed\": 3\n  }\n}";

    private static List<FishLookSheet.Fish> Fish() =>
        new List<FishLookSheet.Fish> { new FishLookSheet.Fish { name = "KoiNemoRig", displayName = "Koi Nemo", json = Json } };

    private static List<(string, List<string[]>)> RoundTrip(List<DecorCatalogSheet.SheetTab> tabs) =>
        DecorCatalogSheet.ReadXlsx(DecorCatalogSheet.ExportXlsx(tabs));

    private static Dictionary<string, string> Files() => new Dictionary<string, string> { ["KoiNemoRig"] = Json };

    // Sets one cell of the fish's row in the tab that has that column.
    private static void Edit(List<(string name, List<string[]> table)> tabs, string key, string value)
    {
        var table = tabs.First(t => t.table[0].Contains(key)).table;
        var row = table.First(r => r[0] == "KoiNemoRig");
        row[System.Array.IndexOf(table[0], key)] = value;
    }

    [Test]
    public void ExportThenPullChangesNothing()
    {
        var errors = new List<string>();
        var files = Files();
        var changed = FishLookSheet.Apply(RoundTrip(FishLookSheet.Tabs(Fish())), files, errors, _ => true);
        CollectionAssert.IsEmpty(changed);
        CollectionAssert.IsEmpty(errors);
        Assert.AreEqual(Json, files["KoiNemoRig"]);
    }

    [Test]
    public void TabsShowTheFileValuesAndLeaveTheRestEmpty()
    {
        var tabs = RoundTrip(FishLookSheet.Tabs(Fish()));
        CollectionAssert.AreEqual(new[] { "Cá – hình dáng", "Cá – màu", "Cá – bề mặt", "Cá – vây" }, tabs.Select(t => t.Item1));
        var colours = tabs[1].Item2;
        Assert.AreEqual(FishLookSheet.DefaultRow, colours[1][0]);
        Assert.AreEqual("#1A40D9", colours[2][System.Array.IndexOf(colours[0], "primary")]);
        Assert.AreEqual("", colours[2][System.Array.IndexOf(colours[0], "secondary")]);
        var fins = tabs[3].Item2;
        Assert.AreEqual("0.55, 1", fins[2][System.Array.IndexOf(fins[0], "finRim")]);
    }

    [Test]
    public void EditsSetAddAndRemoveKeysOnlyInTheirBlock()
    {
        var tabs = RoundTrip(FishLookSheet.Tabs(Fish()));
        Edit(tabs, "primary", "#FF0000");
        Edit(tabs, "size", "3");
        Edit(tabs, "vibrance", "1,2");
        Edit(tabs, "gloss", "");
        var errors = new List<string>();
        var files = Files();
        var changed = FishLookSheet.Apply(tabs, files, errors, _ => true);
        CollectionAssert.IsEmpty(errors);
        CollectionAssert.AreEqual(new[] { "KoiNemoRig" }, changed);
        string json = files["KoiNemoRig"];
        StringAssert.Contains("\"build\": {\"fins\": {\"pectoral\": 1.1}}", json);
        StringAssert.Contains("\"model\": {\n    \"size\": 3\n  }", json);
        StringAssert.Contains("\"primary\": [1, 0, 0]", json);
        StringAssert.Contains("\"vibrance\": 1.2", json);
        StringAssert.DoesNotContain("gloss", json);
        StringAssert.Contains("\"finRim\": [0.55, 1.0]", json);
        var config = UnityEngine.JsonUtility.FromJson<PipelineFishConfig>(json);
        Assert.AreEqual(3f, config.model.size);
        Assert.AreEqual(1f, config.look.gloss);
        Assert.AreEqual(3, config.look.seed);
    }

    [Test]
    public void BadCellsKeepTheOldValue()
    {
        var tabs = RoundTrip(FishLookSheet.Tabs(Fish()));
        Edit(tabs, "pattern", "Nope");
        Edit(tabs, "size", "0");
        Edit(tabs, "gloss", "abc");
        Edit(tabs, "primary", "#12");
        Edit(tabs, "iridescence", "5");
        var errors = new List<string>();
        var files = Files();
        var changed = FishLookSheet.Apply(tabs, files, errors, name => name == "Koi");
        CollectionAssert.IsEmpty(changed);
        Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
        Assert.AreEqual(Json, files["KoiNemoRig"]);
    }

    [Test]
    public void ANewTemplateRowMakesATemplateAndFishMayUseIt()
    {
        var tabs = RoundTrip(FishLookSheet.Tabs(Fish()));
        var shape = tabs[0].Item2;
        var row = new string[shape[0].Length];
        for (int i = 0; i < row.Length; i++) row[i] = "";
        row[0] = "Mẫu:Galaxy";
        row[System.Array.IndexOf(shape[0], "size")] = "2.2";
        shape.Insert(2, row);
        Edit(tabs, "mẫu", "Galaxy");
        Edit(tabs, "size", "");
        var errors = new List<string>();
        var files = Files();
        var changed = FishLookSheet.Apply(tabs, files, errors, _ => true);
        CollectionAssert.IsEmpty(errors);
        CollectionAssert.AreEquivalent(new[] { "mẫu: Galaxy", "KoiNemoRig" }, changed);
        Assert.AreEqual("{\n  \"model\": {\n    \"size\": 2.2\n  }\n}\n", files["mẫu: Galaxy"]);
        StringAssert.StartsWith("{\n  \"base\": \"Galaxy\",\n  \"build\"", files["KoiNemoRig"]);
        var config = PipelineFishConfig.Parse(files["KoiNemoRig"], "KoiNemoRig.json", name => files["mẫu: " + name]);
        Assert.AreEqual(2.2f, config.model.size);
        Assert.AreEqual("Koi", config.look.pattern);

        // The template shows as its own row and the fish's mẫu cell reads back; clearing it drops the key.
        var again = RoundTrip(FishLookSheet.Tabs(new List<FishLookSheet.Fish>
        {
            new FishLookSheet.Fish { name = "KoiNemoRig", displayName = "Koi Nemo", json = files["KoiNemoRig"] },
            new FishLookSheet.Fish { name = "mẫu: Galaxy", displayName = "", json = files["mẫu: Galaxy"] },
        }));
        Assert.AreEqual("mẫu: Galaxy", again[0].Item2[2][0]);
        Edit(again, "mẫu", "");
        FishLookSheet.Apply(again, files, errors, _ => true);
        CollectionAssert.IsEmpty(errors);
        Assert.AreEqual(Json.Replace("\"size\": 2.4", "PLACEHOLDER").Replace("  \"model\": {\n    PLACEHOLDER\n  },\n", "  \"model\": {},\n"),
            files["KoiNemoRig"]);
    }

    [Test]
    public void AFishCannotUseAMissingTemplate()
    {
        var tabs = RoundTrip(FishLookSheet.Tabs(Fish()));
        Edit(tabs, "mẫu", "Nope");
        var errors = new List<string>();
        var files = Files();
        CollectionAssert.IsEmpty(FishLookSheet.Apply(tabs, files, errors, _ => true));
        Assert.AreEqual(1, errors.Count);
    }

    [Test]
    public void AMissingBlockIsAdded()
    {
        string json = FishLookSheet.SetBlock("{\n  \"shop\": {\n    \"price\": 0\n  }\n}", "model",
            new Dictionary<string, string> { ["size"] = "2" });
        Assert.AreEqual("{\n  \"shop\": {\n    \"price\": 0\n  },\n  \"model\": {\n    \"size\": 2\n  }\n}", json);
        Assert.AreEqual(2f, UnityEngine.JsonUtility.FromJson<PipelineFishConfig>(json).model.size);
    }
}
