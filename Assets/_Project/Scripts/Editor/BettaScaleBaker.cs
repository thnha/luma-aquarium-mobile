using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Bake shallow overlapping scales in bind space into the existing UV atlas.
// The two sides and UV seams share one cylindrical field; skinning carries it
// with the fish. R = clean scale shading, GB = tangent normal XY, A = opaque skin.
public static class BettaScaleBaker
{
    public const string MapPath = "Assets/_Project/Art/Betta/Textures/BETTA_OrderedScales.png";
    const string FbxPath = "Assets/_Project/Art/Betta/Betta.fbx";
    const string BakeVersion = "ordered-scales-2";
    const int Size = 2048;
    const int Sections = 65;
    const float Pitch = .0063f, Relief = .000045f;

    public static Texture2D LoadOrBake()
    {
        var map = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
        var importer = AssetImporter.GetAtPath(MapPath);
        return map && importer && importer.userData == Stamp() ? map : Bake();
    }

    static string Stamp() => BakeVersion + ";" + AssetDatabase.GetAssetDependencyHash(FbxPath);

    public static Texture2D Bake()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var body = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "BODY");
        Mesh mesh = body.sharedMesh;
        Vector3[] positions = mesh.vertices, normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        Vector2[] uv = mesh.uv;
        int[] triangles = mesh.triangles;
        bool[] led = mesh.boneWeights.Select(w =>
        {
            string n = body.bones[w.boneIndex0].name;
            return n == "Root" || n == "Head" || n.StartsWith("Spine_")
                || n.StartsWith("Gill_") || n.StartsWith("Jaw_") || n.StartsWith("Eye_") || n.StartsWith("Belly");
        }).ToArray();
        bool[] skin = BettaFinSurface.BodyIslands(positions, uv, triangles, led);
        Vector3 Bind(string name) => mesh.bindposes[System.Array.FindIndex(body.bones, b => b.name == name)].inverse.MultiplyPoint3x4(Vector3.zero);
        Vector3 head = Bind("Head"), forward = (head - Bind("Tail_Base")).normalized;
        Vector3 up = Vector3.Cross(forward, Vector3.right).normalized;
        float min = -.185f, max = .035f, step = (max - min) / (Sections - 1);
        var roof = Enumerable.Repeat(float.NegativeInfinity, Sections).ToArray();
        var floor = Enumerable.Repeat(float.PositiveInfinity, Sections).ToArray();
        var width = new float[Sections];
        // Intersect actual opaque triangles, so the rows wrap around the anatomy
        // instead of assuming either side's UV island is a rectangular photograph.
        for (int k = 0; k < Sections; k++)
        {
            float station = min + step * k;
            for (int j = 0; j < triangles.Length; j += 3)
            {
                if (!skin[triangles[j]]) continue;
                for (int e = 0; e < 3; e++)
                {
                    Vector3 a = positions[triangles[j + e]] - head, b = positions[triangles[j + (e + 1) % 3]] - head;
                    float sa = Vector3.Dot(a, forward), sb = Vector3.Dot(b, forward);
                    if (!((sa <= station && station < sb) || (sb <= station && station < sa))) continue;
                    Vector3 p = Vector3.Lerp(a, b, (station - sa) / (sb - sa));
                    float y = Vector3.Dot(p, up);
                    roof[k] = Mathf.Max(roof[k], y); floor[k] = Mathf.Min(floor[k], y);
                    width[k] = Mathf.Max(width[k], Mathf.Abs(p.x));
                }
            }
        }
        for (int k = 0; k < Sections; k++)
            if (float.IsInfinity(roof[k]))
            {
                int nearest = Enumerable.Range(0, Sections).Where(i => !float.IsInfinity(roof[i])).OrderBy(i => Mathf.Abs(i - k)).First();
                roof[k] = roof[nearest]; floor[k] = floor[nearest]; width[k] = width[nearest];
            }
        // Remove small socket/gill steps from the coordinate frame only.
        for (int pass = 0; pass < 4; pass++)
            foreach (float[] values in new[] { roof, floor, width })
            {
                float[] previous = (float[])values.Clone();
                for (int k = 1; k < Sections - 1; k++) values[k] = (previous[k - 1] + 2 * previous[k] + previous[k + 1]) * .25f;
            }
        float Section(float[] values, float s)
        {
            float index = Mathf.Clamp((s - min) / step, 0, Sections - 1);
            int i = Mathf.Min((int)index, Sections - 2);
            return Mathf.Lerp(values[i], values[i + 1], index - i);
        }
        Vector2 Field(Vector3 p)
        {
            Vector3 d = p - head; float s = Vector3.Dot(d, forward);
            float top = Section(roof, s), bottom = Section(floor, s);
            float angle = Mathf.Atan2(d.x / Mathf.Max(.001f, Section(width, s)),
                (Vector3.Dot(d, up) - (top + bottom) * .5f) / Mathf.Max(.002f, (top - bottom) * .5f));
            // An even number of rows closes at the back/belly seam with the same stagger.
            float around = (angle / (2 * Mathf.PI) + .5f) * 32f;
            float along = (.03f - s) / Pitch - 3f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.04f, .016f, s))
                + 1.5f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.17f, -.11f, s)));
            return new Vector2(along, around);
        }
        // The foremost oval covers the one behind it. Its exposed trailing arc
        // forms a scalloped lip; adjacent rows are offset by half a scale.
        Vector2 Detail(Vector3 point)
        {
            Vector2 p = Field(point); float best = float.PositiveInfinity, radius = 1f;
            int row = Mathf.FloorToInt(p.y);
            for (int r = row - 1; r <= row + 1; r++)
            {
                float offset = Mathf.Repeat(r, 2) * .5f;
                int cell = Mathf.FloorToInt(p.x - offset);
                for (int c = cell - 1; c <= cell + 1; c++)
                {
                    float center = c + offset;
                    float x = (p.x - center) / .68f, y = (p.y - r - .5f) / .62f;
                    float d = x * x + y * y;
                    if (d > 1 || center >= best) continue;
                    best = center; radius = Mathf.Sqrt(d);
                }
            }
            float edge = 1 - radius;
            float rim = Mathf.SmoothStep(0, 1, Mathf.Clamp01(edge / .12f));
            float dome = 1 - radius * radius;
            float face = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.027f, .012f, Vector3.Dot(point - head, forward)));
            return new Vector2(Mathf.Lerp(1, rim * (.85f + .15f * dome), face),
                Relief * rim * (.7f + .3f * dome) * face);
        }
        var pixels = new Color32[Size * Size]; var filled = new bool[pixels.Length];
        const float epsilon = .00008f;
        for (int j = 0; j < triangles.Length; j += 3)
        {
            int ia = triangles[j], ib = triangles[j + 1], ic = triangles[j + 2];
            if (!skin[ia]) continue;
            Vector2 a = uv[ia] * Size, b = uv[ib] * Size, c = uv[ic] * Size;
            float area = Cross(b - a, c - a); if (Mathf.Abs(area) < 1e-8f) continue;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0, Size - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))), 0, Size - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0, Size - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))), 0, Size - 1);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                Vector2 q = new Vector2(x + .5f, y + .5f);
                float wb = Cross(q - a, c - a) / area, wc = Cross(b - a, q - a) / area, wa = 1 - wb - wc;
                if (wa < 0 || wb < 0 || wc < 0) continue;
                Vector3 p = positions[ia] * wa + positions[ib] * wb + positions[ic] * wc;
                Vector3 n = (normals[ia] * wa + normals[ib] * wb + normals[ic] * wc).normalized;
                Vector4 tangent = tangents[ia] * wa + tangents[ib] * wb + tangents[ic] * wc;
                Vector3 t = Vector3.ProjectOnPlane(tangent, n).normalized;
                Vector3 bitangent = Vector3.Cross(n, t) * Mathf.Sign(tangent.w);
                Vector2 detail = Detail(p);
                float dx = (Detail(p + t * epsilon).y - Detail(p - t * epsilon).y) / (2 * epsilon);
                float dy = (Detail(p + bitangent * epsilon).y - Detail(p - bitangent * epsilon).y) / (2 * epsilon);
                Vector3 normal = new Vector3(-dx, -dy, 1).normalized;
                int index = y * Size + x;
                pixels[index] = new Color(detail.x, normal.x * .5f + .5f, normal.y * .5f + .5f, 1);
                filled[index] = true;
            }
        }
        BettaMaskBaker.Dilate(pixels, filled, 8);
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(MapPath, texture.EncodeToPNG()); }
        finally { Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(MapPath);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = false;
        importer.alphaIsTransparency = false; importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.anisoLevel = 4;
        importer.userData = Stamp();
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
