using NUnit.Framework;

public sealed class DecorCatalogSheetTests
{
    private static AquariumDecorOffer[] Offers() => new[]
    {
        new AquariumDecorOffer { id = "hornwort", displayName = "Rong đuôi chó", description = "Cây, lá mềm", price = 60 },
        new AquariumDecorOffer { id = "seiryu", displayName = "Đá vân Seiryu", description = "Đá", price = 160, category = AquariumDecorCategory.Rocks },
    };

    [Test]
    public void ExportThenApplyChangesNothing()
    {
        var offers = Offers();
        var report = DecorCatalogSheet.Apply(offers, DecorCatalogSheet.Export(offers));
        Assert.AreEqual(0, report.changed);
        Assert.IsEmpty(report.errors);
        Assert.AreEqual("Cây, lá mềm", offers[0].description);
    }

    [Test]
    public void QuotedFieldsKeepCommasQuotesAndLineBreaks()
    {
        var rows = DecorCatalogSheet.ParseCsv("id,tên\r\na,\"x, \"\"y\"\"\nz\"\r\n");
        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual("x, \"y\"\nz", rows[1][1]);
    }

    [Test]
    public void ApplyUpdatesByIdAndKeepsEmptyCells()
    {
        var offers = Offers();
        var report = DecorCatalogSheet.Apply(offers, "giá,id,tên,mô tả\n1.200,seiryu,,Đá mới\n75,unknown,X,\n");
        Assert.AreEqual(1, report.changed);
        Assert.AreEqual(1200, offers[1].price);
        Assert.AreEqual("Đá vân Seiryu", offers[1].displayName);
        Assert.AreEqual("Đá mới", offers[1].description);
        CollectionAssert.AreEqual(new[] { "unknown" }, report.unknown);
    }

    [Test]
    public void BadPriceKeepsTheOldOne()
    {
        var offers = Offers();
        var report = DecorCatalogSheet.Apply(offers, "id,giá\nhornwort,abc\nhornwort,0\n");
        Assert.AreEqual(60, offers[0].price);
        Assert.AreEqual(2, report.errors.Count);
    }

    [Test]
    public void ExcelHasOneTabPerGroupAndReadsBack()
    {
        var tabs = DecorCatalogSheet.ReadXlsx(DecorCatalogSheet.ExportXlsx(Offers()));
        CollectionAssert.AreEqual(new[] { "Đá", "Cây" }, tabs.ConvertAll(t => t.name));
        CollectionAssert.AreEqual(new[] { "id", "ảnh", "tên", "mô tả", "giá", "kích thước (R × C × S)", "điểm" }, tabs[1].table[0]);
        CollectionAssert.AreEqual(new[] { "hornwort", "", "Rong đuôi chó", "Cây, lá mềm", "60", "", "" }, tabs[1].table[1]);
        var offers = Offers();
        Assert.AreEqual(0, DecorCatalogSheet.Apply(offers, tabs).changed);
    }

    [Test]
    public void ReadOnlyColumnsAreShownButNeverApplied()
    {
        var details = new DecorCatalogSheet.Details { image = "https://x.test/a.png?v=1", size = "0.10 × 0.20 × 0.30", points = 3 };
        byte[] data = DecorCatalogSheet.ExportXlsx(Offers(), _ => details);
        using (var zip = new System.IO.Compression.ZipArchive(new System.IO.MemoryStream(data)))
        using (var reader = new System.IO.StreamReader(zip.GetEntry("xl/worksheets/sheet2.xml").Open()))
            StringAssert.Contains("<f>IMAGE(\"https://x.test/a.png?v=1\")</f>", reader.ReadToEnd());
        var tabs = DecorCatalogSheet.ReadXlsx(data);
        CollectionAssert.AreEqual(new[] { "0.10 × 0.20 × 0.30", "3" }, new[] { tabs[1].table[1][5], tabs[1].table[1][6] });
        tabs[1].table[1][5] = "9 × 9 × 9"; tabs[1].table[1][6] = "99";
        var offers = Offers();
        Assert.AreEqual(0, DecorCatalogSheet.Apply(offers, tabs).changed);
    }

    [Test]
    public void TabsMergeSkipNotesAndCatchDuplicates()
    {
        var offers = Offers();
        var tabs = new System.Collections.Generic.List<(string, System.Collections.Generic.List<string[]>)>
        {
            ("Ghi chú", DecorCatalogSheet.ParseCsv("không có cột id\n")),
            ("Đá", DecorCatalogSheet.ParseCsv("id,giá\nseiryu,170\n")),
            ("Cây", DecorCatalogSheet.ParseCsv("id,giá\nhornwort,65\nseiryu,180\n")),
        };
        var report = DecorCatalogSheet.Apply(offers, tabs);
        Assert.AreEqual(2, report.changed);
        Assert.AreEqual(170, offers[1].price);
        Assert.AreEqual(65, offers[0].price);
        StringAssert.Contains("Tab Cây, hàng 3", report.errors[0]);
    }

    [Test]
    public void FishMayBeFreeButDecorMayNot()
    {
        var items = new System.Collections.Generic.List<DecorCatalogSheet.SheetItem>
        {
            new DecorCatalogSheet.SheetItem { id = "plakat", price = 50, freeAllowed = true },
            new DecorCatalogSheet.SheetItem { id = "hornwort", price = 60 },
        };
        var report = DecorCatalogSheet.Apply(items, new[] { ("Cá", DecorCatalogSheet.ParseCsv("id,giá\nplakat,0\nhornwort,0\n")) });
        Assert.AreEqual(0, items[0].price);
        Assert.IsTrue(items[0].changed);
        Assert.AreEqual(60, items[1].price);
        Assert.AreEqual(1, report.errors.Count);
    }

    [Test]
    public void FishJsonKeepsEverythingButTheShopText()
    {
        string json = "{\n  \"build\": {\"price\": 7},\n  \"shop\": {\n    \"displayName\": \"Old\",\n    \"inShop\": true,\n" +
            "    \"description\": \"a \\\"b\\\"\",\n    \"price\": 0\n  },\n  \"look\": {\"seed\": 3}\n}";
        var item = new DecorCatalogSheet.SheetItem { name = "Cá \"Mới\"", description = "Đỏ, cam", price = 120 };
        string result = FishCatalogSheet.SetShopFields(json, item);
        Assert.AreEqual(json.Replace("\"Old\"", "\"Cá \\\"Mới\\\"\"").Replace("\"a \\\"b\\\"\"", "\"Đỏ, cam\"")
            .Replace("\"price\": 0", "\"price\": 120"), result);
        StringAssert.Contains("\"build\": {\"price\": 7}", result);
    }

    [Test]
    public void TabsMayHaveAnyNumberOfReadOnlyColumns()
    {
        var walls = new DecorCatalogSheet.SheetTab { name = "Thành hồ", viewHeaders = new string[0] };
        walls.rows.Add(new DecorCatalogSheet.SheetRow { item = new DecorCatalogSheet.SheetItem { id = "wall_brick", name = "Gạch", description = "d", price = 350 } });
        var tanks = new DecorCatalogSheet.SheetTab { name = "Hồ", viewHeaders = new[] { "a", "b", "c" } };
        tanks.rows.Add(new DecorCatalogSheet.SheetRow { item = new DecorCatalogSheet.SheetItem { id = "tank_deep", name = "Hồ sâu", description = "d", price = 1500 },
            view = new[] { "x", "40" } });
        var tabs = DecorCatalogSheet.ReadXlsx(DecorCatalogSheet.ExportXlsx(new[] { walls, tanks }));
        CollectionAssert.AreEqual(new[] { "id", "ảnh", "tên", "mô tả", "giá" }, tabs[0].table[0]);
        CollectionAssert.AreEqual(new[] { "tank_deep", "", "Hồ sâu", "d", "1500", "x", "40", "" }, tabs[1].table[1]);
    }

    [Test]
    public void TankCatalogOverridesBuiltInText()
    {
        var spec = AquariumTank.Find("tank_deep");
        var saved = (spec.displayName, spec.description, spec.price);
        var catalog = UnityEngine.ScriptableObject.CreateInstance<AquariumSurfaceCatalog>();
        try
        {
            catalog.tanks = new[] { new AquariumTankOffer { id = "tank_deep", displayName = "Hồ sâu lớn", description = "", price = 1800 } };
            AquariumTank.ApplyCatalog(catalog);
            Assert.AreEqual(("Hồ sâu lớn", saved.description, 1800), (spec.displayName, spec.description, spec.price));
        }
        finally
        {
            (spec.displayName, spec.description, spec.price) = saved;
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> Slots = new System.Collections.Generic.Dictionary<string, string>
    {
        ["hornwort"] = "", ["plakat"] = "fish", ["wall_river"] = "wall", ["wall_gold"] = "wall", ["tank_square"] = "tank"
    };

    [Test]
    public void GiftPacksGoToTheSheetAndComeBack()
    {
        var packs = new[]
        {
            new AquariumGiftPack { id = "starter_v1", gifts = new[] { new AquariumGift { id = "xu", count = 1250 },
                new AquariumGift { id = "plakat", count = 1, slot = "fish" }, new AquariumGift { id = "hornwort", count = 2 } } },
            new AquariumGiftPack { id = "tet_2027", gifts = new[] { new AquariumGift { id = "xu", count = 500 } } },
        };
        var tabs = DecorCatalogSheet.ReadXlsx(DecorCatalogSheet.ExportXlsx(new[] { GiftSheet.Tab(packs) }));
        CollectionAssert.AreEqual(new[] { "gói", "id", "số lượng", "dùng ngay" }, tabs[0].table[0]);
        CollectionAssert.AreEqual(new[] { "starter_v1", "plakat", "1", "x" }, tabs[0].table[2]);
        var errors = new System.Collections.Generic.List<string>();
        var read = GiftSheet.Read(tabs[0].table, Slots, errors);
        Assert.IsEmpty(errors);
        Assert.AreEqual(2, read.Length);
        Assert.AreEqual(("plakat", 1, "fish"), (read[0].gifts[1].id, read[0].gifts[1].count, read[0].gifts[1].slot));
        Assert.AreEqual(("hornwort", ""), (read[0].gifts[2].id, read[0].gifts[2].slot));
        Assert.AreEqual(500, read[1].gifts[0].count);
    }

    [Test]
    public void BadGiftRowsKeepTheOldPacks()
    {
        var table = DecorCatalogSheet.ParseCsv("gói,id,số lượng,dùng ngay\nstarter_v1,xu,1.250,\nstarter_v1,unknown,1,\n" +
            "starter_v1,hornwort,-1,\nstarter_v1,hornwort,1,x\nstarter_v1,wall_river,1,x\nstarter_v1,wall_gold,1,x\n,plakat,1,\n");
        var errors = new System.Collections.Generic.List<string>();
        Assert.IsNull(GiftSheet.Read(table, Slots, errors));
        Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
        StringAssert.Contains("không có món \"unknown\"", errors[0]);
    }

    [Test]
    public void APackListWithoutTheStarterIsRefused()
    {
        var errors = new System.Collections.Generic.List<string>();
        Assert.IsNull(GiftSheet.Read(DecorCatalogSheet.ParseCsv("gói,id,số lượng\ntet_2027,xu,500\n"), Slots, errors));
        StringAssert.Contains("starter_v1", errors[0]);
    }

    [Test]
    public void ItemTabsIgnoreTheGiftTab()
    {
        var offers = Offers();
        var tabs = new System.Collections.Generic.List<(string, System.Collections.Generic.List<string[]>)>
        {
            ("Gói quà", DecorCatalogSheet.ParseCsv("gói,id,số lượng\nstarter_v1,hornwort,5\nstarter_v1,xu,1250\n")),
            ("Cây", DecorCatalogSheet.ParseCsv("id,giá\nhornwort,65\n")),
        };
        var report = DecorCatalogSheet.Apply(offers, tabs);
        Assert.IsEmpty(report.errors);
        Assert.IsEmpty(report.unknown);
        Assert.AreEqual(65, offers[0].price);
    }

    [TestCase("https://docs.google.com/spreadsheets/d/AbC-12_x/edit#gid=42",
        "https://docs.google.com/spreadsheets/d/AbC-12_x/export?format=xlsx")]
    [TestCase("https://docs.google.com/spreadsheets/d/AbC/edit?usp=sharing",
        "https://docs.google.com/spreadsheets/d/AbC/export?format=xlsx")]
    [TestCase("https://docs.google.com/spreadsheets/d/e/2PACX-1v/pubhtml",
        "https://docs.google.com/spreadsheets/d/e/2PACX-1v/pub?output=xlsx")]
    [TestCase("https://example.com/sheet", null)]
    public void SheetLinksBecomeExportLinks(string link, string expected) =>
        Assert.AreEqual(expected, DecorCatalogSheet.ExportUrl(link));
}
