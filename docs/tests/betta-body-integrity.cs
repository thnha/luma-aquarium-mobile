// Shared attachment points on an inactive prefab must stay coincident.
// Also reject reversed opaque torso faces after relaxation (the head/membranes
// have their own shape and animation checks).
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var renderers=prefab.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true);
var body=renderers.First(r=>r.name=="BODY");var source=body.sharedMesh;
int hi=Array.FindIndex(body.bones,b=>b.name=="Head"),ti=Array.FindIndex(body.bones,b=>b.name=="Tail_Base"),ni=Array.FindIndex(body.bones,b=>b.name=="Spine_01");
var head=source.bindposes[hi].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
var tail=source.bindposes[ti].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
var neck=source.bindposes[ni].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
var original=source.vertices;var triangles=source.triangles;
var groups=original.Select((p,i)=>new{key=UnityEngine.Vector3Int.RoundToInt(p*100000f),i}).GroupBy(v=>v.key).ToDictionary(g=>g.Key,g=>g.First().i);
var reports=new System.Collections.Generic.List<object>();
foreach(float proportion in new[]{.8f,1f,1.3f}){
 var profile=new BettaBodyProfile(head,tail,proportion);
 var shape=BettaHeadShape.Get(source,body.bones,head,neck,proportion);var shaped=shape.vertices;triangles=shape.GetTriangles(0);
 int flipped=0;float minCos=1f;int faceSamples=0,totalRootSamples=0;var flippedPoints=new System.Collections.Generic.List<object>();
 for(int i=0;i<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
    if(UnityEngine.Vector3.Dot(original[a]-head,(head-tail).normalized)>-.22f*UnityEngine.Vector3.Distance(head,tail))continue;
    var before=UnityEngine.Vector3.Cross(profile.Map(original[b])-profile.Map(original[a]),profile.Map(original[c])-profile.Map(original[a]));
    var after=UnityEngine.Vector3.Cross(shaped[b]-shaped[a],shaped[c]-shaped[a]);
    if(before.sqrMagnitude<1e-18f)continue;
    faceSamples++;float cosine=UnityEngine.Vector3.Dot(before,after)/Mathf.Sqrt(before.sqrMagnitude*after.sqrMagnitude);minCos=Mathf.Min(minCos,cosine);
    if(cosine<=0){flipped++;if(flippedPoints.Count<5)flippedPoints.Add(new{i,area=Mathf.Sqrt(before.sqrMagnitude)*.5f,source=new[]{original[a].x,original[a].y,original[a].z}});}
 }
 var fins=new System.Collections.Generic.List<object>();
 foreach(var fin in renderers.Where(r=>r.name.StartsWith("Fin_"))){
    var f=fin.sharedMesh;var v=f.vertices;var fitted=BettaHeadShape.FitFin(f,fin.bones,head,neck,tail,proportion).vertices;int samples=0;float worst=0f;
    for(int i=0;i<v.Length;i++)if(groups.TryGetValue(UnityEngine.Vector3Int.RoundToInt(v[i]*100000f),out int j)){
        worst=Mathf.Max(worst,UnityEngine.Vector3.Distance(shaped[j],fitted[i]));samples++;
    }
    if(worst>.00002f)throw new Exception(fin.name+": BODY root separated by "+worst+" at head scale "+proportion);
    totalRootSamples+=samples;fins.Add(new{fin=fin.name,samples,worst});
 }
 if(flipped>0)throw new Exception("Torso faces reversed after relaxation: "+flipped);
 if(totalRootSamples<45||faceSamples<1000)throw new Exception("Insufficient real torso/root samples");
 reports.Add(new{proportion,faceSamples,totalRootSamples,flipped,minCos,fins});
}
return Newtonsoft.Json.JsonConvert.SerializeObject(reports);
