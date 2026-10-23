using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lumaquarium.UI
{
    // A single local save holds the wallet, owned variants, the fish equipped in the tank, decor stock
    // and the tanks bought (AquariumTank) with the one in use.
    public sealed class FishShopInventory
    {
        public const string DefaultStorageKey = "Lumaquarium.FishShop.v1";
        // Used until the starter gift (ApplyStarter) sets the catalog's value.
        public const int DefaultStartingCoins = 1250;
        private readonly string storageKey;
        private SaveData data;

        [Serializable]
        private sealed class SaveData
        {
            public int version = 1;
            public int balance = DefaultStartingCoins;
            public List<string> ownedIds = new List<string>();
            public string activeId = "";
            public List<DecorStock> decor = new List<DecorStock>();
            public bool starterDecorImported;
            public List<string> ownedTanks = new List<string>();
            public string activeTank = "";
            public List<string> ownedWalls = new List<string>();
            public string activeWall = "";
            public List<string> ownedSubstrates = new List<string>();
            public string activeSubstrate = "";
        }

        [Serializable] private sealed class DecorStock { public string id; public int count; }

        public int Balance => data.balance;
        public string ActiveId => data.activeId;
        public string ActiveTank => data.activeTank;
        public string ActiveWall => data.activeWall;
        public string ActiveSubstrate => data.activeSubstrate;

        public FishShopInventory(string storageKey = DefaultStorageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("A storage key is required.", nameof(storageKey));
            this.storageKey = storageKey;
            Reload();
        }

        public void Reload()
        {
            data = new SaveData();
            if (!PlayerPrefs.HasKey(storageKey)) return;
            try
            {
                var saved = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(storageKey));
                if (saved == null || saved.version != 1 || saved.balance < 0 || saved.ownedIds == null
                    || saved.ownedIds.Exists(string.IsNullOrWhiteSpace)
                    || (!string.IsNullOrEmpty(saved.activeId) && !saved.ownedIds.Contains(saved.activeId))) return;
                if (saved.decor == null) saved.decor = new List<DecorStock>();
                if (saved.ownedTanks == null) saved.ownedTanks = new List<string>();
                if (saved.activeTank == null) saved.activeTank = "";
                if (saved.ownedWalls == null) saved.ownedWalls = new List<string>();
                if (saved.activeWall == null) saved.activeWall = "";
                if (saved.ownedSubstrates == null) saved.ownedSubstrates = new List<string>();
                if (saved.activeSubstrate == null) saved.activeSubstrate = "";
                var seen = new HashSet<string>();
                foreach (var stock in saved.decor)
                    if (stock == null || string.IsNullOrWhiteSpace(stock.id) || stock.count < 0 || !seen.Add(stock.id)) return;
                data = saved;
            }
            catch (ArgumentException) { /* Keep a usable wallet when local data is malformed. */ }
        }

        public bool Owns(string id) => !string.IsNullOrWhiteSpace(id) && data.ownedIds.Contains(id);

        public bool TryPurchase(string id, int price)
        {
            Reload();
            if (string.IsNullOrWhiteSpace(id) || price < 0 || Owns(id) || price > data.balance) return false;
            var next = Copy();
            next.balance -= price;
            next.ownedIds.Add(id);
            next.activeId = id;
            Save(next);
            return true;
        }

        public bool SelectOwned(string id)
        {
            Reload();
            if (!Owns(id)) return false;
            if (data.activeId == id) return true;
            var next = Copy();
            next.activeId = id;
            Save(next);
            return true;
        }

        public int DecorCount(string id) => data.decor.Find(x => x.id == id)?.count ?? 0;

        // The starter gift, given once. A new wallet starts with the given coins; an older one keeps its balance.
        // Existing layouts keep their placed starter items when moving from free placement to stock.
        public void ApplyStarter(int coins, Dictionary<string, int> starter)
        {
            Reload();
            if (data.starterDecorImported) return;
            var next = Copy();
            if (!PlayerPrefs.HasKey(storageKey) && coins >= 0) next.balance = coins;
            foreach (var pair in starter)
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                {
                    var stock = next.decor.Find(x => x.id == pair.Key);
                    if (stock == null) next.decor.Add(new DecorStock { id = pair.Key, count = pair.Value });
                    else stock.count = Math.Max(stock.count, pair.Value);
                }
            next.starterDecorImported = true;
            Save(next);
        }

        public bool TryPurchaseDecor(string id, int price)
        {
            Reload();
            if (string.IsNullOrWhiteSpace(id) || price < 0 || price > data.balance || DecorCount(id) == int.MaxValue) return false;
            var next = Copy();
            next.balance -= price;
            var stock = next.decor.Find(x => x.id == id);
            if (stock == null) next.decor.Add(new DecorStock { id = id, count = 1 });
            else stock.count++;
            Save(next);
            return true;
        }

        public bool OwnsTank(string id) => !string.IsNullOrWhiteSpace(id) && data.ownedTanks.Contains(id);

        // A bought tank goes into use at once. A free tank (price 0) counts as owned from the start.
        public bool TryPurchaseTank(string id, int price)
        {
            Reload();
            if (string.IsNullOrWhiteSpace(id) || price < 0 || OwnsTank(id) || price > data.balance) return false;
            var next = Copy();
            next.balance -= price;
            next.ownedTanks.Add(id);
            next.activeTank = id;
            Save(next);
            return true;
        }

        public bool SelectTank(string id, bool free)
        {
            Reload();
            if (string.IsNullOrWhiteSpace(id) || (!free && !OwnsTank(id))) return false;
            if (data.activeTank == id) return true;
            var next = Copy();
            next.activeTank = id;
            Save(next);
            return true;
        }

        // Wall and floor styles (AquariumSurfaceCatalog): bought once, then put into use. Style 0 is free.
        public bool OwnsWall(string id) => !string.IsNullOrWhiteSpace(id) && data.ownedWalls.Contains(id);
        public bool OwnsSubstrate(string id) => !string.IsNullOrWhiteSpace(id) && data.ownedSubstrates.Contains(id);
        public bool TryPurchaseWall(string id, int price) => TryPurchaseStyle(id, price, true);
        public bool TryPurchaseSubstrate(string id, int price) => TryPurchaseStyle(id, price, false);
        public bool SelectWall(string id, bool free) => SelectStyle(id, free, true);
        public bool SelectSubstrate(string id, bool free) => SelectStyle(id, free, false);

        private bool TryPurchaseStyle(string id, int price, bool wall)
        {
            Reload();
            if (string.IsNullOrWhiteSpace(id) || price < 0 || price > data.balance || (wall ? OwnsWall(id) : OwnsSubstrate(id))) return false;
            var next = Copy();
            next.balance -= price;
            if (wall) { next.ownedWalls.Add(id); next.activeWall = id; }
            else { next.ownedSubstrates.Add(id); next.activeSubstrate = id; }
            Save(next);
            return true;
        }

        private bool SelectStyle(string id, bool free, bool wall)
        {
            Reload();
            if (string.IsNullOrWhiteSpace(id) || (!free && !(wall ? OwnsWall(id) : OwnsSubstrate(id)))) return false;
            if ((wall ? data.activeWall : data.activeSubstrate) == id) return true;
            var next = Copy();
            if (wall) next.activeWall = id; else next.activeSubstrate = id;
            Save(next);
            return true;
        }

        private SaveData Copy() => new SaveData
        {
            balance = data.balance,
            ownedIds = new List<string>(data.ownedIds),
            activeId = data.activeId,
            decor = data.decor.ConvertAll(x => new DecorStock { id = x.id, count = x.count }),
            starterDecorImported = data.starterDecorImported,
            ownedTanks = new List<string>(data.ownedTanks),
            activeTank = data.activeTank,
            ownedWalls = new List<string>(data.ownedWalls),
            activeWall = data.activeWall,
            ownedSubstrates = new List<string>(data.ownedSubstrates),
            activeSubstrate = data.activeSubstrate
        };

        private void Save(SaveData next)
        {
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
