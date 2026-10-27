using System.Text.Json;
using Lumaquarium.Shop;
using NUnit.Framework;

namespace LumaquariumShop.Tests;

public sealed class PurchaseStatsTests
{
    private const string Version = "v1";
    private const string Pack = "com.thnha.bettafishaquarium.coins_1000";

    // The counts in memory, as months of { itemId: count }. Broken makes every Add throw.
    private sealed class MemoryPurchaseStats : IPurchaseStats
    {
        public readonly Dictionary<string, Dictionary<string, int>> Months = new();
        public bool Broken;

        public Task Add(string month, string itemId, int count)
        {
            if (Broken) throw new InvalidOperationException("down");
            if (!Months.TryGetValue(month, out var counts)) Months[month] = counts = new Dictionary<string, int>();
            counts[itemId] = counts.GetValueOrDefault(itemId) + count;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAppStore : IAppStore
    {
        public readonly Dictionary<string, AppStoreTransaction> Known = new();

        public Task<AppStoreTransaction?> GetTransaction(string transactionId) =>
            Task.FromResult(Known.TryGetValue(transactionId, out var t) ? t : null);
    }

    private MemoryShopStore store = null!;
    private MemoryPurchaseStats stats = null!;
    private FakeAppStore apple = null!;
    private DateTimeOffset now;
    private ShopLogic logic = null!;

    [SetUp]
    public void SetUp()
    {
        var save = new ShopSave();
        ShopSave.Add(save.wallet, ShopSave.Coins, 1000);
        store = new MemoryShopStore { Json = JsonSerializer.Serialize(save, ShopLogic.Json) };
        stats = new MemoryPurchaseStats();
        apple = new FakeAppStore();
        now = new DateTimeOffset(2026, 10, 27, 3, 0, 0, TimeSpan.Zero);
        var catalog = new ShopCatalog
        {
            version = Version,
            items =
            {
                new CatalogItem { id = "koi-nemo", slot = "fish", price = 500 },
                new CatalogItem { id = "tank_deep", slot = "tank", price = 1500 },
                new CatalogItem { id = "starter-stone", slot = "", price = 100 },
            },
            coinPacks = { new CoinPack { productId = Pack, coins = 1000 } },
        };
        logic = new ShopLogic(store, catalog, () => now, apple, stats);
    }

    private int Count(string month, string id) =>
        stats.Months.TryGetValue(month, out var counts) ? counts.GetValueOrDefault(id) : 0;

    [Test]
    public async Task EachPurchaseIsCountedInItsMonth()
    {
        await logic.Buy("starter-stone", Version);
        await logic.Buy("starter-stone", Version);
        await logic.Buy("koi-nemo", Version);
        Assert.That(Count("2026-10", "starter-stone"), Is.EqualTo(2));
        Assert.That(Count("2026-10", "koi-nemo"), Is.EqualTo(1));
    }

    [Test]
    public async Task FailedPurchasesAreNotCounted()
    {
        await logic.Buy("koi-nemo", Version);
        Assert.That((await logic.Buy("koi-nemo", Version)).Error, Is.EqualTo(ShopError.Owned));
        Assert.That((await logic.Buy("tank_deep", Version)).Error, Is.EqualTo(ShopError.NotEnoughCoins));
        Assert.That((await logic.Buy("starter-stone", "old")).Error, Is.EqualTo(ShopError.PriceChanged));
        Assert.That((await logic.Buy("nothing", Version)).Error, Is.EqualTo(ShopError.Rejected));
        Assert.That(stats.Months["2026-10"], Is.EqualTo(new Dictionary<string, int> { ["koi-nemo"] = 1 }));
    }

    [Test]
    public async Task TheMonthIsVietnamTime()
    {
        now = new DateTimeOffset(2026, 10, 31, 17, 30, 0, TimeSpan.Zero); // 00:30 on 1 November in UTC+7
        await logic.Buy("starter-stone", Version);
        Assert.That(Count("2026-11", "starter-stone"), Is.EqualTo(1));
        Assert.That(stats.Months.ContainsKey("2026-10"), Is.False);
    }

    [Test]
    public async Task CountingFailingStillSellsTheItem()
    {
        stats.Broken = true;
        var reply = await logic.Buy("koi-nemo", Version);
        Assert.That(reply.Error, Is.EqualTo(ShopError.None));
        Assert.That(store.Saves, Is.EqualTo(1));
    }

    [Test]
    public async Task ACoinPackIsCountedOnceByQuantity()
    {
        apple.Known["2000000111"] = new AppStoreTransaction
        {
            transactionId = "2000000111", bundleId = AppStoreApi.BundleId, productId = Pack, type = "Consumable", quantity = 2,
        };
        await logic.ClaimAppStorePurchase(Pack, "2000000111");
        await logic.ClaimAppStorePurchase(Pack, "2000000111");
        Assert.That(Count("2026-10", "coinpack:" + Pack), Is.EqualTo(2));
    }
}
