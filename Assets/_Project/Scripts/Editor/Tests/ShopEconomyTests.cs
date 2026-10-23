using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

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

    [Test]
    public void EveryDecorationCanBeSold()
    {
        foreach (var offer in Decor().offers)
        {
            Assert.IsNotNull(offer, "empty slot in the decoration catalog");
            Assert.IsTrue(offer.IsValid, $"{offer.id}: needs an id, a price above 0, a prefab and a portrait");
            Assert.GreaterOrEqual(offer.starterCount, 0, offer.id);
        }
    }

    [Test]
    public void ANewPlayerCanAffordSomething()
    {
        var catalog = Decor();
        Assert.Greater(catalog.startingCoins, 0);
        Assert.GreaterOrEqual(catalog.startingCoins, catalog.offers.Min(o => o.price), "starting coins buy no decoration");
    }

    [Test]
    public void SurfacePricesAreNotNegativeAndTheDefaultsAreFree()
    {
        var catalog = Surfaces();
        Assert.IsNotEmpty(catalog.walls);
        Assert.IsNotEmpty(catalog.substrates);
        // A new player uses the first wall, floor and tank without buying them.
        Assert.AreEqual(0, catalog.walls[0].price, catalog.walls[0].id);
        Assert.AreEqual(0, catalog.substrates[0].price, catalog.substrates[0].id);
        foreach (var w in catalog.walls) Assert.GreaterOrEqual(w.price, 0, w.id);
        foreach (var s in catalog.substrates) Assert.GreaterOrEqual(s.price, 0, s.id);
    }

    [Test]
    public void TankOffersMatchTheTankSizes()
    {
        var tanks = Surfaces().tanks;
        CollectionAssert.AreEqual(AquariumTank.Specs.Select(s => s.id), tanks.Select(t => t.id), "run Export Excel to add missing tanks");
        Assert.AreEqual(0, tanks[0].price, "the first tank is the starter");
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
        var ids = new List<string> { DecorCatalogSheet.StartingCoinsId };
        ids.AddRange(Fish().Select(f => f.shopId));
        ids.AddRange(Decor().offers.Select(o => o.id));
        ids.AddRange(surfaces.walls.Select(w => w.id));
        ids.AddRange(surfaces.substrates.Select(s => s.id));
        ids.AddRange(surfaces.tanks.Select(t => t.id));
        foreach (var id in ids) Assert.IsFalse(string.IsNullOrWhiteSpace(id), "empty id");
        CollectionAssert.IsEmpty(ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key), "repeated ids");
    }
}
