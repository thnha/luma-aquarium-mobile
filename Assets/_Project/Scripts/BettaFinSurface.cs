using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Skin stays opaque; the thin membranes use the painted alpha continuously instead
// of losing half their area to the skin material's hard cutout threshold.
public static class BettaFinSurface
{
    static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();
    static readonly Dictionary<Material, Material> Sources = new Dictionary<Material, Material>();

    public static Material Membrane(Material source)
    {
        if (!source) return source;
        if (Sources.TryGetValue(source, out Material original)) source = original;
        if (Materials.TryGetValue(source, out Material cached) && cached) return cached;
        // The referenced Resources material also keeps the transparent shader variants
        // in player builds; a keyword used only by runtime clones can otherwise be stripped.
        var template = Resources.Load<Material>("BettaMembrane");
        var material = new Material(template ? template : source) { name = source.name + "_Membrane" };
        Configure(material, source);
        Materials[source] = material;
        Sources[material] = source;
        return material;
    }

    // The prefab builder refreshes from the current skin even if this domain already
    // cached a membrane before the skin's texture/settings were edited.
    public static void Configure(Material material, Material source)
    {
        material.CopyPropertiesFromMaterial(source);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_AlphaToMask", 0f);
        material.SetFloat("_Cutoff", .08f);
        material.SetFloat("_BumpScale", .12f);
        material.SetColor("_BaseColor", new Color(1f, 1f, 1f, .82f));
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetShaderPassEnabled("DepthOnly", false);
        material.SetShaderPassEnabled("DepthNormals", false);
    }

    public static void Split(Mesh shaped, Mesh source, Transform[] bones)
    {
        BoneWeight[] weights = source.boneWeights;
        var ledBySkin = new bool[weights.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            string name = bones[weights[i].boneIndex0].name;
            ledBySkin[i] = name == "Root" || name == "Head" || name.StartsWith("Spine_")
                || name.StartsWith("Gill_") || name.StartsWith("Jaw_") || name.StartsWith("Eye_") || name.StartsWith("Belly");
        }
        int[] triangles = source.triangles;
        bool[] skin = BodyIslands(source.vertices, source.uv, triangles, ledBySkin);
        var body = new List<int>();
        var fin = new List<int>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var target = skin[triangles[i]] ? body : fin;
            target.Add(triangles[i]); target.Add(triangles[i + 1]); target.Add(triangles[i + 2]);
        }
        shaped.subMeshCount = 2;
        shaped.SetTriangles(body, 0);
        shaped.SetTriangles(fin, 1);
    }

    // Shared with the editor's variant-mask baker: classify complete UV islands so
    // belly vertices weighted to fin roots still belong to the opaque body.
    public static bool[] BodyIslands(Vector3[] positions, Vector2[] uv, int[] triangles, bool[] coreLed)
    {
        var copies = new Dictionary<(Vector3Int, Vector2Int), int>();
        var parent = new int[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var key = (Vector3Int.RoundToInt(positions[i] * 100000f), Vector2Int.RoundToInt(uv[i] * 100000f));
            parent[i] = copies.TryGetValue(key, out int first) ? first : i;
            if (parent[i] == i) copies[key] = i;
        }
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        for (int i = 0; i < triangles.Length; i += 3)
        {
            parent[Find(triangles[i + 1])] = Find(triangles[i]);
            parent[Find(triangles[i + 2])] = Find(triangles[i]);
        }
        var size = new int[positions.Length];
        var core = new int[positions.Length];
        for (int i = 0; i < positions.Length; i++) { int root = Find(i); size[root]++; if (coreLed[i]) core[root]++; }
        var body = new bool[positions.Length];
        for (int i = 0; i < body.Length; i++) { int root = Find(i); body[i] = core[root] > size[root] * .15f; }
        return body;
    }
}
