using System.Text.Json;
using Lumaquarium.Shop;

namespace LumaquariumShop;

// The shop as the server runs it. Every change reads the player's save, runs one ShopRules rule on it and writes
// it back with the write lock it was read with, so two calls at once cannot both spend the same coins; a conflict
// reads again and runs the rule again. Every reply carries the save as it now is.
public sealed class ShopLogic
{
    public const int ImportCoinCap = 5000;
    public const int ImportItemCap = 99;
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

    // An id the catalog or a gift knows can only go into its own slot.
    public Task<ShopReply> Select(string slot, string id)
    {
        string? known = SlotOf(id);
        return Change(save => ShopSave.IsSlot(slot) && (known == null || known == slot) ? ShopRules.Select(save, slot, id) : ShopError.Rejected);
    }

    public Task<ShopReply> ApplyGifts() => Change(save =>
    {
        ShopRules.ApplyGifts(save, catalog.giftPacks);
        return ShopError.None;
    });

    // Only for a player with no save on the server yet. Cloud Save has no create-only write, so the write is
    // checked by reading it back: if another call created the save first, theirs stays.
    public async Task<ShopReply> Import(string? json)
    {
        var stored = await store.Load();
        if (stored.Json != null) return Reply(ShopError.AlreadyImported, stored.Json);
        var save = Filtered(ShopRules.Import(Parse(json), ImportCoinCap));
        if (save == null) return Reply(ShopError.Rejected, null);
        string next = JsonSerializer.Serialize(save, Json);
        try { await store.Save(next, null); }
        catch (WriteConflictException) { return Reply(ShopError.Rejected, (await store.Load()).Json); }
        var now = await store.Load();
        return now.Json == next ? Reply(ShopError.None, next) : Reply(ShopError.AlreadyImported, now.Json);
    }

    // What a save edited by hand may keep: only what the catalog or a gift pack knows, a slot item once, a
    // decoration up to ImportItemCap, only coins in the wallet, the starter pack always counted as had.
    private ShopSave? Filtered(ShopSave? save)
    {
        if (save == null) return null;
        var packs = catalog.giftPacks.Select(p => p.id).ToHashSet();
        save.wallet.RemoveAll(x => x.id != ShopSave.Coins);
        save.items.RemoveAll(x => SlotOf(x.id) == null);
        foreach (var x in save.items) x.count = Math.Min(x.count, SlotOf(x.id) == "" ? ImportItemCap : 1);
        save.items.RemoveAll(x => x.count == 0);
        save.grantsApplied.RemoveAll(x => x != ShopSave.StarterPack && !packs.Contains(x));
        if (!save.grantsApplied.Contains(ShopSave.StarterPack)) save.grantsApplied.Add(ShopSave.StarterPack);
        save.equipped.RemoveAll(x => !ShopSave.IsSlot(x.slot) || !save.Owns(x.id) || SlotOf(x.id) != x.slot);
        return save;
    }

    // The slot of an id the catalog or a gift pack knows ("" for a decoration); null for any other id.
    private string? SlotOf(string id)
    {
        var item = catalog.Find(id);
        if (item != null) return item.slot ?? "";
        foreach (var pack in catalog.giftPacks)
            foreach (var gift in pack.gifts)
                if (gift.id == id && id != ShopSave.Coins) return gift.slot ?? "";
        return null;
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
                // A write that creates the save has no lock to check, so read it back.
                if (stored.Json == null && (await store.Load()).Json != after) continue;
                return Reply(ShopError.None, after);
            }
            catch (WriteConflictException) { }
        }
        return Reply(ShopError.Rejected, null);
    }

    private static ShopSave? Parse(string? json)
    {
        if (json == null) return null;
        try { return ShopSave.Checked(JsonSerializer.Deserialize<ShopSave>(json, Json)); }
        catch (JsonException) { return null; }
    }

    private static ShopReply Reply(string error, string? state) => new() { Error = error, State = state };
}
