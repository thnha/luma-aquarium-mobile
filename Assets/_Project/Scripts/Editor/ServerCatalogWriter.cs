using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Lumaquarium.Shop;
using Lumaquarium.UI;
using UnityEditor;
using UnityEngine;

// Writes what the Cloud Code module LumaquariumShop sells: every shop id with its price and slot, and the gift
// packs, into CloudCode/LumaquariumShop/LumaquariumShop/ShopCatalog.json (embedded in the module when it is
// deployed). Its version, a hash of the rest, is stored on the decoration catalog too, which ships with the remote
// content: the server refuses a purchase from a game showing other prices. Pull and every content build run it.
public static class ServerCatalogWriter
{
    public const string RelativePath = "CloudCode/LumaquariumShop/LumaquariumShop/ShopCatalog.json";
    private const string DecorCatalogPath = AquariumDecorCatalogBuilder.Root + "/AquariumDecorCatalog.asset";

    public static string FullPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, RelativePath);

    public static ShopCatalog Build(AquariumDecorCatalog decor, AquariumSurfaceCatalog surfaces, IEnumerable<DecorCatalogSheet.SheetItem> fish)
    {
        var fishItems = fish.ToList();
        var slots = GiftSheet.Slots(decor, surfaces, fishItems);
        var catalog = new ShopCatalog();
        void Add(string id, int price)
        {
            if (!string.IsNullOrWhiteSpace(id))
                catalog.items.Add(new CatalogItem { id = id, price = price, slot = slots.TryGetValue(id, out string slot) ? slot : "" });
        }
        foreach (var f in fishItems) Add(f.id, f.price);
        foreach (var offer in decor.offers) if (offer != null) Add(offer.id, offer.price);
        if (surfaces != null)
        {
            foreach (var w in surfaces.walls) Add(w.id, w.price);
            foreach (var s in surfaces.substrates) Add(s.id, s.price);
            foreach (var t in surfaces.tanks ?? new AquariumTankOffer[0]) Add(t.id, t.price);
        }
        catalog.giftPacks = FishShopInventory.ToShared(decor.giftPacks);
        catalog.version = Hash(JsonUtility.ToJson(catalog));
        return catalog;
    }

    [MenuItem("Lumaquarium/Shop/Write Server Catalog")]
    private static void WriteFromMenu() => Write();

    public static ShopCatalog Write()
    {
        var decor = AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>(DecorCatalogPath);
        var catalog = Build(decor, SurfaceCatalogSheet.Load(), FishCatalogSheet.Items());
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath));
        File.WriteAllText(FullPath, JsonUtility.ToJson(catalog, true) + "\n");
        if (decor.serverCatalogVersion != catalog.version)
        {
            Undo.RecordObject(decor, "Server Catalog Version");
            decor.serverCatalogVersion = catalog.version;
            EditorUtility.SetDirty(decor);
            AssetDatabase.SaveAssets();
        }
        Debug.Log($"[Shop] Server catalog {catalog.version}: {catalog.items.Count} items, {catalog.giftPacks.Count} gift packs -> {RelativePath}");
        return catalog;
    }

    private static string Hash(string text)
    {
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return string.Concat(bytes.Take(8).Select(b => b.ToString("x2")));
    }
}
