using System.Collections.Generic;
using System.Linq;
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
        public float maxAngle = 30f;                              // total additional bend along a complete ray
        public float ripple = 0.004f;                             // metres of idle water ripple at the tip
        public float speedStiffening = 1.5f;                      // stiffness/damping multiplier at full swim speed
        public float sag = 0f;                                    // droop under its own weight when slow (share of bone length)

        public FinSettings Clone() => (FinSettings)MemberwiseClone();
    }

    public FinSettings caudal = new FinSettings { rootStiffness = 0.1f, tipStiffness = 0.035f, damping = 0.18f, maxAngle = 22f, ripple = 0.005f, speedStiffening = 1.3f, sag = 0.15f };
    public FinSettings dorsalAnal = new FinSettings { rootStiffness = 0.14f, tipStiffness = 0.06f, damping = 0.24f, maxAngle = 22f, ripple = 0.002f, speedStiffening = 2.5f, sag = 0.08f };
    public FinSettings pelvic = new FinSettings { rootStiffness = 0.16f, tipStiffness = 0.06f, damping = 0.18f, maxAngle = 35f, ripple = 0.002f, speedStiffening = 2f, sag = 0.2f };
    public FinSettings pectoral = new FinSettings { rootStiffness = 0.2f, tipStiffness = 0.1f, damping = 0.16f, maxAngle = 35f, ripple = 0.002f, speedStiffening = 1.5f };
    [Tooltip("Extra push of water against the swim direction; makes fins stream back more while moving.")]
    public float flowDrag = 0.5f;
    [Tooltip("Degrees the dorsal fin rises when excited; it folds by the same amount at rest. Anal fin follows at half.")]
    public float dorsalRaise = 12f;
    [Tooltip("Degrees the outermost caudal rays open (+) or close (-) relative to the model; the rays between follow in proportion.")]
    public float caudalSpread;
    [Tooltip("Degrees the whole caudal fin hangs down.")]
    public float caudalDroop;
    [Tooltip("Extra degrees the caudal rays fan open while flaring (BettaFishAI.FlareAmount).")]
    public float flareSpread = 28f;
    [Tooltip("Extra degrees the dorsal fin rises while flaring (anal fin at half), on top of the excited raise.")]
    public float flareRaise = 14f;
    [Tooltip("Share of the caudal fan's opening that closes when hunger folds the fins in (BettaFishAI.FinClamp): at a full fold 1 - foldClose of the angle stays open. The dorsal and anal fold as at rest.")]
    [Range(0f, 0.95f)] public float foldClose = 0.35f;
    [Tooltip("How far the caudal and anal membranes pleat where their rays close together: 0 = squashed flat, 1 = the rays tilt out of the fin plane in turn so the membrane keeps its width as zig-zag folds.")]
    [Range(0f, 1f)] public float pleatAmount = 0.5f;
    [Tooltip("Root bones of chains not simulated (they follow the animation), e.g. the crowntail spike chains (set by PipelineFishImporter).")]
    public string[] skippedChains = new string[0];
    [Tooltip("How far each caudal ray is pulled toward its neighbours' bend (0 = rays move freely). Neighbouring ray chains " +
             "bending apart (clips, plus the simulation) creased the membrane along their weight seam. The intended fan " +
             "(caudal spread, droop, flare) is kept.")]
    [Range(0f, 1f)] public float caudalCoupling = 0.6f;
    [Tooltip("The same for the ray chains of the dorsal fin (A front, B back; most clips hold them ~20 deg apart) and of the anal fin.")]
    [Range(0f, 1f)] public float dorsalAnalCoupling = 0.9f;

    enum Group { Caudal, DorsalAnal, Pelvic, Pectoral }

    // Ray chains of one fin side by side (top / front first), so CoupleRays and Pleat can tie neighbours together.
    // Fish are built on the fin-ray templates (M3R/M4R): Tail_R01..12 and Anal_R01..06.
    static readonly (Group group, bool pleats, string[] roots)[] Fins =
    {
        (Group.Caudal, true, RayRoots("Tail_R", 12)),
        (Group.DorsalAnal, false, new[] { "Dorsal_A_01", "Dorsal_B_01" }),
        (Group.DorsalAnal, true, RayRoots("Anal_R", 6)),
    };

    // Chains listed root -> tip so parents are solved before children.
    // Crowntail spikes hang off the ray bones, so they come after the ray chains.
    static readonly (Group group, string[] bones)[] Chains = new (Group, string[])[]
    {
        (Group.DorsalAnal, Chain("Dorsal_A_", 4)),
        (Group.DorsalAnal, Chain("Dorsal_B_", 4)),
        (Group.Pelvic, Chain("Pelvic_L_", 3)),
        (Group.Pelvic, Chain("Pelvic_R_", 3)),
        (Group.Pectoral, new[] { "Pectoral_L_02", "Pectoral_L_03A" }),
        (Group.Pectoral, new[] { "Pectoral_L_03B" }),
        (Group.Pectoral, new[] { "Pectoral_R_02", "Pectoral_R_03A" }),
        (Group.Pectoral, new[] { "Pectoral_R_03B" }),
    }
        .Concat(Rays(Group.Caudal, "Tail_R", 12, 5))
        .Concat(Rays(Group.DorsalAnal, "Anal_R", 6, 4))
        .Concat(Rays(Group.DorsalAnal, "Dorsal_Crown_", 9, 3))
        .ToArray();

    // Root bones of the Crowntail spike chains; tail types without the Crowntail meshes skip them.
    public static readonly string[] CrownChainRoots =
        Chains.Select(c => c.bones[0]).Where(IsCrown).ToArray();

    static bool IsCrown(string bone) => bone.Contains("_Crown_");

    // "Tail_R", 12, 5 -> Tail_R01_01 .. Tail_R01_05, ..., Tail_R12_01 .. Tail_R12_05 (crown spikes: 3 bones each)
    static IEnumerable<(Group, string[])> Rays(Group group, string prefix, int rays, int bones)
    {
        for (int i = 1; i <= rays; i++) yield return (group, Chain(prefix + i.ToString("00") + "_", bones));
    }

    static string[] RayRoots(string prefix, int rays) =>
        Enumerable.Range(1, rays).Select(i => prefix + i.ToString("00") + "_01").ToArray();

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
        public float bendShare;
        public float raise;                  // dorsal root +1, anal roots -0.65 front .. -0.5 back, else 0 (+ about the side axis tips a ray forward)
        public float fanAngle;               // caudal roots: degrees from the fan's centre about the side axis (model pose), else 0
        public bool caudalRoot;
        public Quaternion restLocal;         // model pose, the reference CoupleRays measures bends from
        public float intent;                 // degrees about the side axis this frame (raise + caudal droop/spread), roots only
    }

    Node[] nodes;
    Node[][][] fins;                         // fin -> ray chain (side by side) -> bone (root -> tip)
    Group[] finGroup;
    bool[] finPleats;
    float[][] restGap;                       // fin -> gap between neighbouring ray tips in the fin plane, model pose
    float caudalFanMax = 1f;                 // degrees from the caudal fan's centre to its outermost ray
    Vector3 lastRootPosition;
    bool initialized;
    float time;
    float arousal;

    void Awake() { Rebind(); }

    // Re-reads the fin chains. Call after changing fin bone scale, since bone lengths are measured here.
    public void Rebind()
    {
        if (skeletonRoot == null) skeletonRoot = transform;
        if (fish == null) fish = GetComponentInParent<BettaFishAI>();
        var list = new List<Node>();
        int ray = 0;
        foreach (var chain in Chains)
        {
            if (skippedChains != null && System.Array.IndexOf(skippedChains, chain.bones[0]) >= 0) continue;
            // Chains this fish doesn't have (e.g. Dorsal_Crown on a non-Crowntail) are skipped quietly.
            if (FindDeep(skeletonRoot, chain.bones[0]) == null) continue;
            bool crown = IsCrown(chain.bones[0]);
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
                    restLocal = t.localRotation,
                    // Neighbouring rays share a travelling wave, rather than pulling the
                    // same membrane in unrelated directions. Tips lag behind their roots.
                    phase = ray * .18f + i * .65f,
                    bendShare = Mathf.Lerp(.35f, 1f, chain.bones.Length > 1 ? i / (float)(chain.bones.Length - 1) : 1f),
                    // Spikes inherit raise from the ray they hang off.
                    raise = i > 0 || crown ? 0f : chain.bones[0].StartsWith("Dorsal") ? 1f : chain.bones[0].StartsWith("Anal") ? -0.5f : 0f,
                    caudalRoot = i == 0 && !crown && chain.group == Group.Caudal,
                });
            }
            ray++;
        }
        // Branches (pectoral tips and crowntail spikes) inherit their parent ray's bend.
        // Share one budget along the actual skeleton path, including those parents.
        var byTransform = list.ToDictionary(n => n.t);
        var pathTotals = new Dictionary<Node, float>();
        foreach (Node tip in list)
        {
            var path = new List<Node>();
            float total = 0f;
            for (Transform t = tip.t; t; t = t.parent)
                if (byTransform.TryGetValue(t, out Node ancestor) && ancestor.group == tip.group)
                { path.Add(ancestor); total += ancestor.bendShare; }
            foreach (Node ancestor in path)
                pathTotals[ancestor] = Mathf.Max(pathTotals.TryGetValue(ancestor, out float old) ? old : 0f, total);
        }
        foreach (Node n in list) n.bendShare /= pathTotals[n];
        nodes = list.ToArray();
        foreach (string fin in new[] { "Tail_", "Dorsal_", "Anal_", "Pelvic_", "Pectoral_" })
            if (!list.Any(n => n.t.name.StartsWith(fin)))
                Debug.LogWarning("[BettaFinPhysics] No ray chain found for the " + fin.TrimEnd('_') + " fin", this);
        // Each fin's ray chains, walked root -> tip through the simulated bones; a fin needs two chains to couple.
        var byName = list.ToDictionary(n => n.t.name);
        var found = Fins.Select(fin => (fin.group, fin.pleats, chains: fin.roots.Where(byName.ContainsKey).Select(root =>
        {
            var chain = new List<Node> { byName[root] };
            string prefix = root.Substring(0, root.Length - 2);
            for (int i = 2; byName.TryGetValue(prefix + i.ToString("00"), out Node next) && next.t.parent == chain[chain.Count - 1].t; i++)
                chain.Add(next);
            return chain.ToArray();
        }).ToArray())).Where(f => f.chains.Length > 1).ToArray();
        fins = found.Select(f => f.chains).ToArray();
        finGroup = found.Select(f => f.group).ToArray();
        finPleats = found.Select(f => f.pleats).ToArray();
        Vector3 lateral = transform.right;
        restGap = fins.Select(fin => Enumerable.Range(0, fin.Length - 1)
            .Select(c => Vector3.ProjectOnPlane(Tip(fin[c + 1]) - Tip(fin[c]), lateral).magnitude).ToArray()).ToArray();
        MeasureFans(lateral);
        initialized = false;
    }

    // Caudal: each ray's angle from the fan's centre, so the fan opens and closes in proportion (one factor on every
    // angle keeps the rays evenly spread). Anal: the front rays tip 30% further than the back ones, so folding closes
    // the fin onto its back rays (toward the tail) and flaring opens it.
    void MeasureFans(Vector3 lateral)
    {
        caudalFanMax = 1f;
        for (int f = 0; f < fins.Length; f++)
        {
            Node[][] fin = fins[f];
            if (finGroup[f] == Group.Caudal)
            {
                Vector3 first = Vector3.ProjectOnPlane(Tip(fin[0]) - fin[0][0].t.position, lateral);
                float[] angle = fin.Select(c => Vector3.SignedAngle(first, Vector3.ProjectOnPlane(Tip(c) - c[0].t.position, lateral), lateral)).ToArray();
                float centre = (angle[0] + angle[angle.Length - 1]) * 0.5f;
                for (int c = 0; c < fin.Length; c++) fin[c][0].fanAngle = angle[c] - centre;
                caudalFanMax = Mathf.Max(1f, fin.Max(c => Mathf.Abs(c[0].fanAngle)));
            }
            else if (fin[0][0].t.name.StartsWith("Anal"))
                for (int c = 0; c < fin.Length; c++) fin[c][0].raise = -0.5f * (1f + 0.3f * (1f - (float)c / (fin.Length - 1)));
        }
    }

    static Vector3 Tip(Node[] chain)
    {
        Node last = chain[chain.Length - 1];
        return last.t.TransformPoint(last.tipLocal);
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
        // Mood: fins rise when excited (food, air, flaring) and fold flat at rest.
        arousal = Mathf.MoveTowards(arousal, fish ? fish.Arousal : 0f, 1.5f * dt);
        // Tired from hunger the fins fold in now and then; flaring fans the tail wide.
        float fold = fish ? fish.FinClamp : 0f;
        // Flaring, every fin is held fully erect: no drooping at low speed, and stiffer as if swimming.
        float flare = fish ? fish.FlareAmount : 0f;
        float raiseAngle = Mathf.Max(-1f, arousal - fold * 2f) * dorsalRaise + flare * flareRaise;
        // Caudal fan: every ray's angle from the fan's centre scales by one factor, so the rays stay evenly spread.
        float fanScale = Mathf.Max(0.05f, 1f + (caudalSpread + flare * flareSpread) / caudalFanMax - fold * foldClose);
        float hold = Mathf.Max(speed01, flare * 0.8f);
        Vector3 lateral = transform.right;

        foreach (Node n in nodes)
        {
            // Restore this frame's animated pose (Animator-written, or the cached one for
            // bones the clips don't key) before simulating on top of it.
            if (Quaternion.Angle(n.t.localRotation, n.lastWritten) > 0.01f) n.baseLocal = n.t.localRotation;
            n.t.localRotation = n.baseLocal;
            // Rotating about the body's side axis tips the fin rays up (raise) or back flat (fold).
            if (n.raise != 0f) n.t.rotation = Quaternion.AngleAxis(raiseAngle * n.raise, lateral) * n.t.rotation;
            // Tail form, relative to the Halfmoon model: scale the fan wider or narrower about its centre, hang the fin (Veiltail).
            float fan = n.caudalRoot ? -caudalDroop + (fanScale - 1f) * n.fanAngle : 0f;
            if (n.caudalRoot) n.t.rotation = Quaternion.AngleAxis(fan, lateral) * n.t.rotation;
            n.intent = raiseAngle * n.raise + fan;

            Vector3 origin = n.t.position;
            Vector3 target = n.t.TransformPoint(n.tipLocal);
            FinSettings s = Settings(n.group);
            // Long betta fins are heavy: they droop when the fish is slow and only extend
            // once water flow holds them out.
            target += Vector3.down * (n.length * s.sag * (0.3f + 0.7f * n.depth) * (1f - hold));
            if (reset) { n.p = n.pp = target; n.lastWritten = n.t.localRotation; continue; }

            float firm = Mathf.Lerp(1f, s.speedStiffening, hold);
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
            float limit = Mathf.Max(0f, s.maxAngle) * n.bendShare;
            if (angle > limit) dir = Vector3.Slerp(restDir.normalized, dir.normalized, limit / angle);
            dir = dir.normalized * n.length;
            n.p = origin + dir;

            n.t.rotation = Quaternion.FromToRotation(restDir, dir) * n.t.rotation;
            n.lastWritten = n.t.localRotation;
        }
        CoupleRays(lateral);
        Pleat(lateral);
    }

    // Neighbouring ray chains of a fin share one membrane, but the clips bend them apart (Dorsal_A vs Dorsal_B up to
    // ~25 deg in Idle) and the simulation adds its own differences, so the skin folded along the weight seam
    // between them. Each bone's bend from its reference pose (model pose plus this frame's intended raise and
    // caudal spread) is pulled toward the average of its own and its neighbours' bends by caudal/dorsalAnalCoupling.
    void CoupleRays(Vector3 lateral)
    {
        if (fins == null) return;
        for (int f = 0; f < fins.Length; f++)
        {
            Node[][] fin = fins[f];
            float coupling = finGroup[f] == Group.Caudal ? caudalCoupling : dorsalAnalCoupling;
            if (coupling <= 0f) continue;
            int length = fin.Max(c => c.Length);
            var reference = new Quaternion[fin.Length, length];
            var bend = new Quaternion[fin.Length, length];
            for (int c = 0; c < fin.Length; c++)
                for (int i = 0; i < fin[c].Length; i++)
                {
                    Node n = fin[c][i];
                    reference[c, i] = i == 0
                        ? Quaternion.AngleAxis(n.intent, lateral) * n.t.parent.rotation * n.restLocal
                        : reference[c, i - 1] * n.restLocal;
                    bend[c, i] = n.t.rotation * Quaternion.Inverse(reference[c, i]);
                }
            // Targets first: writing a parent moves its children.
            var target = new Quaternion[fin.Length, length];
            for (int c = 0; c < fin.Length; c++)
                for (int i = 0; i < fin[c].Length; i++)
                {
                    Quaternion own = bend[c, i];
                    Vector4 sum = new Vector4(own.x, own.y, own.z, own.w);
                    for (int d = -1; d <= 1; d += 2)
                    {
                        int k = c + d;
                        if (k < 0 || k >= fin.Length || i >= fin[k].Length) continue;
                        Quaternion q = bend[k, i];
                        float sign = Quaternion.Dot(own, q) < 0f ? -1f : 1f;
                        sum += new Vector4(q.x, q.y, q.z, q.w) * sign;
                    }
                    sum.Normalize();
                    Quaternion mean = new Quaternion(sum.x, sum.y, sum.z, sum.w);
                    target[c, i] = Quaternion.Slerp(own, mean, coupling) * reference[c, i];
                }
            for (int c = 0; c < fin.Length; c++)
                for (int i = 0; i < fin[c].Length; i++)
                {
                    Node n = fin[c][i];
                    n.t.rotation = target[c, i];
                    n.lastWritten = n.t.localRotation;
                }
        }
    }

    // Rays closing together squeezed the membrane between them. Where two neighbouring ray tips are closer (in the
    // fin plane) than in the model, d < d0, each tilts out of the plane by h = 1/2 sqrt(d0^2 - d^2), alternately
    // left and right, so the membrane keeps its width as zig-zag pleats. A chain takes the mean h of its two pairs
    // and turns about its root on cross(ray, side axis): the tip moves sideways only, its angle in the fan stays.
    void Pleat(Vector3 lateral)
    {
        if (fins == null || pleatAmount <= 0f) return;
        for (int f = 0; f < fins.Length; f++)
        {
            if (!finPleats[f]) continue;
            Node[][] fin = fins[f];
            var h = new float[fin.Length];
            var pairs = new int[fin.Length];
            for (int c = 0; c + 1 < fin.Length; c++)
            {
                float d = Vector3.ProjectOnPlane(Tip(fin[c + 1]) - Tip(fin[c]), lateral).magnitude;
                float d0 = restGap[f][c];
                float pair = 0.5f * Mathf.Sqrt(Mathf.Max(0f, d0 * d0 - d * d)) * pleatAmount;
                h[c] += pair; h[c + 1] += pair; pairs[c]++; pairs[c + 1]++;
            }
            for (int c = 0; c < fin.Length; c++)
            {
                Node root = fin[c][0];
                Vector3 chord = Tip(fin[c]) - root.t.position;
                Vector3 axis = Vector3.Cross(chord, lateral);
                if (pairs[c] == 0 || axis.sqrMagnitude < 1e-12f) continue;
                float angle = Mathf.Atan(h[c] / pairs[c] / chord.magnitude) * Mathf.Rad2Deg * (c % 2 == 0 ? 1f : -1f);
                root.t.rotation = Quaternion.AngleAxis(angle, axis.normalized) * root.t.rotation;
                root.lastWritten = root.t.localRotation;
            }
        }
    }
}
