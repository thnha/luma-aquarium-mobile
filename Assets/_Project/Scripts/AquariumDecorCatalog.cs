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
    public AquariumDecorItem prefab;
    public Sprite portrait;
    public bool IsValid => !string.IsNullOrWhiteSpace(id) && price > 0 && prefab != null && portrait != null;
}

// One line of a gift pack (the price sheet's Gói quà tab): a shop item id, or "xu" for coins. slot is "fish",
// "wall", "floor" or "tank" when the item goes into use for a player who has none of that kind in use yet.
[Serializable]
public sealed class AquariumGift
{
    public string id, slot;
    public int count;
}

// Every player gets each pack once (FishShopInventory.ApplyGifts); starter_v1 is what a new player starts with.
[Serializable]
public sealed class AquariumGiftPack
{
    public string id;
    public AquariumGift[] gifts = new AquariumGift[0];
}

public sealed class AquariumDecorCatalog : ScriptableObject
{
    public AquariumDecorOffer[] offers = new AquariumDecorOffer[0];
    // Kept here because this catalog ships with the remote content: a new pack needs no app update.
    public AquariumGiftPack[] giftPacks = new AquariumGiftPack[0];
    // ServerCatalogWriter's hash of every price and pack; the server refuses a purchase made showing another one.
    public string serverCatalogVersion = "";
    // The Trang trí sheet keeps its four groups: moss and floating plants sit with the plants there.
    public static AquariumDecorCategory InventoryGroup(AquariumDecorCategory category) =>
        category > AquariumDecorCategory.Coral ? AquariumDecorCategory.Plants : category;
    // Plants, moss and floating plants are soft: the fish swims through them.
    public static bool IsPlant(AquariumDecorCategory category) => InventoryGroup(category) == AquariumDecorCategory.Plants;

    public AquariumDecorOffer Find(int kind) => Array.Find(offers, x => x.prefab != null && x.prefab.kind == kind);
}
