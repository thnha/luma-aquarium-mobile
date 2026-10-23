using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumaquarium.Shop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Checks the shop data as edited in the price sheet (DecorCatalogSheet), so a bad edit fails here instead of in the game.
public sealed class ShopEconomyTests
{
    private const string DecorCatalogPath = AquariumDecorCatalogBuilder.Root + "/AquariumDecorCatalog.asset";

    private static AquariumDecorCatalog Decor()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>(DecorCatalogPath);
        Assert.IsNotNull(catalog, DecorCatalogPath);
        return catalog;
    }

    private static AquariumSurfaceCatalog Surfaces()
    {
        var catalog = SurfaceCatalogSheet.Load();
        Assert.IsNotNull(catalog, SurfaceCatalogSheet.CatalogPath);
        return catalog;
    }

    private static PipelineFish[] Fish() => PipelineFishImporter.All().Where(f => f.inShop).ToArray();

    // The server sells at the prices the shop shows. Pull and every content build write it; by hand:
    // Lumaquarium > Shop > Write Server Catalog.
    [Test]
    public void TheServerCatalogIsUpToDate()
    {
        var expected = ServerCatalogWriter.Build(Decor(), Surfaces(), FishCatalogSheet.Items());
        Assert.AreEqual(expected.version, Decor().serverCatalogVersion, "run Lumaquarium > Shop > Write Server Catalog");
        Assert.IsTrue(File.Exists(ServerCatalogWriter.FullPath), ServerCatalogWriter.FullPath);
        Assert.AreEqual(expected.version, JsonUtility.FromJson<ShopCatalog>(File.ReadAllText(ServerCatalogWriter.FullPath)).version,
            "run Lumaquarium > Shop > Write Server Catalog");
    }

    [Test]
    public void EveryDecorationCanBeSold()
    {
        foreach (var offer in Decor().offers)
        {
            Assert.IsNotNull(offer, "empty slot in the decoration catalog");
            Assert.IsTrue(offer.IsValid, $"{offer.id}: needs an id, a price above 0, a prefab and a portrait");
        }
    }

    // What GiftSheet.Read checks when the sheet is pulled, checked again on the catalog as it ships.
    [Test]
    public void GiftPacksNameRealItems()
    {
        var catalog = Decor();
        var slots = GiftSheet.Slots(catalog, Surfaces(), FishCatalogSheet.Items());
        var errors = new List<string>();
        Assert.IsNotNull(GiftSheet.Read(GiftSheet.Tab(catalog.giftPacks).table, slots, errors), string.Join("\n", errors));
        foreach (var gift in catalog.giftPacks.SelectMany(p => p.gifts))
            if (!string.IsNullOrEmpty(gift.slot)) Assert.AreEqual(slots[gift.id], gift.slot, gift.id);
    }

    [Test]
    public void ANewPlayerStartsWithEverythingInUseAndCanAffordSomething()
    {
        var catalog = Decor();
        var starter = catalog.giftPacks.FirstOrDefault(p => p.id == Lumaquarium.UI.FishShopInventory.StarterPack);
        Assert.IsNotNull(starter, "run Export Excel once to create the starter pack");
        foreach (string slot in new[] { "fish", "tank", "wall", "floor" })
            Assert.IsTrue(starter.gifts.Any(g => g.slot == slot && g.count > 0), $"the starter pack puts no {slot} into use");
        int coins = starter.gifts.Where(g => g.id == Lumaquarium.UI.FishShopInventory.Coins).Sum(g => g.count);
        Assert.GreaterOrEqual(coins, catalog.offers.Min(o => o.price), "starting coins buy no decoration");
    }

    [Test]
    public void SurfacePricesAreNotNegative()
    {
        var catalog = Surfaces();
        Assert.IsNotEmpty(catalog.walls);
        Assert.IsNotEmpty(catalog.substrates);
        foreach (var w in catalog.walls) Assert.GreaterOrEqual(w.price, 0, w.id);
        foreach (var s in catalog.substrates) Assert.GreaterOrEqual(s.price, 0, s.id);
    }

    [Test]
    public void TankOffersMatchTheTankSizes()
    {
        var tanks = Surfaces().tanks;
        CollectionAssert.AreEqual(AquariumTank.Specs.Select(s => s.id), tanks.Select(t => t.id), "run Export Excel to add missing tanks");
        foreach (var t in tanks) Assert.GreaterOrEqual(t.price, 0, t.id);
    }

    [Test]
    public void FishInTheShopHaveIdsAndPrices()
    {
        Assert.IsNotEmpty(Fish());
        foreach (var f in Fish())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(f.shopId), f.name);
            Assert.GreaterOrEqual(f.price, 0, f.shopId);
        }
    }

    // The sheet finds rows by id across every tab, so two things may never share one.
    [Test]
    public void IdsAreUniqueAcrossTheWholeSheet()
    {
        var surfaces = Surfaces();
        var ids = new List<string> { Lumaquarium.UI.FishShopInventory.Coins };
        ids.AddRange(Fish().Select(f => f.shopId));
        ids.AddRange(Decor().offers.Select(o => o.id));
        ids.AddRange(surfaces.walls.Select(w => w.id));
        ids.AddRange(surfaces.substrates.Select(s => s.id));
        ids.AddRange(surfaces.tanks.Select(t => t.id));
        foreach (var id in ids) Assert.IsFalse(string.IsNullOrWhiteSpace(id), "empty id");
        CollectionAssert.IsEmpty(ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key), "repeated ids");
    }
}
