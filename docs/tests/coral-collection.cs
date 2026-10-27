// Runs via Pipeline eval_file in Edit Mode. No wallet or layout writes.
var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>("Assets/_Project/Art/Aquarium3D/DecorShop/AquariumDecorCatalog.asset");
string[] ids={"coral-artificial-branch","coral-artificial-fan","coral-plating","coral-purple-branch","coral-torch","coral-mushroom","coral-zoanthid","coral-block","coral-blue-tube","coral-lobophyllia"};
int passed=0; System.Action<bool,string> check=(ok,label)=>{if(!ok)throw new System.Exception(label);passed++;};
check(ids.All(id=>catalog.offers.Any(o=>o.id==id && o.IsValid)),"All ten approved coral designs must be available in the catalog");
check(catalog.offers.Select(o=>o.id).Distinct().Count()==catalog.offers.Length,"Unique catalog IDs");
check(catalog.offers.Select(o=>o.prefab.kind).Distinct().Count()==catalog.offers.Length,"Unique placement kinds");
var reports=new System.Collections.Generic.List<object>();
foreach(var id in ids)
{
    var offer=catalog.offers.Single(o=>o.id==id);var item=offer.prefab;
    check(offer.category==AquariumDecorCategory.Coral && !item.supportsPlants && !item.floats && !item.attachable,"Coral placement flags: "+id);
    var filters=item.GetComponentsInChildren<UnityEngine.MeshFilter>();int tris=0;
    foreach(var filter in filters)
    {
        var mesh=filter.sharedMesh;tris+=mesh.triangles.Length/3;
        check(mesh.colors.Length==mesh.vertexCount && mesh.normals.Length==mesh.vertexCount,"Complete mesh attributes: "+id);
        check(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.sqrMagnitude)),"Finite geometry: "+id);
        check(mesh.triangles.All(i=>i>=0&&i<mesh.vertexCount),"Valid indices: "+id);
        check(mesh.normals.All(n=>n.sqrMagnitude>.5f),"Nonzero normals: "+id);
        check(mesh.bounds.min.y>=-.001f,"Stands on sand: "+id);
    }
    check(tris>500 && tris<=30000,"Mobile geometry budget: "+id+" = "+tris);
    check(item.GetComponent<UnityEngine.BoxCollider>()!=null && item.footprint>=.12f,"Placement collider/footprint: "+id);
    var mat=item.GetComponentInChildren<UnityEngine.MeshRenderer>().sharedMaterial;
    check(mat.shader.name=="Lumaquarium/Aquarium Coral" && !UnityEditor.ShaderUtil.ShaderHasError(mat.shader),"Compiled coral shader: "+id);
    reports.Add(new{id,kind=item.kind,triangles=tris});
}
check(catalog.Find(46).id=="coral-rubble" && catalog.Find(47).id=="coral-block" && catalog.Find(48).id=="coral-mushroom" && catalog.Find(49).id=="coral-zoanthid" && catalog.Find(50).id=="coral-artificial-branch" && catalog.Find(51).id=="coral-artificial-fan","Legacy coral kinds stable");
check(catalog.Find(52).id=="wood-linh-sam-m" && catalog.Find(88).id=="carpet-monte-carlo","Large-piece kinds stable");
return new{passed,models=reports};
