// All source meshes, including membrane roots: sculpting must never fold space back.
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var renderers=prefab.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true);
var body=renderers.First(r=>r.name=="BODY");var source=body.sharedMesh;
int hi=Array.FindIndex(body.bones,b=>b.name=="Head"),ti=Array.FindIndex(body.bones,b=>b.name=="Tail_Base");
var head=source.bindposes[hi].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
var tail=source.bindposes[ti].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
float e=.00002f,minDet=float.PositiveInfinity;int samples=0;
foreach(float p in new[]{.8f,1f,1.3f})
{
    var profile=new BettaBodyProfile(head,tail,p);
    foreach(var renderer in renderers.Where(r=>r.name=="BODY"||r.name.StartsWith("Fin_")))
        foreach(var point in renderer.sharedMesh.vertices)
        {
            var x=(profile.Map(point+UnityEngine.Vector3.right*e)-profile.Map(point-UnityEngine.Vector3.right*e))/(2*e);
            var y=(profile.Map(point+UnityEngine.Vector3.up*e)-profile.Map(point-UnityEngine.Vector3.up*e))/(2*e);
            var z=(profile.Map(point+UnityEngine.Vector3.forward*e)-profile.Map(point-UnityEngine.Vector3.forward*e))/(2*e);
            float determinant=UnityEngine.Vector3.Dot(x,UnityEngine.Vector3.Cross(y,z));
            if(float.IsNaN(determinant)||determinant<=.005f)throw new Exception(renderer.name+" folded/singular sculpt at "+point+", head="+p+", determinant="+determinant);
            minDet=UnityEngine.Mathf.Min(minDet,determinant);samples++;
        }
}
return new{samples,minDet};
