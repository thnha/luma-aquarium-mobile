using UnityEngine;

// Soft-body fins ("dynamic bones"). The tip of every fin ray is a point mass on a soft spring
// back to its animated rest pose. It keeps its world position when the body moves, so fins
// stream back while swimming, flick forward when braking, sweep out on turns, droop when slow, and the
// peduncle stroke from BettaSwimMotion travels out to the fin edges as a wave.
// Faster swimming stiffens the fins (water pressure holds them back), so they stream calmly
// instead of flapping harder as speed rises.
// Runs after BettaSwimMotion so it sees the final body pose.
[DefaultExecutionOrder(100)]
public class BettaFinPhysics : MonoBehaviour
{
    public Transform skeletonRoot;
    public BettaFishAI fish;

    [System.Serializable]
    public class FinSettings
    {
        [Range(0.005f, 0.5f)] public float rootStiffness = 0.12f; // share of offset pulled back per 60 Hz step
        [Range(0.005f, 0.5f)] public float tipStiffness = 0.04f;
        [Range(0f, 0.5f)] public float damping = 0.12f;          // velocity lost per 60 Hz step
        public float maxAngle = 30f;                              // per joint; a chain of N joints can curl up to N x this
        public float ripple = 0.004f;                             // metres of idle water ripple at the tip
        public float speedStiffening = 1.5f;                      // stiffness/damping multiplier at full swim speed
        public float sag = 0f;                                    // droop under its own weight when slow (share of bone length)
    }

    public FinSettings caudal = new FinSettings { rootStiffness = 0.1f, tipStiffness = 0.035f, damping = 0.18f, maxAngle = 22f, ripple = 0.005f, speedStiffening = 1.3f, sag = 0.15f };
    public FinSettings dorsalAnal = new FinSettings { rootStiffness = 0.14f, tipStiffness = 0.06f, damping = 0.24f, maxAngle = 22f, ripple = 0.002f, speedStiffening = 2.5f, sag = 0.08f };
    public FinSettings pelvic = new FinSettings { rootStiffness = 0.16f, tipStiffness = 0.06f, damping = 0.18f, maxAngle = 35f, ripple = 0.002f, speedStiffening = 2f, sag = 0.2f };
    public FinSettings pectoral = new FinSettings { rootStiffness = 0.2f, tipStiffness = 0.1f, damping = 0.16f, maxAngle = 35f, ripple = 0.002f, speedStiffening = 1.5f };
    [Tooltip("Extra push of water against the swim direction; makes fins stream back more while moving.")]
    public float flowDrag = 0.5f;

    enum Group { Caudal, DorsalAnal, Pelvic, Pectoral }

    // Chains listed root -> tip so parents are solved before children.
    static readonly (Group group, string[] bones)[] Chains =
    {
        (Group.Caudal, Chain("Tail_Top_", 5)),
        (Group.Caudal, Chain("Tail_UpMid_", 5)),
        (Group.Caudal, Chain("Tail_", 5)),
        (Group.Caudal, Chain("Tail_LowMid_", 5)),
        (Group.Caudal, Chain("Tail_Low_", 5)),
        (Group.Caudal, Chain("Tail_Bot_", 5)),
        (Group.DorsalAnal, Chain("Dorsal_A_", 4)),
        (Group.DorsalAnal, Chain("Dorsal_B_", 4)),
        (Group.DorsalAnal, Chain("Anal_A_", 4)),
        (Group.DorsalAnal, Chain("Anal_B_", 4)),
        (Group.Pelvic, Chain("Pelvic_L_", 3)),
        (Group.Pelvic, Chain("Pelvic_R_", 3)),
        (Group.Pectoral, new[] { "Pectoral_L_02", "Pectoral_L_03A" }),
        (Group.Pectoral, new[] { "Pectoral_L_03B" }),
        (Group.Pectoral, new[] { "Pectoral_R_02", "Pectoral_R_03A" }),
        (Group.Pectoral, new[] { "Pectoral_R_03B" }),
    };

    // "Tail_", 5 -> Tail_01 .. Tail_05
    static string[] Chain(string prefix, int count)
    {
        var names = new string[count];
        for (int i = 0; i < count; i++) names[i] = prefix + (i + 1).ToString("00");
        return names;
    }

    class Node
    {
        public Transform t;
        public Group group;
        public float depth;                  // 0 at chain root, 1 at tip
        public Vector3 tipLocal;             // tip of this bone in its own space
        public float length;
        public Vector3 p, pp;                // tip particle (current, previous)
        public Quaternion baseLocal, lastWritten;
        public float phase;
    }

    Node[] nodes;
    Vector3 lastRootPosition;
    bool initialized;
    float time;

    void Awake()
    {
        if (skeletonRoot == null) skeletonRoot = transform;
        if (fish == null) fish = GetComponentInParent<BettaFishAI>();
        var list = new System.Collections.Generic.List<Node>();
        foreach (var chain in Chains)
        {
            for (int i = 0; i < chain.bones.Length; i++)
            {
                Transform t = FindDeep(skeletonRoot, chain.bones[i]);
                if (t == null) { Debug.LogWarning("[BettaFinPhysics] Missing bone " + chain.bones[i], this); continue; }
                // Tip = next bone in the chain, or the bone extended along its parent direction.
                Transform next = i + 1 < chain.bones.Length ? FindDeep(skeletonRoot, chain.bones[i + 1]) : null;
                Vector3 tipWorld = next ? next.position : t.position + (t.position - t.parent.position);
                if ((tipWorld - t.position).sqrMagnitude < 1e-8f) tipWorld = t.position + t.parent.rotation * Vector3.back * 0.02f;
                list.Add(new Node
                {
                    t = t,
                    group = chain.group,
                    depth = chain.bones.Length > 1 ? (float)i / (chain.bones.Length - 1) : 1f,
                    tipLocal = t.InverseTransformPoint(tipWorld),
                    length = Vector3.Distance(t.position, tipWorld),
                    baseLocal = t.localRotation,
                    lastWritten = t.localRotation,
                    phase = list.Count * 1.37f,
                });
            }
        }
        nodes = list.ToArray();
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

    FinSettings Settings(Group g) => g == Group.Caudal ? caudal : g == Group.DorsalAnal ? dorsalAnal : g == Group.Pelvic ? pelvic : pectoral;

    void OnEnable() { initialized = false; }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || nodes == null) return;
        time += dt;

        Vector3 rootVelocity = (transform.position - lastRootPosition) / dt;
        lastRootPosition = transform.position;
        bool reset = !initialized || rootVelocity.sqrMagnitude > 25f; // first frame or teleport
        initialized = true;

        int steps = Mathf.Clamp(Mathf.RoundToInt(dt * 60f), 1, 4);
        float stepScale = dt * 60f / steps;  // 1 at 60 fps per step
        Vector3 flow = -rootVelocity * flowDrag * (dt / steps);
        float speed01 = fish ? Mathf.Clamp01(fish.CurrentSpeed / Mathf.Max(0.01f, fish.swimSpeed)) : 0f;

        foreach (Node n in nodes)
        {
            // Restore this frame's animated pose (Animator-written, or the cached one for
            // bones the clips don't key) before simulating on top of it.
            if (Quaternion.Angle(n.t.localRotation, n.lastWritten) > 0.01f) n.baseLocal = n.t.localRotation;
            n.t.localRotation = n.baseLocal;

            Vector3 origin = n.t.position;
            Vector3 target = n.t.TransformPoint(n.tipLocal);
            FinSettings s = Settings(n.group);
            // Long betta fins are heavy: they droop when the fish is slow and only extend
            // once water flow holds them out.
            target += Vector3.down * (n.length * s.sag * (0.3f + 0.7f * n.depth) * (1f - speed01));
            if (reset) { n.p = n.pp = target; n.lastWritten = n.t.localRotation; continue; }

            float firm = Mathf.Lerp(1f, s.speedStiffening, speed01);
            float k = Mathf.Min(0.9f, Mathf.Lerp(s.rootStiffness, s.tipStiffness, n.depth) * firm);
            float stiffness = 1f - Mathf.Pow(1f - k, stepScale);
            float keep = Mathf.Pow(1f - Mathf.Min(0.9f, s.damping * firm), stepScale);
            Vector3 ripple = (n.t.right * Mathf.Sin(time * 2.3f + n.phase) + n.t.forward * Mathf.Sin(time * 1.7f + n.phase * 0.7f))
                * s.ripple * n.depth;

            for (int i = 0; i < steps; i++)
            {
                Vector3 velocity = (n.p - n.pp) * keep;
                n.pp = n.p;
                n.p += velocity + flow;
                n.p += (target + ripple - n.p) * stiffness;
            }

            // Keep bone length and limit how far the ray can swing from its rest direction.
            Vector3 restDir = target - origin;
            Vector3 dir = n.p - origin;
            if (dir.sqrMagnitude < 1e-10f) dir = restDir;
            float angle = Vector3.Angle(restDir, dir);
            if (angle > s.maxAngle) dir = Vector3.Slerp(restDir.normalized, dir.normalized, s.maxAngle / angle);
            dir = dir.normalized * n.length;
            n.p = origin + dir;

            n.t.rotation = Quaternion.FromToRotation(restDir, dir) * n.t.rotation;
            n.lastWritten = n.t.localRotation;
        }
    }
}
