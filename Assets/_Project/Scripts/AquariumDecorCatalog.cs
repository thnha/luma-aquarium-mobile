using System;
using UnityEngine;

// Moss and Floating were added after the first four; keep this order, catalogs store the numbers.
public enum AquariumDecorCategory { Plants, Wood, Rocks, Coral, Moss, Floating }

[Serializable]
public sealed class AquariumDecorOffer
{
    public string id;
    public string displayName;
    public string description;
    public AquariumDecorCategory category;
    public int price;
    // How many a new player has in stock (the price sheet's tặng lúc đầu column).
    public int starterCount;
    public AquariumDecorItem prefab;
    public Sprite portrait;
    public bool IsValid => !string.IsNullOrWhiteSpace(id) && price > 0 && prefab != null && portrait != null;
}

public sealed class AquariumDecorCatalog : ScriptableObject
{
    public AquariumDecorOffer[] offers = new AquariumDecorOffer[0];
    // Coins of a new player (the price sheet's Chung tab).
    public int startingCoins = Lumaquarium.UI.FishShopInventory.DefaultStartingCoins;
    // The Trang trí sheet keeps its four groups: moss and floating plants sit with the plants there.
    public static AquariumDecorCategory InventoryGroup(AquariumDecorCategory category) =>
        category > AquariumDecorCategory.Coral ? AquariumDecorCategory.Plants : category;
    // Plants, moss and floating plants are soft: the fish swims through them.
    public static bool IsPlant(AquariumDecorCategory category) => InventoryGroup(category) == AquariumDecorCategory.Plants;

    public AquariumDecorOffer Find(int kind) => Array.Find(offers, x => x.prefab != null && x.prefab.kind == kind);
}
