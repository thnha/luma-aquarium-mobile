using System.Collections.Generic;
using UnityEngine;

// Applies a BettaVariant to the shared Betta rig:
// - colours/pattern through a MaterialPropertyBlock (Betta Lit reads them with the baked
//   variant mask, see Shaders/BettaVariant.hlsl), so every fish keeps the same material,
// - head and fin size as bone scale, re-applied every LateUpdate because the clips key
//   scale on every bone (assigned, never multiplied, so a culled Animator can't make it grow),
// - tail form: caudal spread/droop and fin physics preset on BettaFinPhysics,
// - tail form outline: the tail type's base map (fin edges cut in alpha), also through the property block.
// Runs before BettaSwimMotion and BettaFinPhysics so they see the scaled bones.
[DefaultExecutionOrder(-50)]
public class BettaAppearance : MonoBehaviour
{
    public BettaVariant variant;
    [Tooltip("Used when Variant is empty: pick a preset by rarity, or roll a new random fish.")]
    public BettaVariantLibrary library;
    public bool generateRandom;
    public Transform skeletonRoot;
    public BettaFinPhysics finPhysics;
    [Tooltip("Renderers that keep their painted texture (eyes).")]
    public string[] keepOriginalRenderers = { "EYES" };

    static readonly int VariantOnId = Shader.PropertyToID("_VariantOn");
    static readonly int PrimaryId = Shader.PropertyToID("_VariantPrimary");
    static readonly int SecondaryId = Shader.PropertyToID("_VariantSecondary");
    static readonly int AccentId = Shader.PropertyToID("_VariantAccent");
    static readonly int PatternId = Shader.PropertyToID("_VariantPattern");
    static readonly int SeedId = Shader.PropertyToID("_VariantSeed");
    static readonly int IridescenceId = Shader.PropertyToID("_VariantIridescence");
    static readonly int SpotId = Shader.PropertyToID("_VariantSpot");
    static readonly int AccentSpotId = Shader.PropertyToID("_VariantAccentSpot");
    static readonly int BandId = Shader.PropertyToID("_VariantBand");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    static readonly string[] CaudalRoots = { "Tail_Top_01", "Tail_UpMid_01", "Tail_01", "Tail_LowMid_01", "Tail_Low_01", "Tail_Bot_01" };

    readonly List<(Transform bone, float scale)> scaled = new List<(Transform, float)>();
    MaterialPropertyBlock block;

    public BettaVariant Variant => variant;

    void Awake()
    {
        if (finPhysics == null) finPhysics = GetComponent<BettaFinPhysics>();
        if (variant == null && library != null)
            variant = generateRandom ? library.Generate(Random.Range(0, int.MaxValue)) : library.PickPreset();
        if (variant != null) Apply(variant);
    }

    public void Apply(BettaVariant v)
    {
        variant = v;
        ApplyColors();
        ApplyShape();
    }

    void LateUpdate()
    {
        foreach (var (bone, scale) in scaled) bone.localScale = Vector3.one * scale;
    }

    // Edit-mode preview of colours while tuning a variant in the Inspector.
    void OnValidate()
    {
        if (variant != null && !Application.isPlaying) ApplyColors();
    }

    void ApplyColors()
    {
        block ??= new MaterialPropertyBlock();
        BettaPattern p = variant.pattern;
        Texture2D baseMap = variant.tail ? variant.tail.baseMap : null;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (System.Array.IndexOf(keepOriginalRenderers, r.name) >= 0) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(VariantOnId, 1f);
            block.SetColor(PrimaryId, variant.primary);
            block.SetColor(SecondaryId, variant.secondary);
            block.SetColor(AccentId, variant.accent);
            block.SetFloat(PatternId, p ? (float)p.kind : 0f);
            block.SetFloat(SeedId, variant.seed * 2.3999632f); // golden angle: neighbouring seeds look unrelated
            block.SetFloat(IridescenceId, variant.iridescence);
            block.SetFloat(SpotId, p ? p.spotThreshold : 0.58f);
            block.SetFloat(AccentSpotId, p ? p.accentThreshold : 0.3f);
            block.SetFloat(BandId, p ? p.butterflyBand : 0.55f);
            // Always set, so switching back to a form without its own map drops the previous one.
            Texture fallback = r.sharedMaterial && r.sharedMaterial.HasProperty(BaseMapId) ? r.sharedMaterial.GetTexture(BaseMapId) : null;
            if (baseMap || fallback) block.SetTexture(BaseMapId, baseMap ? baseMap : fallback);
            r.SetPropertyBlock(block);
        }
    }

    void ApplyShape()
    {
        Transform root = skeletonRoot ? skeletonRoot : transform;
        BettaTailType tail = variant.tail;
        float fin = variant.finLength;

        scaled.Clear();
        void Scale(string boneName, float scale)
        {
            Transform t = FindDeep(root, boneName);
            if (t == null) { Debug.LogWarning("[BettaAppearance] Missing bone " + boneName, this); return; }
            t.localScale = Vector3.one * scale;
            scaled.Add((t, scale));
        }
        Scale("Head", variant.headScale);
        Scale("Gill_L_01", variant.headScale);
        Scale("Gill_R_01", variant.headScale);
        foreach (string b in CaudalRoots) Scale(b, fin * (tail ? tail.caudalLength : 1f));
        foreach (string b in new[] { "Dorsal_A_01", "Dorsal_B_01" }) Scale(b, fin * (tail ? tail.dorsalLength : 1f));
        foreach (string b in new[] { "Anal_A_01", "Anal_B_01" }) Scale(b, fin * (tail ? tail.analLength : 1f));
        foreach (string b in new[] { "Pelvic_L_01", "Pelvic_R_01" }) Scale(b, fin * (tail ? tail.pelvicLength : 1f));

        if (finPhysics != null)
        {
            if (tail != null)
            {
                finPhysics.caudal = tail.caudal.Clone();
                finPhysics.dorsalAnal = tail.dorsalAnal.Clone();
                finPhysics.caudalSpread = tail.caudalSpread;
                finPhysics.caudalDroop = tail.caudalDroop;
            }
            finPhysics.skippedChains = tail ? tail.skippedFinChains : new string[0];
            finPhysics.Rebind(); // fin bone lengths changed
        }
    }

    static Transform FindDeep(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, boneName);
            if (found) return found;
        }
        return null;
    }
}
