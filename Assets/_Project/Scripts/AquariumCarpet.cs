using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Carpet plants painted onto the sand with a brush instead of placed one by one. The placement area
// is a grid of cells; each cell holds one carpet species or none. Climbing species (3 and up) also
// grow as patches on stone and wood and on the back glass. Carpets are sold in packs of PackCells
// cells and do not count toward the tank's points.
public sealed class AquariumCarpet : MonoBehaviour
{
    public const int PackCells = 10;
    // Patch meshes ride on their stone or wood under this name; they are not part of its surface.
    public const string PatchLayerName = "CarpetPatches";
    public float cellSize = .1f;
    // Patches on stone, wood and glass sit this far apart; three of them use up one cell.
    public const float PatchSpacing = .045f;
    private const int PatchesPerCell = 3;
    private AquariumDecoration decoration;
    private byte[] cells = new byte[0];
    private int columns, rows;
    private List<Patch> patches = new List<Patch>();
    private readonly Dictionary<int, MeshFilter> layers = new Dictionary<int, MeshFilter>();
    private readonly Dictionary<(string host, int species), MeshFilter> patchLayers = new Dictionary<(string, int), MeshFilter>();
    private bool dirty;

    // A tuft grown on a host: its position and normal are in the host's space. With no host it is on the
    // back glass, at x and the height above the sand floor.
    [Serializable] public struct Patch
    {
        public byte species;
        public string host;
        public Vector3 position, normal;
    }

    public sealed class State
    {
        public byte[] cells;
        public List<Patch> patches;
    }

    // Hairgrass and Monte Carlo root in the sand only.
    public static bool Climbs(int species) => species >= 3;

    private string StorageKey => decoration.storageKey + ".Carpet";
    private string PatchKey => decoration.storageKey + ".CarpetPatches";

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
        patches.RemoveAll(p => string.IsNullOrEmpty(p.host) && (p.position.x < decoration.min.x || p.position.x > decoration.max.x));
        dirty = true;
    }

    // The pieces were replaced (undo, a loaded layout): patch meshes go onto the new ones.
    public void Refresh() => dirty = true;

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

    // Cells on the sand plus the patches on pieces still in the tank and on the glass.
    public int Count(int species)
    {
        int count = 0, grown = 0;
        foreach (byte cell in cells) if (cell == species) count++;
        foreach (var patch in patches) if (patch.species == species && Alive(patch)) grown++;
        return count + (grown + PatchesPerCell - 1) / PatchesPerCell;
    }

    private bool Alive(Patch patch) => string.IsNullOrEmpty(patch.host) || decoration.FindPlaced(patch.host) != null;

    public State Snapshot() => new State { cells = (byte[])cells.Clone(), patches = new List<Patch>(patches) };

    public void Restore(State snapshot)
    {
        if (snapshot == null || snapshot.cells.Length != cells.Length) return;
        Array.Copy(snapshot.cells, cells, cells.Length);
        patches = new List<Patch>(snapshot.patches);
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

    // Grows (or with species 0 erases) patches on a stone or piece of wood, or on the back glass when host is
    // null, around a world point. Another species within reach gives way. Returns whether anything changed.
    public bool PaintSurface(AquariumDecorItem host, Vector3 point, Vector3 normal, float radius, int species, int allowance)
    {
        string id = host != null ? host.instanceId : "";
        bool changed = false;
        int nearSame = 0;
        for (int i = patches.Count - 1; i >= 0; i--)
        {
            var patch = patches[i];
            if (patch.host != id) continue;
            float distance = Vector3.Distance(WorldOf(patch, host), point);
            bool erase = species == 0 ? distance <= radius : patch.species != species && distance < PatchSpacing;
            if (erase) { patches.RemoveAt(i); changed = true; }
            else if (patch.species == species && distance < PatchSpacing) nearSame++;
        }
        if (species > 0 && nearSame == 0 && Count(species) < allowance)
        {
            patches.Add(host != null
                ? new Patch { species = (byte)species, host = id, position = host.transform.InverseTransformPoint(point),
                    normal = host.transform.InverseTransformDirection(normal).normalized }
                : new Patch { species = (byte)species, host = "", position = new Vector3(point.x, point.y - decoration.floorY, 0), normal = Vector3.back });
            changed = true;
        }
        dirty |= changed;
        return changed;
    }

    private Vector3 WorldOf(Patch patch, AquariumDecorItem host) =>
        host != null ? host.transform.TransformPoint(patch.position) : GlassPoint(patch);

    private Vector3 GlassPoint(Patch patch) => new Vector3(patch.position.x, decoration.floorY + patch.position.y, decoration.WallZ - .004f);

    private Vector3 Centre(int x, int z) =>
        new Vector3(decoration.min.x + (x + .5f) * cellSize, 0, decoration.min.y + (z + .5f) * cellSize);

    // Grid size, then one digit per cell.
    public void Save()
    {
        if (Lumaquarium.Account.AccountSignIn.ProgressBlocked) return;
        if (!decoration.persistLayout) return;
        var text = new StringBuilder(columns + "x" + rows + ":");
        foreach (byte cell in cells) text.Append((char)('0' + cell));
        PlayerPrefs.SetString(StorageKey, text.ToString());
        // Patches on pieces that went back to storage go with them.
        patches.RemoveAll(p => !Alive(p));
        PlayerPrefs.SetString(PatchKey, JsonUtility.ToJson(new PatchList { items = patches }));
        PlayerPrefs.Save();
    }

    [Serializable] private class PatchList { public List<Patch> items = new List<Patch>(); }

    private void Load()
    {
        LoadPatches();
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

    private void LoadPatches()
    {
        PatchList saved = null;
        try { saved = JsonUtility.FromJson<PatchList>(PlayerPrefs.GetString(PatchKey, "")); }
        catch (ArgumentException) { }
        patches = saved?.items?.FindAll(p => Climbs(p.species) && p.species <= 9 && float.IsFinite(p.position.sqrMagnitude) &&
            p.normal.sqrMagnitude > .5f) ?? new List<Patch>();
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
        var byHost = new Dictionary<(string, int), (List<Vector3> v, List<Color> c, List<int> t)>();
        foreach (var patch in patches)
        {
            var host = string.IsNullOrEmpty(patch.host) ? null : decoration.FindPlaced(patch.host);
            if (host == null && !string.IsNullOrEmpty(patch.host)) continue;
            // Glass patches share the world-space layer of their species with the sand.
            var key = (patch.host, (int)patch.species);
            (List<Vector3> v, List<Color> c, List<int> t) mesh;
            if (host == null)
            {
                if (!bySpecies.TryGetValue(patch.species, out mesh)) bySpecies[patch.species] = mesh = (new List<Vector3>(), new List<Color>(), new List<int>());
            }
            else if (!byHost.TryGetValue(key, out mesh)) byHost[key] = mesh = (new List<Vector3>(), new List<Color>(), new List<int>());
            var random = new System.Random(patch.position.GetHashCode() ^ patch.species);
            // Two tufts spread over the patch's share of the surface, so neighbouring patches close up.
            Vector3 at = host == null ? GlassPoint(patch) : patch.position;
            Vector3 across = Vector3.Cross(patch.normal, Mathf.Abs(patch.normal.y) < .9f ? Vector3.up : Vector3.right).normalized;
            Vector3 along = Vector3.Cross(patch.normal, across);
            for (int k = 0; k < 2; k++)
            {
                Vector3 shift = (across * ((float)random.NextDouble() - .5f) + along * ((float)random.NextDouble() - .5f)) * PatchSpacing;
                AddTuft(mesh.v, mesh.c, mesh.t, patch.species, at + shift, patch.normal, random);
            }
        }
        foreach (var layer in layers)
            if (!bySpecies.ContainsKey(layer.Key)) layer.Value.sharedMesh.Clear();
        foreach (var entry in bySpecies)
        {
            var filter = Layer(entry.Key);
            if (filter != null) Fill(filter.sharedMesh, entry.Value);
        }
        // Layers of pieces that were destroyed (put away, or replaced by undo with a new piece of the same id)
        // are dropped; their meshes go with them.
        foreach (var key in new List<(string, int)>(patchLayers.Keys))
        {
            var filter = patchLayers[key];
            var host = decoration.FindPlaced(key.Item1);
            if (filter != null && byHost.ContainsKey(key) && host != null && filter.transform.parent == host.transform) continue;
            if (filter != null) { Destroy(filter.sharedMesh); Destroy(filter.gameObject); }
            patchLayers.Remove(key);
        }
        foreach (var entry in byHost)
        {
            var filter = PatchLayer(decoration.FindPlaced(entry.Key.Item1), entry.Key.Item2);
            if (filter != null) Fill(filter.sharedMesh, entry.Value);
        }
    }

    private static void Fill(Mesh mesh, (List<Vector3> v, List<Color> c, List<int> t) data)
    {
        mesh.Clear();
        mesh.indexFormat = data.v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(data.v);
        mesh.SetColors(data.c);
        mesh.SetTriangles(data.t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // Patches on a piece are built in its space under it, so they move and lean with it.
    private MeshFilter PatchLayer(AquariumDecorItem host, int species)
    {
        if (patchLayers.TryGetValue((host.instanceId, species), out var filter)) return filter;
        var material = MaterialOf(species);
        if (material == null) return null;
        var go = new GameObject(PatchLayerName + species, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(host.transform, false);
        filter = go.GetComponent<MeshFilter>();
        filter.sharedMesh = new Mesh { name = PatchLayerName + species };
        var meshRenderer = go.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        patchLayers[(host.instanceId, species)] = filter;
        return filter;
    }

    private Material MaterialOf(int species)
    {
        if (decoration.catalog == null) return null;
        foreach (var offer in decoration.catalog.offers)
            if (offer.prefab != null && offer.prefab.carpet == species)
            {
                var renderer = offer.prefab.GetComponentInChildren<MeshRenderer>();
                return renderer != null ? renderer.sharedMaterial : null;
            }
        return null;
    }

    // One renderer per species, using the material of that species' shop item.
    private MeshFilter Layer(int species)
    {
        if (layers.TryGetValue(species, out var filter)) return filter;
        var material = MaterialOf(species);
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

    public static void AddTuft(List<Vector3> v, List<Color> c, List<int> t, int species, Vector3 at, System.Random random) =>
        AddTuft(v, c, t, species, at, Vector3.up, random);

    // A tuft of one carpet species grown at a point, standing out of the surface along its normal.
    public static void AddTuft(List<Vector3> v, List<Color> c, List<int> t, int species, Vector3 at, Vector3 normal, System.Random random)
    {
        int first = v.Count;
        Grow(v, c, t, species, random);
        // Off the sand the tuft sinks a little into the stone, wood or glass so no gap shows at its base.
        var turn = Quaternion.FromToRotation(Vector3.up, normal);
        Vector3 offset = normal == Vector3.up ? at : at - normal * .003f;
        for (int i = first; i < v.Count; i++) v[i] = turn * v[i] + offset;
    }

    // One tuft around the origin, growing up +Y. 1 is hairgrass (thin upright blades), 2 Monte Carlo (tiny
    // round leaves close to the sand), 3 HC Cuba (even tinier bright leaves packed flat), 4 Java moss (low
    // feathery fronds), 5 Riccia (forked lime ribbons in a cushion), 6 Marimo (velvet domes of algae).
    // Colours ride on vertex colours.
    private static void Grow(List<Vector3> v, List<Color> c, List<int> t, int species, System.Random random)
    {
        Func<float> next = () => (float)random.NextDouble();
        if (species == 1)
        {
            for (int n = 0; n < 9; n++)
            {
                float a = n * 2.39996f + next(), height = .045f + next() * .05f, lean = .2f + next() * .35f;
                var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var side = new Vector3(-outward.z, 0, outward.x) * .0035f;
                Vector3 root = outward * next() * .012f, mid = root + outward * lean * height * .4f + Vector3.up * height * .55f;
                Vector3 tip = root + outward * lean * height + Vector3.up * height;
                int start = v.Count;
                v.Add(root - side); v.Add(root + side); v.Add(mid - side * .6f); v.Add(mid + side * .6f); v.Add(tip);
                Color dark = new Color(.16f, .38f, .1f), light = new Color(.45f, .72f, .24f);
                c.Add(dark); c.Add(dark); c.Add(Color.Lerp(dark, light, .5f)); c.Add(Color.Lerp(dark, light, .5f)); c.Add(light);
                t.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3, start + 2, start + 4, start + 3 });
            }
        }
        else if (species == 2 || species == 3)
        {
            // HC Cuba packs more, smaller and lighter leaves lower than Monte Carlo.
            bool cuba = species == 3;
            int leaves = cuba ? 30 : 12;
            for (int n = 0; n < leaves; n++)
            {
                float a = n * 2.39996f + next(), r = Mathf.Sqrt(next()) * .03f;
                float size = cuba ? .005f + next() * .004f : .007f + next() * .005f;
                Vector3 centre = new Vector3(Mathf.Cos(a) * r, (cuba ? .003f : .006f) + next() * (cuba ? .009f : .02f), Mathf.Sin(a) * r);
                float turn = next() * Mathf.PI;
                var u = new Vector3(Mathf.Cos(turn), .25f, Mathf.Sin(turn)) * size;
                var w = new Vector3(-Mathf.Sin(turn), .1f, Mathf.Cos(turn)) * size;
                int start = v.Count;
                v.Add(centre + u); v.Add(centre + w); v.Add(centre - u); v.Add(centre - w);
                Color leaf = cuba ? Color.Lerp(new Color(.24f, .54f, .12f), new Color(.46f, .78f, .22f), next())
                    : Color.Lerp(new Color(.28f, .6f, .16f), new Color(.5f, .8f, .26f), next());
                for (int k = 0; k < 4; k++) c.Add(leaf);
                t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }
        else if (species == 4)
        {
            Color dark = new Color(.08f, .22f, .06f), light = new Color(.26f, .46f, .12f);
            for (int n = 0; n < 9; n++)
            {
                float a = n * 2.39996f + next() * .5f, length = .026f + next() * .018f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0, dir.x);
                Vector3 p = dir * next() * .008f;
                const int segments = 5;
                for (int i = 1; i <= segments; i++)
                {
                    float f = i / (float)segments;
                    dir = (dir + side * (next() - .5f) * .3f).normalized;
                    side = new Vector3(-dir.z, 0, dir.x);
                    p += dir * (length / segments);
                    p.y = .001f + .007f * Mathf.Sin(Mathf.PI * f * .8f);
                    float leaf = .009f * (1 - .4f * f);
                    Color tone = Color.Lerp(dark, light, f * (.8f + .3f * next()));
                    for (int s = -1; s <= 1; s += 2)
                        Leaf(v, c, t, p, (dir * .45f + side * s * .55f + Vector3.up * .1f).normalized, leaf, leaf * .5f, tone);
                }
                Leaf(v, c, t, p, dir, .006f, .003f, light);
            }
        }
        else if (species == 5)
        {
            Color dark = new Color(.24f, .52f, .12f), light = new Color(.46f, .76f, .22f);
            for (int n = 0; n < 12; n++)
            {
                float a = n * 2.39996f + next() * .4f, r = .004f + next() * .016f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                // Ribbons near the middle stand higher, rounding the cushion.
                Vector3 root = dir * r + Vector3.up * (.002f + .01f * (1 - r / .02f) * next());
                Vector3 fork = root + dir * (.011f + next() * .007f) + Vector3.up * .002f;
                Ribbon(v, c, t, root, fork, .0065f, dark, Color.Lerp(dark, light, .5f));
                for (int s = -1; s <= 1; s += 2)
                {
                    float spread = .45f + next() * .25f;
                    var branch = new Vector3(Mathf.Cos(a + s * spread), 0, Mathf.Sin(a + s * spread));
                    Ribbon(v, c, t, fork, fork + branch * (.01f + next() * .007f) + Vector3.up * .001f, .0055f, Color.Lerp(dark, light, .5f), light);
                }
            }
        }
        else
        {
            int domes = 5 + (int)(next() * 3);
            for (int n = 0; n < domes; n++)
            {
                float a = n * 2.39996f + next(), r = Mathf.Sqrt(next()) * .026f, radius = .007f + next() * .006f;
                var centre = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                Dome(v, c, t, centre, radius, next);
            }
        }
    }

    // A flat diamond leaf; the carpet material draws both faces.
    private static void Leaf(List<Vector3> v, List<Color> c, List<int> t, Vector3 p, Vector3 dir, float length, float width, Color color)
    {
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        side = side.sqrMagnitude < 1e-6f ? Vector3.right : side.normalized;
        int i = v.Count;
        v.Add(p); v.Add(p + dir * (length * .45f) + side * (width * .5f));
        v.Add(p + dir * length); v.Add(p + dir * (length * .45f) - side * (width * .5f));
        c.Add(color * .85f); c.Add(color); c.Add(color * 1.1f); c.Add(color);
        t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
    }

    // A flat strip from one point to another, narrowing a little toward its end.
    private static void Ribbon(List<Vector3> v, List<Color> c, List<int> t, Vector3 from, Vector3 to, float width, Color start, Color end)
    {
        Vector3 dir = to - from, side = Vector3.Cross(Vector3.up, dir);
        side = side.sqrMagnitude < 1e-9f ? Vector3.right : side.normalized;
        int i = v.Count;
        v.Add(from - side * width * .5f); v.Add(from + side * width * .5f);
        v.Add(to - side * width * .4f); v.Add(to + side * width * .4f);
        c.Add(start); c.Add(start); c.Add(end); c.Add(end);
        t.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
    }

    // A low velvet dome: rings from the base up to a cap, speckled darker and lighter.
    private static void Dome(List<Vector3> v, List<Color> c, List<int> t, Vector3 centre, float radius, Func<float> next)
    {
        const int around = 10, up = 4;
        Color dark = new Color(.04f, .15f, .05f), light = new Color(.17f, .36f, .11f);
        int start = v.Count;
        for (int ring = 0; ring < up; ring++)
        {
            float phi = ring / (float)up * Mathf.PI * .5f;
            for (int k = 0; k < around; k++)
            {
                float theta = k / (float)around * Mathf.PI * 2;
                float bump = 1 + (next() - .5f) * .25f;
                v.Add(centre + new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), Mathf.Sin(phi) * .75f, Mathf.Sin(theta) * Mathf.Cos(phi)) * (radius * bump));
                c.Add(Color.Lerp(dark, light, ring / (float)up * .4f + next() * .6f));
            }
        }
        int cap = v.Count;
        v.Add(centre + Vector3.up * radius * .75f);
        c.Add(light);
        for (int ring = 0; ring < up; ring++)
            for (int k = 0; k < around; k++)
            {
                int a = start + ring * around + k, b = start + ring * around + (k + 1) % around;
                if (ring == up - 1) { t.AddRange(new[] { a, cap, b }); continue; }
                t.AddRange(new[] { a, a + around, b, b, a + around, b + around });
            }
    }

    private void OnDestroy()
    {
        foreach (var layer in layers)
            if (layer.Value != null) { Destroy(layer.Value.sharedMesh); Destroy(layer.Value.gameObject); }
        foreach (var layer in patchLayers.Values)
            if (layer != null) { Destroy(layer.sharedMesh); Destroy(layer.gameObject); }
    }
}
