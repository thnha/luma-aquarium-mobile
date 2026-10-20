// Run in Edit Mode after building the screen. Checks scene wiring and actual Button event routing.
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
var shop=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.FishShopScreen>(UnityEngine.FindObjectsInactive.Include);
check(shop!=null,"Shop is integrated into MainAquarium");
check(!shop.content.activeSelf,"Shop starts closed");
check(shop.offers.Length==5 && shop.offers.All(o=>o.IsValid),"All five fish have real variants, portraits and valid prices");
check(shop.offers.Select(o=>o.id).Distinct().Count()==5,"Offers have unique persistent IDs");
check(shop.offers.Select(o=>o.price).SequenceEqual(new[]{350,450,650,900,1200}),"Approved fish prices are wired");
check(shop.optionButtons.Length==5 && shop.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true)!=null,"Every offer has a card in the scrolling list");
check(shop.aquarium!=null && shop.fish!=null && shop.aquariumFishName!=null && shop.aquariumPortrait!=null,"Shop is connected to the existing fish and HUD");
var entry=shop.aquarium.header.transform.Find("FishShopButton").GetComponent<UnityEngine.UI.Button>();
check(entry.onClick.GetPersistentTarget(0)==shop && entry.onClick.GetPersistentMethodName(0)=="Open","Aquarium entry button opens the shop");
check(shop.GetComponent<UnityEngine.Canvas>().sortingOrder>shop.aquarium.GetComponent<UnityEngine.Canvas>().sortingOrder,"Shop draws above aquarium HUD");
check(UnityEngine.Object.FindObjectsByType<BettaFishAI>(UnityEngine.FindObjectsSortMode.None).Length==1,"Integration preserves the single aquarium fish");
return new {passed=checks.Count,checks};
