using UnityEditor;

// Import settings for the Blender-exported template rig (Assets/_Project/Art/Betta/Betta.fbx, source of the
// shared animation clips) and the pipeline fish (Art/Fish, see PipelineFishImporter), which come from the same rig.
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
        // The pipeline bakes its normal map on the triangulated mesh in MikkTSpace, as Unity computes it.
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.CalculateMikk;
    }

    void OnPreprocessAnimation()
    {
        if (!IsRigged) return;
        var model = (ModelImporter)assetImporter;
        var clips = model.defaultClipAnimations;
        foreach (var clip in clips)
        {
            // Swim / Rest / Idle loop; Turn, Nibble, Flare, Gulp, Bubble and Yawn are one-shots.
            string n = clip.name.ToLowerInvariant();
            clip.loopTime = n.Contains("idle") || n.Contains("swim") || n.Contains("rest");
            clip.name = clip.name.Contains("|") ? clip.name.Substring(clip.name.LastIndexOf('|') + 1) : clip.name;
        }
        model.clipAnimations = clips;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(FishDir)) return;
        var mask = (TextureImporter)assetImporter;
        // Baked detail (fish_bake.py): _Normal is a tangent-space normal map, _Detail is cavity (R) and AO (G).
        // Both are smooth data at 2048, so block compression is fine there.
        if (assetPath.EndsWith("_Normal.png") || assetPath.EndsWith("_Detail.png"))
        {
            mask.textureType = assetPath.EndsWith("_Normal.png") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            mask.sRGBTexture = false;
            mask.maxTextureSize = 2048;
            mask.textureCompression = TextureImporterCompression.CompressedHQ;
            return;
        }
        mask.maxTextureSize = 1024;
        mask.sRGBTexture = assetPath.Contains("BaseColor");
        // Body/fin and eye edges sit in R/G: block compression would smear them.
        mask.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
