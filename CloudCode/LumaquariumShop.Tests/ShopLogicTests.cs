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
    public async Task ABrokenLocalSaveIsRefused()
    {
        const string local = "{\"version\":2,\"wallet\":[{\"id\":\"xu\",\"count\":-5}]}";
        Assert.That((await logic.Import(local)).Error, Is.EqualTo(ShopError.Rejected));
        Assert.That(store.Json, Is.Null);
    }

    [Test]
    public void TheEmbeddedCatalogHasAVersionAndTheStarterPack()
    {
        var catalog = CatalogResource.Catalog;
        Assert.That(catalog.version, Is.Not.Empty);
        Assert.That(catalog.giftPacks.Any(p => p.id == ShopSave.StarterPack), Is.True);
    }
}
