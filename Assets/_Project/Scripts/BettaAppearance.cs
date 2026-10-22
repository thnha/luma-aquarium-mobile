using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Shows a BettaVariant on the aquarium fish:
// - instantiates the variant's own model (a pipeline fish from Art/Fish, see PipelineFishImporter)
//   under this object, which keeps the AI, feeding and movement,
// - paints it through a MaterialPropertyBlock: Betta Lit reads the colours with the fish's own
//   variant mask (see Shaders/BettaVariant.hlsl), so every fish keeps its shared material.
// Runs before BettaSwimMotion and BettaFinPhysics on the model.
[DefaultExecutionOrder(-50)]
public class BettaAppearance : MonoBehaviour
{
    public BettaVariant variant;

    static readonly int VariantOnId = Shader.PropertyToID("_VariantOn");
    static readonly int PrimaryId = Shader.PropertyToID("_VariantPrimary");
    static readonly int SecondaryId = Shader.PropertyToID("_VariantSecondary");
    static readonly int AccentId = Shader.PropertyToID("_VariantAccent");
    static readonly int PatternId = Shader.PropertyToID("_VariantPattern");
    static readonly int SeedId = Shader.PropertyToID("_VariantSeed");
    static readonly int IridescenceId = Shader.PropertyToID("_VariantIridescence");
    static readonly int DetailId = Shader.PropertyToID("_VariantDetail");
    static readonly int SilverId = Shader.PropertyToID("_VariantSilver");
    static readonly int SpotId = Shader.PropertyToID("_VariantSpot");
    static readonly int AccentSpotId = Shader.PropertyToID("_VariantAccentSpot");
    static readonly int BandId = Shader.PropertyToID("_VariantBand");
    static readonly int BandSoftnessId = Shader.PropertyToID("_VariantBandSoftness");
    static readonly int EdgeId = Shader.PropertyToID("_VariantEdge");
    static readonly int SpeckleId = Shader.PropertyToID("_VariantSpeckle");
    static readonly int GlossId = Shader.PropertyToID("_Gloss");
    static readonly int BlackGlossId = Shader.PropertyToID("_BlackGloss");
    static readonly int WhiteScalesId = Shader.PropertyToID("_WhiteScales");
    static readonly int FinReliefId = Shader.PropertyToID("_FinRelief");
    static readonly int FinTranslucencyId = Shader.PropertyToID("_FinTranslucency");
    static readonly int ScaleReliefId = Shader.PropertyToID("_ScaleRelief");
    static readonly int FinBlackId = Shader.PropertyToID("_FinBlack");
    static readonly int FinBrightnessId = Shader.PropertyToID("_FinBrightness");
    static readonly int VibranceId = Shader.PropertyToID("_Vibrance");
    static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    static readonly int HeadId = Shader.PropertyToID("_VariantHead");
    static readonly int TailId = Shader.PropertyToID("_VariantTail");
    static readonly int DepthId = Shader.PropertyToID("_VariantDepth");
    static readonly int FinEdgeAlphaId = Shader.PropertyToID("_FinEdgeAlpha");
    static readonly int FinEdgeCaudalId = Shader.PropertyToID("_FinEdgeCaudal");
    static readonly int FinEdgePassId = Shader.PropertyToID("_FinEdgePass");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int VariantMaskId = Shader.PropertyToID("_VariantMask");

    // Transparent copy of each fish material that draws only the clear fin rims (Betta Lit _FinEdgePass).
    static readonly Dictionary<Material, Material> rimMaterials = new Dictionary<Material, Material>();

    MaterialPropertyBlock block;
    GameObject modelInstance;
    GameObject modelPrefab;

    public BettaVariant Variant => variant;

    void Awake()
    {
        if (variant != null) Apply(variant);
    }

    public void Apply(BettaVariant v)
    {
        if (v == null) return;
        if (v.modelPrefab == null)
        {
            Debug.LogWarning("[BettaAppearance] " + v.name + " has no model. Run Lumaquarium > Fish > Import Pipeline Fish.", this);
            return;
        }
        variant = v;
        ShowModel(v.modelPrefab);
        // Pipeline fish are plain white with a variant mask in their own UVs.
        var painted = new List<Renderer>();
        foreach (var r in modelInstance.GetComponentsInChildren<Renderer>(true))
            if (r.sharedMaterial && r.sharedMaterial.HasProperty(VariantMaskId) && r.sharedMaterial.GetTexture(VariantMaskId)) painted.Add(r);
        ApplyColors(painted);
    }

    void ShowModel(GameObject prefab)
    {
        // modelInstance isn't serialized: a model already under the fish (saved in a scene, or copied
        // with the fish by Instantiate) is adopted if it is this one and removed otherwise, so two
        // models never overlap (an unpainted white one showed through the painted one).
        var children = new List<Transform>();
        if (modelInstance == null)
            foreach (Transform child in transform) children.Add(child);
        foreach (var child in children)
        {
            if (!child.GetComponentInChildren<SkinnedMeshRenderer>(true)) continue;
            if (modelInstance == null && child.name == prefab.name)
            {
                modelInstance = child.gameObject;
                modelPrefab = prefab;
                continue;
            }
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
        if (modelInstance != null && modelPrefab == prefab) return;
        if (modelInstance != null)
        {
            modelInstance.SetActive(false);
            if (Application.isPlaying) Destroy(modelInstance);
            else DestroyImmediate(modelInstance);
        }
        modelPrefab = prefab;
        modelInstance = Instantiate(prefab, transform, false);
        modelInstance.name = prefab.name;
        foreach (var child in modelInstance.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = gameObject.layer;
    }

    void ApplyColors(IEnumerable<Renderer> renderers)
    {
        block ??= new MaterialPropertyBlock();
        BettaPattern p = variant.pattern;
        foreach (var r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetFloat(VariantOnId, 1f);
            block.SetColor(PrimaryId, variant.primary);
            block.SetColor(SecondaryId, variant.secondary);
            block.SetColor(AccentId, variant.accent);
            block.SetFloat(PatternId, p ? (float)p.kind : 0f);
            block.SetFloat(SeedId, variant.seed * 2.3999632f); // golden angle: neighbouring seeds look unrelated
            block.SetFloat(IridescenceId, variant.iridescence);
            block.SetFloat(DetailId, variant.textureDetail);
            block.SetFloat(SilverId, variant.silverSheen);
            block.SetFloat(SpotId, p ? p.spotThreshold : 0.58f);
            block.SetFloat(AccentSpotId, p ? p.accentThreshold : 0.3f);
            block.SetFloat(BandId, p ? p.butterflyBand : 0.55f);
            block.SetFloat(BandSoftnessId, p ? p.butterflyBandSoftness : 0.04f);
            block.SetColor(EdgeId, variant.finEdge);
            block.SetVector(SpeckleId, new Vector4(p ? p.speckleScale : 220f, p ? p.speckleDensity : 0.35f, 0f, 0f));
            Texture baseMap = r.sharedMaterial && r.sharedMaterial.HasProperty(BaseMapId) ? r.sharedMaterial.GetTexture(BaseMapId) : null;
            if (baseMap) block.SetTexture(BaseMapId, baseMap);
            block.SetFloat(GlossId, variant.gloss);
            block.SetFloat(BlackGlossId, variant.blackGloss);
            block.SetFloat(WhiteScalesId, variant.whiteScales);
            block.SetFloat(FinReliefId, variant.finRelief);
            block.SetFloat(FinTranslucencyId, variant.finTranslucency);
            block.SetFloat(ScaleReliefId, variant.scaleRelief);
            block.SetFloat(FinBlackId, variant.finBlack);
            block.SetFloat(FinBrightnessId, variant.finBrightness);
            block.SetFloat(VibranceId, variant.vibrance);
            block.SetFloat(BrightnessId, variant.brightness);
            BodyAxis(r, out Vector3 head, out Vector3 tail);
            block.SetVector(HeadId, head);
            block.SetVector(TailId, tail);
            block.SetVector(DepthId, new Vector4(0f, 0f, variant.pigmentDepth, variant.finEdgeClear));
            block.SetVector(FinEdgeAlphaId, new Vector4(variant.finRim.x, variant.finRimAlpha, variant.finRim.y, 0f));
            block.SetVector(FinEdgeCaudalId, new Vector4(variant.caudalRim.x, variant.caudalRim.y, variant.caudalStart, 0f));
            r.SetPropertyBlock(block);
            SetRimMaterial(r, variant.finRimAlpha < 0.999f);
        }
    }

    // Front and back of the fish in the shader's object space, from the mesh bounds (mesh data is not
    // readable in builds). A skinned mesh is drawn relative to its root bone (position and rotation, no
    // scale); the fish swims along this object's forward.
    void BodyAxis(Renderer r, out Vector3 head, out Vector3 tail)
    {
        head = tail = Vector3.zero;
        var skinned = r as SkinnedMeshRenderer;
        var filter = skinned ? null : r.GetComponent<MeshFilter>();
        Mesh mesh = skinned ? skinned.sharedMesh : filter ? filter.sharedMesh : null;
        if (!mesh) return;
        // Along the swim direction (the model itself is pitched), as far as the mesh bounds reach.
        Bounds b = mesh.bounds;
        Vector3 f = r.transform.InverseTransformDirection(transform.forward);
        Vector3 e = b.extents;
        Vector3 reach = f * (Mathf.Abs(f.x) * e.x + Mathf.Abs(f.y) * e.y + Mathf.Abs(f.z) * e.z);
        Transform space = skinned && skinned.rootBone ? skinned.rootBone : r.transform;
        Matrix4x4 toObject = Matrix4x4.TRS(space.position, space.rotation, Vector3.one).inverse * r.transform.localToWorldMatrix;
        head = toObject.MultiplyPoint3x4(b.center + reach);
        tail = toObject.MultiplyPoint3x4(b.center - reach);
    }

    static void SetRimMaterial(Renderer r, bool on)
    {
        Material body = r.sharedMaterials.Length > 0 ? r.sharedMaterials[0] : null;
        if (!body) return;
        if (!on)
        {
            if (r.sharedMaterials.Length > 1) r.sharedMaterials = new[] { body };
            return;
        }
        if (!rimMaterials.TryGetValue(body, out Material rim) || !rim)
        {
            rim = new Material(body) { name = body.name + " (Clear Rims)", renderQueue = (int)RenderQueue.Transparent };
            rim.SetFloat(FinEdgePassId, 1f);
            rim.SetFloat("_Surface", 1f);
            rim.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            rim.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            rim.SetFloat("_SrcBlendAlpha", 1f);
            rim.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            // Front faces only, with depth: a thin fin is two close sheets, and blending both in triangle
            // order (no sorting inside one mesh) showed the far sheet through the near one in shards.
            rim.SetFloat("_Cull", (float)CullMode.Back);
            rim.SetFloat("_ZWrite", 1f);
            rim.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            rim.SetShaderPassEnabled("ShadowCaster", false);
            rim.SetShaderPassEnabled("DepthOnly", false);
            rim.SetShaderPassEnabled("DepthNormals", false);
            rimMaterials[body] = rim;
        }
        r.sharedMaterials = new[] { body, rim };
    }
}
