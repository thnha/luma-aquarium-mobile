using System.Collections.Generic;
using System.Linq;
using Lumaquarium.UI;
using NUnit.Framework;
using UnityEngine;

public sealed class ShopQueryTests
{
    private static List<ShopEntry> Sample()
    {
        var list = new List<ShopEntry>
        {
            new ShopEntry { tab = ShopTab.Fish, name = "Betta Halfmoon", price = 400 },
            new ShopEntry { tab = ShopTab.Wood, name = "Lũa rễ cây", price = 250 },
            new ShopEntry { tab = ShopTab.Wood, name = "Lũa nhánh", price = 280 },
            new ShopEntry { tab = ShopTab.Rocks, name = "Đá núi", price = 180 },
            new ShopEntry { tab = ShopTab.Coral, name = "San hô đỏ", price = 300 },
            new ShopEntry { tab = ShopTab.Walls, name = "Gạch nung", price = 250 },
        };
        for (int i = 0; i < list.Count; i++) { list[i].order = i; list[i].id = "e" + i; }
        return list;
    }

    private static string[] Names(IEnumerable<ShopEntry> entries) => entries.Select(e => e.name).ToArray();

    [Test]
    public void EmptyEntryListFiltersToEmpty() =>
        Assert.IsEmpty(ShopQuery.Filter(new List<ShopEntry>(), ShopTab.All, "", ShopSort.Default));

    [Test]
    public void SearchWithNoMatchIsEmpty() =>
        Assert.IsEmpty(ShopQuery.Filter(Sample(), ShopTab.All, "xyz", ShopSort.Default));

    [TestCase("Lũa", "lua")]
    [TestCase("Đá núi", "da nui")]
    [TestCase("  San hô đỏ ", "san ho do")]
    [TestCase("ĐÁ", "da")]
    [TestCase("Rêu Christmas", "reu christmas")]
    [TestCase(null, "")]
    public void NormalizeDropsVietnameseMarks(string input, string expected) =>
        Assert.AreEqual(expected, ShopQuery.Normalize(input));

    [Test]
    public void AllTabShowsEverythingInDefaultOrder() =>
        CollectionAssert.AreEqual(Names(Sample()), Names(ShopQuery.Filter(Sample(), ShopTab.All, "", ShopSort.Default)));

    [Test]
    public void TabKeepsOnlyItsItems() =>
        CollectionAssert.AreEqual(new[] { "Lũa rễ cây", "Lũa nhánh" }, Names(ShopQuery.Filter(Sample(), ShopTab.Wood, null, ShopSort.Default)));

    [Test]
    public void SearchIgnoresMarksAndStaysInTab()
    {
        CollectionAssert.AreEqual(new[] { "Lũa rễ cây", "Lũa nhánh" }, Names(ShopQuery.Filter(Sample(), ShopTab.All, "lua", ShopSort.Default)));
        CollectionAssert.AreEqual(new[] { "San hô đỏ" }, Names(ShopQuery.Filter(Sample(), ShopTab.All, "SAN HO", ShopSort.Default)));
        Assert.IsEmpty(ShopQuery.Filter(Sample(), ShopTab.Rocks, "lua", ShopSort.Default));
    }

    [Test]
    public void PriceSortIsStable()
    {
        CollectionAssert.AreEqual(new[] { "Đá núi", "Lũa rễ cây", "Gạch nung", "Lũa nhánh", "San hô đỏ", "Betta Halfmoon" },
            Names(ShopQuery.Filter(Sample(), ShopTab.All, "", ShopSort.PriceAscending)));
        CollectionAssert.AreEqual(new[] { "Betta Halfmoon", "San hô đỏ", "Lũa nhánh", "Lũa rễ cây", "Gạch nung", "Đá núi" },
            Names(ShopQuery.Filter(Sample(), ShopTab.All, "", ShopSort.PriceDescending)));
    }

    [TestCase(AquariumDecorCategory.Plants, ShopTab.Plants)]
    [TestCase(AquariumDecorCategory.Wood, ShopTab.Wood)]
    [TestCase(AquariumDecorCategory.Rocks, ShopTab.Rocks)]
    [TestCase(AquariumDecorCategory.Coral, ShopTab.Coral)]
    [TestCase(AquariumDecorCategory.Moss, ShopTab.Moss)]
    [TestCase(AquariumDecorCategory.Floating, ShopTab.Floating)]
    public void DecorCategoryPicksTab(AquariumDecorCategory category, ShopTab tab) =>
        Assert.AreEqual(tab, ShopEntry.TabOf(category));

    [Test]
    public void DecorGroupAllNeverLeaksFishOrAquariumItems()
    {
        CollectionAssert.AreEqual(new[] { "Lũa rễ cây", "Lũa nhánh", "Đá núi", "San hô đỏ" },
            Names(ShopQuery.Filter(Sample(), ShopGroup.Decor, ShopTab.All, "", ShopSort.Default)));
        Assert.IsEmpty(ShopQuery.Filter(Sample(), ShopGroup.Decor, ShopTab.All, "gach", ShopSort.Default));
    }

    [Test]
    public void MossCategoryIncludesFloatingAndKeepsSearchAndPriceOrder()
    {
        var entries = new[]
        {
            new ShopEntry { tab = ShopTab.Moss, name = "Rêu xanh", price = 200, order = 0 },
            new ShopEntry { tab = ShopTab.Floating, name = "Bèo xanh", price = 100, order = 1 },
            new ShopEntry { tab = ShopTab.Plants, name = "Cây xanh", price = 50, order = 2 }
        };
        CollectionAssert.AreEqual(new[] { "Bèo xanh", "Rêu xanh" },
            Names(ShopQuery.Filter(entries, ShopGroup.Decor, ShopTab.Moss, "xanh", ShopSort.PriceAscending)));
        CollectionAssert.AreEqual(new[] { "Bèo xanh" },
            Names(ShopQuery.Filter(entries, ShopGroup.Decor, ShopTab.Floating, "beo", ShopSort.Default)));
    }

    [TestCase(ShopTab.Fish, ShopGroup.Fish)]
    [TestCase(ShopTab.Wood, ShopGroup.Decor)]
    [TestCase(ShopTab.Floating, ShopGroup.Decor)]
    [TestCase(ShopTab.Tanks, ShopGroup.Aquarium)]
    [TestCase(ShopTab.Walls, ShopGroup.Aquarium)]
    [TestCase(ShopTab.Substrates, ShopGroup.Aquarium)]
    public void DirectTabLinksPickTheirGroup(ShopTab tab, ShopGroup group) => Assert.AreEqual(group, ShopQuery.GroupOf(tab));

    [Test]
    public void BuildIncludesTankOffersWithCatalogPrices()
    {
        var surfaces = ScriptableObject.CreateInstance<AquariumSurfaceCatalog>();
        try
        {
            surfaces.tanks = new[] { new AquariumTankOffer { id = "tank_deep", displayName = "Hồ sâu thử", description = "Giá từ catalog", price = 1750 } };
            var tank = ShopEntry.Build(null, null, surfaces).Find(e => e.id == "tank_deep");
            Assert.IsNotNull(tank, "Tank offers must be available in the unified shop.");
            Assert.AreEqual("Hồ sâu thử", tank.name);
            Assert.AreEqual(1750, tank.price);
            Assert.AreEqual("Tank", tank.kind.ToString());
            Assert.AreEqual(3, ShopEntry.Build(null, null, surfaces).Count(e => e.kind == ShopKind.Tank), "A partial catalog must not hide other built-in tank sizes.");
        }
        finally { Object.DestroyImmediate(surfaces); }
    }

    [Test]
    public void MissingTankCatalogKeepsBuiltInSizes()
    {
        CollectionAssert.AreEqual(AquariumTank.Specs.Select(s => s.id),
            ShopEntry.Build(null, null, null).Where(e => e.kind == ShopKind.Tank).Select(e => e.id));
    }

    [Test]
    public void EmptyTankCatalogKeepsBuiltInSizes()
    {
        var surfaces = ScriptableObject.CreateInstance<AquariumSurfaceCatalog>();
        try
        {
            CollectionAssert.AreEqual(AquariumTank.Specs.Select(s => s.id),
                ShopEntry.Build(null, null, surfaces).Where(e => e.kind == ShopKind.Tank).Select(e => e.id));
        }
        finally { Object.DestroyImmediate(surfaces); }
    }

    [Test]
    public void BuildListsWallsThenFloors()
    {
        var surfaces = ScriptableObject.CreateInstance<AquariumSurfaceCatalog>();
        try
        {
            surfaces.walls = new[] { new AquariumWallStyle { id = "w0", displayName = "Đá sỏi" }, new AquariumWallStyle { id = "w1", displayName = "Gạch", price = 350 } };
            surfaces.substrates = new[] { new AquariumSubstrateStyle { id = "s0", displayName = "Cát vàng" } };
            var entries = ShopEntry.Build(null, null, surfaces).Where(e => e.kind != ShopKind.Tank).ToList();
            CollectionAssert.AreEqual(new[] { "w0", "w1", "s0" }, entries.Select(e => e.id).ToArray());
            CollectionAssert.AreEqual(new[] { ShopTab.Walls, ShopTab.Walls, ShopTab.Substrates }, entries.Select(e => e.tab).ToArray());
            CollectionAssert.AreEqual(new[] { ShopKind.Wall, ShopKind.Wall, ShopKind.Substrate }, entries.Select(e => e.kind).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, entries.Select(e => e.order).ToArray());
            Assert.AreEqual(1, entries[1].source);
            Assert.AreEqual(350, entries[1].price);
        }
        finally { Object.DestroyImmediate(surfaces); }
    }
}
