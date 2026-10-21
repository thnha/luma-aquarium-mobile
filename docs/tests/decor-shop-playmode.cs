// Run while MainAquarium is playing. All purchases and layouts use isolated keys.
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
var shop=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>();
if(d==null || shop==null || !UnityEngine.Application.isPlaying)throw new System.Exception("Run MainAquarium in Play Mode first.");
var results=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);results.Add(name);};
string originalKey=d.storageKey, originalInventory=d.inventoryStorageKey;
bool originalPersist=d.persistLayout;
d.storageKey="Lumaquarium.Test.Layout."+System.Guid.NewGuid();
d.inventoryStorageKey="Lumaquarium.Test.Stock."+System.Guid.NewGuid();d.persistLayout=true;
d.SendMessage("SaveLayout");string baseline=UnityEngine.PlayerPrefs.GetString(d.storageKey);
var wallet=new Lumaquarium.UI.FishShopInventory(d.inventoryStorageKey);
System.Func<string,AquariumDecorItem> find=id=>d.itemsRoot.GetComponentsInChildren<AquariumDecorItem>().Last(x=>x.instanceId==id);
try
{
    int initial=d.Count;
    check(!d.Begin(9),"Cannot place unpurchased moss");
    shop.Open();shop.SelectItem(6);
    check(shop.IsOpen && !d.orbit.enabled && d.screen.hud.alpha==0,"Shop owns input and hides HUD");
    shop.RequestPurchase();shop.CancelPurchase();wallet.Reload();
    check(wallet.Balance==1250 && wallet.DecorCount("java-moss")==0,"Cancel purchase preserves money and stock");
    shop.RequestPurchase();shop.ConfirmPurchase();shop.ConfirmPurchase();wallet.Reload();
    check(wallet.Balance==1170 && wallet.DecorCount("java-moss")==1,"Double confirmation buys one item");
    shop.RequestPurchase();shop.ConfirmPurchase();wallet.Reload();
    check(wallet.Balance==1090 && wallet.DecorCount("java-moss")==2,"Repeat UI purchase adds a second item");
    shop.SelectCategory(1);check(shop.cards[12].activeSelf && !shop.cards[0].activeSelf,"Wood category filters cards");
    shop.SelectItem(12);shop.RequestPurchase();shop.ConfirmPurchase();
    shop.SelectCategory(0);shop.SelectItem(9);shop.RequestPurchase();shop.ConfirmPurchase();wallet.Reload();
    check(wallet.Balance==930 && wallet.DecorCount("wood-short")==1 && wallet.DecorCount("duckweed")==1,"Shared wallet charges each selected item");
    shop.Close();check(!shop.IsOpen && d.orbit.enabled && d.screen.hud.alpha==1,"Closing shop restores aquarium input");
    check(d.Begin(15),"Begin purchased wood");d.Cancel();
    check(d.Available(15)==1 && d.Count==initial,"Cancel placement retains item in stock");
    check(d.Begin(15) && d.MoveTo(new UnityEngine.Vector3(0,0,-.24f)),"Place wood on free sand");
    string woodId=d.Selected.instanceId;check(d.ConfirmPlacement(),"Confirm wood");
    check(d.Available(15)==0 && !d.Begin(15),"One purchase permits only one placed wood");
    var wood=find(woodId);
    check(d.Begin(9) && d.AttachTo(wood,wood.transform.TransformPoint(wood.plantMounts[0])),"Attach moss to wood");
    string mossId=d.Selected.instanceId;check(d.ConfirmPlacement(),"Confirm attached moss");
    var moss=find(mossId);var original=moss.transform.position;
    d.EditExisting();d.Select(wood);d.Rotate();
    check(moss.transform.parent==wood.transform && UnityEngine.Vector3.Distance(original,moss.transform.position)>.01f,"Rotating wood carries moss");
    d.Cancel();check(UnityEngine.Vector3.Distance(original,moss.transform.position)<.001f,"Cancel wood rotation restores moss");
    d.EditExisting();d.Select(moss);d.MoveTo(new UnityEngine.Vector3(0,0,-.24f));d.Cancel();
    check(moss.transform.parent==wood.transform && UnityEngine.Vector3.Distance(original,moss.transform.position)<.001f,"Cancel detaching restores attachment");
    check(d.Begin(9) && !d.AttachTo(wood,wood.transform.TransformPoint(wood.plantMounts[0])),"Occupied wood mount rejects second moss");
    d.Cancel();check(d.Available(9)==1,"Rejected attachment does not spend stock");
    check(d.Begin(12) && d.MoveTo(new UnityEngine.Vector3(0,0,-.24f)),"Floating plant can overlap wood horizontally");
    string floatId=d.Selected.instanceId;
    check(UnityEngine.Mathf.Abs(d.Selected.transform.position.y-(d.water.surfaceY-.012f))<.001f,"Floating plant stays at water surface");
    check(d.ConfirmPlacement(),"Confirm floating plant");
    d.SendMessage("LoadLayout");wood=find(woodId);moss=find(mossId);
    check(d.Count==initial+3 && moss.transform.parent==wood.transform,"Reload restores attachment without duplicating items");
    check(UnityEngine.Mathf.Abs(find(floatId).transform.position.y-(d.water.surfaceY-.012f))<.001f,"Reload keeps floating plant at surface");
    check(d.Available(15)==0 && d.Available(9)==1,"Reload preserves available stock");
    d.EditExisting();d.Select(wood);d.Delete();
    check(d.Count==initial+1 && d.Available(15)==1 && d.Available(9)==2,"Storing wood returns wood and attached moss to stock");
    d.EditExisting();d.Select(find(floatId));d.Delete();
    check(d.Count==initial && d.Available(12)==1,"Storing floating plant returns it to stock");
    int limit=d.capacity;d.capacity=d.Count;
    try{check(!d.Begin(9) && d.Available(9)==2,"Full aquarium never spends stock");}
    finally{d.capacity=limit;d.Cancel();}
    check(UnityEngine.Object.FindObjectsByType<BettaFishAI>(UnityEngine.FindObjectsSortMode.None).Length==1,"Decoration shop never duplicates fish");
}
finally
{
    shop.Close();d.Cancel();UnityEngine.PlayerPrefs.SetString(d.storageKey,baseline);d.SendMessage("LoadLayout");
    UnityEngine.PlayerPrefs.DeleteKey(d.storageKey);UnityEngine.PlayerPrefs.DeleteKey(d.inventoryStorageKey);
    d.storageKey=originalKey;d.inventoryStorageKey=originalInventory;d.persistLayout=originalPersist;
}
return new{passed=results.Count,checks=results};
