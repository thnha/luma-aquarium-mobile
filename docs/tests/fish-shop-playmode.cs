// Run while MainAquarium is playing. Replaces only the in-memory inventory with an isolated test wallet.
// Stop Play Mode afterwards to restore the original visual state of the scene fish.
var s=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.FishShopScreen>();
if(s==null || !UnityEngine.Application.isPlaying)throw new System.Exception("Play MainAquarium first.");
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
var field=typeof(Lumaquarium.UI.FishShopScreen).GetField("inventory",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
var originalInventory=field.GetValue(s);
string key="Lumaquarium.Test.FishShop.UI."+System.Guid.NewGuid();
var inventory=new Lumaquarium.UI.FishShopInventory(key);
var originalVariant=s.fish.Variant;
s.Close();
float alpha=s.aquarium.hud.alpha;
bool orbit=s.aquarium.orbit.enabled;
try
{
    field.SetValue(s,inventory);
    var entry=s.aquarium.header.transform.Find("FishShopButton").GetComponent<UnityEngine.UI.Button>();
    entry.onClick.Invoke();
    check(s.IsOpen && s.aquarium.hud.alpha==0 && !s.aquarium.hud.blocksRaycasts && !s.aquarium.orbit.enabled,"Entry opens shop, hides HUD and locks orbit");
    s.optionButtons[2].onClick.Invoke();
    check(s.heroName.text=="Koi Galaxy Plakat" && s.priceLabel.text=="650 xu" && s.heroPortrait.sprite==s.offers[2].portrait,"Selection updates fish portrait, name and price");
    check(s.fish.Variant==originalVariant,"Browsing does not replace the aquarium fish");
    s.SelectFish(-1);
    check(s.heroName.text=="Koi Galaxy Plakat","Invalid selection leaves current offer intact");
    s.buyButton.onClick.Invoke();
    check(s.confirmation.activeSelf && inventory.Balance==1250 && !inventory.Owns("koi"),"Buying first opens confirmation without spending");
    s.cancelButton.onClick.Invoke();
    check(!s.confirmation.activeSelf && inventory.Balance==1250,"Cancel keeps the wallet unchanged");
    s.optionButtons[0].onClick.Invoke();s.buyButton.onClick.Invoke();s.confirmButton.onClick.Invoke();
    check(inventory.Balance==900 && inventory.Owns("royal") && s.fish.Variant==s.offers[0].variant,"Confirmed purchase spends xu and equips the actual 3D variant");
    check(s.aquariumFishName.text=="Royal Blue Halfmoon" && s.aquariumPortrait.sprite==s.offers[0].portrait,"Purchase updates aquarium name and portrait");
    s.ConfirmPurchase();s.BuyOrEquip();
    check(inventory.Balance==900 && !s.confirmation.activeSelf,"Repeated confirmation does not double-charge");
    s.optionButtons[2].onClick.Invoke();s.buyButton.onClick.Invoke();s.confirmButton.onClick.Invoke();
    check(inventory.Balance==250 && s.fish.Variant==s.offers[2].variant,"Second purchase replaces the equipped variant without creating another fish");
    s.optionButtons[3].onClick.Invoke();
    check(!s.buyButton.interactable && s.buyLabel.text=="Chưa đủ xu","Insufficient balance disables purchase");
    s.BuyOrEquip();
    check(!s.confirmation.activeSelf && inventory.Balance==250 && !inventory.Owns("orchid"),"Direct purchase call also enforces available funds");
    s.optionButtons[0].onClick.Invoke();
    check(s.buyButton.interactable && s.buyLabel.text=="Dùng cá này","Previously owned fish can be chosen again");
    s.buyButton.onClick.Invoke();
    check(inventory.Balance==250 && s.fish.Variant==s.offers[0].variant,"Equipping an owned fish is free");
    s.backButton.onClick.Invoke();
    check(!s.IsOpen && s.aquarium.hud.alpha==alpha && s.aquarium.hud.blocksRaycasts && s.aquarium.orbit.enabled==orbit,"Back restores HUD and camera");
    s.Open();
    check(!s.confirmation.activeSelf && !s.buyButton.interactable && s.buyLabel.text=="Đang trong hồ","Reopening retains ownership and active fish");
    var reloaded=new Lumaquarium.UI.FishShopInventory(key);
    check(reloaded.Balance==250 && reloaded.ActiveId=="royal" && reloaded.Owns("koi"),"UI transaction survives local reload");
    field.SetValue(s,reloaded);
    typeof(Lumaquarium.UI.FishShopScreen).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(s,null);
    check(s.fish.Variant==s.offers[0].variant && s.aquariumFishName.text=="Royal Blue Halfmoon","Startup restores the saved equipped fish and header");
    check(UnityEngine.Object.FindObjectsByType<BettaFishAI>(UnityEngine.FindObjectsSortMode.None).Length==1,"No extra aquarium fish is spawned");
    // Each thumbnail's variant is also exercised on the live rig, including Crowntail mesh switching and Rosetail ruffles.
    for(int i=0;i<s.offers.Length;i++)
    {
        s.Close();UnityEngine.PlayerPrefs.DeleteKey(key);
        inventory=new Lumaquarium.UI.FishShopInventory(key);field.SetValue(s,inventory);
        s.Open();s.optionButtons[i].onClick.Invoke();s.buyButton.onClick.Invoke();s.confirmButton.onClick.Invoke();
        var expected=s.offers[i].variant;
        var enabled=s.fish.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).Where(r=>r.enabled&&r.name.StartsWith("Fin_")).Select(r=>r.name).OrderBy(n=>n);
        check(s.fish.Variant==expected && enabled.SequenceEqual(expected.tail.finMeshes.OrderBy(n=>n)),"Apply actual variant and correct fin meshes: "+expected.displayName);
        bool ruffled=s.fish.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).Where(r=>r.enabled&&r.name.StartsWith("Fin_")).All(r=>r.sharedMesh.name.EndsWith("_Ruffled"));
        check(ruffled==expected.tail.ruffle.Enabled,"Fin edges ruffled only for Rosetail: "+expected.displayName);
    }
}
finally
{
    s.Close();field.SetValue(s,originalInventory);
    UnityEngine.PlayerPrefs.DeleteKey(key);UnityEngine.PlayerPrefs.Save();
}
return new {passed=checks.Count,checks};
