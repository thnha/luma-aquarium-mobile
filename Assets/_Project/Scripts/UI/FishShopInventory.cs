using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lumaquarium.UI
{
    // A single local save holds the wallet, everything owned (fish, decorations, tanks, wall and floor styles) and
    // what is in use. Owning is having a count above 0; a price of 0 only means the item is given for nothing.
    // Save version 2. A version 1 save is moved over once and kept under <key>.v1.bak. A save that cannot be read
    // is never written over: the inventory stays empty, LoadFailed is set and every change is refused.
    public sealed class FishShopInventory
    {
        // The key keeps its old name: it names the save, not its version.
        public const string DefaultStorageKey = "Lumaquarium.FishShop.v1";
        public const string Coins = "xu";
        public const string StarterPack = "starter_v1";
        public const string FishSlot = "fish", WallSlot = "wall", FloorSlot = "floor", TankSlot = "tank";
        private const int Version = 2;
        private readonly string storageKey;
        private SaveData data;

        [Serializable] private sealed class Amount { public string id; public int count; }
        [Serializable] private sealed class InUse { public string slot, id; }

        [Serializable]
        private sealed class SaveData
        {
            public int version = Version;
            public List<Amount> wallet = new List<Amount>();
            public List<Amount> items = new List<Amount>();
            public List<InUse> equipped = new List<InUse>();
            public List<string> grantsApplied = new List<string>();
        }

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
        public string BackupKey => storageKey + ".v1.bak";
        public int Balance => Count(data.wallet, Coins);
        public string ActiveId => Equipped(data, FishSlot);
        public string ActiveTank => Equipped(data, TankSlot);
        public string ActiveWall => Equipped(data, WallSlot);
        public string ActiveSubstrate => Equipped(data, FloorSlot);

        public FishShopInventory(string storageKey = DefaultStorageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("A storage key is required.", nameof(storageKey));
            this.storageKey = storageKey;
            Reload();
        }

        public void Reload()
        {
            data = new SaveData();
            LoadFailed = false;
            if (!PlayerPrefs.HasKey(storageKey)) return;
            string json = PlayerPrefs.GetString(storageKey);
            SaveData loaded = null;
            int version = 0;
            try
            {
                version = JsonUtility.FromJson<VersionOnly>(json)?.version ?? 0;
                loaded = version == Version ? Checked(JsonUtility.FromJson<SaveData>(json))
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

        // Null when the save breaks a rule the rest of the code relies on.
        private static SaveData Checked(SaveData save)
        {
            if (save == null) return null;
            save.wallet ??= new List<Amount>();
            save.items ??= new List<Amount>();
            save.equipped ??= new List<InUse>();
            save.grantsApplied ??= new List<string>();
            bool ValidAmounts(List<Amount> list)
            {
                var seen = new HashSet<string>();
                foreach (var x in list)
                    if (x == null || string.IsNullOrWhiteSpace(x.id) || x.count < 0 || !seen.Add(x.id)) return false;
                return true;
            }
            var slots = new HashSet<string>();
            foreach (var x in save.equipped)
                if (x == null || string.IsNullOrWhiteSpace(x.slot) || string.IsNullOrWhiteSpace(x.id) || !slots.Add(x.slot)) return null;
            return ValidAmounts(save.wallet) && ValidAmounts(save.items) && !save.grantsApplied.Exists(string.IsNullOrWhiteSpace) ? save : null;
        }

        // Everything owned or in use stays owned; whatever was in use (free or bought) is owned from now on.
        // Version 1 players were given their starting coins already, so the starter pack counts as applied.
        private static SaveData Migrate(SaveV1 old)
        {
            if (old == null || old.balance < 0 || old.ownedIds == null || old.ownedIds.Exists(string.IsNullOrWhiteSpace)
                || (!string.IsNullOrEmpty(old.activeId) && !old.ownedIds.Contains(old.activeId))) return null;
            var save = new SaveData();
            Add(save.wallet, Coins, old.balance);
            foreach (var list in new[] { old.ownedIds, old.ownedTanks, old.ownedWalls, old.ownedSubstrates })
                if (list != null)
                    foreach (string id in list)
                        if (!string.IsNullOrWhiteSpace(id)) AtLeast(save.items, id, 1);
            var seen = new HashSet<string>();
            foreach (var stock in old.decor ?? new List<Amount>())
            {
                if (stock == null || string.IsNullOrWhiteSpace(stock.id) || stock.count < 0 || !seen.Add(stock.id)) return null;
                Add(save.items, stock.id, stock.count);
            }
            foreach (var (slot, id) in new[] { (FishSlot, old.activeId), (TankSlot, old.activeTank), (WallSlot, old.activeWall), (FloorSlot, old.activeSubstrate) })
                if (!string.IsNullOrWhiteSpace(id)) { Equip(save, slot, id); AtLeast(save.items, id, 1); }
            save.grantsApplied.Add(StarterPack);
            return Checked(save);
        }

        public bool Owns(string id) => !string.IsNullOrWhiteSpace(id) && Count(data.items, id) > 0;
        public bool OwnsTank(string id) => Owns(id);
        public bool OwnsWall(string id) => Owns(id);
        public bool OwnsSubstrate(string id) => Owns(id);
        public int DecorCount(string id) => Count(data.items, id);

        // Fish, tanks, walls and floors are bought once and put into use; decorations are bought again and again.
        public bool TryPurchase(string id, int price) => Buy(id, price, FishSlot);
        public bool TryPurchaseDecor(string id, int price) => Buy(id, price, null);
        public bool TryPurchaseTank(string id, int price) => Buy(id, price, TankSlot);
        public bool TryPurchaseWall(string id, int price) => Buy(id, price, WallSlot);
        public bool TryPurchaseSubstrate(string id, int price) => Buy(id, price, FloorSlot);

        public bool SelectOwned(string id) => Select(FishSlot, id);
        public bool SelectTank(string id) => Select(TankSlot, id);
        public bool SelectWall(string id) => Select(WallSlot, id);
        public bool SelectSubstrate(string id) => Select(FloorSlot, id);

        private bool Buy(string id, int price, string slot)
        {
            Reload();
            if (LoadFailed || string.IsNullOrWhiteSpace(id) || price < 0 || price > Balance) return false;
            if (slot != null ? Owns(id) : DecorCount(id) == int.MaxValue) return false;
            var next = Copy();
            Add(next.wallet, Coins, -price);
            Add(next.items, id, 1);
            if (slot != null) Equip(next, slot, id);
            Save(next);
            return true;
        }

        private bool Select(string slot, string id)
        {
            Reload();
            if (LoadFailed || !Owns(id)) return false;
            if (Equipped(data, slot) == id) return true;
            var next = Copy();
            Equip(next, slot, id);
            Save(next);
            return true;
        }

        // Gives each pack the player has not had yet, in order. A gift with a slot goes into use only when nothing of
        // that kind is. A slot still empty after a version 1 save was moved over (the tank's first look was in use
        // without being owned) is filled from the starter pack, before any other pack can fill it.
        public void ApplyGifts(IEnumerable<AquariumGiftPack> packs)
        {
            Reload();
            if (LoadFailed || packs == null) return;
            var next = Copy();
            bool changed = false;
            if (next.grantsApplied.Contains(StarterPack))
                foreach (var pack in packs)
                    if (pack != null && pack.id == StarterPack && pack.gifts != null)
                        foreach (var gift in pack.gifts)
                            if (gift != null && !string.IsNullOrEmpty(gift.slot) && !string.IsNullOrWhiteSpace(gift.id)
                                && string.IsNullOrEmpty(Equipped(next, gift.slot)))
                            {
                                AtLeast(next.items, gift.id, 1);
                                Equip(next, gift.slot, gift.id);
                                changed = true;
                            }
            foreach (var pack in packs)
            {
                if (pack == null || string.IsNullOrWhiteSpace(pack.id) || next.grantsApplied.Contains(pack.id)) continue;
                foreach (var gift in pack.gifts ?? new AquariumGift[0])
                {
                    if (gift == null || string.IsNullOrWhiteSpace(gift.id) || gift.count < 0) continue;
                    if (gift.id == Coins) { Add(next.wallet, Coins, gift.count); continue; }
                    Add(next.items, gift.id, gift.count);
                    if (gift.count > 0 && !string.IsNullOrEmpty(gift.slot) && string.IsNullOrEmpty(Equipped(next, gift.slot)))
                        Equip(next, gift.slot, gift.id);
                }
                next.grantsApplied.Add(pack.id);
                changed = true;
            }
            if (changed) Save(next);
        }

        // Pieces already placed in the tank never count for more than the stock holds (layouts from before stock).
        public void KeepPlacedInStock(IReadOnlyDictionary<string, int> placed)
        {
            Reload();
            if (LoadFailed || placed == null) return;
            var next = Copy();
            bool changed = false;
            foreach (var pair in placed)
                if (!string.IsNullOrWhiteSpace(pair.Key) && Count(next.items, pair.Key) < pair.Value)
                {
                    AtLeast(next.items, pair.Key, pair.Value);
                    changed = true;
                }
            if (changed) Save(next);
        }

        private static int Count(List<Amount> list, string id) => list.Find(x => x.id == id)?.count ?? 0;

        private static void Add(List<Amount> list, string id, int amount)
        {
            var entry = list.Find(x => x.id == id);
            if (entry == null) list.Add(new Amount { id = id, count = Math.Max(0, amount) });
            else entry.count = (int)Math.Clamp((long)entry.count + amount, 0, int.MaxValue);
        }

        private static void AtLeast(List<Amount> list, string id, int count)
        {
            if (Count(list, id) < count) Add(list, id, count - Count(list, id));
        }

        private static string Equipped(SaveData save, string slot) => save.equipped.Find(x => x.slot == slot)?.id ?? "";

        private static void Equip(SaveData save, string slot, string id)
        {
            var entry = save.equipped.Find(x => x.slot == slot);
            if (entry == null) save.equipped.Add(new InUse { slot = slot, id = id });
            else entry.id = id;
        }

        private SaveData Copy() => new SaveData
        {
            wallet = data.wallet.ConvertAll(x => new Amount { id = x.id, count = x.count }),
            items = data.items.ConvertAll(x => new Amount { id = x.id, count = x.count }),
            equipped = data.equipped.ConvertAll(x => new InUse { slot = x.slot, id = x.id }),
            grantsApplied = new List<string>(data.grantsApplied)
        };

        private void Save(SaveData next)
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
