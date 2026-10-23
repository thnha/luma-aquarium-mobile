#nullable disable
using System;
using System.Collections.Generic;

namespace Lumaquarium.Shop
{
    [Serializable] public sealed class Gift { public string id, slot; public int count; }
    [Serializable] public sealed class GiftPack { public string id; public List<Gift> gifts = new List<Gift>(); }
    // slot is "fish", "wall", "floor" or "tank" for what is bought once and put into use, "" for a decoration.
    [Serializable] public sealed class CatalogItem { public string id, slot; public int price; }

    // What the server sells, written from the price sheet by ServerCatalogWriter (Editor) into the Cloud Code module.
    // version is a hash of the rest; the game sends the version it shows, and a different one is refused.
    [Serializable]
    public sealed class ShopCatalog
    {
        public string version = "";
        public List<CatalogItem> items = new List<CatalogItem>();
        public List<GiftPack> giftPacks = new List<GiftPack>();

        public CatalogItem Find(string id) => string.IsNullOrWhiteSpace(id) ? null : items.Find(x => x.id == id);
    }
}
