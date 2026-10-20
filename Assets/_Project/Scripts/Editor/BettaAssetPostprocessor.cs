using UnityEditor;

// Import settings for the Blender-exported Betta (Assets/_Project/Art/Betta),
// so re-exporting Betta.fbx from Blender keeps the same setup.
public class BettaAssetPostprocessor : AssetPostprocessor
{
    const string ArtDir = "Assets/_Project/Art/Betta/";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(ArtDir)) return;
        var model = (ModelImporter)assetImporter;
        model.globalScale = 1f;
        model.useFileScale = true;
        model.bakeAxisConversion = true;
        model.importBlendShapes = false;
        model.importCameras = false;
        model.importLights = false;
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.animationType = ModelImporterAnimationType.Generic;
        model.optimizeGameObjects = false;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(ArtDir)) return;
        var model = (ModelImporter)assetImporter;
        var clips = model.defaultClipAnimations;
        foreach (var clip in clips)
        {
            // Swim / Rest / Idle loop; Turn, Nibble and Flare are one-shots.
            string n = clip.name.ToLowerInvariant();
            clip.loopTime = n.Contains("idle") || n.Contains("swim") || n.Contains("rest");
            clip.name = clip.name.Contains("|") ? clip.name.Substring(clip.name.LastIndexOf('|') + 1) : clip.name;
        }
        model.clipAnimations = clips;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtDir)) return;
        var tex = (TextureImporter)assetImporter;
        tex.maxTextureSize = 2048;
        if (assetPath.Contains("Normal")) tex.textureType = TextureImporterType.NormalMap;
        tex.sRGBTexture = assetPath.Contains("BaseColor");
        tex.alphaIsTransparency = assetPath.Contains("BaseColor");
    }
}
