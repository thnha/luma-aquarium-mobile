using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Scenery in place of the removed painted backdrop: sand that rises toward the back, sun shafts from
// the surface, floating motes and the depth tint. Works on the installed environment prefab, so the
// MainAquarium scene and later installs share it. Running it again rebuilds from the flat sand.
public static class AquariumAmbienceBuilder
{
    private const string EnvironmentPath = "Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab";
    private const string Root = "Assets/_Project/Art/Aquarium3D";

    [MenuItem("Lumaquarium/Preview/Build Sloped Sand Sun Shafts and Depth")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(EnvironmentPath);
        try
        {
            var decoration = root.GetComponentInChildren<AquariumDecoration>(true);
            if (decoration == null) throw new InvalidOperationException("The environment has no placement component.");
            var water = root.GetComponentInChildren<AquariumWaterEffects>(true);
            float top = water != null ? water.surfaceY : decoration.floorY + 2.1f;
            decoration.sandRise = .22f;
            decoration.sandRiseFrom = .3f;
            decoration.sandRiseTo = 2.2f;
            Slope(Find(root, "SandBed3D"), decoration, "SandBed", true);
            Slope(Find(root, "PebbleBed3D"), decoration, "PebbleBed", false);

            var shafts = Child(root, "SunShafts").gameObject;
            var sun = shafts.GetComponent<AquariumSunShafts>();
            if (sun == null) sun = shafts.AddComponent<AquariumSunShafts>();
            sun.material = Glow("SunShaft", false, new Color(.75f, .95f, 1, .16f));
            sun.decoration = decoration;
            sun.water = water;

            Motes(Child(root, "FloatingMotes"), decoration, top);

            var lighting = decoration.GetComponent<AquariumLighting>();
            if (lighting != null) { lighting.depthFog = .22f; lighting.fogNear = 0; lighting.fogFar = 2.6f; }
            PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Sloped sand, sun shafts, floating motes and depth tint applied to the aquarium environment.");
    }

    private static Transform Find(GameObject root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);

    private static Transform Child(GameObject root, string name)
    {
        var found = Find(root, name);
        if (found != null) return found;
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        return go.transform;
    }

    // Raises each vertex by the same curve the placement uses (AquariumDecoration.SandAt), starting from
    // a copy of the flat mesh kept on the first run.
    private static void Slope(Transform bed, AquariumDecoration decoration, string name, bool smooth)
    {
        if (bed == null) return;
        var filter = bed.GetComponent<MeshFilter>();
        string flatPath = $"{Root}/{name}-flat.asset", slopedPath = $"{Root}/{name}-sloped.asset";
        var flat = AssetDatabase.LoadAssetAtPath<Mesh>(flatPath);
        if (flat == null)
        {
            flat = UnityEngine.Object.Instantiate(filter.sharedMesh);
            AssetDatabase.CreateAsset(flat, flatPath);
        }
        var sloped = UnityEngine.Object.Instantiate(flat);
        sloped.name = name + "-sloped";
        var vertices = sloped.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = bed.TransformPoint(vertices[i]);
            world.y += decoration.SandAt(world) - decoration.floorY;
            vertices[i] = bed.InverseTransformPoint(world);
        }
        sloped.vertices = vertices;
        // Pebbles keep their own rounded normals; the sand surface takes the slope's.
        if (smooth) sloped.RecalculateNormals();
        sloped.RecalculateBounds();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(slopedPath);
        if (existing == null) AssetDatabase.CreateAsset(sloped, slopedPath);
        else
        {
            EditorUtility.CopySerialized(sloped, existing);
            existing.name = sloped.name;
            UnityEngine.Object.DestroyImmediate(sloped);
            sloped = existing;
            EditorUtility.SetDirty(existing);
        }
        filter.sharedMesh = sloped;
        var collider = bed.GetComponent<MeshCollider>();
        if (collider != null) collider.sharedMesh = sloped;
    }

    private static Material Glow(string name, bool round, Color color)
    {
        string path = $"{Root}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Lumaquarium/Aquarium Glow");
            if (shader == null) throw new InvalidOperationException("Import the Aquarium Glow shader first.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_Color", color);
        material.SetFloat("_Round", round ? 1 : 0);
        EditorUtility.SetDirty(material);
        return material;
    }

    // Specks drifting slowly through the whole water column, fading in and out.
    private static void Motes(Transform holder, AquariumDecoration decoration, float top)
    {
        var system = holder.GetComponent<ParticleSystem>();
        if (system == null) system = holder.gameObject.AddComponent<ParticleSystem>();
        holder.position = new Vector3((decoration.min.x + decoration.max.x) * .5f, (decoration.floorY + top) * .5f,
            (decoration.min.y + decoration.max.y) * .5f);
        var main = system.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10, 16);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.006f, .016f);
        main.startColor = new Color(.85f, 1, .95f, .6f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = system.emission; emission.enabled = true; emission.rateOverTime = 6;
        var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
        shape.rotation = Vector3.zero;
        shape.scale = new Vector3(decoration.max.x - decoration.min.x, top - decoration.floorY - .2f, decoration.max.y - decoration.min.y);
        var noise = system.noise; noise.enabled = true; noise.strength = .02f; noise.frequency = .3f;
        noise.scrollSpeed = .1f; noise.quality = ParticleSystemNoiseQuality.Low;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(1, .75f), new GradientAlphaKey(0, 1) });
        var color = system.colorOverLifetime; color.enabled = true; color.color = fade;
        var renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = Glow("FloatingMote", true, new Color(.8f, 1, .95f, .7f));
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
