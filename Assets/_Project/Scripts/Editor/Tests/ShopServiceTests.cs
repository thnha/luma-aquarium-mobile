using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lumaquarium.Shop;
using Lumaquarium.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ShopServiceTests
{
    private const string Key = "Lumaquarium.Tests.ShopService";

    // Answers at once, so the tests can wait on the tasks without a frame passing.
    private sealed class FakeCloud : IShopCloud
    {
        public readonly List<(string function, Dictionary<string, object> args)> Calls = new List<(string, Dictionary<string, object>)>();
        public Func<string, ShopReply> Answer = _ => new ShopReply();
        public bool Offline;

        public Task<ShopReply> Call(string function, Dictionary<string, object> args)
        {
            Calls.Add((function, args));
            if (Offline) throw new ShopOfflineException("test");
            return Task.FromResult(Answer(function));
        }
    }

    private static string SaveWith(int coins) =>
        "{\"version\":2,\"wallet\":[{\"id\":\"xu\",\"count\":" + coins + "}],\"items\":[],\"equipped\":[],\"grantsApplied\":[]}";

    private static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();
    private static void Run(Task task) => task.GetAwaiter().GetResult();
    private static int Balance() => new FishShopInventory(Key).Balance;

    [SetUp, TearDown]
    public void Clear()
    {
        foreach (string key in new[] { Key, Key + ".imported", Key + ".imported.bak", Key + ".unreadable.bak" }) PlayerPrefs.DeleteKey(key);
        ShopService.Use(null);
    }

    [Test]
    public void TheLocalBackendRunsTheRulesOnTheDevice()
    {
        PlayerPrefs.SetString(Key, SaveWith(1250));
        var local = new LocalShopBackend(Key);
        Assert.AreEqual(ShopError.None, Run(local.Buy("starter-stone", 100, null)));
        Assert.AreEqual(ShopError.NotEnoughCoins, Run(local.Buy("koi-nemo", 99999, "fish")));
        Assert.AreEqual(1150, Balance());
    }

    [Test]
    public void TheServerGetsTheIdAndCatalogVersionAndItsAnswerIsKept()
    {
        var cloud = new FakeCloud { Answer = _ => new ShopReply { State = SaveWith(77) } };
        var server = new ServerShopBackend(cloud, Key) { CatalogVersion = "abc" };
        Assert.AreEqual(ShopError.None, Run(server.Buy("starter-stone", 1, null)));
        Assert.AreEqual("BuyItem", cloud.Calls[0].function);
        Assert.AreEqual("starter-stone", cloud.Calls[0].args["id"]);
        Assert.AreEqual("abc", cloud.Calls[0].args["catalogVersion"]);
        Assert.AreEqual(77, Balance());
    }

    [Test]
    public void ARefusalStillBringsTheServersSave()
    {
        var cloud = new FakeCloud { Answer = _ => new ShopReply { Error = ShopError.NotEnoughCoins, State = SaveWith(5) } };
        Assert.AreEqual(ShopError.NotEnoughCoins, Run(new ServerShopBackend(cloud, Key).Select("fish", "koi-nemo")));
        Assert.AreEqual(5, Balance());
    }

    [Test]
    public void OfflineTheSaveStaysAsItIs()
    {
        PlayerPrefs.SetString(Key, SaveWith(40));
        var cloud = new FakeCloud { Offline = true };
        Assert.AreEqual(ShopError.Offline, Run(new ServerShopBackend(cloud, Key).Buy("starter-stone", 1, null)));
        Assert.AreEqual(40, Balance());
    }

    [Test]
    public void AnAnswerThatIsNotASaveIsRefused()
    {
        PlayerPrefs.SetString(Key, SaveWith(40));
        var cloud = new FakeCloud { Answer = _ => new ShopReply { State = "{\"version\":2,\"wallet\":[{\"id\":\"xu\",\"count\":-1}]}" } };
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("cannot be read"));
        Assert.AreEqual(ShopError.Rejected, Run(new ServerShopBackend(cloud, Key).Buy("starter-stone", 1, null)));
        Assert.AreEqual(40, Balance());
    }

    [Test]
    public void ALocalSaveIsSentOnceThenOnlyTheGifts()
    {
        PlayerPrefs.SetString(Key, SaveWith(40));
        var cloud = new FakeCloud { Answer = f => new ShopReply { State = SaveWith(f == "ImportLocalSave" ? 40 : 1290) } };
        ShopService.Use(new ServerShopBackend(cloud, Key));
        Run(ShopService.Prepare());
        CollectionAssert.AreEqual(new[] { "ImportLocalSave", "ApplyGifts" }, cloud.Calls.Select(c => c.function));
        Assert.AreEqual(SaveWith(40), cloud.Calls[0].args["save"]);
        Assert.AreEqual(SaveWith(40), PlayerPrefs.GetString(Key + ".imported.bak"), "the old save is kept");
        Assert.AreEqual(1290, Balance());
        Run(ShopService.Prepare());
        CollectionAssert.AreEqual(new[] { "ImportLocalSave", "ApplyGifts", "ApplyGifts" }, cloud.Calls.Select(c => c.function));
    }

    [Test]
    public void OfflineTheImportWaitsForNextTime()
    {
        PlayerPrefs.SetString(Key, SaveWith(40));
        var cloud = new FakeCloud { Offline = true };
        ShopService.Use(new ServerShopBackend(cloud, Key));
        Run(ShopService.Prepare());
        Assert.AreEqual(0, PlayerPrefs.GetInt(Key + ".imported", 0));
        Assert.IsFalse(PlayerPrefs.HasKey(Key + ".imported.bak"));
        cloud.Offline = false;
        Run(ShopService.Prepare());
        Assert.AreEqual("ImportLocalSave", cloud.Calls[1].function);
    }

    [Test]
    public void ANewPlayerHasNothingToSend()
    {
        var cloud = new FakeCloud { Answer = _ => new ShopReply { State = SaveWith(1250) } };
        ShopService.Use(new ServerShopBackend(cloud, Key));
        Run(ShopService.Prepare());
        CollectionAssert.AreEqual(new[] { "ApplyGifts" }, cloud.Calls.Select(c => c.function));
        Assert.AreEqual(1, PlayerPrefs.GetInt(Key + ".imported", 0));
        Assert.AreEqual(1250, Balance());
    }

    [TestCase(ShopError.NotEnoughCoins, "Chưa đủ xu để mua món này.")]
    [TestCase(ShopError.Offline, "Cần kết nối mạng để mua và đổi đồ.")]
    [TestCase(ShopError.Rejected, "Chưa lưu được giao dịch. Vui lòng thử lại.")]
    public void EveryRefusalHasAMessage(string error, string message) => Assert.AreEqual(message, ShopService.Message(error));
}
