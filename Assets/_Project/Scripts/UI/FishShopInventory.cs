using System;
using System.Collections.Generic;
using Lumaquarium.Shop;
using UnityEngine;

namespace Lumaquarium.UI
{
    // A single local save holds the wallet, everything owned (fish, decorations, tanks, wall and floor styles) and
    // what is in use (ShopSave); what may change it is ShopRules, shared with the Cloud Code module. With the server
    // in use (ShopService) this save is a copy of the server's last answer, kept for reading and for playing offline.
    // Save version 2. A version 1 save is moved over once and kept under <key>.v1.bak. A save that cannot be read
    // is never written over: the inventory stays empty, LoadFailed is set and every change is refused.
    public sealed class FishShopInventory
    {
        // The key keeps its old name: it names the save, not its version.
        public const string DefaultStorageKey = "Lumaquarium.FishShop.v1";
        public const string Coins = ShopSave.Coins;
        public const string StarterPack = ShopSave.StarterPack;
        public const string FishSlot = ShopSave.FishSlot, WallSlot = ShopSave.WallSlot, FloorSlot = ShopSave.FloorSlot, TankSlot = ShopSave.TankSlot;
        private readonly string storageKey;
        private ShopSave data;

        // Version 1, read only to move it over.
        [Serializable]
        private sealed class SaveV1
        {
            public int balance;
            public List<string> ownedIds;
            public string activeId;
            public List<Amount> decor;
            public bool starterDecorImported;
            public List<string> ownedTanks, ownedWalls, ownedSubstrates;
            public string activeTank, activeWall, activeSubstrate;
        }

        [Serializable] private sealed class VersionOnly { public int version; }

        public bool LoadFailed { get; private set; }
        public string StorageKey => storageKey;
        public string BackupKey => storageKey + ".v1.bak";
        public int Balance => data.Balance();
        public string ActiveId => data.Equipped(FishSlot);
        public string ActiveTank => data.Equipped(TankSlot);
        public string ActiveWall => data.Equipped(WallSlot);
        public string ActiveSubstrate => data.Equipped(FloorSlot);

        public FishShopInventory(string storageKey = DefaultStorageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("A storage key is required.", nameof(storageKey));
            this.storageKey = storageKey;
            Reload();
        }

        public void Reload()
        {
            data = new ShopSave();
            LoadFailed = false;
            if (!PlayerPrefs.HasKey(storageKey)) return;
            string json = PlayerPrefs.GetString(storageKey);
            ShopSave loaded = null;
            int version = 0;
            try
            {
                version = JsonUtility.FromJson<VersionOnly>(json)?.version ?? 0;
                loaded = version == ShopSave.CurrentVersion ? ShopSave.Checked(JsonUtility.FromJson<ShopSave>(json))
                    : version == 1 ? Migrate(JsonUtility.FromJson<SaveV1>(json)) : null;
            }
            catch (ArgumentException) { }
            if (loaded == null)
            {
                LoadFailed = true;
                Debug.LogError($"[Inventory] Save {storageKey} (version {version}) cannot be read; it is kept as it is and nothing is saved.");
                return;
            }
            if (version == 1)
            {
                if (!PlayerPrefs.HasKey(BackupKey)) PlayerPrefs.SetString(BackupKey, json);
                Save(loaded);
            }
            else data = loaded;
        }

        // Everything owned or in use stays owned; whatever was in use (free or bought) is owned from now on.
        // Version 1 players were given their starting coins already, so the starter pack counts as applied.
        private static ShopSave Migrate(SaveV1 old)
        {
            if (old == null || old.balance < 0 || old.ownedIds == null || old.ownedIds.Exists(string.IsNullOrWhiteSpace)
                || (!string.IsNullOrEmpty(old.activeId) && !old.ownedIds.Contains(old.activeId))) return null;
            var save = new ShopSave();
            ShopSave.Add(save.wallet, Coins, old.balance);
            foreach (var list in new[] { old.ownedIds, old.ownedTanks, old.ownedWalls, old.ownedSubstrates })
                if (list != null)
                    foreach (string id in list)
                        if (!string.IsNullOrWhiteSpace(id)) ShopSave.AtLeast(save.items, id, 1);
            var seen = new HashSet<string>();
            foreach (var stock in old.decor ?? new List<Amount>())
            {
                if (stock == null || string.IsNullOrWhiteSpace(stock.id) || stock.count < 0 || !seen.Add(stock.id)) return null;
                ShopSave.Add(save.items, stock.id, stock.count);
            }
            foreach (var (slot, id) in new[] { (FishSlot, old.activeId), (TankSlot, old.activeTank), (WallSlot, old.activeWall), (FloorSlot, old.activeSubstrate) })
                if (!string.IsNullOrWhiteSpace(id)) { save.Equip(slot, id); ShopSave.AtLeast(save.items, id, 1); }
            save.grantsApplied.Add(StarterPack);
            return ShopSave.Checked(save);
        }

        public bool Owns(string id) => data.Owns(id);
        public bool OwnsTank(string id) => Owns(id);
        public bool OwnsWall(string id) => Owns(id);
        public bool OwnsSubstrate(string id) => Owns(id);
        public int DecorCount(string id) => data.CountOf(id);

        // Fish, tanks, walls and floors are bought once and put into use; decorations are bought again and again.
        public bool TryPurchase(string id, int price) => Buy(id, price, FishSlot) == ShopError.None;
        public bool TryPurchaseDecor(string id, int price) => Buy(id, price, null) == ShopError.None;
        public bool TryPurchaseTank(string id, int price) => Buy(id, price, TankSlot) == ShopError.None;
        public bool TryPurchaseWall(string id, int price) => Buy(id, price, WallSlot) == ShopError.None;
        public bool TryPurchaseSubstrate(string id, int price) => Buy(id, price, FloorSlot) == ShopError.None;

        public bool SelectOwned(string id) => Select(FishSlot, id) == ShopError.None;
        public bool SelectTank(string id) => Select(TankSlot, id) == ShopError.None;
        public bool SelectWall(string id) => Select(WallSlot, id) == ShopError.None;
        public bool SelectSubstrate(string id) => Select(FloorSlot, id) == ShopError.None;

        // ShopError.None when bought; slot is null or "" for a decoration.
        public string Buy(string id, int price, string slot) => Change(next => ShopRules.Buy(next, id, price, slot));

        public string Select(string slot, string id) => Change(next => ShopRules.Select(next, slot, id));

        // Only for an Editor coin pack bought on the Unity IAP Fake Store with the local rules (IAPManager).
        public string AddCoins(int coins) => Change(next =>
        {
            ShopSave.Add(next.wallet, Coins, coins);
            return ShopError.None;
        });

        public void ApplyGifts(IEnumerable<AquariumGiftPack> packs)
        {
            Reload();
            if (LoadFailed || packs == null) return;
            var next = data.Copy();
            if (ShopRules.ApplyGifts(next, ToShared(packs))) Save(next);
        }

        public void KeepPlacedInStock(IReadOnlyDictionary<string, int> placed)
        {
            Reload();
            if (LoadFailed || placed == null) return;
            var next = data.Copy();
            if (ShopRules.KeepPlacedInStock(next, placed)) Save(next);
        }

        // The server's answer (ShopReply.State) becomes the save. A save that could not be read is kept under
        // <key>.unreadable.bak first, as the server's copy is the one that counts. False when the answer is not a save.
        public bool ReplaceWith(string json)
        {
            ShopSave next = null;
            try { next = ShopSave.Checked(JsonUtility.FromJson<ShopSave>(json)); }
            catch (ArgumentException) { }
            if (next == null)
            {
                Debug.LogError("[Inventory] The server sent a save that cannot be read; the save is kept as it is.");
                return false;
            }
            if (LoadFailed && PlayerPrefs.HasKey(storageKey)) PlayerPrefs.SetString(storageKey + ".unreadable.bak", PlayerPrefs.GetString(storageKey));
            LoadFailed = false;
            Save(next);
            return true;
        }

        // The save as stored (a version 1 save already moved over), or null when there is none or it cannot be read.
        public string StoredJson()
        {
            Reload();
            return !LoadFailed && PlayerPrefs.HasKey(storageKey) ? PlayerPrefs.GetString(storageKey) : null;
        }

        public static List<GiftPack> ToShared(IEnumerable<AquariumGiftPack> packs)
        {
            var list = new List<GiftPack>();
            foreach (var pack in packs ?? new AquariumGiftPack[0])
            {
                if (pack == null) { list.Add(null); continue; }
                var shared = new GiftPack { id = pack.id };
                foreach (var gift in pack.gifts ?? new AquariumGift[0])
                    shared.gifts.Add(gift == null ? null : new Gift { id = gift.id, slot = gift.slot, count = gift.count });
                list.Add(shared);
            }
            return list;
        }

        // Runs one rule on a copy and saves the copy when the rule allows the change.
        private string Change(Func<ShopSave, string> rule)
        {
            Reload();
            if (LoadFailed) return ShopError.SaveUnreadable;
            var next = data.Copy();
            string error = rule(next);
            if (error == ShopError.None) Save(next);
            return error;
        }

        private void Save(ShopSave next)
        {
            if (LoadFailed) throw new InvalidOperationException("The save could not be read; it is not written over.");
            string previous = PlayerPrefs.GetString(storageKey, "");
            bool existed = PlayerPrefs.HasKey(storageKey);
            try
            {
                PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(next));
                PlayerPrefs.Save();
                data = next;
            }
            catch
            {
                if (existed) PlayerPrefs.SetString(storageKey, previous);
                else PlayerPrefs.DeleteKey(storageKey);
                throw;
            }
        }
    }
}
