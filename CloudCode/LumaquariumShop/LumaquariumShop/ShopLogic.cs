using System.Text.Json;
using Lumaquarium.Shop;

namespace LumaquariumShop;

// The shop as the server runs it. Every change reads the player's save, runs one ShopRules rule on it and writes
// it back with the write lock it was read with, so two calls at once cannot both spend the same coins; a conflict
// reads again and runs the rule again. Every reply carries the save as it now is.
public sealed class ShopLogic
{
    public const int ImportCoinCap = 5000;
    private const int Attempts = 3;
    public static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    private readonly IShopStore store;
    private readonly ShopCatalog catalog;

    public ShopLogic(IShopStore store, ShopCatalog catalog)
    {
        this.store = store;
        this.catalog = catalog;
    }

    public Task<ShopReply> GetState() => Change(_ => ShopError.None);

    // The price is the catalog's: the game only names the item and the catalog version it shows.
    public Task<ShopReply> Buy(string id, string catalogVersion)
    {
        if (catalogVersion != catalog.version) return Change(_ => ShopError.PriceChanged);
        var item = catalog.Find(id);
        return item == null ? Change(_ => ShopError.Rejected) : Change(save => ShopRules.Buy(save, item.id, item.price, item.slot));
    }

    public Task<ShopReply> Select(string slot, string id) =>
        Change(save => ShopSave.IsSlot(slot) ? ShopRules.Select(save, slot, id) : ShopError.Rejected);

    public Task<ShopReply> ApplyGifts() => Change(save =>
    {
        ShopRules.ApplyGifts(save, catalog.giftPacks);
        return ShopError.None;
    });

    // Only for a player with no save on the server yet.
    public async Task<ShopReply> Import(string json)
    {
        var stored = await store.Load();
        if (stored.Json != null) return Reply(ShopError.AlreadyImported, stored.Json);
        var save = ShopRules.Import(Parse(json), ImportCoinCap);
        if (save == null) return Reply(ShopError.Rejected, null);
        string next = JsonSerializer.Serialize(save, Json);
        await store.Save(next, null);
        return Reply(ShopError.None, next);
    }

    private async Task<ShopReply> Change(Func<ShopSave, string> rule)
    {
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var stored = await store.Load();
            var save = stored.Json == null ? new ShopSave() : Parse(stored.Json);
            if (save == null) return Reply(ShopError.SaveUnreadable, null);
            string before = JsonSerializer.Serialize(save, Json);
            string error = rule(save);
            if (error != ShopError.None) return Reply(error, before);
            string after = JsonSerializer.Serialize(save, Json);
            if (after == before) return Reply(ShopError.None, after);
            try
            {
                await store.Save(after, stored.WriteLock);
                return Reply(ShopError.None, after);
            }
            catch (WriteConflictException) { }
        }
        return Reply(ShopError.Rejected, null);
    }

    private static ShopSave? Parse(string json)
    {
        try { return ShopSave.Checked(JsonSerializer.Deserialize<ShopSave>(json, Json)); }
        catch (JsonException) { return null; }
    }

    private static ShopReply Reply(string error, string? state) => new() { Error = error, State = state };
}
