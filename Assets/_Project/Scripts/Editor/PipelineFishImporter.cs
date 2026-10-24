using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Turns every PipelineFish under Art/Fish into a playable fish. The Blender pipeline already fitted the
// template rig, skinned the mesh and baked the template clips onto it, so nothing is fitted here:
//   <Name>.mat              Betta Lit with the fish's variant mask and optional pattern mask
//                           (colours come from BettaAppearance)
//   <Name>.overrideController  the shared Betta.controller playing this fish's own clips
//   <Name>.prefab           FBX + swim/fin/reaction components, as BettaVariant.modelPrefab
//   Data/Betta/Variants/<Name>.asset   the shop's variant (FishShopScreenBuilder lists every inShop fish)
// When Art/Fish/<Name>/<Name>.json exists (PipelineFishConfig) it is the source of every setting: it is
// written into <Name>.asset and the variant first, so edits made in the Inspector are overwritten.
public static class PipelineFishImporter
{
    public const string FishDir = "Assets/_Project/Art/Fish";
    const string VariantDir = "Assets/_Project/Data/Betta/Variants";
    const string BettaController = "Assets/_Project/Art/Betta/Betta.controller";
    const string BubbleMaterial = "Assets/_Project/Art/Aquarium3D/Bubbles.mat";
    // The template rig lies pitched in its FBX; tilt the model level in the tank.
    const float PitchCorrection = 20f;

    [MenuItem("Lumaquarium/Fish/Import Pipeline Fish and Update Shop")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before importing pipeline fish.");
        // The Blender build copies files in behind Unity's back: pick them up first.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        PipelineFishConfig.CreateMissingAssets(FishDir);
        // One broken fish must not hold back the others or the shop.
        var failed = new List<string>();
        foreach (var fish in All())
        {
            try { Build(fish); }
            catch (Exception e)
            {
                failed.Add(fish.name);
                Debug.LogError("[Lumaquarium] Pipeline fish " + fish.name + " not built: " + e.Message);
            }
        }
        AssetDatabase.SaveAssets();
        if (failed.Count > 0) Debug.LogError("[Lumaquarium] Not built (see above): " + string.Join(", ", failed));
        FishShopScreenBuilder.Build(false);
        ShopScreenBuilder.Build(); // re-reads the fish offers and removes the fish shop canvas again
    }

    public static PipelineFish[] All() => AssetDatabase.FindAssets("t:PipelineFish", new[] { FishDir })
        .Select(g => AssetDatabase.LoadAssetAtPath<PipelineFish>(AssetDatabase.GUIDToAssetPath(g)))
        .OrderBy(f => AssetDatabase.GetAssetPath(f)).ToArray();

    public static string VariantPath(PipelineFish fish) => VariantDir + "/" + fish.name + ".asset";

    public static BettaVariant Build(PipelineFish fish)
    {
        string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(fish)).Replace('\\', '/');
        string source = dir + "/" + fish.name;
        string configPath = PipelineFishConfig.PathFor(dir, fish.name);
        var config = PipelineFishConfig.Load(configPath);
        config?.ApplyTo(fish, configPath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(source + ".fbx");
        var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(source + "_VariantMask.png");
        var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(source + "_BaseColor.png");
        if (!model || !mask || !baseColor)
            throw new InvalidOperationException("Copy " + fish.name + ".fbx, _VariantMask.png, _BaseColor.png (and _PatternMask.png, _Normal.png, _Detail.png) from betta-splendens/work into " + dir);

        var material = FishMaterial(source, mask, baseColor,
            AssetDatabase.LoadAssetAtPath<Texture2D>(source + "_PatternMask.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>(source + "_Normal.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>(source + "_Detail.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>(source + "_ScaleCells.png"));
        var controller = Controller(source, model);

        var root = new GameObject(fish.name);
        GameObject prefab;
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.transform.localRotation = Quaternion.Euler(PitchCorrection, 0f, 0f);
            instance.transform.localScale = Vector3.one * fish.size;
            foreach (var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.sharedMaterial = material;
                r.quality = SkinQuality.Bone4;
                r.updateWhenOffscreen = false;
            }
            var animator = instance.GetComponent<Animator>();
            if (!animator) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var swim = root.AddComponent<BettaSwimMotion>();
            swim.skeletonRoot = instance.transform;
            swim.bodySwimAmplitude = fish.bodySwimAmplitude;
            swim.strokeSwimAmplitude = fish.strokeSwimAmplitude;
            var fins = root.AddComponent<BettaFinPhysics>();
            fins.skeletonRoot = instance.transform;
            fins.skippedChains = BettaFinPhysics.CrownChainRoots;
            var reactions = root.AddComponent<BettaReactions>();
            reactions.animator = animator;
            var mouth = root.AddComponent<BettaMouth>();
            mouth.skeletonRoot = instance.transform;
            mouth.bubbleMaterial = AssetDatabase.LoadAssetAtPath<Material>(BubbleMaterial);
            var binding = root.AddComponent<BettaRiggedModel>();
            binding.animator = animator;
            binding.reactions = reactions;
            prefab = PrefabUtility.SaveAsPrefabAsset(root, source + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }

        string variantPath = VariantPath(fish);
        var variant = AssetDatabase.LoadAssetAtPath<BettaVariant>(variantPath);
        if (!variant)
        {
            variant = ScriptableObject.CreateInstance<BettaVariant>();
            AssetDatabase.CreateAsset(variant, variantPath);
        }
        variant.displayName = fish.displayName;
        variant.modelPrefab = prefab;
        variant.pattern = fish.pattern;
        variant.primary = fish.primary;
        variant.secondary = fish.secondary;
        variant.accent = fish.accent;
        variant.finEdge = fish.finEdge;
        variant.iridescence = fish.iridescence;
        variant.seed = fish.seed;
        config?.ApplyTo(variant, configPath);
        EditorUtility.SetDirty(variant);
        Debug.Log("[Lumaquarium] Built pipeline fish " + source + ".prefab");
        return variant;
    }

    static Material FishMaterial(string source, Texture2D mask, Texture2D baseColor, Texture2D pattern, Texture2D normal, Texture2D detail, Texture2D cells)
    {
        string path = source + ".mat";
        var shader = Shader.Find("Lumaquarium/Betta Lit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.SetTexture("_BaseMap", baseColor);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", .5f);
        material.SetTexture("_VariantMask", mask);
        // Optional: head, fin-ray streaks and height for patterns such as Galaxy.
        material.SetTexture("_PatternMask", pattern);
        // Scales and fin folds baked from the raw Meshy mesh (fish_bake.py): normal map, and the
        // detail map as occlusion (G = AO; BettaVariant.hlsl reads R = cavity).
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", 1f);
        SetKeyword(material, "_NORMALMAP", normal);
        material.SetTexture("_OcclusionMap", detail);
        material.SetFloat("_OcclusionStrength", 1f);
        SetKeyword(material, "_OCCLUSIONMAP", detail);
        // Optional: each scale's centre, so a scale takes one colour (pipeline/scale_edges.py).
        material.SetTexture("_ScaleCellMap", cells);
        // The ordered scale atlas follows the shared Betta's UVs; a transparent map turns it off.
        material.SetTexture("_ScaleMap", NoScales());
        // Meshy fins are thin shells: draw both faces.
        material.SetFloat("_Cull", 0f);
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetKeyword(Material material, string keyword, bool on)
    {
        if (on) material.EnableKeyword(keyword);
        else material.DisableKeyword(keyword);
    }

    static Texture2D NoScales()
    {
        string path = FishDir + "/NoScales.png";
        if (!File.Exists(path))
        {
            var empty = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            empty.SetPixels32(Enumerable.Repeat(new Color32(0, 0, 0, 0), 16).ToArray());
            File.WriteAllBytes(path, empty.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(empty);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // The shared state machine (Idle/Swim/Nibble/Rest/Flare, driven by BettaFishAI and BettaReactions)
    // with every clip swapped for the same-named clip baked onto this fish's own rest pose.
    static AnimatorOverrideController Controller(string source, GameObject model)
    {
        var shared = AssetDatabase.LoadAssetAtPath<AnimatorController>(BettaController);
        if (!shared) throw new InvalidOperationException("Run Lumaquarium > Fish > Rebuild Betta Animator Controller first (missing " + BettaController + ").");
        var clips = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        string path = source + ".overrideController";
        var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (!overrides) { overrides = new AnimatorOverrideController(shared); AssetDatabase.CreateAsset(overrides, path); }
        overrides.runtimeAnimatorController = shared;
        foreach (var clip in shared.animationClips.Distinct())
        {
            var own = clips.FirstOrDefault(c => c.name == clip.name);
            if (!own) Debug.LogWarning("[Lumaquarium] " + model.name + " has no " + clip.name + " clip; it keeps the shared one.");
            overrides[clip] = own;
        }
        EditorUtility.SetDirty(overrides);
        return overrides;
    }
}
