// Run in Play Mode. Uses an isolated wallet and restores the original appearance.
var s=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.FishShopScreen>();
if(s==null || !UnityEngine.Application.isPlaying)throw new System.Exception("Play MainAquarium first.");
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
var offer=s.offers.FirstOrDefault(o=>o.id=="aqua-fire");
check(offer!=null && offer.IsValid && offer.price==0,"Aqua Fire Betta is available for 0 xu");
var inventoryField=typeof(Lumaquarium.UI.FishShopScreen).GetField("inventory",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
var originalInventory=inventoryField.GetValue(s);
var originalVariant=s.fish.Variant;
var originalName=s.aquariumFishName.text;
var originalPortrait=s.aquariumPortrait.sprite;
string key="Lumaquarium.Test.AquaFire."+System.Guid.NewGuid();
string emptyKey=key+".EmptyWallet";
var inventory=new Lumaquarium.UI.FishShopInventory(key);
try
{
    s.Close();inventoryField.SetValue(s,inventory);s.Open();
    s.SelectFish(System.Array.IndexOf(s.offers,offer));
    check(s.fish.Variant==originalVariant,"Browsing Aqua Fire does not change the aquarium fish");
    s.BuyOrEquip();s.CancelPurchase();
    check(!inventory.Owns("aqua-fire") && inventory.Balance==1250,"Cancel does not grant the fish or spend xu");
    s.BuyOrEquip();s.ConfirmPurchase();
    check(inventory.Owns("aqua-fire") && inventory.ActiveId=="aqua-fire" && inventory.Balance==1250,"Receiving Aqua Fire equips it without spending xu");
    var visible=s.fish.GetComponentsInChildren<UnityEngine.Renderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy).ToArray();
    check(visible.Length>0 && visible.All(r=>r.sharedMaterial && r.sharedMaterial.name.StartsWith("AquaFireBetta")),"The aquarium renders the imported model with its own material");
    check(visible.All(r=>r.sharedMaterial.GetTexture("_BaseMap").name=="Meshy_AI_Aqua_Fire_Betta_1021043419_texture"),"The original Meshy colour texture is used");
    var skins=s.fish.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy).ToArray();
    check(skins.Length==1 && skins[0].sharedMesh.name=="AquaFireBetta_Skinned","Visible mesh is the skinned original Aqua Fire surface");
    var binding=skins[0].GetComponent<BettaRiggedModel>();
    var ai=s.fish.GetComponent<BettaFishAI>();
    binding.gameObject.SetActive(false);
    check(ai.animator!=binding.animator,"Disabling the imported model restores the shared Animator");
    binding.gameObject.SetActive(true);
    check(ai.animator==binding.animator,"Re-enabling the imported model reconnects the AI Animator");
    s.ConfirmPurchase();s.BuyOrEquip();
    check(inventory.Balance==1250 && !s.confirmation.activeSelf,"Repeated receive is idempotent");
    s.SelectFish(0);s.BuyOrEquip();s.ConfirmPurchase();
    check(s.fish.Variant==s.offers[0].variant && s.fish.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().Any(r=>r.enabled),"Switching back restores the original rigged fish");
    check(ai.animator==s.fish.GetComponent<BettaReactions>().animator && s.fish.GetComponent<BettaSwimMotion>().enabled && s.fish.GetComponent<BettaFinPhysics>().enabled,"Switching back restores the original Animator, swim motion and fin simulation");
    s.SelectFish(System.Array.IndexOf(s.offers,offer));s.BuyOrEquip();
    check(inventory.Balance==900 && s.fish.Variant==offer.variant,"An owned Aqua Fire can be equipped again for free");
    var reloaded=new Lumaquarium.UI.FishShopInventory(key);inventoryField.SetValue(s,reloaded);
    typeof(Lumaquarium.UI.FishShopScreen).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(s,null);
    check(reloaded.ActiveId=="aqua-fire" && s.fish.Variant==offer.variant && s.aquariumFishName.text=="Aqua Fire Betta","Reload restores Aqua Fire and its HUD name");
    check(UnityEngine.Object.FindObjectsByType<BettaFishAI>(UnityEngine.FindObjectsSortMode.None).Length==1,"The aquarium keeps one fish controller");
    s.Close();
    var empty=new Lumaquarium.UI.FishShopInventory(emptyKey);
    empty.TryPurchase("royal",350);empty.TryPurchase("orchid",900);
    inventoryField.SetValue(s,empty);s.Open();s.SelectFish(System.Array.IndexOf(s.offers,offer));
    check(empty.Balance==0 && s.buyButton.interactable && s.buyLabel.text=="Nhận cá","A wallet with 0 xu can receive Aqua Fire");
    s.BuyOrEquip();s.ConfirmPurchase();
    check(empty.Balance==0 && empty.Owns("aqua-fire") && empty.ActiveId=="aqua-fire","Receiving Aqua Fire at 0 xu grants and equips it without changing the balance");
}
finally
{
    s.Close();s.fish.Apply(originalVariant);inventoryField.SetValue(s,originalInventory);
    s.aquariumFishName.text=originalName;s.aquariumPortrait.sprite=originalPortrait;
    UnityEngine.PlayerPrefs.DeleteKey(key);UnityEngine.PlayerPrefs.DeleteKey(emptyKey);UnityEngine.PlayerPrefs.Save();
}
return new {passed=checks.Count,checks};
