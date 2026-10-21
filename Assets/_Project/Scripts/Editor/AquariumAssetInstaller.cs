using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Promotes the approved presentation without replacing the main scene's fish or systems.
public static class AquariumAssetInstaller
{
    public const string MainPath = "Assets/_Project/Scenes/MainAquarium.unity";
    public const string AssetPath = "Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab";
    private const string MainStorage = "Lumaquarium.Decoration.Main.v1";

    [MenuItem("Lumaquarium/Assets/Apply Approved Aquarium to Main")]
    public static void ApplyApprovedToMain()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before applying assets.");
        var active = SceneManager.GetActiveScene();
        if (active.isDirty && active.path != MainPath)
            throw new InvalidOperationException("Save the current scene before applying approved assets.");
        var main = SceneManager.GetSceneByPath(MainPath);
        if (!main.IsValid() || !main.isLoaded) main = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Additive);
        var preview = SceneManager.GetSceneByPath(Aquarium3DPreviewBuilder.PreviewPath);
        bool openedPreview = !preview.IsValid() || !preview.isLoaded;
        if (openedPreview) preview = EditorSceneManager.OpenScene(Aquarium3DPreviewBuilder.PreviewPath, OpenSceneMode.Additive);
        GameObject staging = null;
        try
        {
            var screen = Find<MainAquariumScreen>(preview);
            var source = Find<AquariumDecoration>(preview);
            var fish = Find<BettaFishAI>(main);
            var feeding = Find<FeedingSystem>(main);
            var orbit = Find<OrbitTouchCamera>(main);
            var camera = orbit.GetComponent<Camera>();
            var sun = Components<Light>(main).First(x => x.type == LightType.Directional);
            var backdrop = preview.GetRootGameObjects().SingleOrDefault(x => x.name == "AquariumBackdrop");
            if (source.GetComponent<AquariumLighting>() == null || source.prefabs.Any(x => x == null))
                throw new InvalidOperationException("Complete the approved natural-assets preview first.");
            var existing = main.GetRootGameObjects().SingleOrDefault(x => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x) == AssetPath);
            if (main.GetRootGameObjects().Any(x => x.name == "AquariumEnvironment" && x != existing))
                throw new InvalidOperationException("AquariumEnvironment already exists and is not the installed environment asset.");
            if (Components<MainAquariumScreen>(main).Any(x => existing == null || !x.transform.IsChildOf(existing.transform)))
                throw new InvalidOperationException("MainAquarium has another authored HUD; integrate it before installing this presentation.");

            Directory.CreateDirectory("Assets/_Project/Scenes/Backups");
            string backup = $"Assets/_Project/Scenes/Backups/MainAquarium-before-approved-assets-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}";
            File.Copy(MainPath, backup + "-saved.unity");
            EditorSceneManager.SaveScene(main, backup + "-working.unity", true);
            // Clone all three roots and remap their cross-root references together. The source stays untouched.
            staging = new GameObject("AquariumEnvironment");
            SceneManager.MoveGameObjectToScene(staging, main);
            staging.SetActive(false);
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            foreach (var root in new[] { source.gameObject, screen.gameObject, backdrop })
            {
                if (root == null) continue;
                var clone = UnityEngine.Object.Instantiate(root, staging.transform, false);
                clone.name = root == source.gameObject ? "AquariumDecor" : root.name;
                MapHierarchy(root.transform, clone.transform, map);
            }
            RemapAndClearExternalReferences(staging, map);
            var stagedDecoration = staging.GetComponentInChildren<AquariumDecoration>(true);
            stagedDecoration.storageKey = MainStorage;
            var availability = staging.GetComponentInChildren<MainAquariumScreen>(true).decoratePanel.transform.Find("Availability").GetComponent<TMP_Text>();
            availability.text = "Chọn món → kéo trên cát → xác nhận Đặt.\nBố cục được lưu trên thiết bị.";
            // Keep the prefab active when used independently; no runtime scripts execute in Edit Mode.
            staging.SetActive(true);
            if (File.Exists(AssetPath))
                File.Copy(AssetPath, backup + "-environment.prefab");
            var asset = PrefabUtility.SaveAsPrefabAsset(staging, AssetPath);
            if (asset == null) throw new InvalidOperationException("Could not save the approved environment asset.");
            UnityEngine.Object.DestroyImmediate(staging); staging = null;
            var installed = existing ?? (GameObject)PrefabUtility.InstantiatePrefab(asset, main);
            if (existing == null) Undo.RegisterCreatedObjectUndo(installed, "Install approved aquarium assets");
            BindScene(installed, fish, feeding, orbit, camera, sun);
            ConfigureFraming(source, fish, orbit);
            PreserveBlockout(main);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(main);
            if (!EditorSceneManager.SaveScene(main)) throw new InvalidOperationException("Could not save MainAquarium.");
            SceneManager.SetActiveScene(main);
            Debug.Log("[Lumaquarium] Approved environment asset installed in MainAquarium. Backup: " + backup);
        }
        finally
        {
            if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
            if (openedPreview && preview.IsValid() && preview.isLoaded) EditorSceneManager.CloseScene(preview, true);
        }
    }

    private static void BindScene(GameObject root, BettaFishAI fish, FeedingSystem feeding, OrbitTouchCamera orbit, Camera camera, Light sun)
    {
        var d = root.GetComponentInChildren<AquariumDecoration>(true);
        var screen = root.GetComponentInChildren<MainAquariumScreen>(true);
        var lighting = root.GetComponentInChildren<AquariumLighting>(true);
        Undo.RecordObjects(new UnityEngine.Object[] { d, screen, d.water, lighting }, "Bind approved aquarium assets");
        d.viewCamera = camera; d.orbit = orbit; d.screen = screen; d.storageKey = MainStorage;
        screen.fish = fish; screen.feeding = feeding; screen.orbit = orbit; screen.decoration = d;
        d.water.viewCamera = camera; d.water.fish = fish.transform; d.water.decoration = d;
        lighting.sun = sun;
        foreach (var component in new Component[] { d, screen, d.water, lighting })
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
        {
            canvas.worldCamera = camera;
            PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
        }
    }

    private static void ConfigureFraming(AquariumDecoration source, BettaFishAI fish, OrbitTouchCamera orbit)
    {
        // Apply the approved composition while retaining the existing simulation settings and rig.
        Undo.RecordObjects(new UnityEngine.Object[] { orbit, orbit.transform, fish.transform, fish.swimBounds.transform }, "Apply approved aquarium framing");
        var target = orbit.target;
        EditorUtility.CopySerialized(source.orbit, orbit);
        orbit.target = target;
        orbit.transform.SetPositionAndRotation(source.orbit.transform.position, source.orbit.transform.rotation);
        fish.transform.localScale = source.screen.fish.transform.localScale;
        fish.swimBounds.transform.localScale = source.screen.fish.swimBounds.transform.localScale;
    }

    private static void PreserveBlockout(Scene main)
    {
        var aquarium = main.GetRootGameObjects().Single(x => x.name == "AquariumRoot");
        foreach (var renderer in aquarium.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(renderer, "Preserve original aquarium geometry");
            renderer.enabled = false;
        }
        // Preserve the old HUD in the scene, including its serialized references.
        var legacy = main.GetRootGameObjects().SingleOrDefault(x => x.name == "Canvas" || x.name == "LegacyAquariumHUD");
        if (legacy != null)
        {
            Undo.RecordObject(legacy, "Preserve original aquarium HUD");
            legacy.name = "LegacyAquariumHUD";
            legacy.SetActive(false);
        }
    }

    private static T[] Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
    private static T Find<T>(Scene scene) where T : Component => Components<T>(scene).Single();

    private static void MapHierarchy(Transform source, Transform clone, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
    {
        map[source.gameObject] = clone.gameObject;
        var from = source.GetComponents<Component>(); var to = clone.GetComponents<Component>();
        if (from.Length != to.Length || source.childCount != clone.childCount)
            throw new InvalidOperationException("Cloned hierarchy differs from approved preview: " + source.name);
        for (int i = 0; i < from.Length; i++)
        {
            if (from[i] == null || to[i] == null) throw new InvalidOperationException("Missing script in approved preview: " + source.name);
            map[from[i]] = to[i];
        }
        for (int i = 0; i < source.childCount; i++) MapHierarchy(source.GetChild(i), clone.GetChild(i), map);
    }

    private static void RemapAndClearExternalReferences(GameObject root, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = property.objectReferenceValue;
                if (value == null || EditorUtility.IsPersistent(value)) continue;
                if (map.TryGetValue(value, out var mapped)) { property.objectReferenceValue = mapped; continue; }
                var go = value as GameObject ?? (value as Component)?.gameObject;
                if (go != null && go != root && !go.transform.IsChildOf(root.transform)) property.objectReferenceValue = null;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
