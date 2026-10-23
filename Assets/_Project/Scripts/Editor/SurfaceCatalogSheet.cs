using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The Thành hồ, Cát nền and Hồ tabs of the price sheet (DecorCatalogSheet). All three live in the surface catalog,
// which ships with the remote content; tank sizes and capacity stay in AquariumTank.Specs.
public static class SurfaceCatalogSheet
{
    public const string CatalogPath = "Assets/_Project/Art/Aquarium3D/Surfaces/AquariumSurfaceCatalog.asset";

    public static AquariumSurfaceCatalog Load() => AssetDatabase.LoadAssetAtPath<AquariumSurfaceCatalog>(CatalogPath);

    // One tank offer per AquariumTank.Specs entry, in its order; offers already in the catalog keep their values.
    public static void EnsureTanks(AquariumSurfaceCatalog catalog)
    {
        var old = catalog.tanks ?? new AquariumTankOffer[0];
        var tanks = AquariumTank.Specs.Select(spec => old.FirstOrDefault(t => t != null && t.id == spec.id)
            ?? new AquariumTankOffer { id = spec.id, displayName = spec.displayName, description = spec.description, price = spec.price }).ToArray();
        if (tanks.SequenceEqual(old)) return;
        catalog.tanks = tanks;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }

    // Everything here may be free: the first wall and floor are the tank's own look, the first tank is the starter.
    public static List<DecorCatalogSheet.SheetItem> Items(AquariumSurfaceCatalog catalog)
    {
        var items = new List<DecorCatalogSheet.SheetItem>();
        foreach (var w in catalog.walls) items.Add(Item(w.id, w.displayName, w.description, w.price));
        foreach (var s in catalog.substrates) items.Add(Item(s.id, s.displayName, s.description, s.price));
        foreach (var t in catalog.tanks) items.Add(Item(t.id, t.displayName, t.description, t.price));
        return items;
    }

    private static DecorCatalogSheet.SheetItem Item(string id, string name, string description, int price) =>
        new DecorCatalogSheet.SheetItem { id = id, name = name, description = description, price = price, freeAllowed = true };

    public static IEnumerable<DecorCatalogSheet.SheetTab> Tabs(AquariumSurfaceCatalog catalog)
    {
        var walls = new DecorCatalogSheet.SheetTab { name = "Thành hồ", viewHeaders = new string[0] };
        foreach (var w in catalog.walls) walls.rows.Add(Row(w.id, w.displayName, w.description, w.price, w.portrait, null));
        var floors = new DecorCatalogSheet.SheetTab { name = "Cát nền", viewHeaders = new string[0] };
        foreach (var s in catalog.substrates) floors.rows.Add(Row(s.id, s.displayName, s.description, s.price, s.portrait, null));
        var tanks = new DecorCatalogSheet.SheetTab { name = "Hồ", viewHeaders = new[] { "kích thước (màn hình)", "sức chứa (điểm)" } };
        foreach (var t in catalog.tanks)
        {
            var spec = AquariumTank.Find(t.id);
            string size = spec == null ? "" : string.Format(CultureInfo.InvariantCulture, "rộng {0} × sâu {1}", spec.widthScreens, spec.depthScreens);
            tanks.rows.Add(Row(t.id, t.displayName, t.description, t.price, null, new[] { size, spec?.capacity.ToString() ?? "" }));
        }
        return new[] { walls, floors, tanks };
    }

    private static DecorCatalogSheet.SheetRow Row(string id, string name, string description, int price, Sprite portrait, string[] view) =>
        new DecorCatalogSheet.SheetRow
        {
            item = new DecorCatalogSheet.SheetItem { id = id, name = name, description = description, price = price },
            image = portrait != null ? DecorCatalogSheet.ImageUrl(AssetDatabase.GetAssetPath(portrait), "surface") : null,
            view = view
        };

    public static void Save(AquariumSurfaceCatalog catalog, IEnumerable<DecorCatalogSheet.SheetItem> items)
    {
        var changed = items.Where(i => i.changed).ToList();
        if (changed.Count == 0) return;
        Undo.RecordObject(catalog, "Apply surface sheet");
        foreach (var item in changed)
        {
            var wall = catalog.walls.FirstOrDefault(x => x.id == item.id);
            if (wall != null) { wall.displayName = item.name; wall.description = item.description; wall.price = item.price; continue; }
            var floor = catalog.substrates.FirstOrDefault(x => x.id == item.id);
            if (floor != null) { floor.displayName = item.name; floor.description = item.description; floor.price = item.price; continue; }
            var tank = catalog.tanks.FirstOrDefault(x => x.id == item.id);
            if (tank != null) { tank.displayName = item.name; tank.description = item.description; tank.price = item.price; }
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }
}
