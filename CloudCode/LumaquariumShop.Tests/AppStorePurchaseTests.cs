using System.Text;
using System.Text.Json;
using Lumaquarium.Shop;
using NUnit.Framework;

namespace LumaquariumShop.Tests;

public sealed class AppStorePurchaseTests
{
    private const string Small = "com.thnha.bettafishaquarium.coins_1000";
    private const string Large = "com.thnha.bettafishaquarium.coins_10000";

    // Apple as the tests need it: known transactions answer, Down throws as if Apple were out of reach.
    private sealed class FakeAppStore : IAppStore
    {
        public readonly Dictionary<string, AppStoreTransaction> Known = new();
        public bool Down;
        public int Calls;

        public Task<AppStoreTransaction?> GetTransaction(string transactionId)
        {
            Calls++;
            if (Down) throw new AppStoreUnavailableException("down");
            return Task.FromResult(Known.TryGetValue(transactionId, out var t) ? t : null);
        }
    }

    private MemoryShopStore store = null!;
    private FakeAppStore apple = null!;
    private ShopLogic logic = null!;
    private static ShopSave State(ShopReply reply) => JsonSerializer.Deserialize<ShopSave>(reply.State, ShopLogic.Json)!;

    [SetUp]
    public void Setup()
    {
        store = new MemoryShopStore { Json = JsonSerializer.Serialize(new ShopSave(), ShopLogic.Json) };
        apple = new FakeAppStore();
        logic = new ShopLogic(store, new ShopCatalog(), appStore: apple);
    }

    private void Sold(string id, string product, Action<AppStoreTransaction>? change = null)
    {
        var t = new AppStoreTransaction { transactionId = id, bundleId = AppStoreApi.BundleId, productId = product, type = "Consumable" };
        change?.Invoke(t);
        apple.Known[id] = t;
    }

    [Test]
    public async Task ARealPurchaseAddsThePacksCoins()
    {
        Sold("2000000111", Large);
        var reply = await logic.ClaimAppStorePurchase(Large, "2000000111");
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(reply).Balance(), Is.EqualTo(10000));
        Assert.That(State(reply).purchases, Is.EqualTo(new[] { "apple:2000000111" }));
    }

    [Test]
    public async Task TheSamePurchaseSentAgainPaysOnceAndSkipsApple()
    {
        Sold("2000000111", Small);
        await logic.ClaimAppStorePurchase(Small, "2000000111");
        var again = await logic.ClaimAppStorePurchase(Small, "2000000111");
        Assert.That(again.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(again).Balance(), Is.EqualTo(1000));
        Assert.That(apple.Calls, Is.EqualTo(1));
        Assert.That(store.Saves, Is.EqualTo(1));
    }

    [Test]
    public async Task AppleOutOfReachIsRejectedSoTheGameKeepsThePurchase()
    {
        apple.Down = true;
        var reply = await logic.ClaimAppStorePurchase(Small, "2000000111");
        Assert.That(reply.Error, Is.EqualTo(ShopError.Rejected));
        Assert.That(State(reply).Balance(), Is.Zero);
    }

    [TestCase("unknown")]
    [TestCase("otherProduct")]
    [TestCase("otherApp")]
    [TestCase("refunded")]
    [TestCase("notConsumable")]
    public async Task APurchaseAppleDoesNotBackIsInvalid(string why)
    {
        if (why != "unknown")
            Sold("2000000111", Small, t =>
            {
                if (why == "otherProduct") t.productId = Large;
                if (why == "otherApp") t.bundleId = "com.example.other";
                if (why == "refunded") t.revocationDate = 1761500000000;
                if (why == "notConsumable") t.type = "Non-Consumable";
            });
        var reply = await logic.ClaimAppStorePurchase(Small, "2000000111");
        Assert.That(reply.Error, Is.EqualTo(ShopError.PurchaseInvalid));
        Assert.That(State(reply).Balance(), Is.Zero);
        Assert.That(State(reply).purchases, Is.Empty);
    }

    [TestCase("com.thnha.bettafishaquarium.coins_99999", "2000000111")]
    [TestCase(Small, "")]
    [TestCase(Small, "../2000000111")]
    public async Task AnUnknownPackOrMalformedIdNeverReachesApple(string product, string transactionId)
    {
        var reply = await logic.ClaimAppStorePurchase(product, transactionId);
        Assert.That(reply.Error, Is.EqualTo(ShopError.PurchaseInvalid));
        Assert.That(apple.Calls, Is.Zero);
    }

    [Test]
    public async Task AQuantityAboveOnePaysEachPack()
    {
        Sold("2000000111", Small, t => t.quantity = 3);
        Assert.That(State(await logic.ClaimAppStorePurchase(Small, "2000000111")).Balance(), Is.EqualTo(3000));
    }

    [Test]
    public async Task AWriteConflictRetriesWithoutPayingTwice()
    {
        Sold("2000000111", Small);
        store.Conflicts = 1;
        var reply = await logic.ClaimAppStorePurchase(Small, "2000000111");
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(reply).Balance(), Is.EqualTo(1000));
    }

    [Test]
    public async Task AnImportedLocalSaveCannotBringPurchases()
    {
        store.Json = null;
        var local = new ShopSave { purchases = { "apple:2000000111" } };
        var reply = await logic.Import(JsonSerializer.Serialize(local, ShopLogic.Json));
        Assert.That(State(reply).purchases, Is.Empty);
    }

    [Test]
    public void DecodeReadsTheSignedTransactionPayload()
    {
        string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string jws = Part("{\"alg\":\"ES256\"}") + "." + Part("{\"transactionId\":\"2000000111\",\"bundleId\":\"com.thnha.bettafishaquarium\",\"productId\":\"p\",\"type\":\"Consumable\",\"quantity\":2,\"environment\":\"Sandbox\"}") + ".sig";
        var t = AppStoreApi.Decode(jws)!;
        Assert.That((t.transactionId, t.productId, t.type, t.quantity, t.environment, t.revocationDate),
            Is.EqualTo(("2000000111", "p", "Consumable", 2, "Sandbox", (long?)null)));
    }
}
