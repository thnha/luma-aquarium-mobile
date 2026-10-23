using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// Aquarium sound setup: the AquariumMixer (Master > Ambience > Water / Bubbles, SFX, UI; snapshots Default and
// Muffled, the latter low-passing Master), mobile import settings for Assets/_Project/Audio, and an AquariumAudio
// on the environment prefab's water, wired to its clips and mixer groups. Running it again keeps an existing
// mixer and only refreshes imports and wiring. Clips in Audio/Placeholder are generated stand-ins.
public static class AquariumAudioBuilder
{
    private const string EnvironmentPath = "Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab";
    private const string AudioRoot = "Assets/_Project/Audio";
    private const string ClipRoot = AudioRoot + "/Placeholder";
    private const string MixerPath = AudioRoot + "/AquariumMixer.mixer";

    [MenuItem("Lumaquarium/Audio/Build Aquarium Audio")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) ?? CreateMixer();
        ConfigureImports();

        var root = PrefabUtility.LoadPrefabContents(EnvironmentPath);
        try
        {
            var water = root.GetComponentInChildren<AquariumWaterEffects>(true);
            if (water == null || water.bubbles == null) throw new InvalidOperationException("The environment has no water or bubble emitter.");
            var audio = water.GetComponent<AquariumAudio>();
            if (audio == null) audio = water.gameObject.AddComponent<AquariumAudio>();
            audio.water = water;
            audio.mixer = mixer;
            audio.waterGroup = Group(mixer, "Water");
            audio.bubblesGroup = Group(mixer, "Bubbles");
            audio.sfxGroup = Group(mixer, "SFX");
            audio.waterLoop = Clip("water_loop");
            audio.bubbleLoop = Clip("bubble_loop");
            audio.bloops = Clips("bloop_");
            audio.ripples = Clips("ripple_");
            audio.gulps = Clips("gulp_");
            audio.referenceRate = water.bubbles.emission.rateOverTimeMultiplier;
            PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Aquarium audio: mixer, imports and AquariumAudio on the environment water.");
    }

    // Loops stay compressed in memory; short one-shots decode on load. Everything is mono: the tank is a 2D mix.
    private static void ConfigureImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            bool loop = path.Contains("_loop");
            importer.forceToMono = true;
            importer.loadInBackground = loop;
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad,
                compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM,
                quality = .6f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
            };
            importer.SaveAndReimport();
        }
    }

    private static AudioClip Clip(string name) => Clips(name).FirstOrDefault();   // any format: .wav, .ogg, .mp3...

    private static AudioClip[] Clips(string prefix) => AssetDatabase.FindAssets("t:AudioClip", new[] { ClipRoot })
        .Select(AssetDatabase.GUIDToAssetPath).Where(p => System.IO.Path.GetFileName(p).StartsWith(prefix)).OrderBy(p => p)
        .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();

    private static AudioMixerGroup Group(AudioMixer mixer, string name) =>
        mixer.FindMatchingGroups(name).FirstOrDefault(g => g.name == name);

    // Unity has no public API for authoring mixers, so this goes through the editor's AudioMixerController.
    private static AudioMixer CreateMixer()
    {
        System.IO.Directory.CreateDirectory(AudioRoot);
        var editor = typeof(Editor).Assembly;
        var controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController");
        var controller = (AudioMixer)Call(controllerType, null, "CreateMixerControllerAtPath", MixerPath);
        var master = Get(controller, "masterGroup");

        object NewGroup(string name, object parent)
        {
            var group = Call(controllerType, controller, "CreateNewGroup", name, false);
            Call(controllerType, controller, "AddChildToParent", group, parent);
            return group;
        }
        var ambience = NewGroup("Ambience", master);
        NewGroup("Water", ambience);
        NewGroup("Bubbles", ambience);
        NewGroup("SFX", master);
        NewGroup("UI", master);

        var defaults = Get(controller, "TargetSnapshot");
        ((UnityEngine.Object)defaults).name = "Default";
        Call(controllerType, controller, "CloneNewSnapshotFromTarget", false);
        var muffled = Get(controller, "TargetSnapshot");
        ((UnityEngine.Object)muffled).name = "Muffled";

        var effectType = editor.GetType("UnityEditor.Audio.AudioMixerEffectController");
        var lowpass = Activator.CreateInstance(effectType, "Lowpass Simple");
        AssetDatabase.AddObjectToAsset((UnityEngine.Object)lowpass, controller);
        Call(effectType, lowpass, "PreallocateGUIDs");
        Call(master.GetType(), master, "InsertEffect", lowpass, 1);   // after Master's attenuation
        Call(effectType, lowpass, "SetValueForParameter", controller, defaults, "Cutoff freq", 22000f);
        Call(effectType, lowpass, "SetValueForParameter", controller, muffled, "Cutoff freq", 900f);
        Call(ambience.GetType(), ambience, "SetValueForVolume", controller, muffled, -6f);

        Set(controller, "TargetSnapshot", defaults);
        Set(controller, "startSnapshot", defaults);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
    }

    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static object Call(Type type, object target, string name, params object[] args) =>
        type.GetMethods(Any).First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);

    private static object Get(object target, string name) => target.GetType().GetProperty(name, Any).GetValue(target);

    private static void Set(object target, string name, object value) => target.GetType().GetProperty(name, Any).SetValue(target, value);
}
