var d = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
if (d == null || d.prefabs.Length < 26) throw new System.Exception("FAIL: shop must provide all plant, moss, floating plant, wood and rock prefabs");
var results = new System.Collections.Generic.List<string>();
System.Action<bool,string> check = (ok,label) => { if(!ok) throw new System.Exception("FAIL: " + label); results.Add(label); };
var catalog = d.GetType().GetField("catalog").GetValue(d) as UnityEngine.ScriptableObject;
check(catalog != null,"Catalog connected to aquarium");
check(UnityEngine.Object.FindObjectsByType<BettaFishAI>(UnityEngine.FindObjectsSortMode.None).Length==1,"Preserve one fish");
foreach(var p in d.prefabs)
{
    check(p!=null && p.GetComponentsInChildren<UnityEngine.MeshRenderer>().Length>0,"Renderable prefab");
    check(p.GetComponent<UnityEngine.Collider>()!=null,"Selectable prefab");
    check(p.kind>=0 && d.prefabs[p.kind]==p,"Stable layout kind");
}
return new {passed=results.Count,checks=results};
