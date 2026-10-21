using System.Collections.Generic;
using UnityEngine;

// Rosetail edge: builds a copy of a flat Fin_* mesh whose outer edge folds in waves.
// - The free edge is the longest run of the mesh border not skinned to a fin root bone (*_Base, *_01);
//   the rest of the border is the base, where the fin joins the body.
// - Two harmonic fields on the welded source mesh: t (0 base .. 1 free edge) and s (metres along the free edge),
//   so folds stay smooth inside the fin instead of creasing where two parts of the edge are equally close.
// - The mesh is subdivided twice (the source fins have ~20 vertices along the edge) and every vertex
//   moves off the fin plane by depth * sin(s / wavelength), fading in from t = start.
// Positions are in bind pose, so skinning and BettaFinPhysics bend the folds with the fin.
// Results are cached per source mesh and settings; every fish with the same tail shares one mesh.
public static class BettaFinRuffle
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Fold height as a share of the fin size. 0 = flat fin (no ruffle).")]
        [Range(0f, 0.1f)] public float depth;
        [Tooltip("Length of one fold (up and down) along the edge, as a share of the fin size.")]
        [Range(0.08f, 0.5f)] public float wavelength = 0.2f;
        [Tooltip("Where folds begin, from the fin base (0) to the edge (1).")]
        [Range(0f, 0.95f)] public float start = 0.5f;

        public bool Enabled => depth > 0f;
    }

    const int Subdivisions = 2;
    const int HarmonicIterations = 400;

    static readonly Dictionary<(Mesh, float, float, float), Mesh> Cache = new Dictionary<(Mesh, float, float, float), Mesh>();
    static readonly Dictionary<Mesh, Mesh> Sources = new Dictionary<Mesh, Mesh>();

    public static Mesh Source(Mesh mesh) => Sources.TryGetValue(mesh, out Mesh source) ? source : mesh;

    public static Mesh Get(Mesh source, Transform[] bones, Settings settings)
    {
        source = Source(source);
        var key = (source, settings.depth, settings.wavelength, settings.start);
        if (Cache.TryGetValue(key, out Mesh mesh) && mesh) return mesh;
        if (!source.isReadable)
        {
            Debug.LogWarning("[BettaFinRuffle] " + source.name + " is not readable; enable Read/Write on Betta.fbx.");
            return source;
        }
        mesh = Build(source, bones, settings);
        Cache[key] = mesh;
        if (mesh != source) Sources[mesh] = source;
        return mesh;
    }

    static Mesh Build(Mesh source, Transform[] bones, Settings settings)
    {
        Vector3[] srcPos = source.vertices;
        int n = srcPos.Length;
        Vector3[] srcNormals = source.normals;
        Vector4[] srcTangents = source.tangents;
        BoneWeight[] srcWeights = source.boneWeights;
        var srcUv = new List<Vector2>();
        source.GetUVs(0, srcUv);
        bool hasNormals = srcNormals.Length == n, hasTangents = srcTangents.Length == n, hasUv = srcUv.Count == n;
        if (srcWeights.Length != n || !hasNormals) return source;

        // Weld UV-seam copies so the border and the fields see one surface.
        int[] weld = Weld(srcPos, out int welded);
        var tris = new List<int>();
        for (int sub = 0; sub < source.subMeshCount; sub++) tris.AddRange(source.GetTriangles(sub));

        List<int> loop = LongestBorderLoop(tris, weld);
        if (loop.Count < 4) return source;

        // Base = border vertices mostly skinned to fin root bones.
        var rootWeight = new float[welded];
        for (int i = 0; i < n; i++)
        {
            BoneWeight w = srcWeights[i];
            rootWeight[weld[i]] = Mathf.Max(rootWeight[weld[i]],
                (IsRoot(bones, w.boneIndex0) ? w.weight0 : 0f) + (IsRoot(bones, w.boneIndex1) ? w.weight1 : 0f) +
                (IsRoot(bones, w.boneIndex2) ? w.weight2 : 0f) + (IsRoot(bones, w.boneIndex3) ? w.weight3 : 0f));
        }
        var weldedPos = new Vector3[welded];
        for (int i = 0; i < n; i++) weldedPos[weld[i]] = srcPos[i];
        List<int> free = LongestFreeRun(loop, rootWeight, weldedPos);
        if (free.Count < 3) return source;

        // Harmonic s (arc length along the free edge) and t (0 base .. 1 edge).
        var neighbours = new HashSet<int>[welded];
        for (int i = 0; i < welded; i++) neighbours[i] = new HashSet<int>();
        for (int i = 0; i < tris.Count; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = weld[tris[i + k]], b = weld[tris[i + (k + 1) % 3]];
                neighbours[a].Add(b); neighbours[b].Add(a);
            }
        var arc = new float[free.Count];
        for (int i = 1; i < free.Count; i++) arc[i] = arc[i - 1] + Vector3.Distance(weldedPos[free[i - 1]], weldedPos[free[i]]);
        float edgeLength = arc[arc.Length - 1];

        var s = new float[welded];
        var t = new float[welded];
        var fixedS = new bool[welded];
        var fixedT = new bool[welded];
        for (int i = 0; i < welded; i++) { s[i] = edgeLength * 0.5f; t[i] = 0.5f; }
        foreach (int b in loop) { t[b] = 0f; fixedT[b] = true; }
        for (int i = 0; i < free.Count; i++) { s[free[i]] = arc[i]; t[free[i]] = 1f; fixedS[free[i]] = fixedT[free[i]] = true; }
        var nb = new int[welded][];
        for (int i = 0; i < welded; i++) { nb[i] = new int[neighbours[i].Count]; neighbours[i].CopyTo(nb[i]); }
        Relax(s, fixedS, nb);
        Relax(t, fixedT, nb);

        // Fin plane normal (the fins are flat) and size.
        Vector3 planeNormal = Vector3.zero;
        foreach (Vector3 nrm in srcNormals) planeNormal += Vector3.Dot(nrm, srcNormals[0]) >= 0f ? nrm : -nrm;
        planeNormal.Normalize();
        Vector3 extent = source.bounds.size;
        float[] axes = { extent.x, extent.y, extent.z };
        System.Array.Sort(axes);
        float size = Mathf.Sqrt(axes[2] * axes[1]);

        // Subdivide with every attribute (midpoints of a shared edge are shared).
        var pos = new List<Vector3>(srcPos);
        var normals = new List<Vector3>(srcNormals);
        var tangents = hasTangents ? new List<Vector4>(srcTangents) : null;
        var uv = hasUv ? srcUv : null;
        var weights = new List<BoneWeight>(srcWeights);
        var fs = new List<float>(n);
        var ft = new List<float>(n);
        for (int i = 0; i < n; i++) { fs.Add(s[weld[i]]); ft.Add(t[weld[i]]); }

        var subTris = new List<int[]>();
        for (int sub = 0; sub < source.subMeshCount; sub++) subTris.Add(source.GetTriangles(sub));
        for (int level = 0; level < Subdivisions; level++)
        {
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int m)) return m;
                m = pos.Count;
                pos.Add((pos[a] + pos[b]) * 0.5f);
                normals.Add((normals[a] + normals[b]).normalized);
                if (tangents != null)
                {
                    Vector3 tan = ((Vector3)tangents[a] + (Vector3)tangents[b]).normalized;
                    tangents.Add(new Vector4(tan.x, tan.y, tan.z, tangents[a].w));
                }
                if (uv != null) uv.Add((uv[a] + uv[b]) * 0.5f);
                weights.Add(Blend(weights[a], weights[b]));
                fs.Add((fs[a] + fs[b]) * 0.5f);
                ft.Add((ft[a] + ft[b]) * 0.5f);
                mid[key] = m;
                return m;
            }
            for (int sub = 0; sub < subTris.Count; sub++)
            {
                int[] src = subTris[sub];
                var dst = new int[src.Length * 4];
                for (int i = 0, o = 0; i < src.Length; i += 3)
                {
                    int a = src[i], b = src[i + 1], c = src[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    dst[o++] = a; dst[o++] = ab; dst[o++] = ca;
                    dst[o++] = ab; dst[o++] = b; dst[o++] = bc;
                    dst[o++] = ca; dst[o++] = bc; dst[o++] = c;
                    dst[o++] = ab; dst[o++] = bc; dst[o++] = ca;
                }
                subTris[sub] = dst;
            }
        }

        // Folds. A slow second wave keeps them from looking machine-even.
        float wavelength = settings.wavelength * size;
        float amplitude = settings.depth * size;
        for (int i = 0; i < pos.Count; i++)
        {
            float ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.start, 1f, ft[i]));
            float taper = Mathf.Clamp01(fs[i] / (0.5f * wavelength)) * Mathf.Clamp01((edgeLength - fs[i]) / (0.5f * wavelength));
            float phase = 2f * Mathf.PI * fs[i] / wavelength + 0.9f * Mathf.Sin(2f * Mathf.PI * fs[i] / (2.7f * wavelength));
            pos[i] += planeNormal * (amplitude * ramp * taper * Mathf.Sin(phase));
        }

        // Normals of the folded surface, welded across UV seams, on the side of the original normal.
        int[] subWeld = Weld(pos.ToArray(), out int subWelded);
        var acc = new Vector3[subWelded];
        foreach (int[] tri in subTris)
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(pos[tri[i + 1]] - pos[tri[i]], pos[tri[i + 2]] - pos[tri[i]]);
                acc[subWeld[tri[i]]] += face; acc[subWeld[tri[i + 1]]] += face; acc[subWeld[tri[i + 2]]] += face;
            }
        for (int i = 0; i < pos.Count; i++)
        {
            Vector3 folded = acc[subWeld[i]].normalized;
            if (folded == Vector3.zero) continue;
            if (Vector3.Dot(folded, normals[i]) < 0f) folded = -folded;
            normals[i] = folded;
            if (tangents != null)
            {
                Vector3 tan = (Vector3)tangents[i];
                tan = (tan - folded * Vector3.Dot(tan, folded)).normalized;
                tangents[i] = new Vector4(tan.x, tan.y, tan.z, tangents[i].w);
            }
        }

        var mesh = new Mesh { name = source.name + "_Ruffled" };
        if (pos.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(pos);
        mesh.SetNormals(normals);
        if (tangents != null) mesh.SetTangents(tangents);
        if (uv != null) mesh.SetUVs(0, uv);
        mesh.boneWeights = weights.ToArray();
        mesh.bindposes = source.bindposes;
        mesh.subMeshCount = subTris.Count;
        for (int sub = 0; sub < subTris.Count; sub++) mesh.SetTriangles(subTris[sub], sub);
        mesh.RecalculateBounds();
        return mesh;
    }

    static bool IsRoot(Transform[] bones, int index)
    {
        if (bones == null || index < 0 || index >= bones.Length || bones[index] == null) return false;
        string name = bones[index].name;
        return name.EndsWith("_Base") || (name.EndsWith("_01") && !name.Contains("_Crown_"));
    }

    static int[] Weld(Vector3[] positions, out int count)
    {
        var ids = new Dictionary<Vector3Int, int>();
        var weld = new int[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 p = positions[i] * 100000f;
            var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            if (!ids.TryGetValue(key, out int id)) ids[key] = id = ids.Count;
            weld[i] = id;
        }
        count = ids.Count;
        return weld;
    }

    // Border edges are used by one triangle; follow them (in winding order) into loops, keep the longest.
    static List<int> LongestBorderLoop(List<int> tris, int[] weld)
    {
        var uses = new Dictionary<long, int>();
        long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < tris.Count; i += 3)
            for (int k = 0; k < 3; k++)
            {
                long key = Key(weld[tris[i + k]], weld[tris[i + (k + 1) % 3]]);
                uses[key] = uses.TryGetValue(key, out int c) ? c + 1 : 1;
            }
        var next = new Dictionary<int, int>();
        for (int i = 0; i < tris.Count; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = weld[tris[i + k]], b = weld[tris[i + (k + 1) % 3]];
                if (a != b && uses[Key(a, b)] == 1) next[a] = b;
            }
        var seen = new HashSet<int>();
        var best = new List<int>();
        foreach (int start in next.Keys)
        {
            if (seen.Contains(start)) continue;
            var loop = new List<int>();
            for (int v = start; !seen.Contains(v); )
            {
                seen.Add(v); loop.Add(v);
                if (!next.TryGetValue(v, out v)) break;
            }
            if (loop.Count > best.Count) best = loop;
        }
        return best;
    }

    // Longest stretch (by length) of the loop whose vertices are not root-skinned, plus the base vertex at each end.
    static List<int> LongestFreeRun(List<int> loop, float[] rootWeight, Vector3[] pos)
    {
        int n = loop.Count;
        bool IsBase(int i) => rootWeight[loop[((i % n) + n) % n]] >= 0.5f;
        int first = -1;
        for (int i = 0; i < n && first < 0; i++) if (IsBase(i)) first = i;
        if (first < 0) return new List<int>();

        int bestStart = 0, bestEnd = 0;
        float bestLength = 0f;
        for (int i = 0; i < n; )
        {
            if (IsBase(first + i)) { i++; continue; }
            int j = i;
            float length = 0f;
            while (j < n && !IsBase(first + j))
            {
                length += Vector3.Distance(pos[loop[(first + j) % n]], pos[loop[(first + j + 1) % n]]);
                j++;
            }
            if (length > bestLength) { bestLength = length; bestStart = i; bestEnd = j; }
            i = j;
        }
        var run = new List<int>();
        for (int i = bestStart - 1; i <= bestEnd; i++) run.Add(loop[(first + i + n) % n]);
        return run;
    }

    static void Relax(float[] values, bool[] fixedValues, int[][] neighbours)
    {
        for (int iteration = 0; iteration < HarmonicIterations; iteration++)
            for (int i = 0; i < values.Length; i++)
            {
                if (fixedValues[i] || neighbours[i].Length == 0) continue;
                float sum = 0f;
                foreach (int j in neighbours[i]) sum += values[j];
                values[i] = sum / neighbours[i].Length;
            }
    }

    // Midpoint skin weights: the four strongest of both vertices' bones, renormalised.
    static BoneWeight Blend(BoneWeight a, BoneWeight b)
    {
        var w = new Dictionary<int, float>(8);
        void Add(int bone, float weight)
        {
            if (weight <= 0f) return;
            w[bone] = (w.TryGetValue(bone, out float x) ? x : 0f) + weight * 0.5f;
        }
        Add(a.boneIndex0, a.weight0); Add(a.boneIndex1, a.weight1); Add(a.boneIndex2, a.weight2); Add(a.boneIndex3, a.weight3);
        Add(b.boneIndex0, b.weight0); Add(b.boneIndex1, b.weight1); Add(b.boneIndex2, b.weight2); Add(b.boneIndex3, b.weight3);
        var sorted = new List<KeyValuePair<int, float>>(w);
        sorted.Sort((x, y) => y.Value.CompareTo(x.Value));
        float total = 0f;
        for (int i = 0; i < sorted.Count && i < 4; i++) total += sorted[i].Value;
        var r = new BoneWeight();
        if (total <= 0f) return a;
        if (sorted.Count > 0) { r.boneIndex0 = sorted[0].Key; r.weight0 = sorted[0].Value / total; }
        if (sorted.Count > 1) { r.boneIndex1 = sorted[1].Key; r.weight1 = sorted[1].Value / total; }
        if (sorted.Count > 2) { r.boneIndex2 = sorted[2].Key; r.weight2 = sorted[2].Value / total; }
        if (sorted.Count > 3) { r.boneIndex3 = sorted[3].Key; r.weight3 = sorted[3].Value / total; }
        return r;
    }
}
