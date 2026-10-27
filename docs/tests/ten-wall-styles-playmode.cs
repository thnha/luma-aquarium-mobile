if (!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Run in MainAquarium Play Mode");
var ids = new[] { "wall_basalt", "wall_limestone", "wall_sandstone", "wall_amazon_roots", "wall_frosted_aqua",
    "wall_matte_black", "wall_dragon_stone", "wall_pearl_marble", "wall_turquoise_mosaic", "wall_sunken_relic" };
var decoration = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
var tank = UnityEngine.Object.FindFirstObjectByType<AquariumTank>();
if (decoration == null || tank == null) throw new System.Exception("MainAquarium must be loaded and initialized");
var catalog = decoration.surfaces;
var server = UnityEngine.JsonUtility.FromJson<Lumaquarium.Shop.ShopCatalog>(System.IO.File.ReadAllText(ServerCatalogWriter.FullPath));
var renderer = tank.GetComponentsInChildren<UnityEngine.MeshRenderer>().First(r => r.name == "TankWalls");
var current = new Lumaquarium.UI.FishShopInventory(decoration.inventoryStorageKey);
var playerBefore = current.StoredJson();
var previous = current.ActiveWall;
var key = "Lumaquarium.Tests.TenWalls." + System.Guid.NewGuid().ToString("N");
var inventory = new Lumaquarium.UI.FishShopInventory(key);
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new System.Exception(name); checks++; }
try
{
    int cost = ids.Sum(id => catalog.Wall(id).price);
    inventory.AddCoins(cost + 100);
    Check(server.version == decoration.catalog.serverCatalogVersion, "client/server version");
    foreach (string id in ids)
    {
        var wall = catalog.walls.Single(w => w.id == id);
        var item = server.items.Single(i => i.id == id);
        Check(item.slot == "wall" && item.price == wall.price, id + " server price/slot");
        Check(inventory.TryPurchaseWall(id, wall.price), id + " purchase");
        Check(!inventory.TryPurchaseWall(id, wall.price), id + " duplicate refused");
        var reloaded = new Lumaquarium.UI.FishShopInventory(key);
        Check(reloaded.OwnsWall(id) && reloaded.ActiveWall == id, id + " saved selection");
        tank.ApplyWall(id);
        var material = renderer.sharedMaterial;
        Check(material.GetTexture("_BaseMap") == wall.albedo, id + " runtime texture");
        Check(UnityEngine.Mathf.Approximately(material.GetFloat("_Tiling"), wall.tiling), id + " runtime scale");
        Check(material.GetColor("_BaseColor") == wall.tint, id + " runtime tint");
    }
    Check(inventory.Balance == 100, "exact total coins spent in isolated inventory");
    Check(inventory.SelectWall(ids[0]), "select previously owned wall");
    Check(new Lumaquarium.UI.FishShopInventory(key).ActiveWall == ids[0], "reselection saved");
    Check(current.StoredJson() == playerBefore, "player save untouched");
    var entries = Lumaquarium.UI.ShopEntry.Build(null, null, catalog);
    Check(entries.Count(e => e.kind == Lumaquarium.UI.ShopKind.Wall) == catalog.walls.Length, "all wall cards");
    return new { passed = checks, newWalls = ids.Length, totalWalls = catalog.walls.Length, serverVersion = server.version,
        frame = UnityEngine.Time.frameCount, playerSavePreserved = true };
}
finally
{
    tank.ApplyWall(previous);
    UnityEngine.PlayerPrefs.DeleteKey(key);
    UnityEngine.PlayerPrefs.DeleteKey(key + ".v1.bak");
    UnityEngine.PlayerPrefs.DeleteKey(key + ".unreadable.bak");
}
