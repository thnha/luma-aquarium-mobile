// Six named stones and six corals: stable kinds after the moss walls, resting on the sand, mobile-sized meshes.
var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>("Assets/_Project/Art/Aquarium3D/DecorShop/AquariumDecorCatalog.asset");
var results=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,label)=>{ if(!ok) throw new System.Exception("FAIL: "+label); results.Add(label); };
string[] ids={"stone-elephant-skin","stone-coral-rock","stone-lava-red","stone-sea-pebbles","stone-co-thach","stone-tuyet-son",
    "coral-rubble","coral-block","coral-mushroom","coral-zoanthid","coral-artificial-branch","coral-artificial-fan"};
check(catalog.offers.Length==52,"Catalog lists 24 shop items, 8 driftwood species, 3 mosses, 2 moss walls, 12 stones/corals and 3 starters");
check(catalog.Find(27).id=="wood-linh-sam" && catalog.Find(34).id=="wood-bonsai" && catalog.Find(35)!=null && catalog.Find(39)!=null && catalog.Find(39).prefab.wallMounted && catalog.Find(2).id=="starter-wood","Released kinds keep their items");
for(int i=0;i<ids.Length;i++)
{
    var offer=System.Array.Find(catalog.offers,x=>x.id==ids[i]);
    var tab=i<6?AquariumDecorCategory.Rocks:AquariumDecorCategory.Coral;
    check(offer!=null && offer.IsValid && offer.category==tab,ids[i]+" is a valid offer in the "+(i<6?"Đá":"San hô")+" tab");
    var item=offer.prefab;
    check(item.kind==40+i && catalog.Find(40+i)==offer,ids[i]+" uses kind "+(40+i));
    check(!item.attachable && !item.floats && !item.wallMounted,ids[i]+" sits on the sand like the other rocks");
    check(i<6 ? item.supportsPlants && item.plantMounts.Length>=1 && item.plantMounts.Length<=3 : !item.supportsPlants,ids[i]+(i<6?" takes moss on its top faces":" stays bare"));
    var mesh=item.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
    var box=item.GetComponent<UnityEngine.BoxCollider>();
    check(System.Array.TrueForAll(item.plantMounts,new UnityEngine.Bounds(box.center,box.size).Contains),ids[i]+" mounts sit on the stone");
    check(mesh.bounds.min.y>-.001f && mesh.triangles.Length/3<10000,ids[i]+" stands on the sand within the mobile triangle budget");
    check(mesh.colors.Length==mesh.vertexCount,ids[i]+" carries its colour");
    check(item.footprint>=.12f && item.footprint<=.32f,ids[i]+" has a placement footprint");
}
var shop=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>(UnityEngine.FindObjectsInactive.Include);
if(shop!=null) check(shop.categoryButtons.Length==4,"Decoration shop has Cây/Lũa/Đá/San hô tabs");
var panel=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.DecorInventoryPanel>(UnityEngine.FindObjectsInactive.Include);
if(panel!=null) check(panel.tabButtons.Length==4,"Inventory panel has a San hô tab");
return new{passed=results.Count,checks=results};
