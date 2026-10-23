using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Draws the tank wall patterns and the sand / gravel floors as tileable 512 px textures, their 256 px shop
// portraits, and the AquariumSurfaceCatalog listing them. Style 0 of each list keeps the tank's own look.
// To restyle one later, overwrite its PNG in Art/Aquarium3D/Surfaces; the catalog keeps pointing at it.
public static class AquariumSurfaceBuilder
{
    private const string Dir = "Assets/_Project/Art/Aquarium3D/Surfaces";
    private const string PortraitDir = Ui3D.Root + "/Portraits/Shop";
    private const string CatalogPath = Dir + "/AquariumSurfaceCatalog.asset";
    private const string EnvironmentPath = "Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab";
    private const string ScenePath = "Assets/_Project/Scenes/MainAquarium.unity";
    private const int Size = 512;

    [MenuItem("Lumaquarium/Build Surface Catalog")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Dir);
        Directory.CreateDirectory(PortraitDir);
        var stone = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Aquarium3D/Textures/RiverStone-v3.png");
        var sand = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Aquarium3D/Textures/RiverSand-v3.png");
        if (stone == null || sand == null) throw new InvalidOperationException("Run the hardscape builder first (RiverStone-v3 / RiverSand-v3).");

        var catalog = AssetDatabase.LoadAssetAtPath<AquariumSurfaceCatalog>(CatalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<AquariumSurfaceCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
        // The price sheet (SurfaceCatalogSheet) owns names, descriptions and prices: a rebuild keeps them.
        var kept = catalog.walls.Select(w => (w.id, w.displayName, w.description, w.price))
            .Concat(catalog.substrates.Select(f => (f.id, f.displayName, f.description, f.price))).ToDictionary(k => k.id);
        catalog.walls = new[]
        {
            // AquariumTank tints the original wall to 60 % (wallTint), so its portrait does too.
            Wall("wall_river", "Đá sỏi", "Vách đá sỏi tự nhiên của hồ", 0, Read(stone, new Color(.6f, .6f, .6f)), 3, true),
            Wall("wall_slate", "Đá xám", "Vách đá phiến xám có vân nứt", 300, Draw(Slate), 1.5f),
            Wall("wall_brick", "Gạch nung", "Vách gạch đỏ xếp so le", 350, Draw(Brick), 1.2f),
            Wall("wall_planks", "Ván gỗ", "Vách ván gỗ dọc ấm áp", 400, Draw(Planks), 1),
            Wall("wall_gold", "Hoa văn vàng", "Hoa văn vàng nổi trên nền xanh đậm", 450,
                Draw((u, v) => Ornament(u, v, Hex(0x1F3B3A), Hex(0xD9B45A))), 1.2f),
            Wall("wall_jade", "Hoa văn xanh ngọc", "Hoa văn xanh ngọc trên nền sáng", 450,
                Draw((u, v) => Ornament(u, v, Hex(0xBFE3E0), Hex(0x2E8C88))), 1.2f),
        };
        catalog.substrates = new[]
        {
            Floor("sand_river", "Cát vàng", "Cát sông vàng kèm sỏi nhỏ", 0, Read(sand, Color.white), 5, true, Color.white, true),
            Floor("sand_white", "Cát trắng", "Cát trắng mịn, hồ sáng hơn", 150,
                Draw((u, v) => Sand(u, v, Hex(0xD9D2C2), Hex(0xFAF7EE))), 5, true, Hex(0xE8E4DA)),
            Floor("sand_black", "Cát đen", "Cát đen làm nổi màu cá", 200,
                Draw((u, v) => Sand(u, v, Hex(0x1C1C1E), Hex(0x4A4A4E))), 5, true, Hex(0x55555A)),
            Floor("sand_pink", "Cát hồng", "Cát hồng san hô mịn", 250,
                Draw((u, v) => Sand(u, v, Hex(0xD9A0A0), Hex(0xF6D3CC))), 5, false, Color.white),
            Floor("gravel_river", "Sỏi suối", "Sỏi suối tròn nhiều màu", 300,
                Draw((u, v) => Pebbles(u, v, Hex(0x6E5A45), Hex(0xC9B79A))), 3, true, Color.white),
            Floor("gravel_black", "Sỏi đen", "Sỏi đen bóng", 300,
                Draw((u, v) => Pebbles(u, v, Hex(0x1A1A1C), Hex(0x5A5A60))), 3, false, Color.white),
        };
        foreach (var w in catalog.walls)
            if (kept.TryGetValue(w.id, out var k)) { w.displayName = k.displayName; w.description = k.description; w.price = k.price; }
        foreach (var f in catalog.substrates)
            if (kept.TryGetValue(f.id, out var k)) { f.displayName = k.displayName; f.description = k.description; f.price = k.price; }
        SurfaceCatalogSheet.EnsureTanks(catalog);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Assign(catalog);
        Debug.Log($"[Lumaquarium] Surface catalog: {catalog.walls.Length} walls, {catalog.substrates.Length} floors.");
    }

    // The environment prefab and the open MainAquarium both get the catalog (like the decoration catalog).
    private static void Assign(AquariumSurfaceCatalog catalog)
    {
        var root = PrefabUtility.LoadPrefabContents(EnvironmentPath);
        try
        {
            var inPrefab = root.GetComponentInChildren<AquariumDecoration>(true);
            if (inPrefab != null) { inPrefab.surfaces = catalog; PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPath); }
            else Debug.LogWarning("[Lumaquarium] AquariumEnvironment prefab has no AquariumDecoration; catalog not assigned there.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = SceneManager.GetActiveScene();
        var decoration = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>(FindObjectsInactive.Include);
        if (scene.path != ScenePath || decoration == null)
        {
            Debug.LogWarning("[Lumaquarium] MainAquarium is not the active scene or has no AquariumDecoration; catalog not assigned in the scene.");
            return;
        }
        Directory.CreateDirectory("Assets/_Project/Scenes/Backups");
        File.Copy(scene.path, "Assets/_Project/Scenes/Backups/MainAquarium-before-surfaces-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".unity");
        Undo.RecordObject(decoration, "Assign surface catalog");
        decoration.surfaces = catalog;
        EditorUtility.SetDirty(decoration);
        if (PrefabUtility.IsPartOfPrefabInstance(decoration)) PrefabUtility.RecordPrefabInstancePropertyModifications(decoration);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static AquariumWallStyle Wall(string id, string name, string description, int price, Color[] pixels, float tiling, bool original = false) =>
        new AquariumWallStyle
        {
            id = id, displayName = name, description = description, price = price, tiling = tiling,
            albedo = original ? null : SaveTexture(id, pixels), portrait = SavePortrait(id, pixels)
        };

    private static AquariumSubstrateStyle Floor(string id, string name, string description, int price, Color[] pixels, float tiling,
        bool pebbles, Color pebbleTint, bool original = false) =>
        new AquariumSubstrateStyle
        {
            id = id, displayName = name, description = description, price = price, tiling = tiling, pebbles = pebbles,
            pebbleTint = pebbleTint, albedo = original ? null : SaveTexture(id, pixels), portrait = SavePortrait(id, pixels)
        };

    private static Texture2D SaveTexture(string id, Color[] pixels)
    {
        string path = $"{Dir}/{id}.png";
        var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = Size;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // The middle 256 px of the tile with rounded corners, imported as a UI sprite.
    private static Sprite SavePortrait(string id, Color[] pixels)
    {
        const int side = 256;
        string path = $"{PortraitDir}/surface_{id}.png";
        var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
        var crop = new Color[side * side];
        for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 127.5f) - 100), dy = Mathf.Max(0, Mathf.Abs(y - 127.5f) - 100);
                var c = pixels[(y + 128) * Size + x + 128];
                c.a = Mathf.Clamp01(28 - Mathf.Sqrt(dx * dx + dy * dy));
                crop[y * side + x] = c;
            }
        texture.SetPixels(crop);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Reads a (non-readable) texture through the GPU at 512 px, multiplied by a tint.
    private static Color[] Read(Texture2D source, Color tint)
    {
        var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        var pixels = copy.GetPixels();
        UnityEngine.Object.DestroyImmediate(copy);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(pixels[i].r * tint.r, pixels[i].g * tint.g, pixels[i].b * tint.b, 1);
        return pixels;
    }

    private static Color[] Draw(Func<float, float, Color> pixel)
    {
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = pixel((x + .5f) / Size, (y + .5f) / Size);
        return pixels;
    }

    // ---- Patterns. u and v run 0..1 across the tile; every pattern wraps at the edges. ----

    private static Color Slate(float u, float v)
    {
        var c = Color.Lerp(Hex(0x4A5056), Hex(0x8A9198), Fbm(u, v, 4, 11));
        return Mathf.Abs(Noise(u, v, 6, 23) - .5f) < .02f ? Shade(c, .55f) : c;
    }

    private static Color Brick(float u, float v)
    {
        const int rows = 8, columns = 4;
        float y = v * rows;
        int row = Mathf.FloorToInt(y);
        float x = u * columns + (row % 2) * .5f;
        int column = Mathf.FloorToInt(x);
        float fx = x - column, fy = y - row;
        if (fx < .04f || fy < .08f) return Shade(Hex(0xCDBFA6), .9f + .1f * Noise(u, v, 32, 5));
        float tone = Hash((column % columns + columns) % columns, row, 7);
        return Shade(Color.Lerp(Hex(0x8E3B24), Hex(0xB86A42), tone), .85f + .3f * Fbm(u, v, 8, 9));
    }

    private static Color Planks(float u, float v)
    {
        const int planks = 4;
        float x = u * planks;
        int plank = Mathf.FloorToInt(x);
        float fx = x - plank;
        if (fx < .025f) return Hex(0x2E1D12);
        float grain = Mathf.Sin((fx * 4 + Fbm(u, v, 4, 31 + plank) * 1.5f) * Mathf.PI * 2) * .5f + .5f;
        var c = Color.Lerp(Hex(0x6B4428), Hex(0xA6764A), Hash(plank, 0, 13) * .5f + grain * .5f);
        return Shade(c, .9f + .1f * Noise(u, v, 64, 3));
    }

    // A four-petal flower inside a diamond frame, raised and lit from the top left.
    private static Color Ornament(float u, float v, Color ground, Color raised)
    {
        const int cells = 4;
        float x = u * cells, y = v * cells;
        float fx = x - Mathf.Floor(x) - .5f, fy = y - Mathf.Floor(y) - .5f;
        float r = Mathf.Sqrt(fx * fx + fy * fy), angle = Mathf.Atan2(fy, fx);
        float petal = r - (.22f + .12f * Mathf.Abs(Mathf.Cos(2 * angle)));
        float diamond = Mathf.Abs(Mathf.Abs(fx) + Mathf.Abs(fy) - .46f);
        float height = Mathf.Clamp01(Mathf.Clamp01(1 - Mathf.Abs(petal) * 30) + Mathf.Clamp01(1 - diamond * 30) + (r < .07f ? 1 : 0));
        float light = height > .05f ? (fx - fy < 0 ? 1.15f : .85f) : 1;
        return Shade(Color.Lerp(ground, raised, height), light * (.9f + .1f * Noise(u, v, 32, 41)));
    }

    private static Color Sand(float u, float v, Color dark, Color light)
    {
        float grain = Hash(Mathf.FloorToInt(u * Size), Mathf.FloorToInt(v * Size), 3);
        return Color.Lerp(dark, light, Fbm(u, v, 8, 17) * .6f + grain * .4f);
    }

    // Round stones: Voronoi cells with dark gaps, each stone its own shade, brighter toward its centre.
    private static Color Pebbles(float u, float v, Color dark, Color light)
    {
        const int cells = 8;
        float x = u * cells, y = v * cells;
        int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
        float first = 9, second = 9;
        int owner = 0;
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int ix = ((cx + i) % cells + cells) % cells, iy = ((cy + j) % cells + cells) % cells;
                float px = cx + i + .2f + .6f * Hash(ix, iy, 51), py = cy + j + .2f + .6f * Hash(ix, iy, 52);
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < first) { second = first; first = d; owner = iy * cells + ix; }
                else if (d < second) second = d;
            }
        if (second - first < .06f) return Shade(dark, .45f);
        var stone = Color.Lerp(dark, light, Hash(owner, 0, 53));
        return Shade(stone, Mathf.Lerp(1.15f, .75f, Mathf.Clamp01(first / .7f)) * (.92f + .08f * Noise(u, v, 64, 54)));
    }

    // ---- Noise ----

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
        }
    }

    // Smooth value noise with `cells` cells across the tile, wrapping at the tile edge.
    private static float Noise(float u, float v, int cells, int seed)
    {
        float x = u * cells, y = v * cells;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int W(int i) => (i % cells + cells) % cells;
        float a = Hash(W(x0), W(y0), seed), b = Hash(W(x0 + 1), W(y0), seed);
        float c = Hash(W(x0), W(y0 + 1), seed), d = Hash(W(x0 + 1), W(y0 + 1), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Fbm(float u, float v, int cells, int seed)
    {
        float sum = 0, weight = .5f;
        for (int octave = 0; octave < 4; octave++, cells *= 2, weight *= .5f) sum += Noise(u, v, cells, seed + octave) * weight;
        return sum / .9375f;
    }

    private static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1);
    private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
}
