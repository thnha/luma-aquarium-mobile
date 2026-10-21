// Edit Mode: the rig must preserve the original surface at bind pose and deform it through fin/spine bones.
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/AquaFireBetta/AquaFireBetta.prefab");
var rig=prefab.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>(true);
check(rig!=null && rig.bones.Length>30,"Aqua Fire is skinned to the existing Betta skeleton");
var source=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/AquaFireBetta/Source/Meshy_AI_Aqua_Fire_Betta_1021043419_texture.fbx").GetComponent<UnityEngine.MeshFilter>().sharedMesh;
check(rig.sharedMesh.vertexCount==source.vertexCount && rig.sharedMesh.triangles.SequenceEqual(source.triangles),"Skinning preserves every original vertex and triangle");
check(rig.sharedMesh.uv.SequenceEqual(source.uv),"Original texture UVs are unchanged");
var imported=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/AquaFireBetta/Source/Meshy_AI_Aqua_Fire_Betta_1021043419_texture.fbx");
var bounds=imported.GetComponent<UnityEngine.MeshRenderer>().bounds;
var original=source.vertices.Select(v=>(imported.transform.TransformPoint(v)-bounds.center)*(.75f/bounds.size.z)).ToArray();
check(original.Zip(rig.sharedMesh.vertices,(a,b)=>UnityEngine.Vector3.Distance(a,b)).Max()<0.000001f,"The fitted skin preserves the original FBX surface and proportions");
var torso=rig.sharedMesh.vertices.Select((v,i)=>new{v,i}).Where(p=>p.v.z>.015f && p.v.z<.07f && p.v.y>-.08f && p.v.y<.02f && UnityEngine.Mathf.Abs(p.v.x)<.04f).Select(p=>p.i).ToArray();
var weights=rig.sharedMesh.boneWeights;
System.Func<int,bool> fin=i=>{string n=rig.bones[i].name;return n.StartsWith("Dorsal_") || n.StartsWith("Anal_") || n.StartsWith("Pelvic_") || n.StartsWith("Pectoral_") || n.StartsWith("Tail_") && n!="Tail_Base";};
check(torso.Length>0 && torso.All(i=>{var w=weights[i];return (!fin(w.boneIndex0) || w.weight0<.00001f) && (!fin(w.boneIndex1) || w.weight1<.00001f) && (!fin(w.boneIndex2) || w.weight2<.00001f) && (!fin(w.boneIndex3) || w.weight3<.00001f);}),"Fin movement cannot pinch the protected torso surface");
var clone=UnityEngine.Object.Instantiate(prefab);
var baked=new UnityEngine.Mesh();
try
{
    var skin=clone.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
    skin.BakeMesh(baked);
    var rest=skin.sharedMesh.vertices;
    float restError=baked.vertices.Zip(rest,(a,b)=>UnityEngine.Vector3.Distance(a,b)).Max();
    check(restError<0.00001f,"Bind pose preserves the original surface within 0.01 mm");
    check(skin.sharedMaterial.GetTexture("_BaseMap").name=="Meshy_AI_Aqua_Fire_Betta_1021043419_texture","The rig keeps the original colour texture");
    var bones=skin.bones;
    var tail=bones.First(b=>b.name=="Tail_03");
    tail.localRotation*=UnityEngine.Quaternion.Euler(0,15,0);
    skin.BakeMesh(baked);
    var moved=baked.vertices;
    check(moved.Zip(rest,(a,b)=>UnityEngine.Vector3.Distance(a,b)).Max()>0.001f,"Tail bone movement deforms the original mesh");
    check(clone.GetComponentInChildren<BettaSwimMotion>()!=null && clone.GetComponentInChildren<BettaFinPhysics>()!=null,"The imported fish reuses the existing swim and fin simulation components");
}
finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(clone);}
return new {passed=checks.Count,checks};
