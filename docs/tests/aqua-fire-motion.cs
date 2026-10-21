// Play Mode: observe real frames, then write Temp/aqua-fire-motion-check.json.
// Uses the existing fish without changing its wallet or saved selection.
if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play MainAquarium first.");
var appearance=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.FishShopScreen>().fish;
var originalVariant=appearance.Variant;
var variant=UnityEditor.AssetDatabase.LoadAssetAtPath<BettaVariant>("Assets/_Project/Data/Betta/Variants/AquaFireBetta.asset");
appearance.Apply(variant);
var skin=appearance.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().Single(r=>r.enabled && r.gameObject.activeInHierarchy);
var bind=variant.modelPrefab.GetComponent<UnityEngine.SkinnedMeshRenderer>();
var expectedPositions=bind.bones.Select(b=>b.localPosition).ToArray();
var baked=new UnityEngine.Mesh();
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
int start=UnityEngine.Time.frameCount,sample=-1;
UnityEngine.Vector3[] before=null;
var tail=skin.bones.First(b=>b.name=="Tail_03");
UnityEngine.Quaternion tailBefore=tail.localRotation;
UnityEditor.EditorApplication.CallbackFunction tick=null;
string path=System.IO.Path.Combine(UnityEngine.Application.dataPath,"../Temp/aqua-fire-motion-check.json");
if(System.IO.File.Exists(path))System.IO.File.Delete(path);
tick=()=>
{
    if(UnityEngine.Time.frameCount<start+10)return;
    if(before==null)
    {
        skin.BakeMesh(baked);before=baked.vertices;tailBefore=tail.localRotation;sample=UnityEngine.Time.frameCount;return;
    }
    if(UnityEngine.Time.frameCount<sample+40)return;
    try
    {
        skin.BakeMesh(baked);
        float deformation=before.Zip(baked.vertices,(a,b)=>UnityEngine.Vector3.Distance(a,b)).Max();
        float tailAngle=UnityEngine.Quaternion.Angle(tailBefore,tail.localRotation);
        float positionError=skin.bones.Select((b,i)=>UnityEngine.Vector3.Distance(b.localPosition,expectedPositions[i])).Max();
        check(deformation>.001f,"The skinned surface changes across actual game frames");
        check(tailAngle>.01f,"The tail ray moves while the game advances");
        check(positionError<.00001f,"Animation preserves the fitted rig positions and original proportions");
        check(appearance.GetComponentsInChildren<BettaSwimMotion>().Any(b=>b.enabled && b.gameObject.activeInHierarchy),"Existing swim motion runs on the fitted rig");
        check(appearance.GetComponentsInChildren<BettaFinPhysics>().Any(b=>b.enabled && b.gameObject.activeInHierarchy),"Existing fin simulation runs on the fitted rig");
        check(appearance.GetComponent<BettaFishAI>().animator==skin.GetComponent<BettaRiggedModel>().animator,"Aquarium AI drives the fitted rig Animator");
        check(skin.sharedMaterial.GetTexture("_BaseMap").name=="Meshy_AI_Aqua_Fire_Betta_1021043419_texture","Animation retains the original colour texture");
        System.IO.File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=checks.Count,checks,deformation,tailAngle,positionError,frames=UnityEngine.Time.frameCount-sample}));
    }
    catch(System.Exception e){System.IO.File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(new{error=e.Message,checks}));}
    finally
    {
        UnityEditor.EditorApplication.update-=tick;
        if(appearance)appearance.Apply(originalVariant);
        UnityEngine.Object.DestroyImmediate(baked);
    }
};
UnityEditor.EditorApplication.update+=tick;
return new{watching=true,result=path};
