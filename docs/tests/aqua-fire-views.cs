// Render front close-ups for all animation states and whole-fish quarter views.
// Uses CPU-baked poses so screenshots cannot accidentally show a stale GPU skin pose.
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/AquaFireBetta/AquaFireBetta.prefab");
var clone=UnityEngine.Object.Instantiate(prefab);
foreach(var behaviour in clone.GetComponentsInChildren<UnityEngine.MonoBehaviour>())behaviour.enabled=false;
var animator=clone.GetComponentInChildren<UnityEngine.Animator>();animator.enabled=false;
var skin=clone.GetComponent<UnityEngine.SkinnedMeshRenderer>();skin.forceRenderingOff=true;
var cameraObject=new UnityEngine.GameObject("AquaFireFrontVerificationCamera");
var camera=cameraObject.AddComponent<UnityEngine.Camera>();camera.enabled=false;
camera.orthographic=true;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
camera.backgroundColor=new UnityEngine.Color(.035f,.075f,.11f);camera.cullingMask=1<<31;
var snapshot=new UnityEngine.GameObject("AquaFireVerificationSurface",typeof(UnityEngine.MeshFilter),typeof(UnityEngine.MeshRenderer));
snapshot.layer=31;snapshot.transform.position=new UnityEngine.Vector3(1000,0,0);
snapshot.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=skin.sharedMaterial;
var baked=new UnityEngine.Mesh();
var output=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../docs/previews/AquaFireFix"));
System.IO.Directory.CreateDirectory(output);
var paths=new System.Collections.Generic.List<string>();
var previous=UnityEngine.RenderTexture.active;
try
{
    snapshot.GetComponent<UnityEngine.MeshFilter>().sharedMesh=skin.sharedMesh;
    Capture("front-original",true);
    foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct())
    {
        string name=clip.name.Replace("AquaFire_","");
        foreach(float fraction in new[]{0f,.33f,.67f})
        {
            clip.SampleAnimation(animator.gameObject,clip.length*fraction);
            skin.BakeMesh(baked);
            snapshot.GetComponent<UnityEngine.MeshFilter>().sharedMesh=baked;
            Capture("front-"+name+"-"+UnityEngine.Mathf.RoundToInt(fraction*100),true);
        }
        Capture("quarter-"+name,false);
    }
}
finally
{
    UnityEngine.RenderTexture.active=previous;
    UnityEngine.Object.DestroyImmediate(snapshot);UnityEngine.Object.DestroyImmediate(cameraObject);
    UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(clone);
}
return new{rendered=paths.Count,output,paths};
void Capture(string name,bool front)
{
    int width=front?400:640,height=400;
    camera.aspect=width/(float)height;camera.orthographicSize=front?.14f:.39f;
    var center=snapshot.transform.position+(front?new UnityEngine.Vector3(0,-.065f,.285f):UnityEngine.Vector3.zero);
    camera.transform.position=center+(front?UnityEngine.Vector3.forward*2:new UnityEngine.Vector3(2,.15f,1.5f));
    camera.transform.LookAt(center);
    var target=UnityEngine.RenderTexture.GetTemporary(width,height,24,UnityEngine.RenderTextureFormat.ARGB32);
    var texture=new UnityEngine.Texture2D(width,height,UnityEngine.TextureFormat.RGB24,false);
    try
    {
        camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;
        texture.ReadPixels(new UnityEngine.Rect(0,0,width,height),0,0);texture.Apply();
        string path=System.IO.Path.Combine(output,name+".png");System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());paths.Add(path);
    }
    finally{camera.targetTexture=null;UnityEngine.RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(texture);}
}
