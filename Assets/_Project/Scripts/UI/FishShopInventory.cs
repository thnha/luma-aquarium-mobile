using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lumaquarium.UI
{
    // A single local save holds the wallet, owned variants and the fish equipped in the tank.
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
        }

        public int Balance => data.balance;
        public string ActiveId => data.activeId;

        public FishShopInventory(string storageKey = DefaultStorageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("A storage key is required.", nameof(storageKey));
            this.storageKey = storageKey;
            data = new SaveData();
            if (!PlayerPrefs.HasKey(storageKey)) return;
            try
            {
                var saved = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(storageKey));
                if (saved == null || saved.version != 1 || saved.balance < 0 || saved.ownedIds == null
                    || saved.ownedIds.Exists(string.IsNullOrWhiteSpace)
                    || (!string.IsNullOrEmpty(saved.activeId) && !saved.ownedIds.Contains(saved.activeId))) return;
                data = saved;
            }
            catch (ArgumentException) { /* Keep a usable wallet when local data is malformed. */ }
        }

        public bool Owns(string id) => !string.IsNullOrWhiteSpace(id) && data.ownedIds.Contains(id);

        public bool TryPurchase(string id, int price)
        {
            if (string.IsNullOrWhiteSpace(id) || price <= 0 || Owns(id) || price > data.balance) return false;
            var next = Copy();
            next.balance -= price;
            next.ownedIds.Add(id);
            next.activeId = id;
            Save(next);
            return true;
        }

        public bool SelectOwned(string id)
        {
            if (!Owns(id)) return false;
            if (data.activeId == id) return true;
            var next = Copy();
            next.activeId = id;
            Save(next);
            return true;
        }

        private SaveData Copy() => new SaveData
        {
            balance = data.balance,
            ownedIds = new List<string>(data.ownedIds),
            activeId = data.activeId
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
