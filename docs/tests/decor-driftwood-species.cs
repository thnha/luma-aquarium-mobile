// Eight named driftwood species: stable kinds after the released ones, plant mounts on the wood, mobile-sized meshes.
var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>("Assets/_Project/Art/Aquarium3D/DecorShop/AquariumDecorCatalog.asset");
var results=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,label)=>{ if(!ok) throw new System.Exception("FAIL: "+label); results.Add(label); };
string[] ids={"wood-linh-sam","wood-hai-son-tung","wood-do-quyen","wood-spider","wood-san-da","wood-mopani","wood-tra-gung","wood-bonsai"};
check(catalog.Find(15)!=null && catalog.Find(15).id=="wood-short" && catalog.Find(2).id=="starter-wood","Released kinds keep their items");
for(int i=0;i<ids.Length;i++)
{
    var offer=System.Array.Find(catalog.offers,x=>x.id==ids[i]);
    check(offer!=null && offer.IsValid && offer.category==AquariumDecorCategory.Wood,ids[i]+" is a valid wood offer");
    var item=offer.prefab;
    check(item.kind==27+i && catalog.Find(27+i)==offer,ids[i]+" uses kind "+(27+i));
    check(item.supportsPlants && item.plantMounts.Length==3,ids[i]+" has three plant mounts");
    var mesh=item.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
    var box=item.GetComponent<UnityEngine.BoxCollider>();
    var bounds=new UnityEngine.Bounds(box.center,box.size);
    check(System.Array.TrueForAll(item.plantMounts,bounds.Contains),ids[i]+" mounts sit on the wood");
    check(mesh.bounds.min.y>-.001f && mesh.triangles.Length/3<10000,ids[i]+" stands on the sand within the mobile triangle budget");
    check(mesh.colors.Length==mesh.vertexCount,ids[i]+" carries its wood colour");
}
return new{passed=results.Count,checks=results};
