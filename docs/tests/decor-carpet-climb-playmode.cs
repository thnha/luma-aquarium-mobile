// Run while MainAquarium is playing. Climbing carpets brushed onto sand, stone and the back glass. Isolated keys.
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
if(d==null || !UnityEngine.Application.isPlaying)throw new System.Exception("Run MainAquarium in Play Mode first.");
var results=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);results.Add(name);};
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var carpet=d.GetComponent<AquariumCarpet>();
string originalKey=d.storageKey, originalInventory=d.inventoryStorageKey;bool originalPersist=d.persistLayout;
var originalCarpet=carpet.Snapshot();
d.SendMessage("SaveLayout");string baseline=UnityEngine.PlayerPrefs.GetString(originalKey);
d.storageKey="Lumaquarium.Test.Carpet."+System.Guid.NewGuid();
d.inventoryStorageKey="Lumaquarium.Test.CarpetStock."+System.Guid.NewGuid();d.persistLayout=true;
var stock=new Lumaquarium.UI.FishShopInventory(d.inventoryStorageKey);stock.AddCoins(100000);
System.Func<string,int> kindOf=id=>System.Array.Find(d.catalog.offers,o=>o.id==id).prefab.kind;
System.Func<UnityEngine.Ray,bool> brush=ray=>(bool)typeof(AquariumDecoration).GetMethod("Brush",flags).Invoke(d,new object[]{ray});
System.Func<int,int> patchesOf=species=>((System.Collections.Generic.List<AquariumCarpet.Patch>)typeof(AquariumCarpet).GetField("patches",flags).GetValue(carpet)).FindAll(p=>p.species==species).Count;
try
{
    // Empty tank with one stone in the middle.
    typeof(AquariumDecoration).GetMethod("Apply",flags).Invoke(d,new object[]{System.Activator.CreateInstance(typeof(AquariumDecoration).GetNestedType("Layout",System.Reflection.BindingFlags.NonPublic))});
    carpet.Restore(new AquariumCarpet.State{cells=new byte[carpet.Snapshot().cells.Length],patches=new System.Collections.Generic.List<AquariumCarpet.Patch>()});
    stock.TryPurchaseDecor("stone-slab",1);
    for(int i=0;i<3;i++) stock.TryPurchaseDecor("carpet-java-moss",1);
    check(d.Begin(kindOf("stone-slab")),"Stone picked from stock");
    var stone=d.Selected;
    check(d.MoveTo(new UnityEngine.Vector3(0,d.floorY,0)) && d.ConfirmPlacement() && d.FindPlaced(stone.instanceId)==stone,"Stone placed in the middle");
    int java=kindOf("carpet-java-moss");
    check(d.Begin(java) && d.Brushing,"Java moss carpet opens the brush");
    check(stone.GetComponentsInChildren<UnityEngine.MeshCollider>().Length>0,"Brush puts mesh colliders on the stone");
    var top=stone.GetComponentInChildren<UnityEngine.MeshRenderer>().bounds;
    check(brush(new UnityEngine.Ray(new UnityEngine.Vector3(top.center.x,top.max.y+1,top.center.z),UnityEngine.Vector3.down)),"Brushing the stone grows a patch");
    var layer=stone.transform.Find(AquariumCarpet.PatchLayerName+"4");
    check(patchesOf(4)==1,"One patch recorded on the stone");
    // Glass: aim horizontally at the back glass above the sand.
    var glassAt=new UnityEngine.Vector3(d.min.x+.15f,d.floorY+.5f,d.WallZ);
    check(brush(new UnityEngine.Ray(glassAt+UnityEngine.Vector3.back*2,UnityEngine.Vector3.forward)),"Brushing the back glass grows a patch");
    check(patchesOf(4)==2,"Glass patch recorded");
    // Sand beside the stone.
    var sandAt=new UnityEngine.Vector3(d.max.x-.15f,d.floorY,d.min.y+.15f);
    check(brush(new UnityEngine.Ray(sandAt+UnityEngine.Vector3.up*2,UnityEngine.Vector3.down)) && carpet.Count(4)>=2,"Brushing the sand fills cells");
    int used=carpet.Count(4);
    check(used<=3*AquariumCarpet.PackCells,"Count stays within the packs bought");
    d.ConfirmPlacement();
    // Destroy lands at the end of the frame, so read the brush's own list.
    check(!d.Brushing && ((System.Collections.ICollection)typeof(AquariumDecoration).GetField("brushColliders",flags).GetValue(d)).Count==0,"Xong removes the brush colliders");
    // LateUpdate rebuilds layers; force it now.
    typeof(AquariumCarpet).GetMethod("Rebuild",flags).Invoke(carpet,null);
    layer=stone.transform.Find(AquariumCarpet.PatchLayerName+"4");
    check(layer!=null && layer.GetComponent<UnityEngine.MeshFilter>().sharedMesh.vertexCount>0,"Patch mesh rides on the stone");
    var surface=(UnityEngine.Vector3[])typeof(AquariumDecoration).GetMethod("Surface",flags).Invoke(d,new object[]{stone,null});
    check(surface.Length==stone.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.vertexCount,"Carpet is not part of the stone's surface");
    // Saved and reloaded.
    string saved=UnityEngine.PlayerPrefs.GetString(d.storageKey+".CarpetPatches");
    check(saved.Contains(stone.instanceId),"Stone patch saved with its host");
    // Undo a Cất of the stone brings the stone patch back.
    d.EditExisting();
    d.Select(stone);
    d.Delete();carpet.Save();
    check(patchesOf(4)==1 && carpet.Count(4)<=used,"Putting the stone away drops its patch");
    d.UndoLast();
    typeof(AquariumCarpet).GetMethod("Rebuild",flags).Invoke(carpet,null);
    var back=d.FindPlaced(stone.instanceId);
    check(back!=null && patchesOf(4)==2 && back.transform.Find(AquariumCarpet.PatchLayerName+"4")!=null,"Undo restores stone and patch");
    // Erase on the glass.
    d.Begin(java);typeof(AquariumDecoration).GetField("erasing",flags).SetValue(d,true);
    check(brush(new UnityEngine.Ray(glassAt+UnityEngine.Vector3.back*2,UnityEngine.Vector3.forward)) && patchesOf(4)==1,"Erasing removes the glass patch");
    d.Cancel();
    check(patchesOf(4)==2,"Hủy brings the erased patch back");
}
catch(System.Exception e) when (!(e.Message.StartsWith("FAIL")))
{
    throw new System.Exception("FAIL after \""+(results.Count>0?results[results.Count-1]:"start")+"\": "+(e.InnerException??e));
}
finally
{
    d.Leave();
    UnityEngine.PlayerPrefs.DeleteKey(d.storageKey);UnityEngine.PlayerPrefs.DeleteKey(d.storageKey+".Carpet");UnityEngine.PlayerPrefs.DeleteKey(d.storageKey+".CarpetPatches");
    UnityEngine.PlayerPrefs.DeleteKey(d.inventoryStorageKey);
    d.storageKey=originalKey;d.inventoryStorageKey=originalInventory;
    UnityEngine.PlayerPrefs.SetString(originalKey,baseline);d.persistLayout=false;d.SendMessage("LoadLayout");carpet.Restore(originalCarpet);d.persistLayout=originalPersist;
}
return new{passed=results.Count,checks=results};
