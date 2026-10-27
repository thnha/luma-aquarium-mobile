using Lumaquarium.Shop;

namespace LumaquariumShop;

public sealed partial class ShopLogic
{
    // A coin pack bought on the App Store. Apple is asked whether the transaction is real, for this app and this
    // pack and not refunded; the coins are the catalog's. Each transaction pays once: one already in purchases
    // answers None again, so the game can finish it. Apple out of reach is Rejected (the game keeps the purchase
    // and sends it again later); a transaction Apple denies is PurchaseInvalid (the game finishes it, no coins).
    public async Task<ShopReply> ClaimAppStorePurchase(string productId, string transactionId)
    {
        var pack = catalog.FindCoinPack(productId);
        if (pack == null || pack.coins <= 0 || string.IsNullOrEmpty(transactionId) || !transactionId.All(char.IsAsciiDigit) || appStore == null)
            return await Change(_ => ShopError.PurchaseInvalid);
        string key = "apple:" + transactionId;
        var current = await GetState();
        if (Parse(current.State)?.purchases.Contains(key) == true) return current;

        AppStoreTransaction? transaction;
        try { transaction = await appStore.GetTransaction(transactionId); }
        catch (AppStoreUnavailableException) { return await Change(_ => ShopError.Rejected); }
        if (transaction == null || transaction.transactionId != transactionId || transaction.bundleId != AppStoreApi.BundleId
            || transaction.productId != productId || transaction.type != "Consumable" || transaction.revocationDate != null)
            return await Change(_ => ShopError.PurchaseInvalid);

        int coins = (int)Math.Min(int.MaxValue, (long)pack.coins * Math.Max(1, transaction.quantity));
        bool paid = false;
        var reply = await Change(save =>
        {
            paid = false;
            if (save.purchases.Contains(key)) return ShopError.None;
            save.purchases.Add(key);
            ShopSave.Add(save.wallet, ShopSave.Coins, coins);
            paid = true;
            return ShopError.None;
        });
        if (paid && reply.Error == ShopError.None) await Count("coinpack:" + productId, Math.Max(1, transaction.quantity));
        return reply;
    }
}
