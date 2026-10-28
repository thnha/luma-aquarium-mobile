using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keeps the foreground, water background and reflections on one lighting clock.
// On top of the day/night clock the tank can be lit from above by daylight or by a lamp, and from
// behind by a backlight; both lamps come in white, blue or red and blend in as smoothly as the clock.
// Day and night follow the phone's clock (night from nightFromHour to nightToHour); the Ngày/Đêm
// button overrides it until the clock next turns day or night.
public sealed class AquariumLighting : MonoBehaviour
{
    public enum TopSource { Daylight, Lamp }
    public enum LampColor { White, Blue, Red }

    public Light sun;
    public Image nightBackdrop;
    public TMP_Text label;
    // Depth tint toward the water colour (AquariumDepth.hlsl): none at fogNear, depthFog at fogFar (world z).
    public float depthFog = .22f, fogNear = 0, fogFar = 2.6f;
    // Aquarium Glow, for the soft panel of light behind the tank. Found by name when left empty.
    public Shader glowShader;
    public float lampIntensity = 2f, backlightStrength = .75f;
    public string storageKey = "Lumaquarium.Lighting.v1";
    public bool persist = true;
    public bool followClock = true;
    [Range(0, 23)] public int nightFromHour = 21;
    [Range(0, 23)] public int nightToHour = 6;
    public bool IsNight { get; private set; }
    public float NightBlend { get; private set; }
    public TopSource Top { get; private set; }
    public LampColor TopColor { get; private set; }
    public bool Backlight { get; private set; }
    public LampColor BackColor { get; private set; }
    public float LampBlend { get; private set; }
    public float BackBlend { get; private set; }
    public Color LampNow { get; private set; } = Color.white;
    public Color BackNow { get; private set; } = Color.white;
    public Light BackFill => backFill;
    public Renderer BackPanel => backPanel;
    private Color oldAmbient, oldLightColor, oldShaderAmbient;
    private float oldIntensity, oldShadow, oldReflection, oldActive, oldNight;
    private Quaternion oldRotation;
    private Camera viewCamera;
    private Color oldBackground;
    private Light backFill;
    private MeshRenderer backPanel;
    private Material panelMaterial;
    private bool clockNight;
    private float nextClockCheck;

    public static Color ColorOf(LampColor color) => color switch
    {
        LampColor.Blue => new Color(.32f, .58f, 1),
        LampColor.Red => new Color(1, .3f, .26f),
        _ => new Color(.96f, .98f, 1)
    };

    private void OnEnable()
    {
        oldAmbient = RenderSettings.ambientLight;
        oldReflection = RenderSettings.reflectionIntensity;
        oldActive = Shader.GetGlobalFloat("_AquariumLightingActive");
        oldNight = Shader.GetGlobalFloat("_AquariumNight");
        oldShaderAmbient = Shader.GetGlobalColor("_AquariumAmbientColor");
        var decoration = GetComponent<AquariumDecoration>();
        viewCamera = decoration?.viewCamera;
        if (viewCamera != null) oldBackground = viewCamera.backgroundColor;
        if (sun != null) { oldLightColor = sun.color; oldIntensity = sun.intensity; oldShadow = sun.shadowStrength; oldRotation = sun.transform.rotation; }
        BuildBacklight(decoration);
        Load();
        clockNight = ClockSaysNight();
        ApplyImmediate(followClock && clockNight);
    }

    public void Toggle() { IsNight = !IsNight; UpdateLabel(); }

    // Night from nightFromHour until nightToHour, wrapping past midnight (21 to 6 by default).
    public bool ClockSaysNight()
    {
        int hour = System.DateTime.Now.Hour;
        return nightFromHour > nightToHour ? hour >= nightFromHour || hour < nightToHour
            : hour >= nightFromHour && hour < nightToHour;
    }

    // When the clock turns day or night, the tank follows it again, whatever the button was set to.
    private void FollowClock()
    {
        if (!followClock || Time.unscaledTime < nextClockCheck) return;
        nextClockCheck = Time.unscaledTime + 5f;
        bool night = ClockSaysNight();
        if (night == clockNight) return;
        clockNight = night;
        IsNight = night;
        UpdateLabel();
    }
    public void ApplyImmediate(bool night)
    {
        IsNight = night;
        NightBlend = night ? 1 : 0;
        LampBlend = Top == TopSource.Lamp ? 1 : 0;
        BackBlend = Backlight ? 1 : 0;
        LampNow = ColorOf(TopColor);
        BackNow = ColorOf(BackColor);
        UpdateLabel();
        Apply();
    }

    public void SetTop(TopSource source) { Top = source; Save(); }
    // Picking a lamp colour switches the top light to the lamp, as a real lamp would have to be on.
    public void SetLampColor(LampColor color) { TopColor = color; Top = TopSource.Lamp; Save(); }
    public void SetBacklight(bool on) { Backlight = on; Save(); }
    public void SetBacklight(LampColor color) { BackColor = color; Backlight = true; Save(); }

    private void UpdateLabel() { if (label != null) label.text = Lumaquarium.Loc.T(IsNight ? "Ban đêm" : "Ban ngày"); }
    private void Update()
    {
        FollowClock();
        float step = Time.unscaledDeltaTime / 1.5f;
        NightBlend = Mathf.MoveTowards(NightBlend, IsNight ? 1 : 0, step);
        LampBlend = Mathf.MoveTowards(LampBlend, Top == TopSource.Lamp ? 1 : 0, step);
        BackBlend = Mathf.MoveTowards(BackBlend, Backlight ? 1 : 0, step);
        LampNow = Vector4.MoveTowards(LampNow, ColorOf(TopColor), step);
        BackNow = Vector4.MoveTowards(BackNow, ColorOf(BackColor), step);
        Apply();
    }
    private void Apply()
    {
        float t = Mathf.SmoothStep(0, 1, NightBlend);
        float lamp = Mathf.SmoothStep(0, 1, LampBlend);
        float back = Mathf.SmoothStep(0, 1, BackBlend);
        Color ambient = Color.Lerp(new Color(.40f,.46f,.44f), new Color(.10f,.14f,.22f), t);
        // The lamp's colour fills the shadows a little; the tank stays readable under red or blue.
        ambient = Color.Lerp(ambient, ambient * .7f + LampNow * .1f, lamp);
        RenderSettings.ambientLight = ambient;
        RenderSettings.reflectionIntensity = Mathf.Lerp(.75f,.24f,t);
        Shader.SetGlobalFloat("_AquariumLightingActive",1);
        Shader.SetGlobalFloat("_AquariumNight",t);
        Shader.SetGlobalColor("_AquariumAmbientColor",ambient);
        Shader.SetGlobalFloat("_AquariumLamp",lamp);
        Shader.SetGlobalColor("_AquariumLampColor",LampNow);
        Shader.SetGlobalColor("_AquariumBackLightColor",BackNow * (back * backlightStrength));
        // Clear, bright water by day; the deep blue of a dark room at night.
        Color water = Color.Lerp(new Color(.13f,.45f,.52f), new Color(.03f,.09f,.17f), t);
        water = Color.Lerp(water, water * .8f + LampNow * .05f, lamp);
        // A backlight tints the water only faintly: the water colour is also the depth tint, and more
        // hazed every plant and stone at the back in the lamp colour. The panel itself carries the glow.
        water += BackNow * (.04f * back);
        water.a = 1;
        if (viewCamera != null) viewCamera.backgroundColor = water;
        // The camera converts its background to linear but a global colour reaches the shader as given:
        // passing it in linear keeps the far sand fading into the same colour as the water behind it.
        Shader.SetGlobalColor("_AquariumWaterColor", water.linear);
        // A lit back wall shows through the water more clearly.
        Shader.SetGlobalFloat("_AquariumFogAmount", depthFog * (1 - .5f * back));
        Shader.SetGlobalFloat("_AquariumFogNear", fogNear);
        Shader.SetGlobalFloat("_AquariumFogFar", fogFar);
        if (sun != null)
        {
            // Daylight slants in and follows the clock; the lamp hangs over the tank and keeps its strength at night.
            Color daylight = Color.Lerp(new Color(1,.95f,.83f), new Color(.46f,.66f,1),t);
            sun.color = Color.Lerp(daylight, LampNow, lamp);
            sun.intensity = Mathf.Lerp(Mathf.Lerp(1.7f,.75f,t), lampIntensity, lamp);
            var daylightRotation = Quaternion.Slerp(Quaternion.Euler(52,-28,0),Quaternion.Euler(65,32,0),t);
            sun.transform.rotation = Quaternion.Slerp(daylightRotation, Quaternion.Euler(84,-12,0), lamp);
            // The backlight fills the shadows the top light casts on the back wall and the sand.
            sun.shadowStrength = oldShadow * (1 - .65f * back);
        }
        if (backFill != null)
        {
            backFill.color = BackNow;
            backFill.intensity = 2.2f * back;
            backFill.enabled = back > .001f;
        }
        if (backPanel != null)
        {
            // Red lights the stone dimmer than white or blue to the eye, so it gets a little more.
            float gain = 1 + .2f * Mathf.Clamp01(BackNow.r - BackNow.b);
            panelMaterial.SetColor("_Color", new Color(BackNow.r * gain, BackNow.g * gain, BackNow.b * gain, .55f * back));
            backPanel.enabled = back > .001f;
        }
        if (nightBackdrop != null) nightBackdrop.color = new Color(1,1,1,t);
    }

    // Another tank size moves the depth tint and the backlight to its back glass (AquariumTank).
    public void Retank()
    {
        var decoration = GetComponent<AquariumDecoration>();
        if (decoration == null) return;
        // As far past the back glass as the first tank's tint reached past its own.
        fogFar = decoration.max.y + 1.45f;
        ClearBacklight();
        BuildBacklight(decoration);
        Apply();
    }

    // A point light behind the tank (the fish shader takes additional lights) and a soft panel of light
    // on the back glass. The plant, wood and stone shaders read the same colour as a rim (AquariumDepth.hlsl).
    private void BuildBacklight(AquariumDecoration decoration)
    {
        if (decoration == null || backFill != null) return;
        float top = decoration.water != null ? decoration.water.surfaceY : decoration.floorY + 2.1f;
        float centre = (decoration.min.x + decoration.max.x) * .5f;
        var fill = new GameObject("BacklightFill") { hideFlags = HideFlags.DontSave };
        fill.transform.SetParent(transform, false);
        fill.transform.position = new Vector3(centre, decoration.floorY + .9f, decoration.max.y + .9f);
        backFill = fill.AddComponent<Light>();
        backFill.type = LightType.Point;
        // Reaches the front glass of a deep tank too.
        backFill.range = Mathf.Max(3.2f, decoration.max.y - decoration.min.y + 1.6f);
        backFill.shadows = LightShadows.None;
        backFill.enabled = false;

        var shader = glowShader != null ? glowShader : Shader.Find("Lumaquarium/Aquarium Glow");
        if (shader == null) return;
        panelMaterial = new Material(shader) { name = "BacklightPanel", hideFlags = HideFlags.DontSave };
        panelMaterial.SetFloat("_Round", 2);
        // Lights the stone (wall * (1 + lamp)) rather than adding colour over it, which washed it out (Aquarium Glow).
        panelMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor);
        panelMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        var panel = new GameObject("BacklightPanel", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.DontSave };
        panel.transform.SetParent(transform, false);
        // Just in front of the back wall (AquariumTank), across the whole glass.
        float z = decoration.max.y + .03f, width = decoration.max.x - decoration.min.x + .1f;
        panel.transform.position = new Vector3(centre, decoration.floorY - .1f, z);
        panel.transform.rotation = Quaternion.identity;
        panel.GetComponent<MeshFilter>().sharedMesh = Quad(width, top - decoration.floorY + .3f);
        backPanel = panel.GetComponent<MeshRenderer>();
        backPanel.sharedMaterial = panelMaterial;
        backPanel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        backPanel.receiveShadows = false;
        backPanel.enabled = false;
    }

    // Stands on its pivot: uv.y is 0 at the sand, where the glow is brightest, and 1 at the surface.
    private static Mesh Quad(float width, float height)
    {
        var mesh = new Mesh { name = "BacklightPanel", hideFlags = HideFlags.DontSave };
        mesh.vertices = new[] { new Vector3(-width / 2, 0, 0), new Vector3(width / 2, 0, 0), new Vector3(-width / 2, height, 0), new Vector3(width / 2, height, 0) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void Load()
    {
        if (!persist || !PlayerPrefs.HasKey(storageKey)) return;
        // Packed as top, lamp colour, backlight on, backlight colour: one digit each.
        string value = PlayerPrefs.GetString(storageKey);
        if (value.Length < 4) return;
        Top = value[0] == '1' ? TopSource.Lamp : TopSource.Daylight;
        TopColor = (LampColor)Mathf.Clamp(value[1] - '0', 0, 2);
        Backlight = value[2] == '1';
        BackColor = (LampColor)Mathf.Clamp(value[3] - '0', 0, 2);
    }

    private void Save()
    {
        if (Lumaquarium.Account.AccountSignIn.ProgressBlocked) return;
        if (!persist) return;
        PlayerPrefs.SetString(storageKey, $"{(int)Top}{(int)TopColor}{(Backlight ? 1 : 0)}{(int)BackColor}");
        PlayerPrefs.Save();
    }

    private void OnDisable()
    {
        RenderSettings.ambientLight = oldAmbient;
        RenderSettings.reflectionIntensity = oldReflection;
        Shader.SetGlobalFloat("_AquariumLightingActive",oldActive);
        Shader.SetGlobalFloat("_AquariumNight",oldNight);
        Shader.SetGlobalColor("_AquariumAmbientColor",oldShaderAmbient);
        Shader.SetGlobalFloat("_AquariumFogAmount",0);
        Shader.SetGlobalFloat("_AquariumLamp",0);
        Shader.SetGlobalColor("_AquariumBackLightColor",Color.clear);
        if (viewCamera != null) viewCamera.backgroundColor = oldBackground;
        if (sun != null) { sun.color = oldLightColor; sun.intensity = oldIntensity; sun.shadowStrength = oldShadow; sun.transform.rotation = oldRotation; }
        if (nightBackdrop != null) nightBackdrop.color = new Color(1,1,1,0);
        ClearBacklight();
    }

    private void ClearBacklight()
    {
        if (backFill != null) Destroy(backFill.gameObject);
        if (backPanel != null)
        {
            Destroy(backPanel.GetComponent<MeshFilter>().sharedMesh);
            Destroy(backPanel.gameObject);
        }
        if (panelMaterial != null) Destroy(panelMaterial);
        backFill = null;
        backPanel = null;
        panelMaterial = null;
    }
}
