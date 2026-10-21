using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the Betta material, Animator controller and prefab from Art/Betta/Betta.fbx.
// Menu: Lumaquarium > Build Betta Prefab (safe to re-run).
public static class BettaPrefabBuilder
{
    const string ArtDir = "Assets/_Project/Art/Betta";
    const string FbxPath = ArtDir + "/Betta.fbx";
    public const string PrefabPath = "Assets/_Project/Prefabs/Betta.prefab";

    [MenuItem("Lumaquarium/Build Betta Prefab")]
    public static GameObject Build()
    {
        // Bake first: it may reimport the FBX, which would invalidate a model loaded before it.
        Texture2D variantMask = BettaMaskBaker.LoadOrBake();
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (!fbx) { Debug.LogError("[Lumaquarium] Missing " + FbxPath); return null; }

        Material mat = FishMaterial(variantMask);
        AnimatorController controller = Controller();

        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "Betta";
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            r.sharedMaterial = mat;
            r.updateWhenOffscreen = false;
            // Betta.fbx carries every tail form's Fin_* meshes; default to Halfmoon until
            // BettaAppearance applies a BettaTailType's finMeshes.
            if (r.name.StartsWith("Fin_")) r.enabled = r.name.EndsWith("_Halfmoon");
        }
        if (!go.TryGetComponent(out Animator animator)) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        Debug.Log("[Lumaquarium] Built " + PrefabPath);
        return prefab;
    }

    static Material FishMaterial(Texture2D variantMask)
    {
        string path = ArtDir + "/Betta.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        // Betta Lit = URP Lit that also lights back faces correctly (see Shaders/BettaLit.shader).
        var shader = Shader.Find("Lumaquarium/Betta Lit");
        if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", Tex("BETTA_BaseColor_Alpha"));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_MetallicGlossMap", Tex("BETTA_MetallicSmoothness"));
        mat.SetFloat("_Smoothness", 1f);
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetTexture("_BumpMap", Tex("BETTA_Normal_OpenGL"));
        mat.SetFloat("_BumpScale", 0.22f);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_OcclusionMap", Tex("BETTA_Mixed_AO"));
        mat.SetFloat("_OcclusionStrength", 0.35f);
        mat.EnableKeyword("_OCCLUSIONMAP");
        // Per-fish colours come from BettaAppearance (MaterialPropertyBlock), not this material.
        mat.SetTexture("_VariantMask", variantMask);
        // Fins are single planes: cut out by alpha and render both faces.
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.doubleSidedGI = true;
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        EditorUtility.SetDirty(mat);
        SaveMembraneMaterial(mat);
        return mat;
    }

    static void SaveMembraneMaterial(Material skin)
    {
        const string folder = "Assets/_Project/Resources";
        const string path = folder + "/BettaMembrane.mat";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
        Material template = BettaFinSurface.Membrane(skin);
        BettaFinSurface.Configure(template, skin);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(template); AssetDatabase.CreateAsset(material, path); }
        else EditorUtility.CopySerialized(template, material);
        material.name = "BettaMembrane";
        EditorUtility.SetDirty(material);
    }

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/" + name + ".png");

    // Speed / Nibble match BettaFishAI. Clips not exported yet fall back to Idle.
    static AnimatorController Controller()
    {
        string path = ArtDir + "/Betta.controller";
        // Reuse the asset (keeps its GUID and loaded references) and rebuild its contents.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        var oldStates = controller.layers[0].stateMachine;
        foreach (var t in oldStates.anyStateTransitions) oldStates.RemoveAnyStateTransition(t);
        foreach (var s in oldStates.states) oldStates.RemoveState(s.state);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Nibble", AnimatorControllerParameterType.Trigger);

        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n) ?? clips.FirstOrDefault(c => c.name == "Idle");

        var sm = controller.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle");
        var swim = sm.AddState("Swim"); swim.motion = Clip("Swim");
        sm.defaultState = idle;

        var toSwim = idle.AddTransition(swim); toSwim.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed"); toSwim.hasExitTime = false; toSwim.duration = 0.25f;
        var toIdle = swim.AddTransition(idle); toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed"); toIdle.hasExitTime = false; toIdle.duration = 0.25f;

        var nibbleClip = clips.FirstOrDefault(c => c.name == "Nibble");
        if (nibbleClip)
        {
            var nibble = sm.AddState("Nibble"); nibble.motion = nibbleClip;
            var toNibble = sm.AddAnyStateTransition(nibble); toNibble.AddCondition(AnimatorConditionMode.If, 0, "Nibble"); toNibble.duration = 0.1f;
            var back = nibble.AddTransition(idle); back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.15f;
        }

        // Rest loops while BettaReactions holds Resting (AI State.Rest).
        controller.AddParameter("Resting", AnimatorControllerParameterType.Bool);
        var restClip = clips.FirstOrDefault(c => c.name == "Rest");
        if (restClip)
        {
            var rest = sm.AddState("Rest"); rest.motion = restClip;
            var toRest = idle.AddTransition(rest); toRest.AddCondition(AnimatorConditionMode.If, 0, "Resting"); toRest.hasExitTime = false; toRest.duration = 0.6f;
            var fromRest = rest.AddTransition(idle); fromRest.AddCondition(AnimatorConditionMode.IfNot, 0, "Resting"); fromRest.hasExitTime = false; fromRest.duration = 0.4f;
        }

        // Flare plays once from any state when the fish is tapped (BettaReactions).
        controller.AddParameter("Flare", AnimatorControllerParameterType.Trigger);
        var flareClip = clips.FirstOrDefault(c => c.name == "Flare");
        if (flareClip)
        {
            var flare = sm.AddState("Flare"); flare.motion = flareClip;
            var toFlare = sm.AddAnyStateTransition(flare); toFlare.AddCondition(AnimatorConditionMode.If, 0, "Flare"); toFlare.duration = 0.1f; toFlare.canTransitionToSelf = false;
            var back = flare.AddTransition(idle); back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.2f;
        }
        return controller;
    }
}
