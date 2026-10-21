using UnityEngine;

// Procedural swim layered on top of the Animator (runs in LateUpdate):
// - a slow, small body wave on the rear spine (bettas keep the body fairly still),
// - follow-the-leader turning: each spine segment chases the heading of the one in front,
//   so the head turns first and the body curves into a C, then straightens,
// - the caudal peduncle (Spine_04/Tail_Base) makes the active stroke; the fins themselves
//   are soft bodies simulated by BettaFinPhysics, which trail and ripple from this motion,
// - propulsion: tail effort follows the AI's burst/coast cycle, forward speed surges with
//   each stroke (BettaFishAI.ThrustPulse) and the head recoils slightly against the tail,
// - active pectoral fanning (fin base only) and gill breathing.
// Clips (Idle, Nibble, Flare...) still play underneath; this only adds rotation.
public class BettaSwimMotion : MonoBehaviour
{
    public BettaFishAI fish;
    public Transform skeletonRoot;

    [Header("Body wave (rear spine)")]
    public float bodyIdleFrequency = 0.4f;
    public float bodySwimFrequency = 1f;
    public float bodyIdleAmplitude = 0.5f;   // degrees, scaled per bone
    public float bodySwimAmplitude = 3f;
    public float wavePhaseStep = 0.6f;       // radians between bones; wave travels head -> tail

    [Header("Tail stroke (caudal peduncle)")]
    public float caudalIdleFrequency = 0.6f;
    public float caudalSwimFrequency = 1.4f;
    public float strokeIdleAmplitude = 2f;   // degrees at Tail_Base
    public float strokeSwimAmplitude = 11f;

    [Header("Propulsion")]
    [Range(0f, 1f)] public float thrustPulse = 0.3f; // how much forward speed surges with each stroke
    public float headRecoil = 0.6f;          // subtle recoil keeps the cheek aligned with the body

    [Header("Turning")]
    public float segmentFollowRate = 7f;     // 1/s; lower = body trails the head longer
    public float maxJointBend = 18f;         // degrees between neighbouring segments
    public float turnTailKick = 0.08f;       // extra caudal degrees per deg/s of turning
    public float maxTailKick = 12f;

    [Header("Fins")]
    public float pectoralFrequency = 3.2f;
    public float pectoralAmplitude = 18f;
    public float pectoralTurnBias = 0.5f;    // outer fin beats harder in a turn
    public float gillRestFrequency = 0.7f;   // breathing slows when resting
    public float gillActiveFrequency = 1.6f; // and speeds up after exertion
    public float gillAmplitude = 1.2f;

    struct Bone
    {
        public Transform t;
        public float weight;
        public int index;
        public Quaternion baseLocal;
        public Quaternion lastWritten;
    }

    // Share of the active tail stroke per spine segment (peduncle does the work).
    static readonly float[] StrokeWeights = { 0f, 0f, 0f, 0.3f, 1f };

    // Rear spine: weight scales body wave and turn bend, index sets the wave delay.
    static readonly (string name, float weight, int index)[] SpineChain =
    {
        ("Spine_01", 0.05f, 0), ("Spine_02", 0.1f, 1), ("Spine_03", 0.2f, 2), ("Spine_04", 0.35f, 3), ("Tail_Base", 0.5f, 4),
    };
    Bone[] spine, pectoralL, pectoralR, gillL, gillR;
    Bone head;
    float[] segmentHeading;                  // lagged world yaw per spine segment
    float[] jointBend;
    float bodyPhase, caudalPhase, finPhase, gillPhase, lastYaw, yawRate;
    float exertion;                           // slow-moving average of effort: quick to rise, slow to recover

    void Awake()
    {
        if (fish == null) fish = GetComponentInParent<BettaFishAI>();
        if (skeletonRoot == null) skeletonRoot = transform;

        spine = new Bone[SpineChain.Length];
        for (int i = 0; i < SpineChain.Length; i++)
            spine[i] = Find(SpineChain[i].name, SpineChain[i].weight, SpineChain[i].index);
        pectoralL = new[] { Find("Pectoral_L_01", 1f, 0) };
        pectoralR = new[] { Find("Pectoral_R_01", 1f, 0) };
        gillL = new[] { Find("Gill_L_01", 1f, 0) };
        gillR = new[] { Find("Gill_R_01", 1f, 0) };
        head = Find("Head", 1f, 0);
        lastYaw = transform.eulerAngles.y;
        segmentHeading = new float[spine.Length];
        jointBend = new float[spine.Length];
        for (int i = 0; i < spine.Length; i++) segmentHeading[i] = lastYaw;
    }

    Bone Find(string boneName, float weight, int index)
    {
        Transform t = FindDeep(skeletonRoot, boneName);
        if (t == null) Debug.LogWarning("[BettaSwimMotion] Missing bone " + boneName, this);
        Quaternion rest = t ? t.localRotation : Quaternion.identity;
        return new Bone { t = t, weight = weight, index = index, baseLocal = rest, lastWritten = rest };
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

        // Effort (from the AI's burst/coast cycle) sets how hard the tail strokes; beat
        // frequency follows actual swim speed, as in real fish, so a slow cruise isn't a
        // fast flutter. While gliding the tail calms down even though the fish still moves.
        float effort = fish ? fish.Effort : 0f;
        float speed01 = fish ? Mathf.Clamp01(fish.CurrentSpeed / Mathf.Max(0.01f, fish.swimSpeed)) : 0f;
        float beat = Mathf.Clamp01(speed01 * 0.7f + effort * 0.3f);
        const float TwoPi = Mathf.PI * 2f;
        bodyPhase += Mathf.Lerp(bodyIdleFrequency, bodySwimFrequency, beat) * TwoPi * dt;
        caudalPhase += Mathf.Lerp(caudalIdleFrequency, caudalSwimFrequency, beat) * TwoPi * dt;

        // Thrust peaks as the tail sweeps through the centre line (twice per beat);
        // mean stays 1 so average speed is unchanged.
        if (fish) fish.ThrustPulse = 1f + thrustPulse * effort * (Mathf.Abs(Mathf.Cos(caudalPhase)) * Mathf.PI * 0.5f - 1f);
        finPhase += pectoralFrequency * TwoPi * dt;
        float rest = fish && fish.IsResting ? 0f : effort;
        exertion = Mathf.MoveTowards(exertion, rest, (rest > exertion ? 0.5f : 0.08f) * dt);
        gillPhase += Mathf.Lerp(gillRestFrequency, gillActiveFrequency, exertion) * TwoPi * dt;

        float yaw = transform.eulerAngles.y;
        float rawYawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
        yawRate = Mathf.Lerp(yawRate, rawYawRate, 1f - Mathf.Exp(-8f * dt));
        lastYaw = yaw;

        // Follow the leader: each segment eases toward the heading of the segment in front.
        // The difference between neighbours is that joint's bend, so a turn travels down
        // the body as a C-curve instead of rotating the whole fish rigidly.
        float follow = 1f - Mathf.Exp(-segmentFollowRate * dt);
        float leader = yaw;
        for (int i = 0; i < spine.Length; i++)
        {
            float lag = Mathf.DeltaAngle(leader, segmentHeading[i]) * (1f - follow);
            lag = Mathf.Clamp(lag, -maxJointBend, maxJointBend);
            segmentHeading[i] = leader + lag;
            jointBend[i] = lag;
            leader = segmentHeading[i];
        }

        Vector3 up = transform.up;

        // The peduncle makes the active stroke; turning adds a stronger kick.
        float bodyAmplitude = Mathf.Lerp(bodyIdleAmplitude, bodySwimAmplitude, effort);
        float strokeAmplitude = Mathf.Lerp(strokeIdleAmplitude, strokeSwimAmplitude, effort)
            + Mathf.Min(maxTailKick, Mathf.Abs(yawRate) * turnTailKick);
        float stroke = Mathf.Sin(caudalPhase) * strokeAmplitude;
        for (int i = 0; i < spine.Length; i++)
        {
            float wave = Mathf.Sin(bodyPhase - spine[i].index * wavePhaseStep) * bodyAmplitude;
            float angle = wave * spine[i].weight + stroke * StrokeWeights[i] + jointBend[i];
            Apply(ref spine[i], Quaternion.AngleAxis(angle, up));
        }

        // Head swings slightly against each tail stroke (recoil), which sells the push.
        Apply(ref head, Quaternion.AngleAxis(-Mathf.Sin(caudalPhase) * headRecoil * effort, up));

        // Pectorals fan hardest when hovering; they tuck a little when the tail is working.
        // Hovering bettas beat them alternately (half a cycle apart), drifting toward in-phase
        // strokes while swimming; in a turn the outer fin beats harder.
        float pectoral = pectoralAmplitude * Mathf.Lerp(1f, 0.5f, effort);
        float offset = Mathf.PI * (1f - effort);
        float turnBias = Mathf.Clamp(yawRate / 60f, -1f, 1f) * pectoralTurnBias; // + = turning right, left fin is outer
        float left = Mathf.Sin(finPhase) * pectoral * (1f + turnBias);
        float right = Mathf.Sin(finPhase + offset) * pectoral * (1f - turnBias);
        for (int i = 0; i < pectoralL.Length; i++)
        {
            Apply(ref pectoralL[i], Quaternion.AngleAxis(left * pectoralL[i].weight, up));
            Apply(ref pectoralR[i], Quaternion.AngleAxis(-right * pectoralR[i].weight, up));
        }

        float gill = (Mathf.Sin(gillPhase) * 0.5f + 0.5f) * gillAmplitude * Mathf.Lerp(0.8f, 1.4f, exertion);
        Apply(ref gillL[0], Quaternion.AngleAxis(gill, up));
        Apply(ref gillR[0], Quaternion.AngleAxis(-gill, up));
    }

    // Applies a world-space rotation to a bone on top of this frame's base pose.
    // Bones the Animator doesn't key keep their old value, so reuse the cached base for
    // those instead of stacking rotation every frame.
    static void Apply(ref Bone bone, Quaternion worldRotation)
    {
        if (bone.t == null) return;
        if (Quaternion.Angle(bone.t.localRotation, bone.lastWritten) > 0.01f)
            bone.baseLocal = bone.t.localRotation;
        Transform parent = bone.t.parent;
        Quaternion parentRotation = parent ? parent.rotation : Quaternion.identity;
        Quaternion localDelta = Quaternion.Inverse(parentRotation) * worldRotation * parentRotation;
        bone.t.localRotation = localDelta * bone.baseLocal;
        bone.lastWritten = bone.t.localRotation;
    }
}
