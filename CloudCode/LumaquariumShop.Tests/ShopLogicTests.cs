using System.Text.Json;
using Lumaquarium.Shop;
using NUnit.Framework;

namespace LumaquariumShop.Tests;

public sealed class ShopLogicTests
{
    private const string Version = "v1";
    private MemoryShopStore store = null!;
    private ShopLogic logic = null!;

    private static ShopCatalog Catalog() => new()
    {
        version = Version,
        items =
        {
            new CatalogItem { id = "plakat-galaxy", slot = "fish", price = 0 },
            new CatalogItem { id = "koi-nemo", slot = "fish", price = 500 },
            new CatalogItem { id = "tank_square", slot = "tank", price = 0 },
            new CatalogItem { id = "tank_deep", slot = "tank", price = 1500 },
            new CatalogItem { id = "wall_river", slot = "wall", price = 0 },
            new CatalogItem { id = "starter-stone", slot = "", price = 100 },
        },
        giftPacks =
        {
            new GiftPack
            {
                id = ShopSave.StarterPack,
                gifts =
                {
                    new Gift { id = ShopSave.Coins, count = 1250 },
                    new Gift { id = "plakat-galaxy", count = 1, slot = "fish" },
                    new Gift { id = "tank_square", count = 1, slot = "tank" },
                    new Gift { id = "wall_river", count = 1, slot = "wall" },
                    new Gift { id = "starter-stone", count = 2, slot = "" },
                }
            }
        }
    };

    private static ShopSave State(ShopReply reply) => JsonSerializer.Deserialize<ShopSave>(reply.State, ShopLogic.Json)!;

    [SetUp]
    public void SetUp()
    {
        store = new MemoryShopStore();
        logic = new ShopLogic(store, Catalog());
    }

    [Test]
    public async Task ANewPlayerHasNothingUntilTheGifts()
    {
        var reply = await logic.GetState();
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(reply).Balance(), Is.EqualTo(0));
        Assert.That(store.Json, Is.Null, "reading never creates the save");
    }

    [Test]
    public async Task TheStarterPackIsGivenOnceAndPutIntoUse()
    {
        await logic.ApplyGifts();
        var reply = await logic.ApplyGifts();
        var save = State(reply);
        Assert.That(save.Balance(), Is.EqualTo(1250));
        Assert.That(save.Equipped("fish"), Is.EqualTo("plakat-galaxy"));
        Assert.That(save.Equipped("tank"), Is.EqualTo("tank_square"));
        Assert.That(save.CountOf("starter-stone"), Is.EqualTo(2));
        Assert.That(store.Saves, Is.EqualTo(1), "the second call changes nothing and writes nothing");
    }

    [Test]
    public async Task BuyingTakesTheCatalogPrice()
    {
        await logic.ApplyGifts();
        var reply = await logic.Buy("koi-nemo", Version);
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        var save = State(reply);
        Assert.That(save.Balance(), Is.EqualTo(750));
        Assert.That(save.Equipped("fish"), Is.EqualTo("koi-nemo"), "a fish is put into use when bought");
    }

    [Test]
    public async Task DecorationsAreBoughtAgainAndAgainButFishOnce()
    {
        await logic.ApplyGifts();
        Assert.That((await logic.Buy("starter-stone", Version)).Error, Is.EqualTo(ShopError.None));
        Assert.That(State(await logic.Buy("starter-stone", Version)).CountOf("starter-stone"), Is.EqualTo(4));
        Assert.That((await logic.Buy("plakat-galaxy", Version)).Error, Is.EqualTo(ShopError.Owned));
    }

    [Test]
    public async Task NotEnoughCoinsChangesNothing()
    {
        await logic.ApplyGifts();
        await logic.Buy("koi-nemo", Version);
        string before = store.Json!;
        var reply = await logic.Buy("tank_deep", Version);
        Assert.That(reply.Error, Is.EqualTo(ShopError.NotEnoughCoins));
        Assert.That(State(reply).Balance(), Is.EqualTo(750), "the reply still carries the save");
        Assert.That(store.Json, Is.EqualTo(before));
    }

    [Test]
    public async Task AnotherCatalogVersionIsRefused()
    {
        await logic.ApplyGifts();
        var reply = await logic.Buy("starter-stone", "old");
        Assert.That(reply.Error, Is.EqualTo(ShopError.PriceChanged));
        Assert.That(State(reply).Balance(), Is.EqualTo(1250));
    }

    [Test]
    public async Task AnItemNotInTheCatalogIsRefused()
    {
        await logic.ApplyGifts();
        Assert.That((await logic.Buy("free-money", Version)).Error, Is.EqualTo(ShopError.Rejected));
    }

    [Test]
    public async Task OnlyWhatIsOwnedCanBeSelected()
    {
        await logic.ApplyGifts();
        Assert.That((await logic.Select("fish", "koi-nemo")).Error, Is.EqualTo(ShopError.Rejected));
        Assert.That((await logic.Select("hat", "plakat-galaxy")).Error, Is.EqualTo(ShopError.Rejected));
        await logic.Buy("koi-nemo", Version);
        var reply = await logic.Select("fish", "plakat-galaxy");
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(reply).Equipped("fish"), Is.EqualTo("plakat-galaxy"));
    }

    [Test]
    public async Task AWriteConflictReadsAgainAndRunsTheRuleAgain()
    {
        await logic.ApplyGifts();
        store.Conflicts = 1;
        var reply = await logic.Buy("starter-stone", Version);
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(reply).Balance(), Is.EqualTo(1150), "the coins are taken once");
    }

    [Test]
    public async Task ConflictsThatDoNotStopAreRefused()
    {
        await logic.ApplyGifts();
        store.Conflicts = 10;
        Assert.That((await logic.Buy("starter-stone", Version)).Error, Is.EqualTo(ShopError.Rejected));
    }

    [Test]
    public async Task AnUnreadableSaveIsKeptAsItIs()
    {
        store.Json = "{ not json";
        Assert.That((await logic.ApplyGifts()).Error, Is.EqualTo(ShopError.SaveUnreadable));
        Assert.That(store.Json, Is.EqualTo("{ not json"));
    }

    [Test]
    public async Task ALocalSaveIsImportedOnceWithItsCoinsCapped()
    {
        const string local = "{\"version\":2,\"wallet\":[{\"id\":\"xu\",\"count\":999999}],\"items\":[{\"id\":\"koi-nemo\",\"count\":1}]," +
            "\"equipped\":[{\"slot\":\"fish\",\"id\":\"koi-nemo\"}],\"grantsApplied\":[\"starter_v1\"]}";
        var reply = await logic.Import(local);
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        var save = State(reply);
        Assert.That(save.Balance(), Is.EqualTo(ShopLogic.ImportCoinCap));
        Assert.That(save.Equipped("fish"), Is.EqualTo("koi-nemo"));
        Assert.That(State(await logic.ApplyGifts()).Balance(), Is.EqualTo(ShopLogic.ImportCoinCap), "the starter pack was had already");
        Assert.That((await logic.Import(local)).Error, Is.EqualTo(ShopError.AlreadyImported));
    }

    [Test]
    public async Task ABrokenLocalSaveIsUnreadable()
    {
        const string local = "{\"version\":2,\"wallet\":[{\"id\":\"xu\",\"count\":-5}]}";
        var reply = await logic.Import(local);
        Assert.That(reply.Error, Is.EqualTo(ShopError.SaveUnreadable));
        Assert.That(reply.State, Is.Null);
        Assert.That(store.Json, Is.Null);
    }

    [Test]
    public async Task ALocalSaveThatIsNotJsonIsUnreadable()
    {
        var reply = await logic.Import("{ not json");
        Assert.That(reply.Error, Is.EqualTo(ShopError.SaveUnreadable));
        Assert.That(reply.State, Is.Null);
        Assert.That(store.Json, Is.Null);
    }

    [Test]
    public async Task AnImportedSaveKeepsOnlyWhatTheCatalogKnowsAtTheAllowedCounts()
    {
        const string local = "{\"version\":2,\"wallet\":[{\"id\":\"xu\",\"count\":10},{\"id\":\"gem\",\"count\":50}]," +
            "\"items\":[{\"id\":\"koi-nemo\",\"count\":7},{\"id\":\"starter-stone\",\"count\":500},{\"id\":\"secret-fish\",\"count\":1}," +
            "{\"id\":\"xu\",\"count\":1},{\"id\":\"tank_deep\",\"count\":1}]," +
            "\"equipped\":[{\"slot\":\"fish\",\"id\":\"koi-nemo\"},{\"slot\":\"tank\",\"id\":\"koi-nemo\"}," +
            "{\"slot\":\"wall\",\"id\":\"secret-fish\"},{\"slot\":\"hat\",\"id\":\"starter-stone\"},{\"slot\":\"floor\",\"id\":\"wall_river\"}]," +
            "\"grantsApplied\":[\"mystery_pack\"]}";
        var save = State(await logic.Import(local));
        Assert.That(save.CountOf("koi-nemo"), Is.EqualTo(1));
        Assert.That(save.CountOf("starter-stone"), Is.EqualTo(ShopLogic.ImportItemCap));
        Assert.That(save.CountOf("tank_deep"), Is.EqualTo(1));
        Assert.That(save.Owns("secret-fish"), Is.False);
        Assert.That(save.Owns(ShopSave.Coins), Is.False, "coins are not an item");
        Assert.That(save.wallet.Select(x => x.id), Is.EqualTo(new[] { ShopSave.Coins }));
        Assert.That(save.equipped.Select(x => x.slot + ":" + x.id), Is.EqualTo(new[] { "fish:koi-nemo" }));
        Assert.That(save.grantsApplied, Is.EqualTo(new[] { ShopSave.StarterPack }), "no second starter pack, no unknown pack");
    }

    [Test]
    public async Task ANullLocalSaveIsUnreadable()
    {
        var reply = await logic.Import(null);
        Assert.That(reply.Error, Is.EqualTo(ShopError.SaveUnreadable));
        Assert.That(reply.State, Is.Null);
        Assert.That(store.Json, Is.Null);
    }

    [Test]
    public async Task ASaveCreatedByAnotherCallDuringTheFirstWriteIsKept()
    {
        var other = new ShopSave { grantsApplied = { ShopSave.StarterPack } };
        ShopSave.Add(other.wallet, ShopSave.Coins, 1250);
        store.RacingJson = JsonSerializer.Serialize(other, ShopLogic.Json);
        var reply = await logic.ApplyGifts();
        Assert.That(State(reply).Balance(), Is.EqualTo(1250), "the rule ran again on their save, the pack is not given twice");
    }

    [Test]
    public async Task AnImportRacingAnotherFirstWriteReturnsTheStoredSave()
    {
        var other = new ShopSave { grantsApplied = { ShopSave.StarterPack } };
        ShopSave.Add(other.wallet, ShopSave.Coins, 7);
        store.RacingJson = JsonSerializer.Serialize(other, ShopLogic.Json);
        var reply = await logic.Import("{\"version\":2,\"wallet\":[{\"id\":\"xu\",\"count\":999}]}");
        Assert.That(reply.Error, Is.EqualTo(ShopError.AlreadyImported));
        Assert.That(State(reply).Balance(), Is.EqualTo(7));
    }

    [Test]
    public async Task AnImportWriteConflictIsRefusedWithoutThrowing()
    {
        store.Conflicts = 1;
        var reply = await logic.Import("{\"version\":2}");
        Assert.That(reply.Error, Is.EqualTo(ShopError.Rejected));
    }

    [Test]
    public async Task AWriteWithALockThatIsNotTheStoredOneConflicts()
    {
        store.Json = "{}";
        Assert.ThrowsAsync<WriteConflictException>(() => store.Save("{\"a\":1}", "stale"));
        await store.Save("{\"a\":2}", null);
        Assert.That(store.Json, Is.EqualTo("{\"a\":2}"), "no lock always writes, like Cloud Save");
    }

    [Test]
    public async Task AnItemCannotBeSelectedIntoAnotherSlot()
    {
        await logic.ApplyGifts();
        await logic.Buy("koi-nemo", Version);
        Assert.That((await logic.Select("tank", "koi-nemo")).Error, Is.EqualTo(ShopError.Rejected));
        Assert.That((await logic.Select("wall", "plakat-galaxy")).Error, Is.EqualTo(ShopError.Rejected));
        Assert.That(State(await logic.GetState()).Equipped("tank"), Is.EqualTo("tank_square"));
    }

    [Test]
    public void TheEmbeddedCatalogHasAVersionAndTheStarterPack()
    {
        var catalog = CatalogResource.Catalog;
        Assert.That(catalog.version, Is.Not.Empty);
        Assert.That(catalog.giftPacks.Any(p => p.id == ShopSave.StarterPack), Is.True);
    }
}
