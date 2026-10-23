using Lumaquarium.Shop;
using Lumaquarium.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class FishShopInventoryTests
{
    private const string Key = "Lumaquarium.Tests.ShopInventory";

    // A version 1 save as a player had it: three fish, starter pieces, a bought wall, the first floor and tank.
    private const string PlayerV1 = "{\"version\":1,\"balance\":20,\"ownedIds\":[\"plakat-galaxy\",\"koi-nemo\",\"plakat\"]," +
        "\"activeId\":\"plakat-galaxy\",\"decor\":[{\"id\":\"starter-anubias\",\"count\":2},{\"id\":\"starter-stone\",\"count\":2}," +
        "{\"id\":\"wood-branches\",\"count\":1}],\"starterDecorImported\":true,\"ownedTanks\":[],\"activeTank\":\"\"," +
        "\"ownedWalls\":[\"wall_brick\"],\"activeWall\":\"wall_brick\",\"ownedSubstrates\":[],\"activeSubstrate\":\"\"}";

    private static AquariumGiftPack Pack(string id, params AquariumGift[] gifts) => new AquariumGiftPack { id = id, gifts = gifts };
    private static AquariumGift Gift(string id, int count, string slot = "") => new AquariumGift { id = id, count = count, slot = slot };

    private static AquariumGiftPack Starter() => Pack(FishShopInventory.StarterPack, Gift("xu", 1250),
        Gift("plakat-galaxy", 1, "fish"), Gift("tank_square", 1, "tank"), Gift("wall_river", 1, "wall"),
        Gift("sand_river", 1, "floor"), Gift("starter-stone", 2));

    private static FishShopInventory NewPlayer()
    {
        new FishShopInventory(Key).ApplyGifts(new[] { Starter() });
        return new FishShopInventory(Key);
    }

    [SetUp, TearDown]
    public void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.DeleteKey(Key + ".v1.bak");
        PlayerPrefs.DeleteKey(Key + ".unreadable.bak");
    }

    [Test]
    public void ANewPlayerGetsTheStarterPackInUse()
    {
        Assert.AreEqual(0, new FishShopInventory(Key).Balance, "nothing before the gifts");
        var inventory = NewPlayer();
        Assert.AreEqual(1250, inventory.Balance);
        Assert.AreEqual(("plakat-galaxy", "tank_square", "wall_river", "sand_river"),
            (inventory.ActiveId, inventory.ActiveTank, inventory.ActiveWall, inventory.ActiveSubstrate));
        Assert.IsTrue(inventory.OwnsWall("wall_river"));
        Assert.AreEqual(2, inventory.DecorCount("starter-stone"));
    }

    [Test]
    public void EachPackIsGivenOnceAndNeverReplacesWhatIsInUse()
    {
        var inventory = NewPlayer();
        inventory.TryPurchaseDecor("starter-stone", 50);
        var tet = Pack("tet_2027", Gift("xu", 500), Gift("wall_gold", 1, "wall"));
        inventory.ApplyGifts(new[] { Starter(), tet });
        inventory.ApplyGifts(new[] { Starter(), tet });
        Assert.AreEqual(1250 - 50 + 500, inventory.Balance);
        Assert.AreEqual(3, inventory.DecorCount("starter-stone"));
        Assert.IsTrue(inventory.OwnsWall("wall_gold"));
        Assert.AreEqual("wall_river", inventory.ActiveWall);
    }

    [Test]
    public void BuyingAWallSpendsCoinsAndPutsItInUse()
    {
        var inventory = NewPlayer();
        Assert.IsTrue(inventory.TryPurchaseWall("wall_brick", 350));
        Assert.AreEqual(900, inventory.Balance);
        Assert.IsTrue(inventory.OwnsWall("wall_brick"));
        Assert.AreEqual("wall_brick", new FishShopInventory(Key).ActiveWall, "saved");
    }

    [Test]
    public void CannotBuyTwiceOrWithoutCoins()
    {
        var inventory = NewPlayer();
        Assert.IsTrue(inventory.TryPurchaseSubstrate("sand_white", 150));
        Assert.IsFalse(inventory.TryPurchaseSubstrate("sand_white", 150));
        Assert.IsFalse(inventory.TryPurchaseSubstrate("gravel_black", 99999));
        Assert.AreEqual(1100, inventory.Balance);
        Assert.AreEqual("sand_white", inventory.ActiveSubstrate);
    }

    [Test]
    public void OnlyOwnedItemsGoIntoUseAndFreeOnesAreTakenFirst()
    {
        var inventory = NewPlayer();
        Assert.IsFalse(inventory.SelectWall("wall_slate"));
        Assert.IsFalse(inventory.SelectTank("tank_free"), "price 0 is not owned until taken");
        Assert.IsTrue(inventory.TryPurchaseTank("tank_free", 0));
        Assert.AreEqual(1250, inventory.Balance);
        Assert.IsTrue(inventory.SelectTank("tank_square"));
        Assert.IsTrue(inventory.SelectTank("tank_free"));
        Assert.AreEqual("tank_free", inventory.ActiveTank);
    }

    [Test]
    public void PlacedPiecesAreKeptInStock()
    {
        var inventory = NewPlayer();
        inventory.KeepPlacedInStock(new System.Collections.Generic.Dictionary<string, int> { ["starter-stone"] = 3, ["starter-wood"] = 1 });
        Assert.AreEqual(3, inventory.DecorCount("starter-stone"));
        Assert.AreEqual(1, inventory.DecorCount("starter-wood"));
    }

    [Test]
    public void AVersion1SaveIsMovedOverWithNothingLost()
    {
        PlayerPrefs.SetString(Key, PlayerV1);
        var inventory = new FishShopInventory(Key);
        Assert.IsFalse(inventory.LoadFailed);
        Assert.AreEqual(20, inventory.Balance);
        foreach (string fish in new[] { "plakat-galaxy", "koi-nemo", "plakat" }) Assert.IsTrue(inventory.Owns(fish), fish);
        Assert.AreEqual("plakat-galaxy", inventory.ActiveId);
        Assert.AreEqual(2, inventory.DecorCount("starter-anubias"));
        Assert.AreEqual(1, inventory.DecorCount("wood-branches"));
        Assert.AreEqual("wall_brick", inventory.ActiveWall);
        StringAssert.Contains("\"version\":2", PlayerPrefs.GetString(Key));
        Assert.AreEqual(PlayerV1, PlayerPrefs.GetString(inventory.BackupKey), "the old save is kept");
    }

    [Test]
    public void AMovedOverSaveGetsNoSecondStarterButItsEmptySlotsFilled()
    {
        PlayerPrefs.SetString(Key, PlayerV1);
        var inventory = new FishShopInventory(Key);
        inventory.ApplyGifts(new[] { Starter() });
        Assert.AreEqual(20, inventory.Balance, "no second starter coins");
        Assert.AreEqual("wall_brick", inventory.ActiveWall, "its own choice stays");
        Assert.AreEqual(("tank_square", "sand_river"), (inventory.ActiveTank, inventory.ActiveSubstrate));
        Assert.IsTrue(inventory.OwnsTank("tank_square") && inventory.OwnsSubstrate("sand_river"));
        Assert.AreEqual(2, inventory.DecorCount("starter-stone"), "no second starter pieces");
    }

    [Test]
    public void AFreeItemInUseInVersion1IsOwnedAfterwards()
    {
        PlayerPrefs.SetString(Key, "{\"version\":1,\"balance\":5,\"ownedIds\":[],\"activeId\":\"\",\"activeWall\":\"wall_river\"}");
        var inventory = new FishShopInventory(Key);
        Assert.IsTrue(inventory.OwnsWall("wall_river"));
        Assert.AreEqual("wall_river", inventory.ActiveWall);
    }

    [TestCase("{not json")]
    [TestCase("{\"version\":3,\"wallet\":[]}")]
    [TestCase("{\"version\":1,\"balance\":-5,\"ownedIds\":[]}")]
    [TestCase("{\"version\":2,\"items\":[{\"id\":\"a\",\"count\":1},{\"id\":\"a\",\"count\":2}]}")]
    public void ASaveThatCannotBeReadIsNeverWrittenOver(string json)
    {
        PlayerPrefs.SetString(Key, json);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("cannot be read"));
        var inventory = new FishShopInventory(Key);
        Assert.IsTrue(inventory.LoadFailed);
        LogAssert.ignoreFailingMessages = true;
        Assert.IsFalse(inventory.TryPurchaseDecor("rock", 0));
        inventory.ApplyGifts(new[] { Starter() });
        LogAssert.ignoreFailingMessages = false;
        Assert.AreEqual(json, PlayerPrefs.GetString(Key));
    }

    [Test]
    public void BuyAndSelectSayWhyNot()
    {
        var inventory = NewPlayer();
        Assert.AreEqual(ShopError.NotEnoughCoins, inventory.Buy("koi-nemo", 99999, "fish"));
        Assert.AreEqual(ShopError.Owned, inventory.Buy("plakat-galaxy", 0, "fish"));
        Assert.AreEqual(ShopError.Rejected, inventory.Select("fish", "koi-nemo"));
        Assert.AreEqual(ShopError.None, inventory.Buy("starter-stone", 100, null));
        Assert.AreEqual(1150, new FishShopInventory(Key).Balance);
    }

    [Test]
    public void TheServersAnswerReplacesTheSaveUnlessItIsNotASave()
    {
        var inventory = NewPlayer();
        Assert.IsTrue(inventory.ReplaceWith("{\"version\":2,\"wallet\":[{\"id\":\"xu\",\"count\":77}],\"items\":[],\"equipped\":[],\"grantsApplied\":[\"starter_v1\"]}"));
        Assert.AreEqual(77, new FishShopInventory(Key).Balance);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("cannot be read"));
        Assert.IsFalse(inventory.ReplaceWith("{\"version\":2,\"wallet\":[{\"id\":\"xu\",\"count\":-1}]}"));
        Assert.AreEqual(77, new FishShopInventory(Key).Balance);
    }
}
