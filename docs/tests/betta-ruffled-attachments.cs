// The real ruffled Rosetail must retain its body attachments while fin bones move.
// BODY and separate membranes must retain their common attachment points in pose.
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var fish=UnityEngine.Object.Instantiate(prefab);
var before=new UnityEngine.Mesh();var after=new UnityEngine.Mesh();
try {
    fish.GetComponent<UnityEngine.Animator>().enabled=false;
    var renderer=fish.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r=>r.name=="BODY");
    var source=renderer.sharedMesh;var originalWeights=source.boneWeights;
    var appearance=fish.GetComponent<BettaAppearance>()??fish.AddComponent<BettaAppearance>();
    appearance.Apply(UnityEditor.AssetDatabase.LoadAssetAtPath<BettaVariant>("Assets/_Project/Data/Betta/Variants/AquaPearlRosetail.asset"));
    renderer.BakeMesh(before);
    var skin=renderer.sharedMesh.GetTriangles(0).Distinct().ToArray();
    var rest=renderer.bones.Select(b=>(b.localRotation,b.localScale)).ToArray();
    string[] roots={"Dorsal_A_01","Dorsal_B_01","Anal_A_01","Anal_B_01","Tail_Top_01","Tail_UpMid_01","Tail_01","Tail_LowMid_01","Tail_Low_01","Tail_Bot_01","Pelvic_Base","Pelvic_L_01","Pelvic_R_01","Pectoral_L_01","Pectoral_R_01"};
    int checks=0;float worstTorso=0f,worstRoot=0f;int rootSamples=0;
    var originals=source.vertices;
    var groups=originals.Select((p,i)=>new{key=UnityEngine.Vector3Int.RoundToInt(p*100000f),i}).GroupBy(v=>v.key).ToDictionary(g=>g.Key,g=>g.First().i);
    var profileSources=fish.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("Fin_")).ToDictionary(r=>r,r=>BettaHeadShape.Source(BettaFinRuffle.Source(r.sharedMesh)));
    foreach(string name in roots){
        int index=Array.FindIndex(renderer.bones,b=>b.name==name);var bone=renderer.bones[index];
        bone.localRotation=rest[index].Item1*UnityEngine.Quaternion.Euler(30,0,12);
        bone.localScale=rest[index].Item2*1.4f;
        renderer.BakeMesh(after);var positions=after.vertices;var baseline=before.vertices;
        float movement=skin.Max(i=>UnityEngine.Vector3.Distance(positions[i],baseline[i]));
        worstTorso=Mathf.Max(worstTorso,movement);
        if(movement>.00001f)throw new Exception(name+" pulls opaque body by "+movement+"m");checks++;
        foreach(var pair in profileSources){
            var fin=pair.Key;var sourceFin=pair.Value;
            if(fin.enabled&&fin.sharedMesh.vertexCount<=sourceFin.vertexCount)throw new Exception(fin.name+": Rosetail ruffle missing");
            // Ruffle preserves the original vertices before appending subdivisions.
            var baked=new UnityEngine.Mesh();
            try{fin.BakeMesh(baked);var fp=baked.vertices;var fv=sourceFin.vertices;
                for(int i=0;i<fv.Length;i++)if(groups.TryGetValue(UnityEngine.Vector3Int.RoundToInt(fv[i]*100000f),out int j)){
                    float gap=UnityEngine.Vector3.Distance(fin.transform.TransformPoint(fp[i]),renderer.transform.TransformPoint(positions[j]));
                    worstRoot=Mathf.Max(worstRoot,gap);rootSamples++;
                    if(gap>.00002f)throw new Exception(fin.name+" opens BODY root by "+gap+"m during "+name);
                }
            }finally{UnityEngine.Object.DestroyImmediate(baked);}
        }
        bone.localRotation=rest[index].Item1;bone.localScale=rest[index].Item2;
    }
    if(!source.boneWeights.SequenceEqual(originalWeights))throw new Exception("Authored skin weights changed");
    return new{passed=checks,rootSamples,worstTorso,worstRoot};
} finally {UnityEngine.Object.DestroyImmediate(before);UnityEngine.Object.DestroyImmediate(after);UnityEngine.Object.DestroyImmediate(fish);}
