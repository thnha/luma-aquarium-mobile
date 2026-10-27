#nullable disable
using System;
using System.Collections.Generic;

namespace Lumaquarium.Shop
{
    [Serializable] public sealed class Gift { public string id, slot; public int count; }
    [Serializable] public sealed class GiftPack { public string id; public List<Gift> gifts = new List<Gift>(); }
    // slot is "fish", "wall", "floor" or "tank" for what is bought once and put into use, "" for a decoration.
    [Serializable] public sealed class CatalogItem { public string id, slot; public int price; }
    // A coin pack sold for real money: productId is the App Store product (Consumable), coins what the server adds.
    [Serializable] public sealed class CoinPack { public string productId; public int coins; }

    // What the server sells, written from the price sheet by ServerCatalogWriter (Editor) into the Cloud Code module.
    // version is a hash of the rest; the game sends the version it shows, and a different one is refused.
    [Serializable]
    public sealed class ShopCatalog
    {
        public string version = "";
        public List<CatalogItem> items = new List<CatalogItem>();
        public List<GiftPack> giftPacks = new List<GiftPack>();
        public AdRewardSettings adRewards = new AdRewardSettings();
        public List<CoinPack> coinPacks = DefaultCoinPacks();

        // The packs created in App Store Connect (docs/iap-ios.md). The game lists these; the server pays out its own copy.
        public static List<CoinPack> DefaultCoinPacks() => new List<CoinPack>
        {
            new CoinPack { productId = "com.thnha.bettafishaquarium.coins_1000", coins = 1000 },
            new CoinPack { productId = "com.thnha.bettafishaquarium.coins_6000", coins = 6000 },
            new CoinPack { productId = "com.thnha.bettafishaquarium.coins_10000", coins = 10000 }
        };

        public CatalogItem Find(string id) => string.IsNullOrWhiteSpace(id) ? null : items.Find(x => x.id == id);
        public CoinPack FindCoinPack(string productId) => string.IsNullOrWhiteSpace(productId) ? null : coinPacks?.Find(x => x.productId == productId);
    }
}
