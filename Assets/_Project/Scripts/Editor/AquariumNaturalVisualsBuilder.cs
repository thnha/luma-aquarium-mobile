using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Upgrades only presentation in the independent preview; placement footprints stay unchanged.
public static class AquariumNaturalVisualsBuilder
{
    private const string Root = "Assets/_Project/Art/Aquarium3D";
    private const string LeafTexture = Root + "/Textures/AnubiasLeaf-v2.png";

    [MenuItem("Lumaquarium/Preview/Upgrade Natural Plants Water and Bubbles")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before upgrading visuals.");
        if (SceneManager.GetActiveScene().path != Aquarium3DPreviewBuilder.PreviewPath)
        {
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
            EditorSceneManager.OpenScene(Aquarium3DPreviewBuilder.PreviewPath);
        }
        ApplyToScene(true);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    public static void ApplyToScene(bool backup)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Visual authoring requires Edit Mode.");
        var placement = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
        if (placement == null || SceneManager.GetActiveScene().path != Aquarium3DPreviewBuilder.PreviewPath)
            throw new InvalidOperationException("Open the 3D preview before upgrading it.");
        if (backup)
            SceneBackups.SaveCopy(SceneManager.GetActiveScene(), "Aquarium3D-before-natural-" + SceneBackups.Stamp());
        PrepareLeafTexture();
        var prefab = BuildPlant();
        Undo.RecordObject(placement, "Upgrade plant inventory");
        placement.prefabs[0] = prefab;
        foreach (var old in placement.itemsRoot.GetComponentsInChildren<AquariumDecorItem>().Where(x => x.kind == 0).ToArray())
        {
            var replacement = (AquariumDecorItem)PrefabUtility.InstantiatePrefab(prefab, placement.itemsRoot);
            Undo.RegisterCreatedObjectUndo(replacement.gameObject, "Upgrade placed plant");
            replacement.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            replacement.transform.localScale = old.transform.localScale;
            Undo.DestroyObjectImmediate(old.gameObject);
        }
        var environment = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/UI/Backgrounds/AquariumEnvironment.png");
        var water = placement.water;
        foreach (var material in new[] { water.surface.sharedMaterial, water.bubbles.GetComponent<ParticleSystemRenderer>().sharedMaterial })
        {
            material.SetTexture("_EnvironmentTex", environment);
            EditorUtility.SetDirty(material);
        }
        ConfigureBubbles(water.bubbles);
        // Keep the sand bed underneath the frame when looking down to place an object.
        var floor = GameObject.Find("SandBed3D");
        if (floor != null)
        {
            Undo.RecordObject(floor.transform, "Extend foreground sand");
            float depth = floor.GetComponent<MeshFilter>().sharedMesh.bounds.size.z;
            floor.transform.localScale = new Vector3(1, 1, Mathf.Max(1, 4.4f / depth));
            floor.transform.position = new Vector3(0, .28f, -.6f);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Natural leaf meshes, water reflection and bubble shading applied to preview.");
    }

    private static void PrepareLeafTexture()
    {
        var importer = AssetImporter.GetAtPath(LeafTexture) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Import the generated Anubias leaf texture first.");
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = .45f;
        importer.maxTextureSize = 1024;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.SaveAndReimport();
    }

    private static AquariumDecorItem BuildPlant()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/NaturalLeaf-v2.mat");
        if (material == null)
        {
            var shader = Shader.Find("Lumaquarium/Aquarium Leaf");
            if (shader == null) throw new InvalidOperationException("Import the leaf shader first.");
            material = new Material(shader) { name = "Anubias Natural Leaf" };
            AssetDatabase.CreateAsset(material, Root + "/NaturalLeaf-v2.mat");
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(LeafTexture));
        material.SetColor("_BaseColor", new Color(.94f, 1f, .83f));
        material.SetFloat("_Sway", .007f);
        material.SetFloat("_Cutoff", .45f);
        EditorUtility.SetDirty(material);
        var stemMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/NaturalStem-v2.mat");
        if (stemMaterial == null)
        {
            stemMaterial = new Material(Shader.Find("Lumaquarium/Aquarium Nature"));
            AssetDatabase.CreateAsset(stemMaterial, Root + "/NaturalStem-v2.mat");
        }
        stemMaterial.SetColor("_BaseColor", new Color(.19f, .28f, .065f));
        stemMaterial.SetFloat("_Detail", .09f);
        stemMaterial.SetFloat("_Scale", 70);
        EditorUtility.SetDirty(stemMaterial);
        Mesh blade = SaveMesh(LeafMesh(), "NaturalLeafBlade-v2");
        var root = new GameObject("AquaticPlantNatural");
        var item = root.AddComponent<AquariumDecorItem>();
        item.kind = 0; item.footprint = .14f; item.phase = 2.37f;
        item.foliage = new GameObject("SwayingFoliage").transform;
        item.foliage.SetParent(root.transform, false);
        var stems = new List<CombineInstance>();
        var temporaryMeshes = new List<Mesh>();
        for (int n = 0; n < 19; n++)
        {
            float angle = n * 2.399963f;
            var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            float ring = n < 11 ? 1 : .57f;
            float height = (.25f + (n % 5) * .045f) * ring;
            Vector3 start = radial * (.035f + (n % 3) * .012f);
            Vector3 end = radial * (.08f + (n % 4) * .017f) * ring + Vector3.up * height;
            Mesh stem = StemMesh(start, end, n);
            temporaryMeshes.Add(stem);
            stems.Add(new CombineInstance { mesh = stem, transform = Matrix4x4.identity });
            var leaf = MeshObject("Leaf" + n, item.foliage, blade, material);
            leaf.transform.localPosition = end;
            Vector3 direction = radial * (.6f + (n % 3) * .18f) + Vector3.up * (.55f + (n % 4) * .14f);
            leaf.transform.localRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 0, Mathf.Sin(n * 4) * 14);
            float length = (.29f + (n % 5) * .029f) * (n < 11 ? 1 : .82f);
            leaf.transform.localScale = new Vector3(length * (.48f + (n % 3) * .035f), length, length);
        }
        var merged = new Mesh { name = "AnubiasPetioles" };
        merged.CombineMeshes(stems.ToArray(), true, false);
        foreach (var mesh in temporaryMeshes) UnityEngine.Object.DestroyImmediate(mesh);
        MeshObject("Petioles", item.foliage, SaveMesh(merged, "NaturalStems-v2"), stemMaterial);
        root.layer = 8;
        var collider = root.AddComponent<BoxCollider>();
        collider.isTrigger = true; collider.center = new Vector3(0, .35f, 0); collider.size = new Vector3(.42f, .76f, .42f);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Root + "/AquaticPlantNatural.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return saved.GetComponent<AquariumDecorItem>();
    }

    private static Mesh LeafMesh()
    {
        const int rows = 18, columns = 8;
        var positions = new List<Vector3>(); var uv = new List<Vector2>();
        var colors = new List<Color>(); var triangles = new List<int>();
        for (int row = 0; row <= rows; row++)
            for (int col = 0; col <= columns; col++)
            {
                float t = row / (float)rows, x = col / (float)columns - .5f;
                float cup = Mathf.Sin(t * Mathf.PI) * (.12f - Mathf.Abs(x) * .22f);
                float curl = Mathf.Pow(t, 4) * -.12f + Mathf.Sin(t * 9 + x * 4) * .013f * t;
                positions.Add(new Vector3(x, cup + curl, t));
                uv.Add(new Vector2(col / (float)columns, t));
                colors.Add(Color.Lerp(new Color(.68f, .78f, .59f), new Color(1.12f, 1.1f, .9f), t * .75f + .15f));
                if (row < rows && col < columns)
                {
                    int i = row * (columns + 1) + col;
                    triangles.AddRange(new[] { i, i + columns + 1, i + 1, i + 1, i + columns + 1, i + columns + 2 });
                }
            }
        var mesh = new Mesh { name = "CurvedAnubiasLeaf" };
        mesh.SetVertices(positions); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh StemMesh(Vector3 start, Vector3 end, int seed)
    {
        const int rings = 8, sides = 7;
        var vertices = new List<Vector3>(); var triangles = new List<int>(); var colors = new List<Color>();
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
        for (int ring = 0; ring <= rings; ring++)
            for (int side = 0; side <= sides; side++)
            {
                float t = ring / (float)rings, a = side * Mathf.PI * 2 / sides;
                float r = Mathf.Lerp(.0055f, .0022f, t);
                Vector3 bend = Vector3.Lerp(start, end, t) + new Vector3(Mathf.Sin(t * Mathf.PI) * .024f, 0, 0);
                vertices.Add(bend + rotation * new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                colors.Add(Color.white * (.8f + .2f * Mathf.Sin(seed + side * .6f)));
                if (ring < rings && side < sides)
                {
                    int i = ring * (sides + 1) + side;
                    triangles.AddRange(new[] { i, i + sides + 1, i + 1, i + 1, i + sides + 1, i + sides + 2 });
                }
            }
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = Root + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing); return existing;
        }
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static void ConfigureBubbles(ParticleSystem bubbles)
    {
        Undo.RecordObject(bubbles, "Natural bubble motion");
        bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = bubbles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5.1f, 6.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(.016f, .045f);
        main.startColor = new Color(.91f, .98f, 1, .85f); main.maxParticles = 55;
        var emission = bubbles.emission; emission.rateOverTime = 4.6f;
        var velocity = bubbles.velocityOverLifetime;
        velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.012f, .012f);
        velocity.y = new ParticleSystem.MinMaxCurve(.32f, .39f);
        velocity.z = new ParticleSystem.MinMaxCurve(-.009f, .009f);
        var noise = bubbles.noise; noise.enabled = true; noise.separateAxes = true;
        noise.strengthX = .018f; noise.strengthY = .008f; noise.strengthZ = .012f;
        noise.frequency = .65f; noise.scrollSpeed = .25f; noise.quality = ParticleSystemNoiseQuality.Low;
        var growth = bubbles.sizeOverLifetime; growth.enabled = true;
        growth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .8f, 1, 1.22f));
        var color = bubbles.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.9f, .08f), new GradientAlphaKey(.8f, .85f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        EditorUtility.SetDirty(bubbles);
    }
}
