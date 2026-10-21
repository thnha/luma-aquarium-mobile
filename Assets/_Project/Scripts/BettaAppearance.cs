using System.Collections.Generic;
using UnityEngine;

// Applies a BettaVariant to the shared Betta rig:
// - colours/pattern through a MaterialPropertyBlock (Betta Lit reads them with the baked
//   variant mask, see Shaders/BettaVariant.hlsl), so every fish keeps the same material,
// - fuller head as a smooth bind-space shape, with the eyes moved to match the skull,
// - fin and eye size as bone scale, re-applied every LateUpdate because the clips key
//   scale on every bone (assigned, never multiplied, so a culled Animator can't make it grow),
// - tail form: caudal spread/droop and fin physics preset on BettaFinPhysics,
// - tail form outline: the tail type's Fin_* meshes, and its base map (fin edges cut in alpha) through the property block,
//   with the fin edges folded when the tail type has a ruffle (Rosetail, see BettaFinRuffle).
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
    static readonly int DetailId = Shader.PropertyToID("_VariantDetail");
    static readonly int SilverId = Shader.PropertyToID("_VariantSilver");
    static readonly int SpotId = Shader.PropertyToID("_VariantSpot");
    static readonly int AccentSpotId = Shader.PropertyToID("_VariantAccentSpot");
    static readonly int BandId = Shader.PropertyToID("_VariantBand");
    static readonly int BandSoftnessId = Shader.PropertyToID("_VariantBandSoftness");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    static readonly string[] CaudalRoots = { "Tail_Top_01", "Tail_UpMid_01", "Tail_01", "Tail_LowMid_01", "Tail_Low_01", "Tail_Bot_01" };

    readonly List<(Transform bone, float scale)> scaled = new List<(Transform, float)>();
    readonly List<(Transform bone, Vector3 position)> positioned = new List<(Transform, Vector3)>();
    readonly List<(Transform bone, Matrix4x4 fromHead)> gillAnchors = new List<(Transform, Matrix4x4)>();
    Transform headBone;
    Animator headAnimator;
    Vector3 gillHinge, gillUp;
    Vector3 profileHead, profileNeck, profileTail;
    bool hasProfile;
    readonly Dictionary<SkinnedMeshRenderer, Mesh> flatFins = new Dictionary<SkinnedMeshRenderer, Mesh>();
    Mesh bodySource;
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
        foreach (var (bone, position) in positioned) bone.localPosition = position;
        ApplyGillPose();
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
            block.SetFloat(DetailId, variant.textureDetail);
            block.SetFloat(SilverId, variant.silverSheen);
            block.SetFloat(SpotId, p ? p.spotThreshold : 0.58f);
            block.SetFloat(AccentSpotId, p ? p.accentThreshold : 0.3f);
            block.SetFloat(BandId, p ? p.butterflyBand : 0.55f);
            block.SetFloat(BandSoftnessId, p ? p.butterflyBandSoftness : 0.04f);
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
        // Head and gill bones have different pivots. Scaling them independently pinches the neck.
        Scale("Head", 1f);
        Scale("Gill_L_01", 1f);
        Scale("Gill_R_01", 1f);
        Scale("Eye_L", variant.eyeScale);
        Scale("Eye_R", variant.eyeScale);
        ApplyHeadShape(root);
        foreach (string b in CaudalRoots) Scale(b, fin * (tail ? tail.caudalLength : 1f));
        foreach (string b in new[] { "Dorsal_A_01", "Dorsal_B_01" }) Scale(b, fin * (tail ? tail.dorsalLength : 1f));
        foreach (string b in new[] { "Anal_A_01", "Anal_B_01" }) Scale(b, fin * (tail ? tail.analLength : 1f));
        foreach (string b in new[] { "Pelvic_L_01", "Pelvic_R_01" }) Scale(b, fin * (tail ? tail.pelvicLength : 1f));

        // Betta.fbx carries every form's Fin_* meshes; show this form's (Halfmoon when it names none).
        string[] finMeshes = tail ? tail.finMeshes : null;
        bool useHalfmoon = finMeshes == null || finMeshes.Length == 0;
        BettaFinRuffle.Settings ruffle = tail ? tail.ruffle : null;
        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!r.name.StartsWith("Fin_")) continue;
            r.sharedMaterial = BettaFinSurface.Membrane(r.sharedMaterial);
            r.enabled = useHalfmoon ? r.name.EndsWith("_Halfmoon") : System.Array.IndexOf(finMeshes, r.name) >= 0;
            if (!flatFins.TryGetValue(r, out Mesh flat)) flatFins[r] = flat = BettaHeadShape.Source(BettaFinRuffle.Source(r.sharedMesh));
            if (flat == null) continue;
            Mesh fitted = r.enabled && hasProfile ? BettaHeadShape.FitFin(flat, r.bones, profileHead, profileNeck, profileTail, variant.headScale) : flat;
            r.sharedMesh = r.enabled && ruffle != null && ruffle.Enabled ? BettaFinRuffle.Get(fitted, r.bones, ruffle) : fitted;
        }

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

    void ApplyHeadShape(Transform root)
    {
        positioned.Clear();
        gillAnchors.Clear();
        hasProfile = false;
        SkinnedMeshRenderer body = null;
        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.name == "BODY") { body = r; break; }
        if (body == null || body.sharedMesh == null) return;
        if (bodySource == null) bodySource = BettaHeadShape.Source(body.sharedMesh);
        Matrix4x4[] bindposes = bodySource.bindposes;
        int Index(string name) => System.Array.FindIndex(body.bones, b => b && b.name == name);
        int headIndex = Index("Head"), neckIndex = Index("Spine_01"), tailIndex = Index("Tail_Base");
        if (headIndex < 0 || neckIndex < 0 || tailIndex < 0 || headIndex >= bindposes.Length || neckIndex >= bindposes.Length || tailIndex >= bindposes.Length) return;
        Vector3 head = bindposes[headIndex].inverse.MultiplyPoint3x4(Vector3.zero);
        Vector3 neck = bindposes[neckIndex].inverse.MultiplyPoint3x4(Vector3.zero);
        Vector3 tail = bindposes[tailIndex].inverse.MultiplyPoint3x4(Vector3.zero);
        profileHead = head; profileNeck = neck; profileTail = tail; hasProfile = true;
        body.sharedMesh = BettaHeadShape.Get(bodySource, body.bones, head, neck, variant.headScale);
        Material skin = body.sharedMaterial;
        body.sharedMaterials = new[] { skin, BettaFinSurface.Membrane(skin) };

        // Translate eye anchors instead of stretching their mesh; eyeScale stays uniform.
        foreach (string name in new[] { "Eye_L", "Eye_R" })
        {
            Transform eye = FindDeep(root, name);
            int index = Index(name);
            if (!eye || index < 0 || index >= bindposes.Length) continue;
            Vector3 center = bindposes[index].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 position = bindposes[headIndex].MultiplyPoint3x4(
                BettaHeadShape.ShapePoint(center, head, tail, variant.headScale));
            eye.localPosition = position;
            positioned.Add((eye, position));
        }

        // Idle in the source animation opens/folds the gill tips like a flare. Rebuild
        // the closed operculum from bind matrices so it follows the cheek as one surface.
        // BettaSwimMotion adds the small breathing rotation afterwards.
        headBone = body.bones[headIndex];
        headAnimator = GetComponentInChildren<Animator>();
        gillHinge = bindposes[headIndex].MultiplyPoint3x4(neck);
        gillUp = bindposes[headIndex].MultiplyVector(Vector3.up).normalized;
        foreach (string name in new[] { "Gill_L_01", "Gill_L_02", "Gill_L_03", "Gill_R_01", "Gill_R_02", "Gill_R_03" })
        {
            int i = Index(name);
            if (i >= 0 && i < bindposes.Length)
                gillAnchors.Add((body.bones[i], bindposes[headIndex] * bindposes[i].inverse));
        }
        ApplyGillPose();
    }

    void ApplyGillPose()
    {
        if (!headBone) return;
        Matrix4x4 head = headBone.localToWorldMatrix;
        float flare = 0f;
        if (headAnimator && headAnimator.enabled && headAnimator.isInitialized)
        {
            var state = headAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Flare")) flare = Mathf.Sin(Mathf.PI * Mathf.Clamp01(state.normalizedTime)) * 8f;
        }
        foreach (var (bone, fromHead) in gillAnchors)
        {
            float angle = bone.name.StartsWith("Gill_L") ? flare : -flare;
            Matrix4x4 hinge = Matrix4x4.Translate(gillHinge) * Matrix4x4.Rotate(Quaternion.AngleAxis(angle, gillUp))
                * Matrix4x4.Translate(-gillHinge);
            Matrix4x4 pose = head * hinge * fromHead;
            bone.SetPositionAndRotation(pose.GetColumn(3), pose.rotation);
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
