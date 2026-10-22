using System;
using System.Collections.Generic;
using UnityEngine;

// The glass the fish lives in. Each tank size sets the inner width and depth, measured in screens
// (one screen is the width the camera fits, OrbitTouchCamera.fitHalfWidth * 2), and everything inside
// follows it: the sand and pebbles, the side and back walls, the placement area and its points, the
// carpet grid, the fish's swim bounds, the water surface, the camera's sideways pan and the lighting.
// The tank in use is kept with the wallet (FishShopInventory).
public sealed class AquariumTank : MonoBehaviour
{
    [Serializable]
    public sealed class Spec
    {
        public string id, displayName, description;
        public float widthScreens, depthScreens;
        public int price, capacity;
    }

    public static readonly Spec[] Specs =
    {
        new Spec { id = "tank_square", displayName = "Hồ vuông", description = "Rộng 1 màn hình · đáy vuông", widthScreens = 1, depthScreens = 1, price = 0, capacity = 24 },
        new Spec { id = "tank_deep", displayName = "Hồ sâu", description = "Rộng 1 màn hình · sâu gấp đôi", widthScreens = 1, depthScreens = 2, price = 1500, capacity = 40 },
        new Spec { id = "tank_wide", displayName = "Hồ rộng", description = "Rộng 1,5 màn hình · vuốt ngang để xem", widthScreens = 1.5f, depthScreens = 1.5f, price = 2500, capacity = 48 },
    };

    // Inner face of the front glass (world z), where the sand meets the bottom of the screen, so the fish
    // and the decorations reach right up to the viewer; the sand runs on toward the camera from here.
    public float front = -1.55f;
    // The tank's depth is measured from here, so the back glass stays put while the front reaches forward.
    public float depthFrom = -.55f;
    // How far the sand reaches toward the camera, under the front of the view.
    public float sandFront = -2.8f;
    public float wallTint = .6f;
    public Spec Current { get; private set; }
    public float Width { get; private set; }
    public float Back { get; private set; }

    private AquariumDecoration decoration;
    private Transform sand, pebbles, surface;
    private Vector3 surfacePosition, surfaceScale;
    private MeshRenderer walls;
    private Mesh sandMesh, wallMesh;
    private Material wallMaterial;
    private readonly List<GameObject> pebbleTiles = new List<GameObject>();
    private struct Look { public Texture map; public Color color; public float tiling; }
    private Material sandMaterial, pebbleMaterial, stoneSource;
    private Look wallLook, sandLook;
    private Color pebbleColor = Color.white;
    private AquariumSubstrateStyle substrate;
    private AquariumSurfaceCatalog Surfaces => decoration != null ? decoration.surfaces : null;

    public static Spec Find(string id) => Array.Find(Specs, x => x.id == id);

    // Called by AquariumDecoration before it loads the saved layout, so the layout is checked against this tank.
    public void Init(AquariumDecoration owner)
    {
        decoration = owner;
        sand = Child("SandBed3D");
        pebbles = Child("PebbleBed3D");
        PrepareMaterials();
        surface = decoration.water != null && decoration.water.surface != null ? decoration.water.surface.transform : null;
        if (surface != null) { surfacePosition = surface.position; surfaceScale = surface.localScale; }
        var inventory = new Lumaquarium.UI.FishShopInventory(decoration.inventoryStorageKey);
        Build(Find(inventory.ActiveTank) ?? Specs[0]);
        ApplyWall(inventory.ActiveWall);
        ApplySubstrate(inventory.ActiveSubstrate);
    }

    // Switches to another tank while playing: the layout keeps its positions, and pieces outside the
    // new glass are pulled inside or go back to stock (AquariumDecoration.Refit).
    public void Switch(string id)
    {
        var spec = Find(id);
        if (spec == null || spec == Current || decoration == null) return;
        decoration.Leave();
        Build(spec);
        decoration.Refit();
        var fish = decoration.screen != null ? decoration.screen.fish : null;
        if (fish != null && fish.swimBounds != null)
            fish.transform.position = fish.swimBounds.ClosestPoint(fish.transform.position);
    }

    private void Build(Spec spec)
    {
        Current = spec;
        float half = decoration.orbit != null && decoration.orbit.fitHalfWidth > 0 ? decoration.orbit.fitHalfWidth : .85f;
        Width = spec.widthScreens * half * 2;
        Back = depthFrom + spec.depthScreens * half * 2;
        float x = Width / 2;
        decoration.min = new Vector2(-x + .05f, front + .05f);
        decoration.max = new Vector2(x - .05f, Back - .05f);
        decoration.capacity = spec.capacity;

        float top = decoration.water != null ? decoration.water.surfaceY : decoration.floorY + 2.1f;
        BuildSand(x);
        BuildWalls(x, top);
        // The water surface mirrors the inside of the glass (Aquarium Water).
        Shader.SetGlobalVector("_AquariumTankBox", new Vector4(x, front, Back + .05f, decoration.floorY));
        TilePebbles(x);
        if (surface != null)
        {
            // The surface mesh is centred on its pivot; stretch it from over the near sand, so it fills the top
            // of the screen, to past the back glass.
            var bounds = surface.GetComponent<Renderer>().localBounds;
            float frontZ = Mathf.Min(surfacePosition.z + bounds.min.z * surfaceScale.z, sandFront), backZ = Back + .3f;
            surface.localScale = new Vector3(Mathf.Max(surfaceScale.x, (x + .3f) * 2 / bounds.size.x), surfaceScale.y, (backZ - frontZ) / bounds.size.z);
            surface.position = new Vector3(surfacePosition.x, surfacePosition.y, (frontZ + backZ) / 2);
        }
        var fish = decoration.screen != null ? decoration.screen.fish : null;
        if (fish != null && fish.swimBounds != null)
        {
            // Keep the swim height; open the swim space to the glass on every side.
            var box = fish.swimBounds;
            var world = box.bounds;
            var centre = new Vector3(0, world.center.y, (front + Back) / 2);
            var size = new Vector3(Width - .04f, world.size.y, Back - front - .08f);
            var scale = box.transform.lossyScale;
            box.center = box.transform.InverseTransformPoint(centre);
            box.size = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
        }
        if (decoration.orbit != null)
        {
            decoration.orbit.panLimit = Mathf.Max(0, x - half);
            decoration.orbit.pan = Mathf.Clamp(decoration.orbit.pan, -decoration.orbit.panLimit, decoration.orbit.panLimit);
        }
        var lighting = GetComponent<AquariumLighting>();
        if (lighting != null && lighting.isActiveAndEnabled) lighting.Retank();
        var shafts = GetComponentInChildren<AquariumSunShafts>();
        if (shafts != null) shafts.Rebuild();
    }

    // Sand under the whole tank and on toward the camera, with the same small bumps as the original bed
    // and the sand's rise toward the back (AquariumDecoration.SandAt).
    private void BuildSand(float x)
    {
        if (sand == null) return;
        const float step = .05f;
        float left = -x - .05f, near = sandFront, far = Back + .05f;
        int columns = Mathf.CeilToInt((x * 2 + .1f) / step), rows = Mathf.CeilToInt((far - near) / step);
        var vertices = new Vector3[(columns + 1) * (rows + 1)];
        var triangles = new int[columns * rows * 6];
        float baseY = sand.position.y;
        for (int z = 0, v = 0, t = 0; z <= rows; z++)
            for (int c = 0; c <= columns; c++, v++)
            {
                var world = new Vector3(left + c * (x * 2 + .1f) / columns, 0, near + z * (far - near) / rows);
                world.y = baseY + .025f * Mathf.PerlinNoise(world.x * 6 + 10, world.z * 6 + 20) + decoration.SandAt(world) - decoration.floorY;
                vertices[v] = sand.InverseTransformPoint(world);
                if (c == columns || z == rows) continue;
                triangles[t++] = v; triangles[t++] = v + columns + 1; triangles[t++] = v + 1;
                triangles[t++] = v + 1; triangles[t++] = v + columns + 1; triangles[t++] = v + columns + 2;
            }
        if (sandMesh == null) sandMesh = new Mesh { name = "TankSand" };
        sandMesh.Clear();
        sandMesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        sandMesh.vertices = vertices;
        sandMesh.triangles = triangles;
        sandMesh.RecalculateNormals();
        sandMesh.RecalculateBounds();
        sand.GetComponent<MeshFilter>().sharedMesh = sandMesh;
    }

    // Stone walls on both sides and at the back, from under the sand to just above the surface. They take
    // the lamp, caustics and depth tint like the stones in the tank (Aquarium Hardscape).
    private void BuildWalls(float x, float top)
    {
        if (walls == null)
        {
            var go = new GameObject("TankWalls", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            walls = go.GetComponent<MeshRenderer>();
            walls.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var stone = stoneSource;
            if (stone == null && sand != null) stone = sand.GetComponent<Renderer>().sharedMaterial;
            if (stone != null)
            {
                wallMaterial = new Material(stone) { name = "TankWall" };
                if (wallMaterial.HasProperty("_BaseColor"))
                    wallMaterial.SetColor("_BaseColor", wallMaterial.GetColor("_BaseColor") * wallTint);
                walls.sharedMaterial = wallMaterial;
                wallLook = Capture(wallMaterial);
            }
            wallMesh = new Mesh { name = "TankWalls" };
            go.GetComponent<MeshFilter>().sharedMesh = wallMesh;
        }
        walls.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        walls.transform.localScale = Vector3.one;
        float bottom = decoration.floorY - .1f, wallTop = top + .05f, back = Back + .05f;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int start = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            normals.AddRange(new[] { normal, normal, normal, normal });
            triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
        }
        // Each quad is a (bottom left), b (bottom right), c (top left), d (top right) seen from inside.
        Quad(new Vector3(-x, bottom, front), new Vector3(-x, bottom, back), new Vector3(-x, wallTop, front), new Vector3(-x, wallTop, back), Vector3.right);
        Quad(new Vector3(-x, bottom, back), new Vector3(x, bottom, back), new Vector3(-x, wallTop, back), new Vector3(x, wallTop, back), Vector3.back);
        Quad(new Vector3(x, bottom, back), new Vector3(x, bottom, front), new Vector3(x, wallTop, back), new Vector3(x, wallTop, front), Vector3.left);
        wallMesh.Clear();
        wallMesh.SetVertices(vertices);
        wallMesh.SetNormals(normals);
        wallMesh.SetTriangles(triangles, 0);
        wallMesh.RecalculateBounds();
    }

    // The pebble bed is one fixed patch; copies of it cover a larger floor. Pebbles beyond the glass
    // are hidden by the walls.
    private void TilePebbles(float x)
    {
        foreach (var tile in pebbleTiles) if (tile != null) Destroy(tile);
        pebbleTiles.Clear();
        if (pebbles == null) return;
        var bounds = pebbles.GetComponent<Renderer>().bounds;
        Vector2 size = new Vector2(bounds.size.x, bounds.size.z);
        int fromX = Mathf.FloorToInt((-x - bounds.min.x) / size.x), toX = Mathf.CeilToInt((x - bounds.max.x) / size.x);
        int toZ = Mathf.CeilToInt((Back - bounds.max.z) / size.y);
        for (int i = fromX; i <= toX; i++)
            for (int k = 0; k <= toZ; k++)
            {
                if (i == 0 && k == 0) continue;
                var tile = Instantiate(pebbles.gameObject, pebbles.parent);
                tile.name = "PebbleTile";
                tile.transform.position = pebbles.position + new Vector3(i * size.x, 0, k * size.y);
                pebbleTiles.Add(tile);
            }
        ShowPebbles();
    }

    // Shows a wall style from the surface catalog; style 0 (no texture) is the tank's own stone.
    public void ApplyWall(string id)
    {
        if (wallMaterial == null) return;
        var style = Surfaces != null ? Surfaces.Wall(id) : null;
        Show(wallMaterial, style != null && style.albedo != null
            ? new Look { map = style.albedo, color = style.tint, tiling = style.tiling } : wallLook);
    }

    // Shows a floor style: the sand texture, the pebble colour and whether the loose pebbles show at all.
    public void ApplySubstrate(string id)
    {
        substrate = Surfaces != null ? Surfaces.Substrate(id) : null;
        bool custom = substrate != null && substrate.albedo != null;
        if (sandMaterial != null)
            Show(sandMaterial, custom ? new Look { map = substrate.albedo, color = substrate.tint, tiling = substrate.tiling } : sandLook);
        if (pebbleMaterial != null) pebbleMaterial.SetColor("_BaseColor", custom ? substrate.pebbleTint : pebbleColor);
        ShowPebbles();
    }

    // forceRenderingOff keeps the renderer bounds that TilePebbles measures when the tank size changes.
    // It is a runtime flag that Instantiate does not copy, so new tiles get it from TilePebbles too.
    private void ShowPebbles()
    {
        bool on = substrate == null || substrate.pebbles;
        void Set(GameObject go)
        {
            var r = go != null ? go.GetComponent<Renderer>() : null;
            if (r != null) r.forceRenderingOff = !on;
        }
        if (pebbles != null) Set(pebbles.gameObject);
        foreach (var tile in pebbleTiles) Set(tile);
    }

    // The sand and pebbles get their own material copies so a floor style never changes the shared assets
    // (the pebble material is also the decoration stones'). The copies start as the original look.
    private void PrepareMaterials()
    {
        var pebbleRenderer = pebbles != null ? pebbles.GetComponent<Renderer>() : null;
        stoneSource = pebbleRenderer != null ? pebbleRenderer.sharedMaterial : null;
        if (stoneSource != null && stoneSource.HasProperty("_BaseColor"))
        {
            pebbleMaterial = new Material(stoneSource) { name = "TankPebbles" };
            pebbleColor = pebbleMaterial.GetColor("_BaseColor");
            pebbleRenderer.sharedMaterial = pebbleMaterial;
        }
        var sandRenderer = sand != null ? sand.GetComponent<Renderer>() : null;
        if (sandRenderer != null && sandRenderer.sharedMaterial != null && sandRenderer.sharedMaterial.HasProperty("_Tiling"))
        {
            sandMaterial = new Material(sandRenderer.sharedMaterial) { name = "TankSand" };
            sandLook = Capture(sandMaterial);
            sandRenderer.sharedMaterial = sandMaterial;
        }
    }

    private static Look Capture(Material material) => new Look
    {
        map = material.GetTexture("_BaseMap"), color = material.GetColor("_BaseColor"),
        tiling = material.HasProperty("_Tiling") ? material.GetFloat("_Tiling") : 3
    };

    private static void Show(Material material, Look look)
    {
        material.SetTexture("_BaseMap", look.map);
        material.SetColor("_BaseColor", look.color);
        if (material.HasProperty("_Tiling")) material.SetFloat("_Tiling", look.tiling);
    }

    private Transform Child(string name)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    private void OnDestroy()
    {
        if (sandMesh != null) Destroy(sandMesh);
        if (wallMesh != null) Destroy(wallMesh);
        if (wallMaterial != null) Destroy(wallMaterial);
        if (sandMaterial != null) Destroy(sandMaterial);
        if (pebbleMaterial != null) Destroy(pebbleMaterial);
    }
}
