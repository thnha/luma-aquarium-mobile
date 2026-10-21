using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Bakes the Betta variant mask (see Shaders/BettaVariant.hlsl) from the skin weights in Betta.fbx,
// so variants work before an artist paints a mask. BODY and every Fin_* mesh (one set per tail form,
// own UV islands in the same atlas) are baked into the one texture:
//   R = body (1) / fin (0), per UV island: a body island has vertices led by core bones (Head, Spine...),
//       fin islands have none (per-vertex weights alone don't work: the body is partly skinned to Anal/Pelvic roots),
//   G = fin root (0) -> fin edge (1), distance from the fin chain's root bone over the chain length,
//   B, A = two 3D noises of the bind-pose position (seams match across UV islands).
// Menu: Lumaquarium > Bake Betta Variant Mask (safe to re-run; BettaPrefabBuilder bakes it if missing).
public static class BettaMaskBaker
{
    public const string MaskPath = "Assets/_Project/Art/Betta/Textures/BETTA_VariantMask.png";
    const string FbxPath = "Assets/_Project/Art/Betta/Betta.fbx";
    const string BodyMesh = "BODY";
    const int Size = 1024;
    const float NoiseFrequency = 7f;   // spots across the fish's longest side

    static readonly Regex ChainBone = new Regex(@"^(?<chain>.+_)(?<index>\d\d)[A-Z]?$");

    public static Texture2D LoadOrBake()
    {
        var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
        return mask ? mask : Bake();
    }

    [MenuItem("Lumaquarium/Bake Betta Variant Mask")]
    public static Texture2D Bake()
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) { Debug.LogError("[Lumaquarium] Missing " + FbxPath); return null; }

        // Mesh data has to be CPU-readable for the bake; restore the import setting afterwards.
        bool wasReadable = importer.isReadable;
        if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        var pixels = new Color32[Size * Size];
        var filled = new bool[Size * Size];
        try
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var renderers = fbx.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var body = renderers.FirstOrDefault(r => r.name == BodyMesh);
            if (body == null || body.sharedMesh == null) { Debug.LogError("[Lumaquarium] No " + BodyMesh + " mesh in " + FbxPath); return null; }
            // Halfmoon fins last: where forms share UV space, the default form wins.
            var fins = renderers.Where(r => r.name.StartsWith("Fin_") && r.sharedMesh != null).OrderBy(r => r.name.EndsWith("_Halfmoon") ? 1 : 0);
            var meshes = new[] { body }.Concat(fins).ToArray();
            // Every mesh shares one bind space; each lists only the bones it is skinned to, so chains
            // (a fin mesh may skip a chain's _01 bone) are measured on the union of all of them.
            var bindPositions = new Dictionary<string, Vector3>();
            foreach (var r in meshes)
            {
                Matrix4x4[] bindposes = r.sharedMesh.bindposes;
                for (int i = 0; i < r.bones.Length && i < bindposes.Length; i++)
                    if (r.bones[i] != null) bindPositions[r.bones[i].name] = bindposes[i].inverse.MultiplyPoint3x4(Vector3.zero);
            }
            foreach (var r in meshes) Rasterize(r.sharedMesh, r.bones, bindPositions, body.sharedMesh.bounds, pixels, filled);
            Dilate(pixels, filled, 8);
        }
        finally
        {
            if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(MaskPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Lumaquarium] Baked " + MaskPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
    }

    static bool IsFinBone(string name) =>
        (name.StartsWith("Tail_") && name != "Tail_Base") || name.StartsWith("Dorsal_") || name.StartsWith("Anal_")
        || (name.StartsWith("Pelvic_") && name != "Pelvic_Base") || name.StartsWith("Pectoral_");

    static readonly string[] CoreBones = { "Root", "Head", "Jaw_", "Eye_", "Spine_", "Gill_", "Belly" };
    static bool IsCoreBone(string name) => CoreBones.Any(prefix => name.StartsWith(prefix));

    static void Rasterize(Mesh mesh, Transform[] bones, Dictionary<string, Vector3> bindPositions, Bounds noiseBounds, Color32[] pixels, bool[] filled)
    {
        Vector3[] verts = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        BoneWeight[] weights = mesh.boneWeights;
        int[] tris = mesh.triangles;

        // Fin chains (Tail_Top_01..05, Pectoral_L_01..03B ...): root position and length in mesh space.
        var chainRoot = new Dictionary<string, (Vector3 pos, int index)>();
        var chainLength = new Dictionary<string, float>();
        var chainCount = new Dictionary<string, int>();
        var chainBones = new List<(string chain, Vector3 pos, int index)>();
        foreach (var bone in bindPositions)
        {
            if (!IsFinBone(bone.Key)) continue;
            Match m = ChainBone.Match(bone.Key);
            if (!m.Success) continue;
            string chain = m.Groups["chain"].Value;
            int index = int.Parse(m.Groups["index"].Value);
            chainBones.Add((chain, bone.Value, index));
            if (!chainRoot.TryGetValue(chain, out var root) || index < root.index) chainRoot[chain] = (bone.Value, index);
        }
        foreach (var (chain, pos, index) in chainBones)
        {
            float d = Vector3.Distance(pos, chainRoot[chain].pos);
            chainLength[chain] = Mathf.Max(chainLength.TryGetValue(chain, out float l) ? l : 0f, d);
            chainCount[chain] = Mathf.Max(chainCount.TryGetValue(chain, out int c) ? c : 0, index);
        }
        var boneChain = new string[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null || !IsFinBone(bones[i].name)) continue;
            Match m = ChainBone.Match(bones[i].name);
            if (m.Success) boneChain[i] = m.Groups["chain"].Value;
        }
        foreach (string chain in chainCount.Keys.ToList())
        {
            // Last joint's origin is one segment short of the fin edge.
            int n = chainCount[chain];
            chainLength[chain] = Mathf.Max(1e-4f, chainLength[chain] * (n > 1 ? n / (float)(n - 1) : 2f));
        }

        // Per vertex: is it led by a core body bone, and fin depth.
        var coreLed = new bool[verts.Length];
        var depth = new float[verts.Length];
        for (int v = 0; v < verts.Length; v++)
        {
            BoneWeight w = weights[v];
            float fin = 0f, best = 0f;
            int bestBone = -1;
            void Add(int bone, float weight)
            {
                if (weight <= 0f || boneChain[bone] == null) return;
                fin += weight;
                if (weight > best) { best = weight; bestBone = bone; }
            }
            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
            Transform lead = bones[w.boneIndex0]; // Unity sorts weights, index 0 is the strongest
            coreLed[v] = lead != null && IsCoreBone(lead.name);
            if (bestBone >= 0)
            {
                string chain = boneChain[bestBone];
                depth[v] = Mathf.Clamp01(Vector3.Distance(verts[v], chainRoot[chain].pos) / chainLength[chain]) * Mathf.Clamp01(fin);
            }
        }

        // UV islands: union triangle corners, welding vertices Unity split only for hard normals
        // (same position and UV). An island is body when over 15% of it follows core bones
        // (Betta.fbx: body halves 49%, jaws 100%, every fin 0%).
        bool[] bodyIslands = BettaFinSurface.BodyIslands(verts, uvs, tris, coreLed);
        var body = new float[verts.Length];
        for (int v = 0; v < verts.Length; v++)
        {
            body[v] = bodyIslands[v] ? 1f : 0f;
            depth[v] *= 1f - body[v];
        }

        Bounds bounds = noiseBounds; // the body's, so noise lines up across meshes
        float extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        var noiseOffsetB = new Vector3(13.7f, 4.1f, 27.3f);
        var noiseOffsetA = new Vector3(-31.2f, 19.9f, 8.6f);

        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
            Vector2 a = uvs[i0] * Size, b = uvs[i1] * Size, c = uvs[i2] * Size;
            float area = Cross(b - a, c - a);
            if (Mathf.Abs(area) < 1e-8f) continue;
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0, Size - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))), 0, Size - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0, Size - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))), 0, Size - 1);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float u = Cross(p - a, c - a) / area, v = Cross(b - a, p - a) / area, w0 = 1f - u - v;
                const float Eps = -0.02f;
                if (u < Eps || v < Eps || w0 < Eps) continue;
                Vector3 pos = verts[i0] * w0 + verts[i1] * u + verts[i2] * v;
                Vector3 q = (pos - bounds.center) / extent * NoiseFrequency;
                pixels[y * Size + x] = new Color32(
                    ToByte(body[i0] * w0 + body[i1] * u + body[i2] * v),
                    ToByte(depth[i0] * w0 + depth[i1] * u + depth[i2] * v),
                    ToByte(Fbm(q + noiseOffsetB)),
                    ToByte(Fbm(q + noiseOffsetA)));
                filled[y * Size + x] = true;
            }
        }
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static byte ToByte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

    // Grows UV islands outward so bilinear filtering and mipmaps don't pull in empty texels at seams.
    internal static void Dilate(Color32[] pixels, bool[] filled, int passes)
    {
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        for (int pass = 0; pass < passes; pass++)
        {
            var next = (bool[])filled.Clone();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                if (filled[i]) continue;
                int r = 0, g = 0, b = 0, a = 0, n = 0;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= Size || ny >= Size || !filled[ny * Size + nx]) continue;
                    Color32 c = pixels[ny * Size + nx];
                    r += c.r; g += c.g; b += c.b; a += c.a; n++;
                }
                if (n == 0) continue;
                pixels[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                next[i] = true;
            }
            System.Array.Copy(next, filled, filled.Length);
        }
    }

    // Two octaves of value noise, roughly 0..1 around 0.5.
    static float Fbm(Vector3 p) => ValueNoise(p) * 0.65f + ValueNoise(p * 2.03f + new Vector3(17.1f, 3.7f, 9.2f)) * 0.35f;

    static float ValueNoise(Vector3 p)
    {
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
        float fx = p.x - x, fy = p.y - y, fz = p.z - z;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
        float Lerp3(int ox, int oy) => Mathf.Lerp(Hash(x + ox, y + oy, z), Hash(x + ox, y + oy, z + 1), fz);
        return Mathf.Lerp(
            Mathf.Lerp(Lerp3(0, 0), Lerp3(1, 0), fx),
            Mathf.Lerp(Lerp3(0, 1), Lerp3(1, 1), fx), fy);
    }

    static float Hash(int x, int y, int z)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + z * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
        }
    }
}
