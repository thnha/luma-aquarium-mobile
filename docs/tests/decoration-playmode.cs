// Run with `unity command eval_file --caller plugin --skill unity-cli --file docs/tests/decoration-playmode.cs`
// while Aquarium3DPreview is playing. A temporary persistence key isolates user layouts.
var d = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
if (d == null || !UnityEngine.Application.isPlaying) throw new System.Exception("Run Aquarium3DPreview in Play Mode first.");
var results = new System.Collections.Generic.List<string>();
System.Action<bool,string> check = (ok,name) => { if (!ok) throw new System.Exception("FAIL: " + name); results.Add(name); };
bool persistence = d.persistLayout;
string originalKey = d.storageKey;
d.storageKey = "Lumaquarium.Test." + System.Guid.NewGuid();
d.persistLayout = true;
int before = d.Count;
AquariumDecorItem added = null;
try
{
    check(!d.Begin(-1), "Reject unknown inventory item");
    check(d.Begin(0), "Create pending plant");
    check(d.Count == before, "Pending object is not committed");
    check(!d.MoveTo(new UnityEngine.Vector3(3,0,0)), "Reject position beyond tank");
    check(!d.ConfirmPlacement(), "Cannot confirm out-of-bounds object");
    check(!d.MoveTo(new UnityEngine.Vector3(-.48f,0,.27f)), "Reject overlap with existing plant");
    check(!d.ConfirmPlacement(), "Cannot confirm overlapping object");
    check(!d.MoveTo(new UnityEngine.Vector3(float.NaN,0,0)), "Reject non-finite pointer coordinate");
    check(d.MoveTo(new UnityEngine.Vector3(.12f,0,-.29f)), "Accept free sand position");
    float yaw = d.Selected.transform.eulerAngles.y;
    d.Rotate();
    check(UnityEngine.Mathf.Abs(UnityEngine.Mathf.DeltaAngle(yaw,d.Selected.transform.eulerAngles.y)-30)<.01f, "Rotate selected item");
    added = d.Selected;
    check(d.ConfirmPlacement() && d.Count == before + 1, "Confirm commits exactly one item");
    check(!d.Editing && d.orbit.enabled, "Confirm releases camera");
    string saved = UnityEngine.PlayerPrefs.GetString(d.storageKey);
    var entries = (Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(saved)["items"];
    check(entries.Count == before+1 && (int)entries[before]["kind"] == 0 &&
        System.Math.Abs((float)entries[before]["x"]-.12f)<.00001f &&
        System.Math.Abs((float)entries[before]["z"]+.29f)<.00001f, "Save confirmed layout");
    d.EditExisting();
    d.Select(added);
    var old = added.transform.position;
    d.MoveTo(new UnityEngine.Vector3(5,0,5));
    d.Rotate();
    d.Cancel();
    check(added.transform.position == old && UnityEngine.Mathf.Abs(UnityEngine.Mathf.DeltaAngle(30,added.transform.eulerAngles.y))<.01f, "Cancel restores position and rotation");
    check(UnityEngine.PlayerPrefs.GetString(d.storageKey)==saved, "Cancel preserves saved layout");
    d.Begin(1); d.Cancel();
    check(d.Count == before+1, "Cancel discards new item");
    d.EditExisting(); d.Select(added); d.Delete(); added=null;
    check(d.Count == before, "Delete removes item from committed layout");
    saved = UnityEngine.PlayerPrefs.GetString(d.storageKey);
    UnityEngine.PlayerPrefs.SetString(d.storageKey,"{\"items\":[{\"kind\":99,\"x\":0,\"z\":0,\"yaw\":0}]}");
    d.SendMessage("LoadLayout");
    check(d.Count == before, "Malformed inventory preserves current layout");
    UnityEngine.PlayerPrefs.SetString(d.storageKey,saved);
    d.SendMessage("LoadLayout");
    check(d.Count == before, "Reload restores saved layout without duplicating committed items");
    int capacity = d.capacity;
    d.capacity=d.Count;
    try { check(!d.Begin(0) && d.Selected == null, "Enforce inventory capacity"); }
    finally { d.capacity=capacity; d.Cancel(); }
    int ripples = d.water.RippleCount;
    d.water.Ripple(UnityEngine.Vector3.zero);
    check(d.water.RippleCount == ripples+1, "Water accepts interaction impulse");
    check(d.water.bubbles.particleCount>0, "Bubbles simulate while game advances");
    foreach(var shader in d.GetComponentsInChildren<UnityEngine.Renderer>().Where(x=>x.sharedMaterial!=null)
        .Select(x=>x.sharedMaterial.shader).Distinct())
        check(!UnityEditor.ShaderUtil.ShaderHasError(shader), "Shader compiles: "+shader.name);
}
finally
{
    d.Cancel();
    if(added!=null) { d.EditExisting(); d.Select(added); d.Delete(); }
    UnityEngine.PlayerPrefs.DeleteKey(d.storageKey);
    d.storageKey=originalKey; d.persistLayout=persistence;
}
return new {passed=results.Count, checks=results};
