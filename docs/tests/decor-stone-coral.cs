// Six named stones and six corals: stable kinds after the driftwood species, resting on the sand, mobile-sized meshes.
var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>("Assets/_Project/Art/Aquarium3D/DecorShop/AquariumDecorCatalog.asset");
var results=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,label)=>{ if(!ok) throw new System.Exception("FAIL: "+label); results.Add(label); };
string[] ids={"stone-elephant-skin","stone-coral-rock","stone-lava-red","stone-sea-pebbles","stone-co-thach","stone-tuyet-son",
    "coral-rubble","coral-block","coral-mushroom","coral-zoanthid","coral-artificial-branch","coral-artificial-fan"};
check(catalog.offers.Length==47,"Catalog lists 24 shop items, 8 driftwood species, 12 stones/corals and 3 starters");
check(catalog.Find(27).id=="wood-linh-sam" && catalog.Find(34).id=="wood-bonsai" && catalog.Find(2).id=="starter-wood","Released kinds keep their items");
for(int i=0;i<ids.Length;i++)
{
    var offer=System.Array.Find(catalog.offers,x=>x.id==ids[i]);
    check(offer!=null && offer.IsValid && offer.category==AquariumDecorCategory.Rocks,ids[i]+" is a valid offer in the Đá tab");
    var item=offer.prefab;
    check(item.kind==35+i && catalog.Find(35+i)==offer,ids[i]+" uses kind "+(35+i));
    check(!item.supportsPlants && !item.attachable && !item.floats,ids[i]+" sits on the sand like the other rocks");
    var mesh=item.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
    check(mesh.bounds.min.y>-.001f && mesh.triangles.Length/3<10000,ids[i]+" stands on the sand within the mobile triangle budget");
    check(mesh.colors.Length==mesh.vertexCount,ids[i]+" carries its colour");
    check(item.footprint>=.12f && item.footprint<=.32f,ids[i]+" has a placement footprint");
}
return new{passed=results.Count,checks=results};
