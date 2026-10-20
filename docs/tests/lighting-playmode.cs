// Run in MainAquarium or Aquarium3DPreview Play Mode using unity command eval_file.
var lighting=UnityEngine.Object.FindFirstObjectByType<AquariumLighting>();
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
if(lighting==null || !UnityEngine.Application.isPlaying)throw new System.Exception("Play Aquarium3DPreview first.");
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
int count=d.Count;
try
{
    lighting.ApplyImmediate(false);
    float dayIntensity=lighting.sun.intensity;
    check(lighting.NightBlend==0 && !lighting.IsNight,"Day endpoint");
    check(lighting.nightBackdrop.color.a==0,"Day backdrop visible");
    lighting.ApplyImmediate(true);
    check(lighting.NightBlend==1 && lighting.IsNight,"Night endpoint");
    check(lighting.nightBackdrop.color.a==1,"Night backdrop covers sun rays");
    check(lighting.sun.intensity<dayIntensity && lighting.sun.color.b>lighting.sun.color.r,"Cooler lower light at night");
    check(UnityEngine.Shader.GetGlobalFloat("_AquariumNight")==1 && UnityEngine.Shader.GetGlobalFloat("_AquariumLightingActive")==1,"Water and plants share lighting state");
    check(lighting.label.text=="Ban đêm","Lighting label matches night");
    lighting.label.transform.parent.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    check(!lighting.IsNight && lighting.NightBlend==1,"Button starts smooth transition");
    check(d.Count==count && !d.Editing,"Lighting preserves decorations and camera mode");
    check(d.water.surface.sharedMaterial.GetTexture("_NightEnvironmentTex")!=null && d.water.bubbles.GetComponent<UnityEngine.ParticleSystemRenderer>().sharedMaterial.GetTexture("_NightEnvironmentTex")!=null,"Water and bubbles have moonlit reflection");
    check(d.prefabs[1].footprint==.19f && d.prefabs[2].footprint==.24f,"Hardscape preserves placement footprints");
    check(UnityEngine.GameObject.Find("PebbleBed3D").GetComponent<UnityEngine.Collider>()==null,"Gravel leaves placement floor unobstructed");
}
finally {lighting.ApplyImmediate(false);}
return new {passed=checks.Count,checks};
