using UnityEditor;

// Import settings for the Blender-exported Betta (Assets/_Project/Art/Betta),
// so re-exporting Betta.fbx from Blender keeps the same setup. Pipeline fish
// (Art/Fish, see PipelineFishImporter) come from the same rig and share the model rules.
public class BettaAssetPostprocessor : AssetPostprocessor
{
    const string ArtDir = "Assets/_Project/Art/Betta/";
    const string FishDir = PipelineFishImporter.FishDir + "/";

    bool IsRigged => assetPath.StartsWith(ArtDir) || assetPath.StartsWith(FishDir);

    void OnPreprocessModel()
    {
        if (!IsRigged) return;
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
        if (!IsRigged) return;
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
        if (assetPath.StartsWith(FishDir))
        {
            var mask = (TextureImporter)assetImporter;
            mask.maxTextureSize = 1024;
            mask.sRGBTexture = assetPath.Contains("BaseColor");
            // Body/fin and eye edges sit in R/G: block compression would smear them.
            mask.textureCompression = TextureImporterCompression.Uncompressed;
            return;
        }
        if (!assetPath.StartsWith(ArtDir)) return;
        var tex = (TextureImporter)assetImporter;
        tex.maxTextureSize = 2048;
        if (assetPath.Contains("Normal")) tex.textureType = TextureImporterType.NormalMap;
        tex.sRGBTexture = assetPath.Contains("BaseColor");
        tex.alphaIsTransparency = assetPath.Contains("BaseColor");
    }
}
