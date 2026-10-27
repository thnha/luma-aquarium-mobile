var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>(AquariumDecorCatalogBuilder.Root+"/AquariumDecorCatalog.asset");
var old=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("docs/tests/fixtures/coral-legacy.json"));
int passed=0;
foreach(var row in old)
{
 var offer=catalog.offers.Single(o=>o.id==(string)row["id"]);
 if(offer.prefab.kind!=(int)row["kind"] || offer.price!=(int)row["price"])throw new System.Exception("Legacy identity/price changed: "+offer.id);
 passed++;
}
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
var aquarium=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.MainAquariumScreen>();
var inventory=aquarium.decoratePanel.GetComponent<Lumaquarium.UI.DecorInventoryPanel>();
var shop=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.ShopScreen>(UnityEngine.FindObjectsInactive.Include);
if(d.prefabs.Length!=catalog.offers.Length || inventory.cards.Length!=catalog.offers.Length)throw new System.Exception("All catalog offers require placement and inventory bindings");
passed++;
if(shop==null || inventory.shop!=shop || shop.aquarium!=aquarium || shop.cardTemplate==null)throw new System.Exception("Unified shop links lost during refresh");
passed++;
var entries=Lumaquarium.UI.ShopEntry.Build(shop.fishOffers,catalog,d.surfaces);
foreach(var s in CoralCollectionBuilder.All)
{
 if(!entries.Any(e=>e.id==s.id && e.tab==Lumaquarium.UI.ShopTab.Coral))throw new System.Exception("Missing coral in unified shop: "+s.id);
 passed++;
}
return new{passed,legacyPreserved=old.Count,offers=catalog.offers.Length};
