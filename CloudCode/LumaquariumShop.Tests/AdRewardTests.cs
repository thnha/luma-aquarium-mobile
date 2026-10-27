using System.Text.Json;
using Lumaquarium.Shop;
using NUnit.Framework;

namespace LumaquariumShop.Tests;

public sealed class AdRewardTests
{
    private MemoryShopStore store = null!;
    private ShopLogic logic = null!;
    private DateTimeOffset now;
    private static string Id() => Guid.NewGuid().ToString("N");
    private static ShopSave State(ShopReply reply) => JsonSerializer.Deserialize<ShopSave>(reply.State, ShopLogic.Json)!;

    [SetUp]
    public void Setup()
    {
        now = DateTimeOffset.Parse("2026-10-27T10:00:00Z");
        store = new MemoryShopStore { Json = JsonSerializer.Serialize(new ShopSave(), ShopLogic.Json) };
        logic = new ShopLogic(store, new ShopCatalog(), () => now);
    }

    [Test]
    public async Task ACompletedVideoAwardsTwentyCoinsAndUsesOneTurn()
    {
        var reply = await logic.ClaimAdReward(Id());
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(reply).Balance(), Is.EqualTo(20));
        Assert.That(reply.Ads.Remaining, Is.EqualTo(9));
        Assert.That(reply.Ads.RetryAfterSeconds, Is.EqualTo(60));
        Assert.That(store.Saves, Is.EqualTo(1));
    }

    [Test]
    public async Task SixtySecondsMustPassBeforeAnotherReward()
    {
        await logic.ClaimAdReward(Id());
        now = now.AddSeconds(59);
        var refused = await logic.ClaimAdReward(Id());
        Assert.That(refused.Error, Is.EqualTo(ShopError.AdCooldown));
        Assert.That(State(refused).Balance(), Is.EqualTo(20));
        now = now.AddSeconds(1);
        Assert.That(State(await logic.ClaimAdReward(Id())).Balance(), Is.EqualTo(40));
    }

    [Test]
    public async Task OnlyTenRewardsAreAllowedPerDay()
    {
        for (int i = 0; i < 10; i++)
        {
            Assert.That((await logic.ClaimAdReward(Id())).Error, Is.EqualTo(ShopError.None));
            now = now.AddSeconds(60);
        }
        var reply = await logic.ClaimAdReward(Id());
        Assert.That(reply.Error, Is.EqualTo(ShopError.AdDailyLimit));
        Assert.That(State(reply).Balance(), Is.EqualTo(200));
        Assert.That(reply.Ads.Remaining, Is.Zero);
    }

    [Test]
    public async Task MidnightInVietnamResetsTheLimitButPreservesCooldown()
    {
        now = DateTimeOffset.Parse("2026-10-27T16:59:50Z");
        await logic.ClaimAdReward(Id());
        now = now.AddSeconds(10);
        var status = await logic.GetAdRewardStatus();
        Assert.That(status.Ads.Remaining, Is.EqualTo(10));
        Assert.That(status.Ads.RetryAfterSeconds, Is.EqualTo(50));
        now = now.AddSeconds(50);
        Assert.That((await logic.ClaimAdReward(Id())).Ads.Remaining, Is.EqualTo(9));
    }

    [Test]
    public async Task ReplayingAClaimAfterALostResponseDoesNotAwardAgainEvenOnTheNextDay()
    {
        string id = Id();
        await logic.ClaimAdReward(id);
        now = now.AddDays(1);
        var reply = await logic.ClaimAdReward(id);
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(State(reply).Balance(), Is.EqualTo(20));
        Assert.That(store.Saves, Is.EqualTo(1));
    }

    [Test]
    public async Task ConflictsRetryTheWholeWalletAndCounterWrite()
    {
        store.Conflicts = 2;
        var reply = await logic.ClaimAdReward(Id());
        Assert.That(State(reply).Balance(), Is.EqualTo(20));
        Assert.That(reply.Ads.Remaining, Is.EqualTo(9));
        Assert.That(store.Saves, Is.EqualTo(1));
    }

    [Test]
    public async Task ARewardCannotCreateALocklessSaveBeforeShopPreparation()
    {
        store.Json = null;
        var reply = await logic.ClaimAdReward(Id());
        Assert.That(reply.Error, Is.EqualTo(ShopError.Rejected));
        Assert.That(store.Saves, Is.Zero, "first-time creation has no write lock; Prepare must create the shop first");
    }

    [Test]
    public async Task ReadingStatusDoesNotCreateOrChangeTheSave()
    {
        store.Json = null;
        var reply = await logic.GetAdRewardStatus();
        Assert.That(reply.Ads.Coins, Is.EqualTo(20));
        Assert.That(reply.Ads.Remaining, Is.EqualTo(10));
        Assert.That(store.Saves, Is.Zero);
    }

    [Test]
    public async Task InvalidRequestIdsAndUnreadableSavesAreRejected()
    {
        Assert.That((await logic.ClaimAdReward("bad-id")).Error, Is.EqualTo(ShopError.Rejected));
        store.Json = "not json";
        Assert.That((await logic.ClaimAdReward(Id())).Error, Is.EqualTo(ShopError.SaveUnreadable));
        Assert.That(store.Json, Is.EqualTo("not json"));
    }

    [Test]
    public async Task ImportDiscardsAdCountersFromTheDevice()
    {
        store.Json = null;
        string local = "{\"version\":2,\"ads\":{\"day\":\"2026-10-27\",\"claimed\":10}}";
        await logic.Import(local);
        Assert.That((await logic.GetAdRewardStatus()).Ads.Remaining, Is.EqualTo(10));
    }
}
