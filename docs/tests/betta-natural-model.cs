// Edit Mode: the skull should become fuller without extending the pointed snout.
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var clone = UnityEngine.Object.Instantiate(prefab);
try
{
    var body = clone.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r => r.name == "BODY");
    var source = body.sharedMesh;
    var original = source.vertices;
    int hi = System.Array.FindIndex(body.bones, b => b.name == "Head");
    int ni = System.Array.FindIndex(body.bones, b => b.name == "Spine_01");
    var head = source.bindposes[hi].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
    var neck = source.bindposes[ni].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
    var forward = (head - neck).normalized;
    float oldNose = original.Max(p => UnityEngine.Vector3.Dot(p-head, forward));
    var variants = UnityEditor.AssetDatabase.FindAssets("t:BettaVariant", new[]{"Assets/_Project/Data/Betta/Variants"}).Select(id => UnityEditor.AssetDatabase.LoadAssetAtPath<BettaVariant>(UnityEditor.AssetDatabase.GUIDToAssetPath(id)));
    int checkedVariants = 0;
    foreach(var v in variants)
    {
        var shaped = BettaHeadShape.Get(source,body.bones,head,neck,v.headScale);
        float nose = shaped.vertices.Max(p => UnityEngine.Vector3.Dot(p-head,forward));
        if(nose > oldNose*1.15f) throw new System.Exception(v.name+": snout is still stretched forward: "+nose/oldNose);
        if(shaped.subMeshCount != 2 || shaped.GetTriangles(0).Length == 0 || shaped.GetTriangles(1).Length == 0)
            throw new System.Exception("Skin and thin pectoral/pelvic membranes still share one opaque surface");
        if(shaped.triangles.Length != source.triangles.Length) throw new System.Exception("Splitting membranes lost triangles");
        checkedVariants++;
    }
    var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Art/Betta/Betta.mat");
    if(mat.GetFloat("_BumpScale") > .3f) throw new System.Exception("Scale relief is still too strong");
    var membrane=BettaFinSurface.Membrane(mat);
    if(membrane.GetFloat("_ZWrite")!=0f || membrane.GetFloat("_AlphaToMask")!=0f || !membrane.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
        || BettaFinSurface.Membrane(membrane)!=membrane || mat.GetFloat("_ZWrite")!=1f)
        throw new System.Exception("Membrane must blend alpha, reuse its material and leave the skin opaque");
    var appearance=clone.AddComponent<BettaAppearance>();
    var rosetail=UnityEditor.AssetDatabase.LoadAssetAtPath<BettaVariant>("Assets/_Project/Data/Betta/Variants/AquaPearlRosetail.asset");
    appearance.Apply(rosetail);
    var copy=UnityEngine.Object.Instantiate(clone);
    try
    {
        copy.GetComponent<BettaAppearance>().Apply(rosetail);
        foreach(var fin in clone.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().Where(r=>r.name.StartsWith("Fin_")&&r.enabled))
            if(copy.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r=>r.name==fin.name).sharedMesh != fin.sharedMesh)
                throw new System.Exception("Cloning a shop fish must not ruffle the fins twice");
    }
    finally {UnityEngine.Object.DestroyImmediate(copy);}
    return new {passed=checkedVariants+1,checkedVariants};
}
finally { UnityEngine.Object.DestroyImmediate(clone); }
