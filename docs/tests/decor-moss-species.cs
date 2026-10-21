// Named mosses: stable kinds after the driftwood, attachable pads standing on their base, and rocks that take moss.
var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>("Assets/_Project/Art/Aquarium3D/DecorShop/AquariumDecorCatalog.asset");
var results=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,label)=>{ if(!ok) throw new System.Exception("FAIL: "+label); results.Add(label); };
string[] ids={"moss-mini-taiwan","moss-weeping","moss-flame","mini-fiss"};
int[] kinds={35,36,37,11};
check(catalog.offers.Length==38,"Catalog lists 24 shop items, 8 driftwood species, 3 mosses and 3 starters");
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
return new{passed=results.Count,checks=results};
