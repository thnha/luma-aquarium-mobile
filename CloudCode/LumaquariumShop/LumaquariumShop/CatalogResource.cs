using System.Text.Json;
using Lumaquarium.Shop;

namespace LumaquariumShop;

// ShopCatalog.json, embedded in the module when it is built: prices and gift packs change with a deploy.
public static class CatalogResource
{
    private static readonly Lazy<ShopCatalog> Loaded = new(() =>
    {
        using var stream = typeof(CatalogResource).Assembly.GetManifestResourceStream("ShopCatalog.json")
            ?? throw new InvalidOperationException("ShopCatalog.json is not embedded in the module.");
        return JsonSerializer.Deserialize<ShopCatalog>(stream, ShopLogic.Json)
            ?? throw new InvalidOperationException("ShopCatalog.json is empty.");
    });

    public static ShopCatalog Catalog => Loaded.Value;
}
