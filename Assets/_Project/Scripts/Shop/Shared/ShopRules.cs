#nullable disable
using System.Collections.Generic;

namespace Lumaquarium.Shop
{
    // What the shop allows, run on a copy of the save: by FishShopInventory on the device (local mode) and by the
    // Cloud Code module on the server. A rule that returns an error has not changed the save.
    public static class ShopRules
    {
        // Fish, tanks, walls and floors (a slot) are bought once and put into use; decorations (no slot) again and again.
        public static string Buy(ShopSave save, string id, int price, string slot)
        {
            if (string.IsNullOrWhiteSpace(id) || price < 0) return ShopError.Rejected;
            bool once = !string.IsNullOrEmpty(slot);
            if (once ? save.Owns(id) : save.CountOf(id) == int.MaxValue) return ShopError.Owned;
            if (price > save.Balance()) return ShopError.NotEnoughCoins;
            ShopSave.Add(save.wallet, ShopSave.Coins, -price);
            ShopSave.Add(save.items, id, 1);
            if (once) save.Equip(slot, id);
            return ShopError.None;
        }

        public static string Select(ShopSave save, string slot, string id)
        {
            if (string.IsNullOrEmpty(slot) || !save.Owns(id)) return ShopError.Rejected;
            save.Equip(slot, id);
            return ShopError.None;
        }

        // Gives each pack the player has not had yet, in order. A gift with a slot goes into use only when nothing of
        // that kind is. A slot still empty after a version 1 save was moved over (the tank's first look was in use
        // without being owned) is filled from the starter pack, before any other pack can fill it. True when changed.
        public static bool ApplyGifts(ShopSave save, IEnumerable<GiftPack> packs)
        {
            if (packs == null) return false;
            var list = new List<GiftPack>(packs);
            bool changed = false;
            if (save.grantsApplied.Contains(ShopSave.StarterPack))
                foreach (var pack in list)
                    if (pack != null && pack.id == ShopSave.StarterPack && pack.gifts != null)
                        foreach (var gift in pack.gifts)
                            if (gift != null && !string.IsNullOrEmpty(gift.slot) && !string.IsNullOrWhiteSpace(gift.id)
                                && string.IsNullOrEmpty(save.Equipped(gift.slot)))
                            {
                                ShopSave.AtLeast(save.items, gift.id, 1);
                                save.Equip(gift.slot, gift.id);
                                changed = true;
                            }
            foreach (var pack in list)
            {
                if (pack == null || string.IsNullOrWhiteSpace(pack.id) || save.grantsApplied.Contains(pack.id)) continue;
                foreach (var gift in pack.gifts ?? new List<Gift>())
                {
                    if (gift == null || string.IsNullOrWhiteSpace(gift.id) || gift.count < 0) continue;
                    if (gift.id == ShopSave.Coins) { ShopSave.Add(save.wallet, ShopSave.Coins, gift.count); continue; }
                    ShopSave.Add(save.items, gift.id, gift.count);
                    if (gift.count > 0 && !string.IsNullOrEmpty(gift.slot) && string.IsNullOrEmpty(save.Equipped(gift.slot)))
                        save.Equip(gift.slot, gift.id);
                }
                save.grantsApplied.Add(pack.id);
                changed = true;
            }
            return changed;
        }

        // Pieces already placed in the tank never count for more than the stock holds (layouts from before stock).
        // Local mode only: the server never takes a count from the device. True when changed.
        public static bool KeepPlacedInStock(ShopSave save, IReadOnlyDictionary<string, int> placed)
        {
            if (placed == null) return false;
            bool changed = false;
            foreach (var pair in placed)
                if (!string.IsNullOrWhiteSpace(pair.Key) && save.CountOf(pair.Key) < pair.Value)
                {
                    ShopSave.AtLeast(save.items, pair.Key, pair.Value);
                    changed = true;
                }
            return changed;
        }

        // A save kept on the device before the server, brought over once: checked like any save, its coins capped
        // so a save edited by hand cannot bring a fortune. Null when it cannot be used.
        public static ShopSave Import(ShopSave old, int maxCoins)
        {
            var save = ShopSave.Checked(old);
            if (save == null) return null;
            int coins = save.Balance();
            if (coins > maxCoins) ShopSave.Add(save.wallet, ShopSave.Coins, maxCoins - coins);
            save.version = ShopSave.CurrentVersion;
            return save;
        }
    }
}
