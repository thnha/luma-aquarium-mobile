using UnityEngine;

// Procedural swim layered on top of the Animator (runs in LateUpdate):
// - a slow, small body wave on the rear spine (bettas keep the body fairly still),
// - path-following turns: the pivot (gills) is the head, and each spine segment takes the
//   heading the head had when it was where that segment is now (or a short time ago while
//   hovering), so the head turns first, then the body bends, and the tail turns last,
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
    // Below this speed the body trails the head by time as if moving this fast, so a fish
    // turning on the spot does not stay coiled; above it the body follows the head's path.
    public float trailFollowSpeed = 0.35f;    // m/s
    public float maxSegmentBend = 26f;       // degrees between neighbouring segments; more folds the peduncle
    [Range(0f, 1f)] public float pitchBendScale = 0.5f; // fish bend less up/down than sideways
    public float maxSegmentPitchBend = 10f;
    public float turnTailKick = 0.08f;       // extra caudal degrees per deg/s of turning
    public float maxTailKick = 12f;

    [Header("Display")]
    // Held broadside to a rival the body curls into a C, the fanned tail swung toward it,
    // with a sharp flick of the tail every so often (BettaFishAI.DisplayCurl).
    public float displayCurlAngle = 8f;      // degrees per spine joint at full curl
    public float displayFlickAngle = 10f;    // extra peduncle degrees at the peak of a flick
    public float displayFlickFrequency = 1.2f;
    // Tail beating (BettaFishAI.TailBeat): fast, wide lashes of the rear body toward the rival,
    // the head held still against them. Angles are per joint, so every fish on the shared rig
    // lashes in proportion whatever its size.
    public float tailBeatFrequency = 2.2f;
    public float tailBeatLashAngle = 24f;    // degrees at Tail_Base at the peak of a lash toward the rival
    [Range(0f, 1f)] public float tailBeatBias = 0.35f; // share of each lash held toward the rival
    public float tailBeatRecoil = 3f;        // head degrees against each lash

    [Header("Fins")]
    // Pectorals fan steadily when hovering and slow down / fold in as the fish swims faster.
    public float pectoralHoverFrequency = 2f;
    public float pectoralCruiseFrequency = 1.1f;
    public float pectoralHoverAmplitude = 16f;
    public float pectoralCruiseAmplitude = 6f;
    [Range(0f, 0.5f)] public float pectoralHoverLag = 0.1f; // cycles the right fin trails the left when hovering
    public float pectoralTurnBoost = 0.3f;   // outer fin beats harder in a turn
    public float gillRestFrequency = 0.7f;   // breathing slows when resting
    public float gillActiveFrequency = 1.6f; // and speeds up after exertion
    // Degrees the gill cover (Gill_*_02, as in the Flare clip) swings out about the body axis.
    // The cover skin carries only part of the bone weight, so this needs to be fairly large.
    public float gillOpenAngle = 12f;
    public float gillFlareAngle = 20f;       // extra, held open while displaying (BettaFishAI.FlareAmount)

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
    // Share of the display curl per spine joint: the C is deepest toward the tail.
    static readonly float[] CurlWeights = { 0.3f, 0.6f, 1f, 1f, 0.8f };
    // Share of a tail-beat lash per spine joint: the rear half of the body swings the tail.
    static readonly float[] TailBeatWeights = { 0f, 0.1f, 0.35f, 0.7f, 1f };

    // Rear spine: weight scales body wave and turn bend, index sets the wave delay.
    static readonly (string name, float weight, int index)[] SpineChain =
    {
        ("Spine_01", 0.05f, 0), ("Spine_02", 0.1f, 1), ("Spine_03", 0.2f, 2), ("Spine_04", 0.35f, 3), ("Tail_Base", 0.5f, 4),
    };
    Bone[] spine, pectoralL, pectoralR, gillL, gillR;
    Bone head;
    struct HeadSample { public float time, yaw, pitch, path; }
    const int HistorySize = 1024;          // ~3 s of tail delay at 300 fps
    readonly HeadSample[] history = new HeadSample[HistorySize];
    int historyCount, historyHead;           // ring buffer, newest at historyHead
    float pathLength;                        // distance the head has travelled
    Vector3 lastPosition;
    float[] segmentDistance;                 // local units from the head pivot to each segment's middle
    float[] jointDistance;                   // local units from the head pivot to each segment's joint
    Vector3[] bodyLine;
    float[] jointBend, jointPitch;
    float bodyPhase, caudalPhase, tailBeatPhase, finPhase, gillPhase, lastYaw, yawRate;
    float gillSideL, gillSideR;              // +1 on the fish's right, -1 on its left: the opening direction
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
        // The cover opens by rolling Gill_*_02 about the body axis so its lower edge flares
        // out; swinging Gill_*_01 about the vertical barely moves the cover skin.
        gillL = new[] { Find("Gill_L_02", 1f, 0) };
        gillR = new[] { Find("Gill_R_02", 1f, 0) };
        gillSideL = GillSide(gillL[0]);
        gillSideR = GillSide(gillR[0]);
        head = Find("Head", 1f, 0);
        lastYaw = transform.eulerAngles.y;
        lastPosition = transform.position;
        jointBend = new float[spine.Length];
        jointPitch = new float[spine.Length];

        // Where each segment sits behind the head, measured on the rest pose: the middle
        // between its joint and the next one (the last segment extends as far again).
        float scale = Mathf.Max(0.0001f, transform.lossyScale.x);
        segmentDistance = new float[spine.Length];
        jointDistance = new float[spine.Length];
        bodyLine = new Vector3[spine.Length + 1];
        for (int i = 0; i < spine.Length; i++)
        {
            float start = spine[i].t ? Vector3.Distance(transform.position, spine[i].t.position) / scale : 0f;
            jointDistance[i] = start;
            float end = i + 1 < spine.Length && spine[i + 1].t ? Vector3.Distance(transform.position, spine[i + 1].t.position) / scale
                : start + (i > 0 ? start - segmentDistance[i - 1] : 0.02f);
            segmentDistance[i] = (start + end) * 0.5f;
        }
        if (fish) fish.BodyLength = segmentDistance[spine.Length - 1] * scale;
    }

    float GillSide(Bone gill) =>
        gill.t && Vector3.Dot(gill.t.position - transform.position, transform.right) < 0f ? -1f : 1f;

    void RecordHead(float yaw, float pitch)
    {
        pathLength += Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;
        historyHead = (historyHead + 1) % HistorySize;
        history[historyHead] = new HeadSample { time = Time.time, yaw = yaw, pitch = pitch, path = pathLength };
        historyCount = Mathf.Min(historyCount + 1, HistorySize);
    }

    // Heading the head had when it was `distance` back along its path, or `delay` seconds
    // ago, whichever comes first (a hovering fish barely moves, so time takes over).
    HeadSample PastHead(float distance, float delay)
    {
        HeadSample newer = history[historyHead];
        for (int n = 1; n < historyCount; n++)
        {
            HeadSample older = history[(historyHead - n + HistorySize) % HistorySize];
            float pathBack = pathLength - older.path, timeBack = Time.time - older.time;
            if (pathBack >= distance || timeBack >= delay)
            {
                float newerPath = pathLength - newer.path, newerTime = Time.time - newer.time;
                // Interpolate to whichever limit was crossed first between the two samples.
                float t = 1f;
                if (pathBack >= distance) t = Mathf.Min(t, Mathf.InverseLerp(newerPath, pathBack, distance));
                if (timeBack >= delay) t = Mathf.Min(t, Mathf.InverseLerp(newerTime, timeBack, delay));
                return new HeadSample
                {
                    yaw = Mathf.LerpAngle(newer.yaw, older.yaw, t),
                    pitch = Mathf.LerpAngle(newer.pitch, older.pitch, t),
                };
            }
            newer = older;
        }
        return newer;
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
        // Swimming = moving or pushing with the tail: the faster, the slower and smaller the fanning.
        float cruise = Mathf.Max(speed01, effort);
        finPhase += Mathf.Lerp(pectoralHoverFrequency, pectoralCruiseFrequency, cruise) * TwoPi * dt;
        float rest = fish && fish.IsResting ? 0f : effort;
        exertion = Mathf.MoveTowards(exertion, rest, (rest > exertion ? 0.5f : 0.08f) * dt);
        gillPhase += Mathf.Lerp(gillRestFrequency, gillActiveFrequency, exertion) * TwoPi * dt;

        float yaw = transform.eulerAngles.y;
        float rawYawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
        yawRate = Mathf.Lerp(yawRate, rawYawRate, 1f - Mathf.Exp(-8f * dt));
        lastYaw = yaw;

        // Path following: each segment points the way the head pointed when it was there,
        // so a turn starts at the head and travels down the body to the tail. The difference
        // between neighbours is that joint's bend.
        float pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
        RecordHead(yaw, pitch);
        float scale = transform.lossyScale.x;
        float leaderYaw = yaw, leaderPitch = pitch;
        for (int i = 0; i < spine.Length; i++)
        {
            float distance = segmentDistance[i] * scale;
            HeadSample past = PastHead(distance, distance / Mathf.Max(0.01f, trailFollowSpeed));
            jointBend[i] = Mathf.Clamp(Mathf.DeltaAngle(leaderYaw, past.yaw), -maxSegmentBend, maxSegmentBend);
            jointPitch[i] = Mathf.Clamp(Mathf.DeltaAngle(leaderPitch, past.pitch) * pitchBendScale, -maxSegmentPitchBend, maxSegmentPitchBend);
            leaderYaw += jointBend[i];
            leaderPitch += jointPitch[i] / Mathf.Max(0.01f, pitchBendScale);
        }

        Vector3 up = transform.up;
        Vector3 side = transform.right;

        // Tell the AI where the body is for walls and decor: the joints laid out along the bent
        // spine, then the caudal fin tip. The stroke and body wave are left out (the AI keeps a
        // margin for them), so the check doesn't flicker with every beat.
        if (fish)
        {
            Vector3 point = -transform.forward * (jointDistance[0] * scale);
            float turn = 0f;
            for (int i = 0; i < spine.Length; i++)
            {
                bodyLine[i] = point;
                turn += jointBend[i];
                float next = i + 1 < spine.Length ? jointDistance[i + 1]
                    : Mathf.Max(jointDistance[i], fish.tailReachBodyLengths * fish.BodyLength / Mathf.Max(0.0001f, scale));
                point -= Quaternion.AngleAxis(turn, up) * transform.forward * ((next - jointDistance[i]) * scale);
            }
            bodyLine[spine.Length] = point;
            fish.BodyLine = bodyLine;
        }

        // The peduncle makes the active stroke; turning adds a stronger kick.
        float bodyAmplitude = Mathf.Lerp(bodyIdleAmplitude, bodySwimAmplitude, effort);
        float strokeAmplitude = Mathf.Lerp(strokeIdleAmplitude, strokeSwimAmplitude, effort)
            + Mathf.Min(maxTailKick, Mathf.Abs(yawRate) * turnTailKick);
        // Tail beating takes over from the swim stroke and the display flicks.
        float tailBeat = fish ? fish.TailBeat : 0f;
        float calm = 1f - Mathf.Abs(tailBeat);
        float stroke = Mathf.Sin(caudalPhase) * strokeAmplitude * calm;
        // A negative bend lays the tail out to the fish's right (as after turning right).
        float curl = fish ? fish.DisplayCurl : 0f;
        float flick = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * displayFlickFrequency * 2f * Mathf.PI)), 6f);
        stroke -= curl * flick * displayFlickAngle * calm;
        // Each lash swings well out toward the rival and only partly back.
        tailBeatPhase = Mathf.Abs(tailBeat) > 0.01f ? tailBeatPhase + tailBeatFrequency * TwoPi * dt : 0f;
        float lash = tailBeat * tailBeatLashAngle * (tailBeatBias + (1f - tailBeatBias) * Mathf.Sin(tailBeatPhase));
        for (int i = 0; i < spine.Length; i++)
        {
            float wave = Mathf.Sin(bodyPhase - spine[i].index * wavePhaseStep) * bodyAmplitude;
            float angle = wave * spine[i].weight + stroke * StrokeWeights[i] + jointBend[i]
                - curl * displayCurlAngle * CurlWeights[i] - lash * TailBeatWeights[i];
            Apply(ref spine[i], Quaternion.AngleAxis(angle, up) * Quaternion.AngleAxis(jointPitch[i], side));
        }

        // Head swings slightly against each tail stroke (recoil), which sells the push.
        float recoil = -Mathf.Sin(caudalPhase) * headRecoil * effort * calm + lash / Mathf.Max(0.01f, tailBeatLashAngle) * tailBeatRecoil;
        Apply(ref head, Quaternion.AngleAxis(recoil, up));

        // Pectorals fan hardest when hovering and fold in while cruising. Both fins beat nearly
        // together (a slight lag when hovering, in step when swimming); in a turn the outer fin
        // beats harder.
        float pectoral = Mathf.Lerp(pectoralHoverAmplitude, pectoralCruiseAmplitude, cruise);
        float offset = pectoralHoverLag * TwoPi * (1f - cruise);
        float turnBias = Mathf.Clamp(yawRate / 60f, -1f, 1f) * pectoralTurnBoost; // + = turning right, left fin is outer
        float left = Mathf.Sin(finPhase) * pectoral * (1f + turnBias);
        float right = Mathf.Sin(finPhase + offset) * pectoral * (1f - turnBias);
        for (int i = 0; i < pectoralL.Length; i++)
        {
            Apply(ref pectoralL[i], Quaternion.AngleAxis(left * pectoralL[i].weight, up));
            Apply(ref pectoralR[i], Quaternion.AngleAxis(-right * pectoralR[i].weight, up));
        }

        // A positive roll about the forward axis swings the part below the pivot to the right.
        float gill = (Mathf.Sin(gillPhase) * 0.5f + 0.5f) * gillOpenAngle * Mathf.Lerp(0.8f, 1.4f, exertion)
            + (fish ? fish.FlareAmount * gillFlareAngle : 0f);
        Vector3 bodyAxis = transform.forward;
        Apply(ref gillL[0], Quaternion.AngleAxis(gill * gillSideL, bodyAxis));
        Apply(ref gillR[0], Quaternion.AngleAxis(gill * gillSideR, bodyAxis));
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
