// Play Mode. Controlled left/right 90/180 degree turns on an isolated, invisible fish.
// Writes Temp/aqua-fire-turns-check.json after all maneuvers; never changes the player's fish/save.
if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play MainAquarium first.");
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/AquaFireBetta/AquaFireBetta.prefab");
var actor=new UnityEngine.GameObject("AquaFireTurnVerification");
var ai=actor.AddComponent<BettaFishAI>();ai.enabled=false;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
typeof(BettaFishAI).GetField("currentSpeed",flags).SetValue(ai,.24f);
typeof(BettaFishAI).GetProperty("Effort").SetValue(ai,1f);
var original=prefab.GetComponent<UnityEngine.SkinnedMeshRenderer>();
var rest=original.sharedMesh.vertices;
int headIndex=System.Array.FindIndex(original.bones,b=>b.name=="Head");
var face=Enumerable.Range(0,rest.Length).Where(i=>rest[i].z>.245f && rest[i].y>-.15f).ToArray();
var headLocal=face.Select(i=>original.sharedMesh.bindposes[headIndex].MultiplyPoint3x4(rest[i])).ToArray();
var rear=Enumerable.Range(0,rest.Length).Where(i=>rest[i].z>-.075f && rest[i].z<-.055f && rest[i].y>-.075f && rest[i].y<-.02f && UnityEngine.Mathf.Abs(rest[i].x)<.025f).ToArray();
var rearRest=rear.Aggregate(UnityEngine.Vector3.zero,(sum,i)=>sum+rest[i])/rear.Length;
var rearHeadLocal=original.sharedMesh.bindposes[headIndex].MultiplyPoint3x4(rearRest);
var baked=new UnityEngine.Mesh();
var points=new System.Collections.Generic.List<UnityEngine.Vector3>(rest.Length);
string views=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../docs/previews/AquaFireFix"));
System.IO.Directory.CreateDirectory(views);
var cameraObject=new UnityEngine.GameObject("AquaFireTurnVerificationCamera");
var camera=cameraObject.AddComponent<UnityEngine.Camera>();camera.enabled=false;
camera.orthographic=true;camera.orthographicSize=.46f;camera.aspect=1.6f;camera.cullingMask=1<<31;
camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.035f,.075f,.11f);
var snapshot=new UnityEngine.GameObject("AquaFireTurnVerificationSurface",typeof(UnityEngine.MeshFilter),typeof(UnityEngine.MeshRenderer));
snapshot.layer=31;snapshot.transform.position=new UnityEngine.Vector3(1000,0,0);
snapshot.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=original.sharedMaterial;
snapshot.GetComponent<UnityEngine.MeshFilter>().sharedMesh=baked;
bool captured=false;
var triangles=original.sharedMesh.triangles;
var edges=new System.Collections.Generic.List<(int a,int b,float length)>();
// The rear dorsal membrane starts above the narrow tail-side torso. Inspect the
// central body band here; fin membranes are free to fold and are reviewed in images.
System.Func<UnityEngine.Vector3,bool> core=v=>v.z>-.08f && v.z<.245f && v.y>-.08f && v.y<-.02f && UnityEngine.Mathf.Abs(v.x)<.04f;
for(int i=0;i<triangles.Length-2;i+=111)
{
    int a=triangles[i],b=triangles[i+1];float length=UnityEngine.Vector3.Distance(rest[a],rest[b]);
    if(core(rest[a]) && core(rest[b]) && length>.0004f)edges.Add((a,b,length));
}
var results=new System.Collections.Generic.List<object>();
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
// First four are slow inspection turns; the next four use MainAquarium's
// maximum yaw speed (100 deg/s) and acceleration (220 deg/s²).
int[] turns={90,-90,180,-180,90,-90,180,-180};int maneuver=0,lastFrame=-1,lastSample=-1;
float elapsed=0,maxLag=0,maxBodyLag=0,maxFaceError=0,maxTorsoSpeed=0,maxHeadSpeed=0,minEdgeRatio=1,maxEdgeRatio=1,previousSampleTime=0;
object mostCompressed=null,mostStretched=null;
UnityEngine.Vector3 previousCenter=UnityEngine.Vector3.zero;
UnityEngine.Quaternion previousHead=UnityEngine.Quaternion.identity;
UnityEngine.GameObject model=null;
UnityEngine.SkinnedMeshRenderer skin=null;
BettaSwimMotion swim=null;
var lagField=typeof(BettaSwimMotion).GetField("jointBend",flags);
string path=System.IO.Path.Combine(UnityEngine.Application.dataPath,"../Temp/aqua-fire-turns-check.json");
if(System.IO.File.Exists(path))System.IO.File.Delete(path);
System.Action begin=()=>
{
    if(model){model.SetActive(false);UnityEngine.Object.Destroy(model);}
    actor.transform.rotation=UnityEngine.Quaternion.identity;
    model=UnityEngine.Object.Instantiate(prefab,actor.transform,false);
    skin=model.GetComponent<UnityEngine.SkinnedMeshRenderer>();skin.forceRenderingOff=true;
    model.GetComponent<BettaReactions>().enabled=false;
    swim=model.GetComponent<BettaSwimMotion>();
    var animator=model.GetComponent<BettaRiggedModel>().animator;
    animator.SetFloat("Speed",.24f);animator.Play("Swim",0,0);animator.Update(0);
    elapsed=0;maxLag=0;maxBodyLag=0;maxFaceError=0;maxTorsoSpeed=0;maxHeadSpeed=0;minEdgeRatio=1;maxEdgeRatio=1;previousSampleTime=0;lastSample=-1;
    captured=false;
    mostCompressed=null;mostStretched=null;
    previousHead=UnityEngine.Quaternion.Inverse(skin.transform.rotation)*skin.bones[headIndex].rotation;
};
begin();
UnityEditor.EditorApplication.CallbackFunction tick=null;
System.Action cleanup=()=>
{
    UnityEditor.EditorApplication.update-=tick;
    if(actor)UnityEngine.Object.Destroy(actor);
    UnityEngine.Object.DestroyImmediate(snapshot);UnityEngine.Object.DestroyImmediate(cameraObject);
    UnityEngine.Object.DestroyImmediate(baked);
};
tick=()=>
{
    if(lastFrame==UnityEngine.Time.frameCount)return;
    lastFrame=UnityEngine.Time.frameCount;
    try
    {
        if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play Mode ended before verification finished.");
        bool gameplay=maneuver>=4;
        float degrees=UnityEngine.Mathf.Abs(turns[maneuver]);
        const float maxSpeed=100f,acceleration=220f;
        float ramp=maxSpeed/acceleration;
        float duration=gameplay?degrees/maxSpeed+ramp:degrees==90?3f:5f;
        elapsed+=UnityEngine.Time.deltaTime;
        var headRotation=UnityEngine.Quaternion.Inverse(skin.transform.rotation)*skin.bones[headIndex].rotation;
        if(elapsed>.15f)maxHeadSpeed=UnityEngine.Mathf.Max(maxHeadSpeed,UnityEngine.Quaternion.Angle(previousHead,headRotation)/UnityEngine.Mathf.Max(UnityEngine.Time.deltaTime,.0001f));
        previousHead=headRotation;
        var bends=(float[])lagField.GetValue(swim);
        if(bends!=null)maxLag=UnityEngine.Mathf.Max(maxLag,bends.Max(b=>UnityEngine.Mathf.Abs(b)));
        if(lastSample<0 || lastFrame-lastSample>=6)
        {
            lastSample=lastFrame;skin.BakeMesh(baked);baked.GetVertices(points);
            var fromHead=skin.transform.worldToLocalMatrix*skin.bones[headIndex].localToWorldMatrix;
            for(int k=0;k<face.Length;k++)maxFaceError=UnityEngine.Mathf.Max(maxFaceError,UnityEngine.Vector3.Distance(points[face[k]],fromHead.MultiplyPoint3x4(headLocal[k])));
            var center=rear.Aggregate(UnityEngine.Vector3.zero,(sum,i)=>sum+points[i])/rear.Length;
            maxBodyLag=UnityEngine.Mathf.Max(maxBodyLag,UnityEngine.Vector3.Distance(center,fromHead.MultiplyPoint3x4(rearHeadLocal)));
            if(previousSampleTime>0)maxTorsoSpeed=UnityEngine.Mathf.Max(maxTorsoSpeed,UnityEngine.Vector3.Distance(center,previousCenter)/(elapsed-previousSampleTime));
            previousCenter=center;previousSampleTime=elapsed;
            foreach(var edge in edges)
            {
                float ratio=UnityEngine.Vector3.Distance(points[edge.a],points[edge.b])/edge.length;
                if(ratio<minEdgeRatio)mostCompressed=new{a=edge.a,b=edge.b,restA=rest[edge.a].ToString("F6"),restB=rest[edge.b].ToString("F6"),posedA=points[edge.a].ToString("F6"),posedB=points[edge.b].ToString("F6"),ratio};
                if(ratio>maxEdgeRatio)mostStretched=new{a=edge.a,b=edge.b,restA=rest[edge.a].ToString("F6"),restB=rest[edge.b].ToString("F6"),posedA=points[edge.a].ToString("F6"),posedB=points[edge.b].ToString("F6"),ratio};
                minEdgeRatio=UnityEngine.Mathf.Min(minEdgeRatio,ratio);maxEdgeRatio=UnityEngine.Mathf.Max(maxEdgeRatio,ratio);
            }
            if(!captured && elapsed>.6f+duration*.5f)
            {
                snapshot.transform.rotation=actor.transform.rotation;
                string name="turn-"+(turns[maneuver]>0?"right":"left")+UnityEngine.Mathf.Abs(turns[maneuver])+(gameplay?"-gameplay":"");
                Capture(name+"-quarter",false);Capture(name+"-top",true);captured=true;
            }
        }
        float seconds=UnityEngine.Mathf.Clamp(elapsed-.6f,0,duration);
        float distance=seconds<ramp?.5f*acceleration*seconds*seconds:
            seconds<duration-ramp?maxSpeed*(seconds-.5f*ramp):degrees-.5f*acceleration*(duration-seconds)*(duration-seconds);
        float yaw=gameplay?UnityEngine.Mathf.Sign(turns[maneuver])*distance:
            turns[maneuver]*UnityEngine.Mathf.SmoothStep(0,1,seconds/duration);
        actor.transform.rotation=UnityEngine.Quaternion.Euler(0,yaw,0);
        if(elapsed<duration+1.3f)return;
        string label=(turns[maneuver]>0?"right ":"left ")+UnityEngine.Mathf.Abs(turns[maneuver])+(gameplay?" at gameplay speed":"");
        results.Add(new{turn=label,maxSpineLagDegrees=maxLag,maxBodyLagMeters=maxBodyLag,maxFaceErrorMeters=maxFaceError,maxTorsoSpeed,maxHeadAngularSpeed=maxHeadSpeed,minEdgeRatio,maxEdgeRatio,sampledCoreEdges=edges.Count,mostCompressed,mostStretched});
        check(maxFaceError<.00002f,"Face remains rigid during "+label);
        check(maxLag>9f,"Spine visibly trails the head during "+label);
        check(maxBodyLag>.025f && maxBodyLag<.20f,"The actual torso bends with bounded displacement during "+label);
        check(maxTorsoSpeed<.8f && maxHeadSpeed<90f,"Head and core torso move continuously without large jumps during "+label);
        check(edges.Count>100 && minEdgeRatio>.75f && maxEdgeRatio<1.30f,"Sampled core surface edges avoid severe compression or stretching during "+label);
        maneuver++;
        if(maneuver<turns.Length){begin();return;}
        System.IO.File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=checks.Count,checks,results}));cleanup();
    }
    catch(System.Exception e)
    {
        System.IO.File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(new{error=e.Message,checks,results}));cleanup();
    }
};
UnityEditor.EditorApplication.update+=tick;
return new{watching=true,result=path};
void Capture(string name,bool top)
{
    var center=snapshot.transform.position;
    camera.transform.position=center+(top?UnityEngine.Vector3.up*2:new UnityEngine.Vector3(1.6f,1.8f,1.5f));
    camera.transform.rotation=UnityEngine.Quaternion.LookRotation(center-camera.transform.position,top?UnityEngine.Vector3.forward:UnityEngine.Vector3.up);
    var previous=UnityEngine.RenderTexture.active;
    var target=UnityEngine.RenderTexture.GetTemporary(640,400,24,UnityEngine.RenderTextureFormat.ARGB32);
    var texture=new UnityEngine.Texture2D(640,400,UnityEngine.TextureFormat.RGB24,false);
    try
    {
        camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;
        texture.ReadPixels(new UnityEngine.Rect(0,0,640,400),0,0);texture.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(views,name+".png"),texture.EncodeToPNG());
    }
    finally{camera.targetTexture=null;UnityEngine.RenderTexture.active=previous;UnityEngine.RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(texture);}
}
