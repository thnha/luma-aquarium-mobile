// Play Mode: steady water flow must bend each complete ray gently, without folding it back.
if(!UnityEngine.Application.isPlaying || UnityEngine.Time.deltaTime <= 0) throw new System.Exception("Run in advancing Play Mode");
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var clone=UnityEngine.Object.Instantiate(prefab);
try
{
    clone.GetComponent<UnityEngine.Animator>().enabled=false;
    var physics=clone.AddComponent<BettaFinPhysics>();
    physics.enabled=false;
    var transforms=clone.GetComponentsInChildren<UnityEngine.Transform>();
    var initial=transforms.ToDictionary(t=>t,t=>t.localRotation);
    var update=typeof(BettaFinPhysics).GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
    float dt=UnityEngine.Time.deltaTime;
    for(int i=0;i<180;i++) {clone.transform.position+=UnityEngine.Vector3.right*(.15f*dt);update.Invoke(physics,null);}
    var results=new System.Collections.Generic.List<object>();
    var tips=transforms.Where(t=>t.name.EndsWith("_05")&&t.name.StartsWith("Tail_")
        ||t.name.EndsWith("_04")&&(t.name.StartsWith("Dorsal_")||t.name.StartsWith("Anal_"))
        ||t.name.EndsWith("_03")&&(t.name.Contains("_Crown_")||t.name.StartsWith("Pelvic_"))
        ||t.name.StartsWith("Pectoral_")&&(t.name.EndsWith("_03A")||t.name.EndsWith("_03B"))).ToArray();
    foreach(var tip in tips)
    {
        float bend=0;
        for(var t=tip;t&&t!=clone.transform;t=t.parent) bend+=UnityEngine.Quaternion.Angle(initial[t],t.localRotation);
        float budget=tip.name.StartsWith("Tail_")?physics.caudal.maxAngle:tip.name.StartsWith("Pelvic_")?physics.pelvic.maxAngle:tip.name.StartsWith("Pectoral_")?physics.pectoral.maxAngle:physics.dorsalAnal.maxAngle;
        if(bend>budget+1f) throw new System.Exception(tip.name+" folds through "+bend+" degrees in steady flow (budget "+budget+")");
        results.Add(new{ray=tip.name,bend,budget});
    }
    if(results.Count<40) throw new System.Exception("Branching fin paths were not exercised");
    return new{passed=results.Count,results};
}
finally{UnityEngine.Object.DestroyImmediate(clone);}
