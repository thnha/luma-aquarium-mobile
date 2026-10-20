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
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (!fbx) { Debug.LogError("[Lumaquarium] Missing " + FbxPath); return null; }

        Material mat = FishMaterial();
        AnimatorController controller = Controller();

        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "Betta";
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            r.sharedMaterial = mat;
            r.updateWhenOffscreen = false;
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

    static Material FishMaterial()
    {
        string path = ArtDir + "/Betta.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", Tex("BETTA_BaseColor_Alpha"));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_MetallicGlossMap", Tex("BETTA_MetallicSmoothness"));
        mat.SetFloat("_Smoothness", 1f);
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetTexture("_BumpMap", Tex("BETTA_Normal_OpenGL"));
        mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_OcclusionMap", Tex("BETTA_Mixed_AO"));
        mat.EnableKeyword("_OCCLUSIONMAP");
        // Fins are single planes: cut out by alpha and render both faces.
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.doubleSidedGI = true;
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/" + name + ".png");

    // Speed / Nibble match BettaFishAI. Clips not exported yet fall back to Idle.
    static AnimatorController Controller()
    {
        string path = ArtDir + "/Betta.controller";
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
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
        return controller;
    }
}
