using UnityEngine;

// Procedural swim layered on top of the Animator (runs in LateUpdate):
// - a travelling wave down spine and tail whose speed/amplitude follow the AI's swim speed,
// - a C-bend toward the turn direction from the fish's yaw rate,
// - continuous pectoral fanning, gill breathing and trailing fin sway.
// Clips (Idle, Nibble, Flare...) still play underneath; this only adds rotation.
public class BettaSwimMotion : MonoBehaviour
{
    public BettaFishAI fish;
    public Transform skeletonRoot;

    [Header("Body wave")]
    public float idleFrequency = 0.7f;
    public float swimFrequency = 2.6f;
    public float idleAmplitude = 1.5f;   // degrees per bone
    public float swimAmplitude = 7f;
    public float wavePhaseStep = 0.7f;   // radians between bones; wave travels head -> tail

    [Header("Turning")]
    public float turnBendPerDegPerSec = 0.12f;
    public float maxTurnBend = 12f;      // degrees per bone
    public float turnSmoothing = 4f;

    [Header("Fins")]
    public float pectoralFrequency = 3.2f;
    public float pectoralAmplitude = 18f;
    public float gillFrequency = 1.1f;
    public float gillAmplitude = 4f;
    public float finSwayAmplitude = 5f;

    struct Bone
    {
        public Transform t;
        public float weight;
        public int index;
        public Quaternion baseLocal;
        public Quaternion lastWritten;
    }

    // Main chain: weight scales the body wave and turn bend, index sets the wave delay.
    static readonly (string name, float weight, int index)[] BodyChain =
    {
        ("Spine_02", 0.25f, 0), ("Spine_03", 0.45f, 1), ("Spine_04", 0.7f, 2), ("Tail_Base", 0.9f, 3),
        ("Tail_01", 1f, 4), ("Tail_Low_01", 1f, 4), ("Tail_Bot_01", 1f, 4), ("Tail_Top_01", 0.8f, 3),
        ("Tail_02", 1f, 5), ("Tail_Low_02", 1f, 5), ("Tail_Bot_02", 1f, 5), ("Tail_Top_02", 0.9f, 4), ("Tail_Top_03", 1f, 5),
    };
    static readonly string[] SwayFins =
    {
        "Dorsal_A_02", "Dorsal_A_03", "Dorsal_B_02", "Dorsal_B_03",
        "Anal_A_02", "Anal_A_03", "Anal_B_02", "Anal_B_03",
    };

    Bone[] body, sway, pectoralL, pectoralR, gillL, gillR;
    float phase, finPhase, gillPhase, turnBend, lastYaw;

    void Awake()
    {
        if (fish == null) fish = GetComponentInParent<BettaFishAI>();
        if (skeletonRoot == null) skeletonRoot = transform;

        body = new Bone[BodyChain.Length];
        for (int i = 0; i < BodyChain.Length; i++)
            body[i] = Find(BodyChain[i].name, BodyChain[i].weight, BodyChain[i].index);
        sway = new Bone[SwayFins.Length];
        for (int i = 0; i < SwayFins.Length; i++) sway[i] = Find(SwayFins[i], 1f, i % 2 + 2);
        pectoralL = new[] { Find("Pectoral_L_01", 1f, 0), Find("Pectoral_L_02", 0.6f, 1) };
        pectoralR = new[] { Find("Pectoral_R_01", 1f, 0), Find("Pectoral_R_02", 0.6f, 1) };
        gillL = new[] { Find("Gill_L_01", 1f, 0) };
        gillR = new[] { Find("Gill_R_01", 1f, 0) };
        lastYaw = transform.eulerAngles.y;
    }

    Bone Find(string boneName, float weight, int index)
    {
        Transform t = FindDeep(skeletonRoot, boneName);
        if (t == null) Debug.LogWarning("[BettaSwimMotion] Missing bone " + boneName, this);
        return new Bone { t = t, weight = weight, index = index, baseLocal = t ? t.localRotation : Quaternion.identity, lastWritten = t ? t.localRotation : Quaternion.identity };
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

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float speed01 = fish ? Mathf.Clamp01(fish.CurrentSpeed / Mathf.Max(0.01f, fish.swimSpeed)) : 0f;
        float frequency = Mathf.Lerp(idleFrequency, swimFrequency, speed01);
        float amplitude = Mathf.Lerp(idleAmplitude, swimAmplitude, speed01);
        phase += frequency * Mathf.PI * 2f * dt;
        finPhase += pectoralFrequency * Mathf.PI * 2f * dt;
        gillPhase += gillFrequency * Mathf.PI * 2f * dt;

        // Turning left (negative yaw rate) bends the tail toward the left side, forming a C.
        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
        lastYaw = yaw;
        float targetBend = Mathf.Clamp(-yawRate * turnBendPerDegPerSec, -maxTurnBend, maxTurnBend);
        turnBend = Mathf.Lerp(turnBend, targetBend, 1f - Mathf.Exp(-turnSmoothing * dt));

        Vector3 up = transform.up;
        Vector3 forward = transform.forward;

        for (int i = 0; i < body.Length; i++)
        {
            float wave = Mathf.Sin(phase - body[i].index * wavePhaseStep) * amplitude;
            Apply(ref body[i], up, (wave + turnBend) * body[i].weight);
        }

        // Pectorals fan hardest when hovering; they tuck a little when swimming fast.
        float pectoral = Mathf.Sin(finPhase) * pectoralAmplitude * Mathf.Lerp(1f, 0.5f, speed01);
        for (int i = 0; i < pectoralL.Length; i++)
        {
            Apply(ref pectoralL[i], up, pectoral * pectoralL[i].weight);
            Apply(ref pectoralR[i], up, -pectoral * pectoralR[i].weight);
        }

        float gill = (Mathf.Sin(gillPhase) * 0.5f + 0.5f) * gillAmplitude;
        Apply(ref gillL[0], up, gill);
        Apply(ref gillR[0], up, -gill);

        for (int i = 0; i < sway.Length; i++)
        {
            float s = Mathf.Sin(phase - sway[i].index * wavePhaseStep) * finSwayAmplitude * (0.4f + speed01);
            Apply(ref sway[i], forward, s);
        }
    }

    // Rotates a bone by `degrees` around a world axis on top of this frame's base pose.
    // Bones the Animator doesn't key keep their old value, so reuse the cached base for
    // those instead of stacking rotation every frame.
    static void Apply(ref Bone bone, Vector3 worldAxis, float degrees)
    {
        if (bone.t == null) return;
        if (Quaternion.Angle(bone.t.localRotation, bone.lastWritten) > 0.01f)
            bone.baseLocal = bone.t.localRotation;
        Transform parent = bone.t.parent;
        Vector3 localAxis = parent ? Quaternion.Inverse(parent.rotation) * worldAxis : worldAxis;
        bone.t.localRotation = Quaternion.AngleAxis(degrees, localAxis) * bone.baseLocal;
        bone.lastWritten = bone.t.localRotation;
    }
}
