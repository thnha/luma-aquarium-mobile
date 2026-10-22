using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Carpet plants painted onto the sand with a brush instead of placed one by one. The placement area
// is a grid of cells; each cell holds one carpet species or none. Carpets are sold in packs of
// PackCells cells and do not count toward the tank's points.
public sealed class AquariumCarpet : MonoBehaviour
{
    public const int PackCells = 10;
    public float cellSize = .1f;
    private AquariumDecoration decoration;
    private byte[] cells = new byte[0];
    private int columns, rows;
    private readonly Dictionary<int, MeshFilter> layers = new Dictionary<int, MeshFilter>();
    private bool dirty;

    private string StorageKey => decoration.storageKey + ".Carpet";

    // Called again when the tank changes size: the grid follows the new placement area and keeps the
    // cells that still fit, lined up on the centre and the front glass.
    public void Init(AquariumDecoration owner)
    {
        decoration = owner;
        byte[] before = cells;
        int beforeColumns = columns, beforeRows = rows;
        columns = Mathf.Max(1, Mathf.CeilToInt((decoration.max.x - decoration.min.x) / cellSize));
        rows = Mathf.Max(1, Mathf.CeilToInt((decoration.max.y - decoration.min.y) / cellSize));
        cells = new byte[columns * rows];
        if (before.Length > 0) CopyFrom(before, beforeColumns, beforeRows);
        else if (decoration.persistLayout) Load();
        dirty = true;
    }

    private void CopyFrom(IReadOnlyList<byte> from, int fromColumns, int fromRows)
    {
        int shift = (fromColumns - columns) / 2;
        for (int z = 0; z < rows && z < fromRows; z++)
            for (int x = 0; x < columns; x++)
            {
                int source = x + shift;
                if (source >= 0 && source < fromColumns) cells[z * columns + x] = from[z * fromColumns + source];
            }
    }

    public int Count(int species)
    {
        int count = 0;
        foreach (byte cell in cells) if (cell == species) count++;
        return count;
    }

    public byte[] Snapshot() => (byte[])cells.Clone();

    public void Restore(byte[] snapshot)
    {
        if (snapshot == null || snapshot.Length != cells.Length) return;
        Array.Copy(snapshot, cells, cells.Length);
        dirty = true;
    }

    // Paints every cell whose centre lies within the radius; species 0 erases. A species stops at its
    // allowance of cells. Returns whether anything changed.
    public bool Paint(Vector3 point, float radius, int species, int allowance)
    {
        bool changed = false;
        int used = species > 0 ? Count(species) : 0;
        for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                int i = z * columns + x;
                if (cells[i] == species) continue;
                Vector3 centre = Centre(x, z);
                if ((centre.x - point.x) * (centre.x - point.x) + (centre.z - point.z) * (centre.z - point.z) > radius * radius) continue;
                if (species > 0 && used >= allowance) return changed;
                cells[i] = (byte)species;
                if (species > 0) used++;
                changed = true;
            }
        dirty |= changed;
        return changed;
    }

    private Vector3 Centre(int x, int z) =>
        new Vector3(decoration.min.x + (x + .5f) * cellSize, 0, decoration.min.y + (z + .5f) * cellSize);

    // Grid size, then one digit per cell.
    public void Save()
    {
        if (!decoration.persistLayout) return;
        var text = new StringBuilder(columns + "x" + rows + ":");
        foreach (byte cell in cells) text.Append((char)('0' + cell));
        PlayerPrefs.SetString(StorageKey, text.ToString());
        PlayerPrefs.Save();
    }

    private void Load()
    {
        // A save from a grid of another size (another tank) keeps the cells that fit this one.
        string text = PlayerPrefs.GetString(StorageKey, "");
        int colon = text.IndexOf(':'), times = text.IndexOf('x');
        if (times <= 0 || colon <= times || !int.TryParse(text.Substring(0, times), out int savedColumns) ||
            !int.TryParse(text.Substring(times + 1, colon - times - 1), out int savedRows) ||
            savedColumns <= 0 || savedRows <= 0 || text.Length != colon + 1 + savedColumns * savedRows) return;
        var saved = new byte[savedColumns * savedRows];
        for (int i = 0; i < saved.Length; i++)
        {
            int cell = text[colon + 1 + i] - '0';
            saved[i] = (byte)(cell >= 0 && cell <= 9 ? cell : 0);
        }
        CopyFrom(saved, savedColumns, savedRows);
    }

    private void LateUpdate()
    {
        if (!dirty || decoration == null) return;
        dirty = false;
        Rebuild();
    }

    private void Rebuild()
    {
        var bySpecies = new Dictionary<int, (List<Vector3> v, List<Color> c, List<int> t)>();
        for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                int species = cells[z * columns + x];
                if (species == 0) continue;
                if (!bySpecies.TryGetValue(species, out var mesh))
                    bySpecies[species] = mesh = (new List<Vector3>(), new List<Color>(), new List<int>());
                var random = new System.Random(z * 7919 + x * 131 + species);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 at = Centre(x, z) + new Vector3(((float)random.NextDouble() - .5f) * cellSize, 0, ((float)random.NextDouble() - .5f) * cellSize);
                    at.y = decoration.SandAt(at) - .004f;
                    AddTuft(mesh.v, mesh.c, mesh.t, species, at, random);
                }
            }
        foreach (var layer in layers)
            if (!bySpecies.ContainsKey(layer.Key)) layer.Value.sharedMesh.Clear();
        foreach (var entry in bySpecies)
        {
            var filter = Layer(entry.Key);
            if (filter == null) continue;
            var mesh = filter.sharedMesh;
            mesh.Clear();
            mesh.indexFormat = entry.Value.v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(entry.Value.v);
            mesh.SetColors(entry.Value.c);
            mesh.SetTriangles(entry.Value.t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }

    // One renderer per species, using the material of that species' shop item.
    private MeshFilter Layer(int species)
    {
        if (layers.TryGetValue(species, out var filter)) return filter;
        Material material = null;
        if (decoration.catalog != null)
            foreach (var offer in decoration.catalog.offers)
                if (offer.prefab != null && offer.prefab.carpet == species)
                {
                    var renderer = offer.prefab.GetComponentInChildren<MeshRenderer>();
                    if (renderer != null) material = renderer.sharedMaterial;
                    break;
                }
        if (material == null) return null;
        var go = new GameObject("Carpet" + species, typeof(MeshFilter), typeof(MeshRenderer));
        // Vertices are in world space, so the layer stays at the scene root, unscaled.
        filter = go.GetComponent<MeshFilter>();
        filter.sharedMesh = new Mesh { name = "Carpet" + species };
        var meshRenderer = go.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        layers[species] = filter;
        return filter;
    }

    // A tuft of one carpet species standing at a point: 1 is hairgrass (thin upright blades),
    // 2 is Monte Carlo (tiny round leaves close to the sand). Colours ride on vertex colours.
    public static void AddTuft(List<Vector3> v, List<Color> c, List<int> t, int species, Vector3 at, System.Random random)
    {
        Func<float> next = () => (float)random.NextDouble();
        if (species == 1)
        {
            for (int n = 0; n < 9; n++)
            {
                float a = n * 2.39996f + next(), height = .045f + next() * .05f, lean = .2f + next() * .35f;
                var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var side = new Vector3(-outward.z, 0, outward.x) * .0035f;
                Vector3 root = at + outward * next() * .012f, mid = root + outward * lean * height * .4f + Vector3.up * height * .55f;
                Vector3 tip = root + outward * lean * height + Vector3.up * height;
                int start = v.Count;
                v.Add(root - side); v.Add(root + side); v.Add(mid - side * .6f); v.Add(mid + side * .6f); v.Add(tip);
                Color dark = new Color(.16f, .38f, .1f), light = new Color(.45f, .72f, .24f);
                c.Add(dark); c.Add(dark); c.Add(Color.Lerp(dark, light, .5f)); c.Add(Color.Lerp(dark, light, .5f)); c.Add(light);
                t.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3, start + 2, start + 4, start + 3 });
            }
        }
        else
        {
            for (int n = 0; n < 12; n++)
            {
                float a = n * 2.39996f + next(), r = Mathf.Sqrt(next()) * .03f, size = .007f + next() * .005f;
                Vector3 centre = at + new Vector3(Mathf.Cos(a) * r, .006f + next() * .02f, Mathf.Sin(a) * r);
                float turn = next() * Mathf.PI;
                var u = new Vector3(Mathf.Cos(turn), .25f, Mathf.Sin(turn)) * size;
                var w = new Vector3(-Mathf.Sin(turn), .1f, Mathf.Cos(turn)) * size;
                int start = v.Count;
                v.Add(centre + u); v.Add(centre + w); v.Add(centre - u); v.Add(centre - w);
                Color leaf = Color.Lerp(new Color(.28f, .6f, .16f), new Color(.5f, .8f, .26f), next());
                for (int k = 0; k < 4; k++) c.Add(leaf);
                t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var layer in layers)
            if (layer.Value != null) { Destroy(layer.Value.sharedMesh); Destroy(layer.Value.gameObject); }
    }
}
