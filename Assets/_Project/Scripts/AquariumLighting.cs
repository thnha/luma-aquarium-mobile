using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keeps the foreground, water background and reflections on one lighting clock.
public sealed class AquariumLighting : MonoBehaviour
{
    public Light sun;
    public Image nightBackdrop;
    public TMP_Text label;
    // Depth tint toward the water colour (AquariumDepth.hlsl): none at fogNear, depthFog at fogFar (world z).
    public float depthFog = .5f, fogNear = 0, fogFar = 2.6f;
    public bool IsNight { get; private set; }
    public float NightBlend { get; private set; }
    private Color oldAmbient, oldLightColor, oldShaderAmbient;
    private float oldIntensity, oldReflection, oldActive, oldNight;
    private Quaternion oldRotation;
    private Camera viewCamera;
    private Color oldBackground;

    private void OnEnable()
    {
        oldAmbient = RenderSettings.ambientLight;
        oldReflection = RenderSettings.reflectionIntensity;
        oldActive = Shader.GetGlobalFloat("_AquariumLightingActive");
        oldNight = Shader.GetGlobalFloat("_AquariumNight");
        oldShaderAmbient = Shader.GetGlobalColor("_AquariumAmbientColor");
        viewCamera = GetComponent<AquariumDecoration>()?.viewCamera;
        if (viewCamera != null) oldBackground = viewCamera.backgroundColor;
        if (sun != null) { oldLightColor = sun.color; oldIntensity = sun.intensity; oldRotation = sun.transform.rotation; }
        ApplyImmediate(false);
    }

    public void Toggle() { IsNight = !IsNight; UpdateLabel(); }
    public void ApplyImmediate(bool night) { IsNight = night; NightBlend = night ? 1 : 0; UpdateLabel(); Apply(); }
    private void UpdateLabel() { if (label != null) label.text = IsNight ? "Ban đêm" : "Ban ngày"; }
    private void Update()
    {
        NightBlend = Mathf.MoveTowards(NightBlend, IsNight ? 1 : 0, Time.unscaledDeltaTime / 1.5f);
        Apply();
    }
    private void Apply()
    {
        float t = Mathf.SmoothStep(0, 1, NightBlend);
        Color ambient = Color.Lerp(new Color(.29f,.36f,.29f), new Color(.065f,.10f,.17f), t);
        RenderSettings.ambientLight = ambient;
        RenderSettings.reflectionIntensity = Mathf.Lerp(.75f,.24f,t);
        Shader.SetGlobalFloat("_AquariumLightingActive",1);
        Shader.SetGlobalFloat("_AquariumNight",t);
        Shader.SetGlobalColor("_AquariumAmbientColor",ambient);
        Color water = Color.Lerp(new Color(.045f,.24f,.29f), new Color(.018f,.055f,.11f), t);
        if (viewCamera != null) viewCamera.backgroundColor = water;
        Shader.SetGlobalColor("_AquariumWaterColor", water);
        Shader.SetGlobalFloat("_AquariumFogAmount", depthFog);
        Shader.SetGlobalFloat("_AquariumFogNear", fogNear);
        Shader.SetGlobalFloat("_AquariumFogFar", fogFar);
        if (sun != null)
        {
            sun.color = Color.Lerp(new Color(1,.95f,.83f), new Color(.46f,.66f,1),t);
            sun.intensity = Mathf.Lerp(1.35f,.62f,t);
            sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(52,-28,0),Quaternion.Euler(65,32,0),t);
        }
        if (nightBackdrop != null) nightBackdrop.color = new Color(1,1,1,t);
    }
    private void OnDisable()
    {
        RenderSettings.ambientLight = oldAmbient;
        RenderSettings.reflectionIntensity = oldReflection;
        Shader.SetGlobalFloat("_AquariumLightingActive",oldActive);
        Shader.SetGlobalFloat("_AquariumNight",oldNight);
        Shader.SetGlobalColor("_AquariumAmbientColor",oldShaderAmbient);
        Shader.SetGlobalFloat("_AquariumFogAmount",0);
        if (viewCamera != null) viewCamera.backgroundColor = oldBackground;
        if (sun != null) { sun.color = oldLightColor; sun.intensity = oldIntensity; sun.transform.rotation = oldRotation; }
        if (nightBackdrop != null) nightBackdrop.color = new Color(1,1,1,0);
    }
}
