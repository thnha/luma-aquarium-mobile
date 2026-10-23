using System;
using System.Collections.Generic;
using System.Linq;
using Lumaquarium.UI;
using UnityEditor;

// The Gói quà tab of the price sheet (DecorCatalogSheet): one row per item of a gift pack. id is a shop id from
// any other tab, or "xu" for coins; dùng ngay (any text) puts a fish, wall, floor or tank into use for a player
// who has none of that kind in use yet. The packs are kept in the decoration catalog (giftPacks).
public static class GiftSheet
{
    public const string TabName = "Gói quà";
    private static readonly string[] Header = { "gói", "id", "số lượng", "dùng ngay" };

    public static bool IsGiftTable(List<string[]> table) =>
        table != null && table.Count > 0 && Array.Exists(table[0], h => Same(h, Header[0]));

    private static bool Same(string a, string b) => string.Equals(a?.Trim().Normalize(), b, StringComparison.OrdinalIgnoreCase);

    public static DecorCatalogSheet.SheetTab Tab(AquariumGiftPack[] packs)
    {
        var table = new List<string[]> { Header };
        foreach (var pack in packs ?? new AquariumGiftPack[0])
            foreach (var gift in pack.gifts)
                table.Add(new[] { pack.id, gift.id, gift.count.ToString(), string.IsNullOrEmpty(gift.slot) ? "" : "x" });
        return new DecorCatalogSheet.SheetTab { name = TabName, viewHeaders = new string[0], table = table };
    }

    // The slot of every shop id: "" for decorations, so they are known but never put into use.
    public static Dictionary<string, string> Slots(AquariumDecorCatalog decor, AquariumSurfaceCatalog surfaces,
        IEnumerable<DecorCatalogSheet.SheetItem> fish)
    {
        var slots = new Dictionary<string, string>();
        foreach (var offer in decor.offers) if (offer != null) slots[offer.id] = "";
        foreach (var f in fish) slots[f.id] = FishShopInventory.FishSlot;
        if (surfaces != null)
        {
            foreach (var w in surfaces.walls) slots[w.id] = FishShopInventory.WallSlot;
            foreach (var s in surfaces.substrates) slots[s.id] = FishShopInventory.FloorSlot;
            foreach (var t in surfaces.tanks ?? new AquariumTankOffer[0]) slots[t.id] = FishShopInventory.TankSlot;
        }
        return slots;
    }

    // The packs in the order they first appear, or null (with the reasons in errors) so the old packs are kept.
    public static AquariumGiftPack[] Read(List<string[]> table, IReadOnlyDictionary<string, string> slots, List<string> errors)
    {
        int Column(string name) => Array.FindIndex(table[0], h => Same(h, name));
        int packColumn = Column(Header[0]), idColumn = Column(Header[1]), countColumn = Column(Header[2]), useColumn = Column(Header[3]);
        string Cell(string[] cells, int index) => index >= 0 && index < cells.Length ? cells[index].Trim().Normalize() : "";
        int before = errors.Count;
        if (idColumn < 0 || countColumn < 0) { errors.Add($"Tab {TabName}: cần các cột {string.Join(", ", Header)}."); return null; }
        var packs = new List<(AquariumGiftPack pack, List<AquariumGift> gifts)>();
        for (int i = 1; i < table.Count; i++)
        {
            string packId = Cell(table[i], packColumn), id = Cell(table[i], idColumn), countText = Cell(table[i], countColumn);
            if (packId == "" && id == "") continue;
            string place = $"Tab {TabName}, hàng {i + 1}";
            if (packId == "" || id == "") { errors.Add($"{place}: thiếu gói hoặc id."); continue; }
            string slot = "";
            if (id != FishShopInventory.Coins && !slots.TryGetValue(id, out slot)) { errors.Add($"{place}: không có món \"{id}\"."); continue; }
            if (!int.TryParse(countText.Replace(".", "").Replace(",", "").Replace(" ", ""), out int count) || count < 0)
            { errors.Add($"{place} ({id}): số lượng \"{countText}\" không hợp lệ, phải là số từ 0 trở lên."); continue; }
            bool use = Cell(table[i], useColumn) != "";
            if (use && string.IsNullOrEmpty(slot)) { errors.Add($"{place} ({id}): chỉ cá, thành hồ, cát nền và hồ mới dùng ngay được."); continue; }
            var entry = packs.FirstOrDefault(p => p.pack.id == packId);
            if (entry.pack == null) { entry = (new AquariumGiftPack { id = packId }, new List<AquariumGift>()); packs.Add(entry); }
            if (entry.gifts.Exists(g => g.id == id)) { errors.Add($"{place}: \"{id}\" bị lặp trong gói {packId}."); continue; }
            if (use && entry.gifts.Exists(g => g.slot == slot)) { errors.Add($"{place}: gói {packId} đã có một món dùng ngay cùng loại."); continue; }
            entry.gifts.Add(new AquariumGift { id = id, count = count, slot = use ? slot : "" });
        }
        if (!packs.Exists(p => p.pack.id == FishShopInventory.StarterPack))
            errors.Add($"Tab {TabName}: cần gói {FishShopInventory.StarterPack} (quà của người chơi mới).");
        if (errors.Count > before) return null;
        foreach (var (pack, gifts) in packs) pack.gifts = gifts.ToArray();
        return packs.Select(p => p.pack).ToArray();
    }

    // The starter pack as new players got it before the Gói quà tab: the coins of a new wallet, the scene's fish,
    // the first tank, wall and floor, and the starter pieces placed in the tank.
    public static AquariumGiftPack[] Defaults() => new[]
    {
        new AquariumGiftPack
        {
            id = FishShopInventory.StarterPack,
            gifts = new[]
            {
                new AquariumGift { id = FishShopInventory.Coins, count = 1250 },
                new AquariumGift { id = "plakat-galaxy", count = 1, slot = FishShopInventory.FishSlot },
                new AquariumGift { id = "tank_square", count = 1, slot = FishShopInventory.TankSlot },
                new AquariumGift { id = "wall_river", count = 1, slot = FishShopInventory.WallSlot },
                new AquariumGift { id = "sand_river", count = 1, slot = FishShopInventory.FloorSlot },
                new AquariumGift { id = "starter-anubias", count = 2 },
                new AquariumGift { id = "starter-stone", count = 2 },
                new AquariumGift { id = "starter-wood", count = 2 },
            }
        }
    };

    public static void EnsureStarter(AquariumDecorCatalog catalog)
    {
        if (catalog.giftPacks != null && catalog.giftPacks.Length > 0) return;
        catalog.giftPacks = Defaults();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }
}
