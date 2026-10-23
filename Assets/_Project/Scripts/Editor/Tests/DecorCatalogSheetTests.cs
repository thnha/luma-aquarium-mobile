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
