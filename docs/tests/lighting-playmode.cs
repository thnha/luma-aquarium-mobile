// Run in MainAquarium or Aquarium3DPreview Play Mode using unity command eval_file.
var lighting=UnityEngine.Object.FindFirstObjectByType<AquariumLighting>();
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
if(lighting==null || !UnityEngine.Application.isPlaying)throw new System.Exception("Play Aquarium3DPreview first.");
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
int count=d.Count;
var top=lighting.Top;var lampColor=lighting.TopColor;var back=lighting.Backlight;var backColor=lighting.BackColor;
try
{
    lighting.SetTop(AquariumLighting.TopSource.Daylight);lighting.SetBacklight(false);
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
    lighting.SetLampColor(AquariumLighting.LampColor.Red);lighting.ApplyImmediate(true);
    check(lighting.Top==AquariumLighting.TopSource.Lamp && lighting.LampBlend==1,"Picking a lamp colour turns the top lamp on");
    check(lighting.sun.color.r>lighting.sun.color.b && lighting.sun.intensity>=1.3f,"Red lamp keeps its strength at night");
    check(UnityEngine.Vector3.Angle(lighting.sun.transform.forward,UnityEngine.Vector3.down)<10,"Lamp shines straight down");
    check(UnityEngine.Shader.GetGlobalFloat("_AquariumLamp")==1,"Shafts and caustics follow the lamp");
    lighting.SetLampColor(AquariumLighting.LampColor.Blue);lighting.ApplyImmediate(false);
    check(lighting.sun.color.b>lighting.sun.color.r,"Blue lamp");
    lighting.SetTop(AquariumLighting.TopSource.Daylight);lighting.ApplyImmediate(false);
    check(lighting.sun.intensity==1.35f && lighting.LampBlend==0,"Back to daylight");
    check(!lighting.BackFill.enabled && !lighting.BackPanel.enabled,"Backlight off by default");
    lighting.SetBacklight(AquariumLighting.LampColor.Blue);lighting.ApplyImmediate(false);
    check(lighting.BackFill.enabled && lighting.BackPanel.enabled && lighting.BackFill.color.b>lighting.BackFill.color.r,"Blue backlight lights the fish and the back water");
    check(UnityEngine.Shader.GetGlobalColor("_AquariumBackLightColor").b>0,"Plants and stones get the backlight rim");
    lighting.SetBacklight(false);lighting.ApplyImmediate(false);
    check(UnityEngine.Shader.GetGlobalColor("_AquariumBackLightColor").maxColorComponent==0,"Backlight off clears the rim");
    check(d.prefabs[1].footprint==.19f && d.prefabs[2].footprint==.24f,"Hardscape preserves placement footprints");
    check(UnityEngine.GameObject.Find("PebbleBed3D").GetComponent<UnityEngine.Collider>()==null,"Gravel leaves placement floor unobstructed");
}
finally
{
    // Colour setters also switch their lamp on, so restore the colours first.
    lighting.SetLampColor(lampColor);lighting.SetTop(top);
    lighting.SetBacklight(backColor);lighting.SetBacklight(back);
    lighting.ApplyImmediate(false);
}
return new {passed=checks.Count,checks};
