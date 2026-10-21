// Run while MainAquarium is playing. Moss walls hang on the back glass, tile side by side and reload in place.
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
if(d==null || !UnityEngine.Application.isPlaying)throw new System.Exception("Run MainAquarium in Play Mode first.");
var results=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);results.Add(name);};
string originalKey=d.storageKey, originalInventory=d.inventoryStorageKey;
bool originalPersist=d.persistLayout;
d.storageKey="Lumaquarium.Test.Layout."+System.Guid.NewGuid();
d.inventoryStorageKey="Lumaquarium.Test.Stock."+System.Guid.NewGuid();d.persistLayout=true;
d.SendMessage("SaveLayout");string baseline=UnityEngine.PlayerPrefs.GetString(d.storageKey);
System.Func<string,AquariumDecorItem> find=id=>d.itemsRoot.GetComponentsInChildren<AquariumDecorItem>().Last(x=>x.instanceId==id);
const int wall=38;
float backZ=d.max.y;
try
{
    var wallet=new Lumaquarium.UI.FishShopInventory(d.inventoryStorageKey);
    check(wallet.TryPurchaseDecor("moss-wall-taiwan",220) && wallet.TryPurchaseDecor("moss-wall-taiwan",220),"Buy two moss wall panels");
    int initial=d.Count;
    check(d.Begin(wall) && d.Selected.wallMounted,"Begin a moss wall panel");
    check(UnityEngine.Mathf.Abs(d.Selected.transform.position.z-backZ)<.001f,"New panel starts on the back glass");
    check(d.MoveTo(new UnityEngine.Vector3(-.3f,d.floorY,-.4f)) && UnityEngine.Mathf.Abs(d.Selected.transform.position.z-backZ)<.001f,"Dragging keeps the panel on the glass");
    var rotation=d.Selected.transform.rotation;d.Rotate();
    check(UnityEngine.Quaternion.Angle(rotation,d.Selected.transform.rotation)<.01f,"Panels never turn away from the glass");
    check(!d.MoveTo(new UnityEngine.Vector3(-.9f,d.floorY,0)),"Panel past the side glass is rejected");
    check(!d.MoveTo(new UnityEngine.Vector3(-.3f,d.water.surfaceY,0)),"Panel above the water line is rejected");
    check(d.MoveTo(new UnityEngine.Vector3(-.3f,d.floorY+.2f,0)),"Panel can hang higher on the glass");
    var cameraRay=d.viewCamera.ScreenPointToRay(d.viewCamera.WorldToScreenPoint(new UnityEngine.Vector3(-.3f,d.floorY+.36f,backZ)));
    check(d.MoveAlongRay(cameraRay) && UnityEngine.Vector3.Distance(d.Selected.transform.position,new UnityEngine.Vector3(-.3f,d.floorY+.2f,backZ))<.01f,"Touch drag centres the panel under the finger");
    string firstId=d.Selected.instanceId;var firstPosition=d.Selected.transform.position;
    check(d.ConfirmPlacement(),"Confirm first panel");
    check(d.Begin(wall) && !d.MoveTo(firstPosition),"A second panel cannot cover the first");
    check(d.MoveTo(firstPosition+new UnityEngine.Vector3(.38f,0,0)),"Neighbouring panels tile with a small overlap");
    string secondId=d.Selected.instanceId;var secondPosition=d.Selected.transform.position;
    check(d.ConfirmPlacement() && d.Available(wall)==0,"Confirm second panel spends the last stock");
    d.SendMessage("LoadLayout");
    check(d.Count==initial+2,"Reload keeps both panels");
    check(UnityEngine.Vector3.Distance(find(firstId).transform.position,firstPosition)<.001f &&
        UnityEngine.Vector3.Distance(find(secondId).transform.position,secondPosition)<.001f,"Reload restores panel height on the glass");
    d.EditExisting();d.Select(find(firstId));d.Delete();
    check(d.Count==initial+1 && d.Available(wall)==1,"Storing a panel returns it to stock");
}
finally
{
    d.Cancel();UnityEngine.PlayerPrefs.SetString(d.storageKey,baseline);d.SendMessage("LoadLayout");
    UnityEngine.PlayerPrefs.DeleteKey(d.storageKey);UnityEngine.PlayerPrefs.DeleteKey(d.inventoryStorageKey);
    d.storageKey=originalKey;d.inventoryStorageKey=originalInventory;d.persistLayout=originalPersist;
}
return new{passed=results.Count,checks=results};
