// Run in Edit Mode after building the screen. Checks scene wiring and actual Button event routing.
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
var shop=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.FishShopScreen>(UnityEngine.FindObjectsInactive.Include);
check(shop!=null,"Shop is integrated into MainAquarium");
check(!shop.content.activeSelf,"Shop starts closed");
check(shop.offers.Length>=2 && shop.offers.All(o=>o.IsValid && o.variant.modelPrefab!=null),"Every fish has its own model, a portrait and a valid price");
check(shop.offers.Select(o=>o.id).Distinct().Count()==shop.offers.Length,"Offers have unique persistent IDs");
check(shop.offers.Any(o=>o.variant==shop.fish.variant),"The tank's default fish is sold in the shop");
check(shop.optionButtons.Length==shop.offers.Length && shop.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true)!=null,"Every offer has a card in the scrolling list");
check(shop.aquarium!=null && shop.fish!=null && shop.aquariumFishName!=null && shop.aquariumPortrait!=null,"Shop is connected to the existing fish and HUD");
var entry=shop.aquarium.toolbar.transform.Find("ShopButton").GetComponent<UnityEngine.UI.Button>();
check(entry.onClick.GetPersistentTarget(0)==shop.aquarium && entry.onClick.GetPersistentMethodName(0)=="OpenShop" && shop.aquarium.fishShop==shop,"Dock shop button opens the shop");
check(shop.GetComponent<UnityEngine.Canvas>().sortingOrder>shop.aquarium.GetComponent<UnityEngine.Canvas>().sortingOrder,"Shop draws above aquarium HUD");
check(UnityEngine.Object.FindObjectsByType<BettaFishAI>(UnityEngine.FindObjectsSortMode.None).Length==1,"Integration preserves the single aquarium fish");
return new {passed=checks.Count,checks};
