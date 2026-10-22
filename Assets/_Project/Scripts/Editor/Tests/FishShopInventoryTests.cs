using Lumaquarium.UI;
using NUnit.Framework;
using UnityEngine;

public sealed class FishShopInventoryTests
{
    private const string Key = "Lumaquarium.Tests.ShopInventory";

    [SetUp, TearDown]
    public void Clear() => PlayerPrefs.DeleteKey(Key);

    [Test]
    public void OldSaveWithoutSurfacesStillLoads()
    {
        PlayerPrefs.SetString(Key, "{\"version\":1,\"balance\":500,\"ownedIds\":[],\"activeId\":\"\"}");
        var inventory = new FishShopInventory(Key);
        Assert.AreEqual(500, inventory.Balance);
        Assert.AreEqual("", inventory.ActiveWall);
        Assert.AreEqual("", inventory.ActiveSubstrate);
        Assert.IsFalse(inventory.OwnsWall("wall_brick"));
    }

    [Test]
    public void BuyingAWallSpendsCoinsAndPutsItInUse()
    {
        var inventory = new FishShopInventory(Key); // new wallet: 1250 coins
        Assert.IsTrue(inventory.TryPurchaseWall("wall_brick", 350));
        Assert.AreEqual(900, inventory.Balance);
        Assert.IsTrue(inventory.OwnsWall("wall_brick"));
        Assert.AreEqual("wall_brick", inventory.ActiveWall);
        Assert.AreEqual("wall_brick", new FishShopInventory(Key).ActiveWall, "saved");
    }

    [Test]
    public void CannotBuyTwiceOrWithoutCoins()
    {
        var inventory = new FishShopInventory(Key);
        Assert.IsTrue(inventory.TryPurchaseSubstrate("sand_white", 150));
        Assert.IsFalse(inventory.TryPurchaseSubstrate("sand_white", 150));
        Assert.IsFalse(inventory.TryPurchaseSubstrate("gravel_black", 99999));
        Assert.AreEqual(1100, inventory.Balance);
        Assert.AreEqual("sand_white", inventory.ActiveSubstrate);
    }

    [Test]
    public void SelectNeedsOwnershipUnlessFree()
    {
        var inventory = new FishShopInventory(Key);
        Assert.IsFalse(inventory.SelectWall("wall_slate", false));
        Assert.IsTrue(inventory.SelectWall("wall_river", true));
        Assert.AreEqual("wall_river", inventory.ActiveWall);
        inventory.TryPurchaseSubstrate("sand_black", 200);
        Assert.IsTrue(inventory.SelectSubstrate("sand_river", true));
        Assert.IsTrue(inventory.SelectSubstrate("sand_black", false));
        Assert.AreEqual("sand_black", inventory.ActiveSubstrate);
    }

    [Test]
    public void SurfacePurchasesKeepFishAndDecor()
    {
        var inventory = new FishShopInventory(Key);
        inventory.TryPurchase("fish_a", 100);
        inventory.TryPurchaseDecor("rock", 50);
        inventory.TryPurchaseWall("wall_brick", 350);
        Assert.IsTrue(inventory.Owns("fish_a"));
        Assert.AreEqual(1, inventory.DecorCount("rock"));
        Assert.AreEqual(750, inventory.Balance);
    }
}
