using UnityEditor;
using UnityEngine;

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

    // BettaSwimMotion beats the pectorals at a rate that follows swim speed; the Swim clip's own fixed
    // 1 s pectoral flap on top of it made the two fins beat out of step, so drop it from that clip.
    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        if (!IsRigged || !clip.name.ToLowerInvariant().Contains("swim")) return;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            if ((binding.path.EndsWith("Pectoral_L_01") || binding.path.EndsWith("Pectoral_R_01"))
                && binding.propertyName.StartsWith("m_LocalRotation"))
                AnimationUtility.SetEditorCurve(clip, binding, null);
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
        // Scale cells (scale_edges.py): exact centre UVs per scale, read point-sampled.
        if (assetPath.EndsWith("_ScaleCells.png"))
        {
            mask.sRGBTexture = false;
            mask.mipmapEnabled = false;
            mask.filterMode = FilterMode.Point;
            mask.maxTextureSize = 2048;
            mask.textureCompression = TextureImporterCompression.Uncompressed;
            return;
        }
        // Master-body fish bake colour/masks at 2048 so the head keeps its detail.
        mask.maxTextureSize = 2048;
        mask.sRGBTexture = assetPath.Contains("BaseColor");
        // Body/fin and eye edges sit in R/G: block compression would smear them.
        mask.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
