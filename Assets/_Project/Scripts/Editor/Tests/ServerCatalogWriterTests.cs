using System.Collections.Generic;
using Lumaquarium.Shop;
using NUnit.Framework;
using UnityEngine;

public sealed class ServerCatalogWriterTests
{
    private readonly List<Object> made = new List<Object>();

    private AquariumDecorCatalog Decor(int stonePrice)
    {
        var decor = ScriptableObject.CreateInstance<AquariumDecorCatalog>();
        made.Add(decor);
        decor.offers = new[] { new AquariumDecorOffer { id = "stone", price = stonePrice } };
        decor.giftPacks = new[]
        {
            new AquariumGiftPack { id = "starter_v1", gifts = new[] { new AquariumGift { id = "xu", count = 1250 }, new AquariumGift { id = "koi", count = 1, slot = "fish" } } }
        };
        return decor;
    }

    private static List<DecorCatalogSheet.SheetItem> Fish() =>
        new List<DecorCatalogSheet.SheetItem> { new DecorCatalogSheet.SheetItem { id = "koi", price = 500 } };

    [TearDown]
    public void Destroy()
    {
        foreach (var x in made) Object.DestroyImmediate(x);
        made.Clear();
    }

    [Test]
    public void EveryItemHasItsPriceAndSlotAndThePacksComeAlong()
    {
        var catalog = ServerCatalogWriter.Build(Decor(120), null, Fish());
        Assert.AreEqual(("koi", "fish", 500), (catalog.items[0].id, catalog.items[0].slot, catalog.items[0].price));
        Assert.AreEqual(("stone", "", 120), (catalog.items[1].id, catalog.items[1].slot, catalog.items[1].price));
        Assert.AreEqual("starter_v1", catalog.giftPacks[0].id);
        Assert.AreEqual(1250, catalog.giftPacks[0].gifts[0].count);
    }

    [Test]
    public void TheVersionFollowsThePrices()
    {
        string a = ServerCatalogWriter.Build(Decor(120), null, Fish()).version;
        Assert.IsNotEmpty(a);
        Assert.AreEqual(a, ServerCatalogWriter.Build(Decor(120), null, Fish()).version);
        Assert.AreNotEqual(a, ServerCatalogWriter.Build(Decor(121), null, Fish()).version);
    }
}
