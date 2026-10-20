using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Builds the M1 MainAquarium scene from the starter README layout.
// Menu: Lumaquarium > Build Main Aquarium Scene (safe to re-run; overwrites the scene).
public static class AquariumSceneBuilder
{
    const string Root = "Assets/_Project";
    const string ScenePath = Root + "/Scenes/MainAquarium.unity";
    const string MaterialDir = Root + "/Materials";
    const string PrefabDir = Root + "/Prefabs";

    const float TankWidth = 1.6f, TankHeight = 2.2f, TankDepth = 1.0f, FloorY = 0.25f;

    [MenuItem("Lumaquarium/Build Main Aquarium Scene")]
    public static void Build()
    {
        EnsureFolder(Root, "Scenes");
        EnsureFolder(Root, "Materials");
        EnsureFolder(Root, "Prefabs");
        ConfigurePlayer();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.color = new Color(0.92f, 0.97f, 1f);
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.36f, 0.42f);

        new GameObject("GameManager").AddComponent<MobileSettings>();

        // Aquarium
        var aquarium = new GameObject("AquariumRoot").transform;
        Material glass = TransparentMaterial("TankGlass", new Color(0.8f, 0.95f, 1f, 0.12f), 0.95f);
        Material water = TransparentMaterial("Water", new Color(0.35f, 0.65f, 0.75f, 0.1f), 0.6f);
        Material sand = OpaqueMaterial("Substrate", new Color(0.78f, 0.7f, 0.55f), 0.1f);
        Material rock = OpaqueMaterial("Rock", new Color(0.35f, 0.34f, 0.32f), 0.2f);
        Material plant = OpaqueMaterial("Plant", new Color(0.18f, 0.45f, 0.2f), 0.35f);

        // Portrait tank: 1.6 wide x 2.2 tall x 1.0 deep (inner), floor glass at y = FloorY.
        const float W = TankWidth, H = TankHeight, D = TankDepth, g = 0.02f;
        float midY = FloorY + H * 0.5f;
        var tank = new GameObject("TankGlass").transform;
        tank.SetParent(aquarium);
        Pane("Back", tank, new Vector3(0, midY, D * 0.5f + g), new Vector3(W + 2 * g, H, g), glass);
        Pane("Front", tank, new Vector3(0, midY, -D * 0.5f - g), new Vector3(W + 2 * g, H, g), glass);
        Pane("Left", tank, new Vector3(-W * 0.5f - g, midY, 0), new Vector3(g, H, D + 2 * g), glass);
        Pane("Right", tank, new Vector3(W * 0.5f + g, midY, 0), new Vector3(g, H, D + 2 * g), glass);
        Pane("Bottom", tank, new Vector3(0, FloorY - g * 0.5f, 0), new Vector3(W + 4 * g, g, D + 4 * g), glass);

        float waterTop = FloorY + H - 0.1f;
        Box("WaterVolume", aquarium, new Vector3(0, (FloorY + waterTop) * 0.5f, 0), new Vector3(W - 0.02f, waterTop - FloorY, D - 0.02f), water);
        Box("Substrate", aquarium, new Vector3(0, FloorY + 0.06f, 0), new Vector3(W - 0.02f, 0.12f, D - 0.02f), sand);

        var rocks = new GameObject("Rocks").transform;
        rocks.SetParent(aquarium);
        Sphere("Rock_A", rocks, new Vector3(-0.42f, FloorY + 0.16f, 0.18f), new Vector3(0.42f, 0.26f, 0.34f), rock);
        Sphere("Rock_B", rocks, new Vector3(0.45f, FloorY + 0.14f, 0.25f), new Vector3(0.28f, 0.2f, 0.26f), rock);

        var plants = new GameObject("Plants").transform;
        plants.SetParent(aquarium);
        for (int i = 0; i < 4; i++)
        {
            float x = -0.6f + i * 0.4f + (i % 2) * 0.06f;
            float h = 0.7f + (i % 3) * 0.35f;
            var stem = Capsule("Plant_" + i, plants, new Vector3(x, FloorY + 0.1f + h * 0.5f, 0.38f), new Vector3(0.06f, h * 0.5f, 0.06f), plant);
            stem.transform.rotation = Quaternion.Euler(0, 0, (i - 1.5f) * 4f);
        }

        // Fish swims between substrate and water surface, inside the glass.
        float swimMin = FloorY + 0.05f, swimMax = waterTop - 0.1f;
        var bounds = new GameObject("SwimBounds");
        bounds.transform.SetParent(aquarium);
        bounds.transform.position = new Vector3(0, (swimMin + swimMax) * 0.5f, 0);
        bounds.transform.localScale = new Vector3(W - 0.1f, swimMax - swimMin, D);
        var boundsCollider = bounds.AddComponent<BoxCollider>();
        boundsCollider.isTrigger = true;

        var spawn = new GameObject("FoodSpawnPoint").transform;
        spawn.SetParent(aquarium);
        spawn.position = new Vector3(0, waterTop - 0.05f, 0);

        // Fish: rigged Betta prefab, or a capsule placeholder if the FBX isn't imported.
        var bettaRoot = new GameObject("BettaRoot");
        bettaRoot.transform.position = new Vector3(0, midY, 0);
        Animator fishAnimator = null;
        var bettaPrefab = BettaPrefabBuilder.Build();
        if (bettaPrefab)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(bettaPrefab, bettaRoot.transform);
            model.name = "FishModel";
            fishAnimator = model.GetComponent<Animator>();
        }
        else
        {
            var fishModel = new GameObject("FishModel").transform;
            fishModel.SetParent(bettaRoot.transform, false);
            Material fishMat = OpaqueMaterial("FishPlaceholder", new Color(0.05f, 0.25f, 0.65f), 0.7f);
            var body = Capsule("Body", fishModel, Vector3.zero, new Vector3(0.09f, 0.12f, 0.09f), fishMat);
            body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Sphere("Nose", fishModel, new Vector3(0, 0.02f, 0.11f), Vector3.one * 0.05f, OpaqueMaterial("FishEye", Color.black, 0.9f));
        }
        var fish = bettaRoot.AddComponent<BettaFishAI>();
        fish.swimBounds = boundsCollider;
        fish.animator = fishAnimator;
        if (fishAnimator)
        {
            var swimMotion = bettaRoot.AddComponent<BettaSwimMotion>();
            swimMotion.fish = fish;
            swimMotion.skeletonRoot = fishAnimator.transform;
        }

        // Feeding
        var feeding = new GameObject("FeedingManager").AddComponent<FeedingSystem>();
        feeding.fish = fish;
        feeding.spawnPoint = spawn;
        feeding.pelletPrefab = PelletPrefab();
        fish.feeding = feeding;

        // Camera
        var cameraTarget = new GameObject("CameraTarget").transform;
        cameraTarget.position = new Vector3(0, midY, 0);
        var cam = new GameObject("MainCamera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.09f, 0.12f);
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.gameObject.AddComponent<AudioListener>();
        var orbit = cam.gameObject.AddComponent<OrbitTouchCamera>();
        orbit.target = cameraTarget;
        orbit.fitHalfWidth = W * 0.5f + 0.05f;
        cam.transform.position = cameraTarget.position + Quaternion.Euler(orbit.pitch, 0, 0) * Vector3.back * orbit.distance;
        cam.transform.LookAt(cameraTarget);

        BuildUI(fish, feeding);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Built " + ScenePath);
    }

    static void BuildUI(BettaFishAI fish, FeedingSystem feeding)
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        var safe = UIRect("SafeAreaPanel", canvasGo.transform);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();

        // Header: fish name + hunger slider
        var header = UIRect("Header", safe);
        header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1);
        header.pivot = new Vector2(0.5f, 1); header.sizeDelta = new Vector2(-64, 160); header.anchoredPosition = new Vector2(0, -32);
        Label("FishName", header, "Betta", 56, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), 80);

        var slider = DefaultControls.CreateSlider(new DefaultControls.Resources()).GetComponent<Slider>();
        slider.name = "HungerSlider";
        var sr = slider.GetComponent<RectTransform>();
        sr.SetParent(header, false);
        sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(1, 0); sr.pivot = new Vector2(0.5f, 0);
        sr.sizeDelta = new Vector2(0, 40); sr.anchoredPosition = new Vector2(0, 10);
        slider.minValue = 0; slider.maxValue = 1; slider.interactable = false;
        Object.DestroyImmediate(slider.handleRect.gameObject);
        var status = canvasGo.AddComponent<FishStatusUI>();
        status.fish = fish; status.hungerSlider = slider;

        // Bottom bar: Feed button (Care / Decorate / Photo come in M4)
        var bar = UIRect("BottomBar", safe);
        bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0);
        bar.pivot = new Vector2(0.5f, 0); bar.sizeDelta = new Vector2(-64, 180); bar.anchoredPosition = new Vector2(0, 32);
        var button = DefaultControls.CreateButton(new DefaultControls.Resources()).GetComponent<Button>();
        button.name = "FeedButton";
        var br = button.GetComponent<RectTransform>();
        br.SetParent(bar, false);
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f); br.sizeDelta = new Vector2(320, 140);
        var text = button.GetComponentInChildren<Text>();
        text.text = "Feed"; text.fontSize = 52;
        UnityEventTools.AddPersistentListener(button.onClick, feeding.Feed);

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.transform.SetAsLastSibling();
    }

    static FoodPellet PelletPrefab()
    {
        string path = PrefabDir + "/FoodPellet.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<FoodPellet>(path);
        if (existing) return existing;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "FoodPellet";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * 0.035f;
        go.GetComponent<MeshRenderer>().sharedMaterial = OpaqueMaterial("FoodPellet", new Color(0.55f, 0.3f, 0.12f), 0.2f);
        go.AddComponent<FoodPellet>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path).GetComponent<FoodPellet>();
        Object.DestroyImmediate(go);
        return prefab;
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.productName = "Betta Aquarium 3D";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
    }

    // ---- helpers ----
    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", "_Project");
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static Material OpaqueMaterial(string name, Color color, float smoothness)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material TransparentMaterial(string name, Color color, float smoothness)
    {
        var mat = OpaqueMaterial(name, color, smoothness);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = mat.renderQueue >= (int)RenderQueue.Transparent ? ShadowCastingMode.Off : ShadowCastingMode.On;
        return go;
    }

    static GameObject Box(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Primitive(PrimitiveType.Cube, n, p, pos, s, m);
    static GameObject Pane(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Box(n, p, pos, s, m);
    static GameObject Sphere(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Primitive(PrimitiveType.Sphere, n, p, pos, s, m);
    static GameObject Capsule(string n, Transform p, Vector3 pos, Vector3 s, Material m) => Primitive(PrimitiveType.Capsule, n, p, pos, s, m);

    static RectTransform UIRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Label(string name, Transform parent, string value, int size, Vector2 aMin, Vector2 aMax, Vector2 pos, float height)
    {
        var rt = UIRect(name, parent);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0, height); rt.anchoredPosition = pos;
        var t = rt.gameObject.AddComponent<Text>();
        t.text = value; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
