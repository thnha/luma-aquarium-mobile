// Named mosses: stable kinds after the driftwood, attachable pads standing on their base, and rocks that take moss.
var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>("Assets/_Project/Art/Aquarium3D/DecorShop/AquariumDecorCatalog.asset");
var results=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,label)=>{ if(!ok) throw new System.Exception("FAIL: "+label); results.Add(label); };
string[] ids={"moss-mini-taiwan","moss-weeping","moss-flame","mini-fiss"};
int[] kinds={35,36,37,11};
check(catalog.offers.Length==52,"Catalog lists 24 shop items, 8 driftwood species, 3 mosses, 2 moss walls, 12 stones/corals and 3 starters");
for(int i=0;i<ids.Length;i++)
{
    var offer=System.Array.Find(catalog.offers,x=>x.id==ids[i]);
    check(offer!=null && offer.IsValid && offer.category==AquariumDecorCategory.Plants,ids[i]+" is a valid plant offer");
    var item=offer.prefab;
    check(item.kind==kinds[i] && catalog.Find(kinds[i])==offer,ids[i]+" uses kind "+kinds[i]);
    check(item.attachable && !item.floats && !item.supportsPlants,ids[i]+" attaches to wood or rock and grows on sand");
    check(item.foliage!=null,ids[i]+" sways with the current");
    var mesh=item.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
    check(mesh.bounds.min.y>-.001f && mesh.bounds.min.y<.01f,ids[i]+" pad rests on its base");
    check(mesh.triangles.Length/3<8000 && mesh.colors.Length==mesh.vertexCount,ids[i]+" is coloured and within the mobile triangle budget");
    check(item.footprint>=.06f && item.footprint<=.12f,ids[i]+" has a small sand footprint");
}
string[] rocks={"river-pebbles","dragon-stone","lava-rock","seiryu","rock-cluster","mini-landscape","starter-stone"};
foreach(var id in rocks)
{
    var item=System.Array.Find(catalog.offers,x=>x.id==id).prefab;
    check(item.supportsPlants && item.plantMounts.Length>=1 && item.plantMounts.Length<=3,id+" offers moss mounts");
    // Built rocks carry a box around the mesh; the starter stone keeps whichever collider it shipped with.
    var box=item.GetComponent<UnityEngine.BoxCollider>();
    if(box!=null) check(System.Array.TrueForAll(item.plantMounts,new UnityEngine.Bounds(box.center,box.size).Contains),id+" mounts sit on the rock");
    bool spaced=true;
    for(int a=0;a<item.plantMounts.Length;a++) for(int b=a+1;b<item.plantMounts.Length;b++)
        spaced&=UnityEngine.Vector3.Distance(item.plantMounts[a],item.plantMounts[b])>=.05f;
    check(spaced,id+" mounts are far enough apart for two pads");
}
string[] walls={"moss-wall-taiwan","moss-wall-weeping"};
for(int i=0;i<walls.Length;i++)
{
    var offer=System.Array.Find(catalog.offers,x=>x.id==walls[i]);
    check(offer!=null && offer.IsValid && offer.category==AquariumDecorCategory.Plants,walls[i]+" is a valid plant offer");
    var item=offer.prefab;
    check(item.kind==38+i && catalog.Find(38+i)==offer,walls[i]+" uses kind "+(38+i));
    check(item.wallMounted && !item.attachable && !item.floats && !item.supportsPlants && item.wallSize.x>0 && item.wallSize.y>0,walls[i]+" hangs on the back glass only");
    var mesh=item.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
    check(mesh.bounds.max.z<.01f && mesh.bounds.min.z>-.12f,walls[i]+" lies flat against the glass, facing the room");
    check(mesh.bounds.min.y>-.001f && mesh.bounds.size.y>=item.wallSize.y*.9f && mesh.bounds.size.x>=item.wallSize.x*.9f,walls[i]+" covers its panel from the bottom edge");
    check(mesh.triangles.Length/3<12000 && mesh.colors.Length==mesh.vertexCount,walls[i]+" is coloured and within the mobile triangle budget");
}
return new{passed=results.Count,checks=results};
