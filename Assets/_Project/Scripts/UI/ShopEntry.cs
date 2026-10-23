using System.Collections.Generic;
using UnityEngine;

namespace Lumaquarium.UI
{
    // Tab order in the shop's tab row.
    public enum ShopTab { All, Fish, Wood, Rocks, Coral, Plants, Moss, Floating, Walls, Substrates }

    public enum ShopKind { Decor, Fish, Wall, Substrate }

    // One card in the shop. `source` indexes the list it came from: the fish offers, the decoration
    // catalog's offers, or the surface catalog's walls / substrates. `order` is the default sort position.
    public sealed class ShopEntry
    {
        public ShopTab tab;
        public ShopKind kind;
        public int source;
        public string id, name, description;
        public int price;
        public Sprite portrait;
        public int order;
        private string searchKey;
        // Search finds a name in either language, whichever the shop is showing.
        public string SearchKey => searchKey ??= ShopQuery.Normalize(name) + "\n" + ShopQuery.Normalize(Loc.English(name));

        private static readonly ShopTab[] DecorTabs = { ShopTab.Wood, ShopTab.Rocks, ShopTab.Coral, ShopTab.Plants, ShopTab.Moss, ShopTab.Floating };

        public static ShopTab TabOf(AquariumDecorCategory category) => category switch
        {
            AquariumDecorCategory.Wood => ShopTab.Wood,
            AquariumDecorCategory.Rocks => ShopTab.Rocks,
            AquariumDecorCategory.Coral => ShopTab.Coral,
            AquariumDecorCategory.Moss => ShopTab.Moss,
            AquariumDecorCategory.Floating => ShopTab.Floating,
            _ => ShopTab.Plants
        };

        // Everything the shop sells, in tab order and catalog order within a tab. Any source may be null.
        public static List<ShopEntry> Build(FishShopOffer[] fish, AquariumDecorCatalog decor, AquariumSurfaceCatalog surfaces)
        {
            var list = new List<ShopEntry>();
            void Add(ShopTab tab, ShopKind kind, int source, string id, string name, string description, int price, Sprite portrait) =>
                list.Add(new ShopEntry { tab = tab, kind = kind, source = source, id = id, name = name, description = description,
                    price = price, portrait = portrait, order = list.Count });
            if (fish != null)
                for (int i = 0; i < fish.Length; i++)
                    if (fish[i] != null && fish[i].IsValid)
                        Add(ShopTab.Fish, ShopKind.Fish, i, fish[i].id, fish[i].variant.displayName, fish[i].description, fish[i].price, fish[i].portrait);
            if (decor != null)
                foreach (var tab in DecorTabs)
                    for (int i = 0; i < decor.offers.Length; i++)
                    {
                        var offer = decor.offers[i];
                        if (offer != null && offer.IsValid && TabOf(offer.category) == tab)
                            Add(tab, ShopKind.Decor, i, offer.id, offer.displayName, offer.description, offer.price, offer.portrait);
                    }
            if (surfaces != null)
            {
                for (int i = 0; i < surfaces.walls.Length; i++)
                {
                    var wall = surfaces.walls[i];
                    Add(ShopTab.Walls, ShopKind.Wall, i, wall.id, wall.displayName, wall.description, wall.price, wall.portrait);
                }
                for (int i = 0; i < surfaces.substrates.Length; i++)
                {
                    var floor = surfaces.substrates[i];
                    Add(ShopTab.Substrates, ShopKind.Substrate, i, floor.id, floor.displayName, floor.description, floor.price, floor.portrait);
                }
            }
            return list;
        }
    }
}
