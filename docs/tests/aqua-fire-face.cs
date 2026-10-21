// Edit Mode. Facial vertices must move as one rigid head, even during fin and body posing.
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/AquaFireBetta/AquaFireBetta.prefab");
var source=prefab.GetComponent<UnityEngine.SkinnedMeshRenderer>();
var rest=source.sharedMesh.vertices;var weights=source.sharedMesh.boneWeights;
int headIndex=System.Array.FindIndex(source.bones,b=>b.name=="Head");
var face=Enumerable.Range(0,rest.Length).Where(i=>rest[i].z>.245f && rest[i].y>-.15f).ToArray();
var mouth=face.Where(i=>rest[i].z>.34f).ToArray();
var eyes=face.Where(i=>rest[i].z>.29f && rest[i].z<.34f && rest[i].y>-.10f && rest[i].y<-.025f && UnityEngine.Mathf.Abs(rest[i].x)>.025f).ToArray();
check(face.Length>1000 && mouth.Length>100 && eyes.Length>100,"The test covers the original front face, mouth tip and both eye regions");
check(face.All(i=>weights[i].boneIndex0==headIndex && weights[i].weight0>.99999f),"Every facial vertex follows only the head, without fin or spine stretching");
var clone=UnityEngine.Object.Instantiate(prefab);
var baked=new UnityEngine.Mesh();
float maxFaceError=0,maxMouthError=0,maxEyeError=0;int poses=0;
try
{
    foreach(var behaviour in clone.GetComponentsInChildren<UnityEngine.MonoBehaviour>())behaviour.enabled=false;
    var animator=clone.GetComponentInChildren<UnityEngine.Animator>();animator.enabled=false;
    var skin=clone.GetComponent<UnityEngine.SkinnedMeshRenderer>();
    var expected=face.ToDictionary(i=>i,i=>source.sharedMesh.bindposes[headIndex].MultiplyPoint3x4(rest[i]));
    foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct())
        foreach(float fraction in new[]{0f,.33f,.67f,.99f})
        {
            clip.SampleAnimation(animator.gameObject,clip.length*fraction);
            for(int i=0;i<skin.bones.Length;i++)
                if(skin.bones[i].name.StartsWith("Pectoral_") || skin.bones[i].name.StartsWith("Pelvic_"))
                    skin.bones[i].localRotation*=UnityEngine.Quaternion.Euler(0,20f*UnityEngine.Mathf.Sin(i+fraction*6f),0);
            skin.BakeMesh(baked);var current=baked.vertices;
            var fromHead=skin.transform.worldToLocalMatrix*skin.bones[headIndex].localToWorldMatrix;
            System.Func<int,float> error=i=>UnityEngine.Vector3.Distance(current[i],fromHead.MultiplyPoint3x4(expected[i]));
            maxFaceError=UnityEngine.Mathf.Max(maxFaceError,face.Max(error));
            maxMouthError=UnityEngine.Mathf.Max(maxMouthError,mouth.Max(error));
            maxEyeError=UnityEngine.Mathf.Max(maxEyeError,eyes.Max(error));poses++;
        }
    check(maxFaceError<.00002f,"Front face keeps its shape across all five clips and exaggerated fin poses");
    check(maxMouthError<.00002f,"Mouth surface does not stretch or jump relative to the head");
    check(maxEyeError<.00002f,"Both eye regions stay fixed relative to the head");
}
finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(clone);}
return new{passed=checks.Count,checks,poses,faceVertices=face.Length,mouthVertices=mouth.Length,eyeRegionVertices=eyes.Length,maxFaceError,maxMouthError,maxEyeError};
