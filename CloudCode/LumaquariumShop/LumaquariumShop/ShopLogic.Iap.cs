using System.Security.Cryptography;
using System.Text;
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

        return await PayCoinPack(key, productId, pack.coins, transaction.quantity);
    }

    // A coin pack bought on Google Play; the game sends the purchase token, which it consumes only after this answers
    // None or PurchaseInvalid. Google is asked whether the purchase of this pack in this app is real, paid and not
    // refunded. One Google already shows consumed was paid out by its first claim (to this or another player), so
    // only the save's own key lets it answer None again. Google out of reach is Rejected, like the App Store.
    public async Task<ShopReply> ClaimGooglePlayPurchase(string productId, string purchaseToken)
    {
        var pack = catalog.FindCoinPack(productId);
        if (pack == null || pack.coins <= 0 || !IsPurchaseToken(purchaseToken) || googlePlay == null)
            return await Change(_ => ShopError.PurchaseInvalid);
        string key = "google:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(purchaseToken)))[..32].ToLowerInvariant();
        var current = await GetState();
        if (Parse(current.State)?.purchases.Contains(key) == true) return current;

        GooglePlayPurchase? purchase;
        try { purchase = await googlePlay.GetPurchase(productId, purchaseToken); }
        catch (GooglePlayUnavailableException) { return await Change(_ => ShopError.Rejected); }
        // Pending (2) is not paid yet: Rejected keeps it in the game, which sends it again once Google says it is.
        if (purchase is { purchaseState: 2 }) return await Change(_ => ShopError.Rejected);
        if (purchase == null || purchase.purchaseState != 0 || purchase.consumptionState != 0)
            return await Change(_ => ShopError.PurchaseInvalid);

        return await PayCoinPack(key, productId, pack.coins, purchase.quantity ?? 1);
    }

    private static bool IsPurchaseToken(string token) =>
        !string.IsNullOrEmpty(token) && token.Length <= 1024 && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    private async Task<ShopReply> PayCoinPack(string key, string productId, int packCoins, int quantity)
    {
        quantity = Math.Max(1, quantity);
        int coins = (int)Math.Min(int.MaxValue, (long)packCoins * quantity);
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
        if (paid && reply.Error == ShopError.None) await Count("coinpack:" + productId, quantity);
        return reply;
    }
}
