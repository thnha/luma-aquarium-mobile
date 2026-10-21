// Input/visibility regression: the selected plant must not block the wood behind it.
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
var moveRay=typeof(AquariumDecoration).GetMethod("MoveAlongRay");
if(moveRay==null)throw new System.Exception("FAIL: pointer placement needs a ray path that ignores the selected plant collider");
string key=d.inventoryStorageKey;d.inventoryStorageKey="Lumaquarium.Test.Input."+System.Guid.NewGuid();
string layout=d.storageKey;bool persist=d.persistLayout;d.persistLayout=false;
var results=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);results.Add(name);};
AquariumDecorItem wood=null;
try
{
    var shop=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>();shop.Close();
    var wallet=new Lumaquarium.UI.FishShopInventory(d.inventoryStorageKey);
    wallet.TryPurchaseDecor("wood-short",120);wallet.TryPurchaseDecor("java-moss",80);wallet.TryPurchaseDecor("duckweed",40);
    check(d.Begin(15)&&d.MoveTo(new UnityEngine.Vector3(0,0,-.24f)),"Prepare purchased wood");wood=d.Selected;check(d.ConfirmPlacement(),"Place wood for pointer test");
    check(d.Begin(9),"Prepare moss");var mount=wood.transform.TransformPoint(wood.plantMounts[0]);
    d.Selected.transform.position=mount+UnityEngine.Vector3.up*.18f;UnityEngine.Physics.SyncTransforms();
    var ray=new UnityEngine.Ray(mount+UnityEngine.Vector3.up,UnityEngine.Vector3.down);
    check((bool)moveRay.Invoke(d,new object[]{ray})&&d.Selected.transform.parent==wood.transform,"Ray ignores held moss and attaches to wood behind it");
    d.Cancel();check(d.Begin(12),"Begin floating plant");
    var viewport=d.viewCamera.WorldToViewportPoint(d.Selected.transform.position);
    check(viewport.z>0&&viewport.y>.1f&&viewport.y<.9f,"Floating plant is inside placement camera frame");
}
finally
{
    d.Cancel();if(wood!=null){d.EditExisting();d.Select(wood);d.Delete();}
    UnityEngine.PlayerPrefs.DeleteKey(d.inventoryStorageKey);d.inventoryStorageKey=key;d.storageKey=layout;d.persistLayout=persist;
}
return new{passed=results.Count,checks=results};
