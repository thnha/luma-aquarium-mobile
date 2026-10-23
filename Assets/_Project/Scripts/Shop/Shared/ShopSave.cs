#nullable disable
using System;
using System.Collections.Generic;

namespace Lumaquarium.Shop
{
    [Serializable] public sealed class Amount { public string id; public int count; }
    [Serializable] public sealed class InUse { public string slot, id; }

    // The shop save: the wallet, everything owned (fish, decorations, tanks, wall and floor styles) and what is in
    // use. Owning is having a count above 0. This file is compiled into the game (FishShopInventory reads it with
    // JsonUtility) and into the Cloud Code module LumaquariumShop (System.Text.Json), so it uses no Unity type, no
    // properties (both serializers would differ on them) and nothing newer than C# 9.
    [Serializable]
    public sealed class ShopSave
    {
        public const int CurrentVersion = 2;
        public const string Coins = "xu";
        public const string StarterPack = "starter_v1";
        public const string FishSlot = "fish", WallSlot = "wall", FloorSlot = "floor", TankSlot = "tank";

        public int version = CurrentVersion;
        public List<Amount> wallet = new List<Amount>();
        public List<Amount> items = new List<Amount>();
        public List<InUse> equipped = new List<InUse>();
        public List<string> grantsApplied = new List<string>();

        public static bool IsSlot(string slot) => slot == FishSlot || slot == WallSlot || slot == FloorSlot || slot == TankSlot;

        public int Balance() => Count(wallet, Coins);
        public int CountOf(string id) => Count(items, id);
        public bool Owns(string id) => !string.IsNullOrWhiteSpace(id) && Count(items, id) > 0;
        public string Equipped(string slot) => equipped.Find(x => x.slot == slot)?.id ?? "";

        public void Equip(string slot, string id)
        {
            var entry = equipped.Find(x => x.slot == slot);
            if (entry == null) equipped.Add(new InUse { slot = slot, id = id });
            else entry.id = id;
        }

        public ShopSave Copy() => new ShopSave
        {
            version = version,
            wallet = wallet.ConvertAll(x => new Amount { id = x.id, count = x.count }),
            items = items.ConvertAll(x => new Amount { id = x.id, count = x.count }),
            equipped = equipped.ConvertAll(x => new InUse { slot = x.slot, id = x.id }),
            grantsApplied = new List<string>(grantsApplied)
        };

        public static int Count(List<Amount> list, string id) => list.Find(x => x.id == id)?.count ?? 0;

        public static void Add(List<Amount> list, string id, int amount)
        {
            var entry = list.Find(x => x.id == id);
            if (entry == null) list.Add(new Amount { id = id, count = Math.Max(0, amount) });
            else entry.count = (int)Math.Clamp((long)entry.count + amount, 0, int.MaxValue);
        }

        public static void AtLeast(List<Amount> list, string id, int count)
        {
            if (Count(list, id) < count) Add(list, id, count - Count(list, id));
        }

        // Null when the save breaks a rule the rest of the code relies on.
        public static ShopSave Checked(ShopSave save)
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
    }
}
