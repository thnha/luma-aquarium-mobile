using System.Text.Json;
using Lumaquarium.Shop;
using NUnit.Framework;

namespace LumaquariumShop.Tests;

public sealed class GooglePlayPurchaseTests
{
    private const string Small = "com.thnha.bettafishaquarium.coins_1000";
    private const string Large = "com.thnha.bettafishaquarium.coins_10000";
    private const string Token = "abcdefghijklmnop.AO-J1Oz_token";

    // Google as the tests need it: known purchases answer, Down throws as if Google were out of reach.
    private sealed class FakeGooglePlay : IGooglePlay
    {
        public readonly Dictionary<(string, string), GooglePlayPurchase> Known = new();
        public bool Down;
        public int Calls;

        public Task<GooglePlayPurchase?> GetPurchase(string productId, string purchaseToken)
        {
            Calls++;
            if (Down) throw new GooglePlayUnavailableException("down");
            return Task.FromResult(Known.TryGetValue((productId, purchaseToken), out var p) ? p : null);
        }
    }

    private MemoryShopStore store = null!;
    private FakeGooglePlay google = null!;
    private ShopLogic logic = null!;
    private static ShopSave State(ShopReply reply) => JsonSerializer.Deserialize<ShopSave>(reply.State, ShopLogic.Json)!;

    [SetUp]
    public void Setup()
    {
        store = new MemoryShopStore { Json = JsonSerializer.Serialize(new ShopSave(), ShopLogic.Json) };
        google = new FakeGooglePlay();
        logic = new ShopLogic(store, new ShopCatalog(), googlePlay: google);
    }

    private void Sold(string product, Action<GooglePlayPurchase>? change = null)
    {
        var p = new GooglePlayPurchase { orderId = "GPA.1234-5678-9012-34567" };
        change?.Invoke(p);
        google.Known[(product, Token)] = p;
    }

    [Test]
    public async Task ARealPurchaseAddsThePacksCoins()
    {
        Sold(Large);
        var reply = await logic.ClaimGooglePlayPurchase(Large, Token);
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(reply).Balance(), Is.EqualTo(10000));
        Assert.That(State(reply).purchases, Has.Count.EqualTo(1));
        Assert.That(State(reply).purchases[0], Does.StartWith("google:").And.Not.Contain(Token));
    }

    [Test]
    public async Task TheSamePurchaseSentAgainPaysOnceAndSkipsGoogle()
    {
        Sold(Small);
        await logic.ClaimGooglePlayPurchase(Small, Token);
        // The game consumed it after the first answer; its retry must still be None.
        google.Known[(Small, Token)].consumptionState = 1;
        var again = await logic.ClaimGooglePlayPurchase(Small, Token);
        Assert.That(again.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(again).Balance(), Is.EqualTo(1000));
        Assert.That(google.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task GoogleOutOfReachIsRejectedSoTheGameKeepsThePurchase()
    {
        google.Down = true;
        var reply = await logic.ClaimGooglePlayPurchase(Small, Token);
        Assert.That(reply.Error, Is.EqualTo(ShopError.Rejected));
        Assert.That(State(reply).Balance(), Is.Zero);
    }

    [Test]
    public async Task APendingPaymentIsRejectedUntilGooglePaysIt()
    {
        Sold(Small, p => p.purchaseState = 2);
        var reply = await logic.ClaimGooglePlayPurchase(Small, Token);
        Assert.That(reply.Error, Is.EqualTo(ShopError.Rejected));
        Assert.That(State(reply).Balance(), Is.Zero);
    }

    [TestCase("unknown")]
    [TestCase("otherProduct")]
    [TestCase("refunded")]
    [TestCase("consumedElsewhere")]
    public async Task APurchaseGoogleDoesNotBackIsInvalid(string why)
    {
        if (why == "otherProduct") Sold(Large);
        if (why == "refunded") Sold(Small, p => p.purchaseState = 1);
        if (why == "consumedElsewhere") Sold(Small, p => p.consumptionState = 1);
        var reply = await logic.ClaimGooglePlayPurchase(Small, Token);
        Assert.That(reply.Error, Is.EqualTo(ShopError.PurchaseInvalid));
        Assert.That(State(reply).Balance(), Is.Zero);
        Assert.That(State(reply).purchases, Is.Empty);
    }

    [TestCase("com.thnha.bettafishaquarium.coins_99999", Token)]
    [TestCase(Small, "")]
    [TestCase(Small, "../token")]
    [TestCase(Small, "token/with?query")]
    public async Task AnUnknownPackOrMalformedTokenNeverReachesGoogle(string product, string token)
    {
        var reply = await logic.ClaimGooglePlayPurchase(product, token);
        Assert.That(reply.Error, Is.EqualTo(ShopError.PurchaseInvalid));
        Assert.That(google.Calls, Is.Zero);
    }

    [Test]
    public async Task AQuantityAboveOnePaysEachPack()
    {
        Sold(Small, p => p.quantity = 3);
        Assert.That(State(await logic.ClaimGooglePlayPurchase(Small, Token)).Balance(), Is.EqualTo(3000));
    }

    [Test]
    public async Task AWriteConflictRetriesWithoutPayingTwice()
    {
        Sold(Small);
        store.Conflicts = 1;
        var reply = await logic.ClaimGooglePlayPurchase(Small, Token);
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(reply).Balance(), Is.EqualTo(1000));
    }
}
