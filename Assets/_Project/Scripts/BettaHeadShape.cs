using System.Collections.Generic;
using UnityEngine;

// Reshapes the shared body, cheek and forehead as one longitudinal profile.
// Work in bind space so the jaw, gills and swimming animation keep their skin weights.
public static class BettaHeadShape
{
    static readonly Dictionary<(Mesh, float), Mesh> Cache = new Dictionary<(Mesh, float), Mesh>();
    static readonly Dictionary<Mesh, Mesh> Sources = new Dictionary<Mesh, Mesh>();
    static readonly Dictionary<Mesh, float[]> AttachmentCache = new Dictionary<Mesh, float[]>();

    // A portrait bake or spawn can clone an already equipped fish.
    public static Mesh Source(Mesh mesh) => Sources.TryGetValue(mesh, out Mesh source) ? source : mesh;

    // Random fish use continuous values. Keep at most 26 shapes per source mesh,
    // with at most 0.01 rounding in the requested proportion.
    static float Quantize(float proportion) => Mathf.Round(Mathf.Clamp(proportion, 0.8f, 1.3f) * 50f) / 50f;

    public static Vector3 ShapePoint(Vector3 point, Vector3 head, Vector3 tail, float proportion)
        => new BettaBodyProfile(head, tail, Quantize(proportion)).Map(point);

    public static Mesh Get(Mesh source, Transform[] bones, Vector3 head, Vector3 neck, float proportion)
    {
        source = Source(source);
        proportion = Quantize(proportion);
        var key = (source, proportion);
        if (Cache.TryGetValue(key, out Mesh cached) && cached) return cached;
        if (!source.isReadable || (head - neck).sqrMagnitude < 1e-8f) return source;

        int tailIndex = System.Array.FindIndex(bones, b => b && b.name == "Tail_Base");
        if (tailIndex < 0) return source;
        Vector3 tail = source.bindposes[tailIndex].inverse.MultiplyPoint3x4(Vector3.zero);
        var frame = new BettaBodyProfile(head, tail, proportion);
        Vector3[] positions = source.vertices;
        Vector3[] normals = source.normals;
        Vector4[] tangents = source.tangents;
        BoneWeight[] weights = source.boneWeights;
        if (weights.Length != positions.Length) return source;

        bool Core(int index) => index >= 0 && index < bones.Length && bones[index]
            && (bones[index].name == "Root" || bones[index].name == "Head"
                || bones[index].name.StartsWith("Gill_") || bones[index].name.StartsWith("Jaw_"));

        // Relax cheek and torso ridges on welded geometry, keeping UV copies joined.
        // The mouth, sockets and free fins remain anchored; profile fitting follows.
        float[] attachment = AttachmentWeights(source, bones);
        var coreWeights = new float[positions.Length];
        var skinLed = new bool[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            BoneWeight b = weights[i];
            coreWeights[i] = (Core(b.boneIndex0) ? b.weight0 : 0f) + (Core(b.boneIndex1) ? b.weight1 : 0f)
                + (Core(b.boneIndex2) ? b.weight2 : 0f) + (Core(b.boneIndex3) ? b.weight3 : 0f);
            coreWeights[i] *= attachment[i];
            string lead = bones[b.boneIndex0].name;
            skinLed[i] = Core(b.boneIndex0) || lead.StartsWith("Spine_") || lead.StartsWith("Belly") || lead.StartsWith("Eye_");
        }
        // Belly and back skin can be weighted to fin roots. Classify complete skin
        // islands rather than mistaking those attachment vertices for free fins.
        bool[] skin = BettaFinSurface.BodyIslands(positions, source.uv, source.triangles, skinLed);
        SoftenSurface(source, positions, coreWeights, skin, attachment, FinAnchors(bones), head, neck, tail, out int[] weld, out float[] roundness);

        TransformVertices(frame, positions, normals, tangents, Vector3.Distance(head, neck) * .001f, attachment);

        // Rebuild the relaxed surface normals across UV copies; the rest retains
        // its authored shading. Otherwise the old triangular ridge remains in the lighting.
        var sums = new Vector3[positions.Length];
        int[] tris = source.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            sums[weld[a]] += normal; sums[weld[b]] += normal; sums[weld[c]] += normal;
        }
        if (normals.Length == positions.Length)
            for (int i = 0; i < positions.Length; i++)
            {
                if (roundness[weld[i]] <= 0f || sums[weld[i]].sqrMagnitude < 1e-18f) continue;
                // These are triangle areas in metres: Unity's normalized epsilon would
                // discard small, valid sums and turn some cheek normals into zero.
                Vector3 surfaceNormal = sums[weld[i]] / Mathf.Sqrt(sums[weld[i]].sqrMagnitude);
                normals[i] = Vector3.Slerp(normals[i], surfaceNormal, roundness[weld[i]]).normalized;
                if (tangents.Length != positions.Length) continue;
                Vector3 tangent = tangents[i];
                tangent = Vector3.ProjectOnPlane(tangent, normals[i]).normalized;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
            }

        Mesh mesh = Object.Instantiate(source);
        mesh.name = source.name + "_HeadShape";
        mesh.vertices = positions;
        mesh.boneWeights = BettaBodySkinning.Fit(source, bones, head, tail, skin);
        BettaFinSurface.Split(mesh, source, bones);
        if (normals.Length == positions.Length) mesh.normals = normals;
        if (tangents.Length == positions.Length) mesh.tangents = tangents;
        mesh.RecalculateBounds();
        Cache[key] = mesh;
        Sources[mesh] = source;
        return mesh;
    }

    // Only the part of a membrane next to the reshaped body follows the profile.
    // Free edges lie outside its spatial feather and retain their authored shape.
    public static Mesh FitFin(Mesh source, Transform[] bones, Vector3 head, Vector3 neck, Vector3 tail, float proportion)
    {
        source = Source(source);
        proportion = Quantize(proportion);
        var key = (source, proportion);
        if (Cache.TryGetValue(key, out Mesh cached) && cached) return cached;
        if (!source.isReadable) return source;
        Vector3[] positions = source.vertices, normals = source.normals;
        Vector4[] tangents = source.tangents;
        TransformVertices(new BettaBodyProfile(head, tail, proportion), positions, normals, tangents, Vector3.Distance(head, neck) * .001f, AttachmentWeights(source, bones));
        Mesh mesh = Object.Instantiate(source);
        mesh.name = source.name + "_BodyFit";
        mesh.vertices = positions;
        mesh.boneWeights = BettaBodySkinning.Fit(source, bones, head, tail);
        if (normals.Length == positions.Length) mesh.normals = normals;
        if (tangents.Length == positions.Length) mesh.tangents = tangents;
        mesh.RecalculateBounds();
        Cache[key] = mesh; Sources[mesh] = source;
        return mesh;
    }

    static float[] AttachmentWeights(Mesh source, Transform[] bones)
    {
        if (AttachmentCache.TryGetValue(source, out float[] cached)) return cached;
        bool Tip(int i)
        {
            string name = bones[i].name;
            return name.StartsWith("Pectoral_") && (name.EndsWith("_03A") || name.EndsWith("_03B"))
                || name.StartsWith("Pelvic_") && name.EndsWith("_03")
                || name.StartsWith("Tail_") && name.EndsWith("_05")
                || (name.StartsWith("Dorsal_") || name.StartsWith("Anal_")) && name.EndsWith("_04")
                || name.Contains("_Crown_") && name.EndsWith("_03");
        }
        BoneWeight[] weights = source.boneWeights;
        var result = new float[weights.Length];
        for (int i = 0; i < result.Length; i++)
        {
            BoneWeight w = weights[i];
            float tip = (Tip(w.boneIndex0) ? w.weight0 : 0f) + (Tip(w.boneIndex1) ? w.weight1 : 0f)
                + (Tip(w.boneIndex2) ? w.weight2 : 0f) + (Tip(w.boneIndex3) ? w.weight3 : 0f);
            result[i] = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((tip - .2f) / .45f));
        }
        AttachmentCache[source] = result;
        return result;
    }

    static void TransformVertices(BettaBodyProfile frame, Vector3[] positions, Vector3[] normals, Vector4[] tangents, float epsilon, float[] weights)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 p = positions[i];
            float weight = weights[i];
            if (weight <= 0f) continue;
            positions[i] = Vector3.LerpUnclamped(p, frame.Map(p), weight);
            if ((positions[i] - p).sqrMagnitude < 1e-16f) continue;

            // Jacobian of the sculpt, including the curved forehead. Transform authored
            // normals by its cofactor matrix and tangents by the forward matrix.
            float e = epsilon;
            Vector3 dx = Vector3.right * e, dy = Vector3.up * e, dz = Vector3.forward * e;
            Vector3 jx = (frame.Map(p + dx) - frame.Map(p - dx)) / (2f * e);
            Vector3 jy = (frame.Map(p + dy) - frame.Map(p - dy)) / (2f * e);
            Vector3 jz = (frame.Map(p + dz) - frame.Map(p - dz)) / (2f * e);
            jx = Vector3.LerpUnclamped(Vector3.right, jx, weight);
            jy = Vector3.LerpUnclamped(Vector3.up, jy, weight);
            jz = Vector3.LerpUnclamped(Vector3.forward, jz, weight);
            if (normals.Length == positions.Length)
            {
                Vector3 n = normals[i];
                normals[i] = (Vector3.Cross(jy, jz) * n.x + Vector3.Cross(jz, jx) * n.y + Vector3.Cross(jx, jy) * n.z).normalized;
            }
            if (tangents.Length == positions.Length)
            {
                Vector3 t = tangents[i];
                Vector3 direction = (jx * t.x + jy * t.y + jz * t.z).normalized;
                tangents[i] = new Vector4(direction.x, direction.y, direction.z, tangents[i].w);
            }
        }

    }

    // Separate Fin_* meshes share their attachment positions with BODY. Keep those
    // points on the common analytic profile; only the skin between them is relaxed.
    static HashSet<Vector3Int> FinAnchors(Transform[] bones)
    {
        var anchors = new HashSet<Vector3Int>();
        var animator = bones[0].GetComponentInParent<Animator>(true);
        if (!animator) return anchors;
        foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!renderer.name.StartsWith("Fin_") || !renderer.sharedMesh) continue;
            Mesh mesh = Source(BettaFinRuffle.Source(renderer.sharedMesh));
            foreach (Vector3 point in mesh.vertices) anchors.Add(Vector3Int.RoundToInt(point * 100000f));
        }
        return anchors;
    }

    static void SoftenSurface(Mesh source, Vector3[] positions, float[] core, bool[] skin, float[] attachment, HashSet<Vector3Int> finAnchors,
        Vector3 head, Vector3 neck, Vector3 tail,
        out int[] weld, out float[] roundness)
    {
        int count = positions.Length;
        var groups = new Dictionary<Vector3Int, int>();
        weld = new int[count];
        var points = new List<Vector3>();
        var amounts = new List<float>();
        var neighbours = new List<HashSet<int>>();
        float length = Vector3.Distance(head, neck);
        Vector3 forward = (head - neck).normalized;
        float bodyLength = Vector3.Distance(head, tail);
        Vector3 bodyForward = (head - tail).normalized;
        for (int i = 0; i < count; i++)
        {
            var key = Vector3Int.RoundToInt(positions[i] * 100000f);
            float u = Vector3.Dot(positions[i] - head, forward) / length;
            float cheek = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u + 2f) / .8f))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u + .35f) / .45f)));
            float s = Vector3.Dot(positions[i] - head, bodyForward) / bodyLength;
            float torso = .7f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s + 1.08f) / .16f))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s + .22f) / .16f)));
            if (finAnchors.Contains(key)) torso = 0f;
            float amount = Mathf.Max(core[i] * cheek, skin[i] ? attachment[i] * torso : 0f);
            if (!groups.TryGetValue(key, out int group))
            {
                groups[key] = group = points.Count;
                points.Add(positions[i]); amounts.Add(amount); neighbours.Add(new HashSet<int>());
            }
            else amounts[group] = Mathf.Min(amounts[group], amount);
            weld[i] = group;
        }
        int[] triangles = source.triangles;
        // Nearly collinear stitch triangles cannot tolerate independent relaxation.
        // Pin their welded corners rather than letting a tiny face turn inside out.
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector3 ab = positions[b] - positions[a], ac = positions[c] - positions[a];
            float edge = Mathf.Max(ab.sqrMagnitude, ac.sqrMagnitude, (positions[c] - positions[b]).sqrMagnitude);
            if (Vector3.Cross(ab, ac).sqrMagnitude < edge * edge * .0001f)
                amounts[weld[a]] = amounts[weld[b]] = amounts[weld[c]] = 0f;
        }
        for (int i = 0; i < triangles.Length; i += 3)
            for (int j = 0; j < 3; j++)
            {
                int a = weld[triangles[i + j]], b = weld[triangles[i + (j + 1) % 3]];
                if (a != b) { neighbours[a].Add(b); neighbours[b].Add(a); }
            }
        Vector3[] current = points.ToArray(), next = new Vector3[current.Length];
        // Paired positive/negative relaxation rounds ridges with little volume loss.
        for (int pass = 0; pass < 40; pass++)
        {
            float step = pass % 2 == 0 ? .5f : -.46f;
            for (int i = 0; i < current.Length; i++)
            {
                Vector3 average = Vector3.zero;
                foreach (int j in neighbours[i]) average += current[j];
                next[i] = neighbours[i].Count == 0 ? current[i] : current[i]
                    + (average / neighbours[i].Count - current[i]) * (amounts[i] * step);
            }
            var swap = current; current = next; next = swap;
        }
        for (int i = 0; i < count; i++)
            if (amounts[weld[i]] > 0f) positions[i] = current[weld[i]];
        roundness = amounts.ToArray();
    }
}
