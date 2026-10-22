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
        private readonly string storageKey;
        private SaveData data;

        [Serializable]
        private sealed class SaveData
        {
            public int version = 1;
            public int balance = 1250;
            public List<string> ownedIds = new List<string>();
            public string activeId = "";
            public List<DecorStock> decor = new List<DecorStock>();
            public bool starterDecorImported;
            public List<string> ownedTanks = new List<string>();
            public string activeTank = "";
        }

        [Serializable] private sealed class DecorStock { public string id; public int count; }

        public int Balance => data.balance;
        public string ActiveId => data.activeId;
        public string ActiveTank => data.activeTank;

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

        // Existing layouts keep their placed starter items when moving from free placement to stock.
        public void ImportStarterDecor(Dictionary<string, int> starter)
        {
            Reload();
            if (data.starterDecorImported) return;
            var next = Copy();
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

        private SaveData Copy() => new SaveData
        {
            balance = data.balance,
            ownedIds = new List<string>(data.ownedIds),
            activeId = data.activeId,
            decor = data.decor.ConvertAll(x => new DecorStock { id = x.id, count = x.count }),
            starterDecorImported = data.starterDecorImported,
            ownedTanks = new List<string>(data.ownedTanks),
            activeTank = data.activeTank
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
