using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Lumaquarium;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Every catalog name and description needs an English line in Resources/Localization/en.txt, so renaming an
// item in the price sheet fails here instead of showing Vietnamese in the English game.
public sealed class LocalizationTests
{
    private static Dictionary<string, string> Table()
    {
        var asset = Resources.Load<TextAsset>(Loc.TablePath);
        Assert.IsNotNull(asset, Loc.TablePath);
        return Loc.Parse(asset.text);
    }

    private static void AssertTranslated(IEnumerable<string> texts)
    {
        var table = Table();
        var missing = texts.Where(t => !string.IsNullOrEmpty(t) && Regex.IsMatch(t, "[À-ỹĐđ]") && !table.ContainsKey(t)).Distinct().ToList();
        Assert.IsEmpty(missing, "No English in en.txt for:\n" + string.Join("\n", missing));
    }

    [Test]
    public void ParseSkipsCommentsAndReadsLineBreaks()
    {
        var table = Loc.Parse("# note\n\nMột\\nHai\tOne\\nTwo\r\nkhông có tab\n");
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("One\nTwo", table["Một\nHai"]);
    }

    [Test]
    public void GroupedShopLabelsAreTranslated()
    {
        AssertTranslated(new[] { "Tiểu cảnh", "Hồ", "Nền hồ", "Rêu & bèo", "Tìm cá...", "Tìm tiểu cảnh...", "Tìm hồ, thành, nền..." });
    }

    [Test]
    public void FormatsKeepTheirPlaceholders()
    {
        var wrong = Table().Where(p => Placeholders(p.Key) != Placeholders(p.Value)).Select(p => p.Key).ToList();
        Assert.IsEmpty(wrong, string.Join("\n", wrong));
    }

    private static string Placeholders(string text) =>
        string.Join(",", Regex.Matches(text, @"\{\d+[^}]*\}").Select(m => m.Value).OrderBy(v => v));

    [Test]
    public void DecorCatalogIsTranslated()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>(AquariumDecorCatalogBuilder.Root + "/AquariumDecorCatalog.asset");
        Assert.IsNotNull(catalog);
        AssertTranslated(catalog.offers.Where(o => o != null).SelectMany(o => new[] { o.displayName, o.description }));
    }

    [Test]
    public void SurfaceCatalogIsTranslated()
    {
        var catalog = SurfaceCatalogSheet.Load();
        Assert.IsNotNull(catalog);
        AssertTranslated(catalog.walls.SelectMany(w => new[] { w.displayName, w.description })
            .Concat(catalog.substrates.SelectMany(s => new[] { s.displayName, s.description }))
            .Concat(catalog.tanks.SelectMany(t => new[] { t.displayName, t.description }))
            .Concat(AquariumTank.Specs.SelectMany(t => new[] { t.displayName, t.description })));
    }
}
