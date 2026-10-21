using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Same pipeline as AquaFireBettaBuilder for the Meshy Koi Betta. The source FBX was decimated
// in Blender from 1M to 30k tris and its PNGs downscaled to 2048; nothing else was changed.
// A derived metallic/smoothness map adapts the original PBR material to URP Lit.
public static class KoiNemoBettaBuilder
{
    const string Root = "Assets/_Project/Art/KoiNemoBetta";
    const string Source = Root + "/Source/KoiNemoBetta";
    const string VariantPath = "Assets/_Project/Data/Betta/Variants/KoiNemoBetta.asset";

    [MenuItem("Lumaquarium/Betta/Import Koi Nemo Betta and Update Shop")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before importing Koi Nemo Betta.");
        BuildAssets();
        FishShopScreenBuilder.Build();
    }

    public static void BuildAssets()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + ".fbx");
        if (source == null) throw new InvalidOperationException("Missing the Koi Nemo Betta FBX in " + Root + "/Source.");
        ConfigureTexture(Source + ".png", true);
        ConfigureTexture(Source + "_normal.png", false, true);
        ConfigureTexture(Source + "_metallic.png", false, readable: true);
        ConfigureTexture(Source + "_roughness.png", false, readable: true);

        var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "_metallic.png");
        var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "_roughness.png");
        if (metallic.width != roughness.width || metallic.height != roughness.height)
            throw new InvalidOperationException("Metallic and roughness maps must have matching dimensions.");
        var metalPixels = metallic.GetPixels32();
        var roughPixels = roughness.GetPixels32();
        for (int i = 0; i < metalPixels.Length; i++)
            metalPixels[i] = new Color32(metalPixels[i].r, metalPixels[i].r, metalPixels[i].r, (byte)(255 - roughPixels[i].r));
        var packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
        string packedPath = Root + "/KoiNemoBetta_MetallicSmoothness.png";
        try
        {
            packed.SetPixels32(metalPixels);
            packed.Apply();
            File.WriteAllBytes(packedPath, packed.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(packed); }
        AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureTexture(packedPath, false);
        ConfigureTexture(Source + "_metallic.png", false);
        ConfigureTexture(Source + "_roughness.png", false);

        string materialPath = Root + "/KoiNemoBetta.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + ".png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "_normal.png"));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Smoothness", 1f);
        material.SetFloat("_Cull", 0f); // Preserve both sides of the original fin surfaces.
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(material);

        var root = new GameObject("KoiNemoBetta");
        GameObject prefab;
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.transform.SetParent(root.transform, false);
            var renderer = model.GetComponentInChildren<MeshRenderer>();
            renderer.sharedMaterial = material;
            // The source fish faces along Z. Fit its original proportions to the tank.
            var bounds = renderer.bounds;
            float scale = .75f / bounds.size.z;
            model.transform.localScale *= scale;
            model.transform.localPosition = -bounds.center * scale;
            KoiNemoBettaSkinning.Build(root, renderer, model, material);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/KoiNemoBetta.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var variant = AssetDatabase.LoadAssetAtPath<BettaVariant>(VariantPath);
        if (variant == null)
        {
            variant = ScriptableObject.CreateInstance<BettaVariant>();
            AssetDatabase.CreateAsset(variant, VariantPath);
        }
        variant.displayName = "Koi Nemo Betta";
        variant.modelPrefab = prefab;
        EditorUtility.SetDirty(variant);
        AssetDatabase.SaveAssets();
    }

    static void ConfigureTexture(string path, bool srgb, bool normal = false, bool readable = false)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture " + path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.maxTextureSize = 2048;
        importer.isReadable = readable;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
