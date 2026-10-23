using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

// Remote content (Addressables): the MainAquarium scene with its fish and decor is downloaded from Cloudflare R2
// instead of shipping inside the app, so art and scene changes reach testers without a new TestFlight build.
// Code still ships with the app: content that needs new scripts or new script fields needs a new app version
// (bump Player Settings > Version: each version has its own folder, so older installs keep their content).
//   Lumaquarium > Remote Content > Set Up Addressables      groups, profile, remote catalog (safe to rerun)
//   Lumaquarium > Remote Content > Build Content (Active Platform)
// Batch: Unity -batchmode -quit -projectPath mobile -buildTarget iOS -executeMethod RemoteContent.BuildFromCommandLine
// Output: ServerData/<BuildTarget>/<version>/, uploaded by Tools/upload_content_r2.sh to the same path in the bucket.
// IosBuilder and AndroidBuilder build it before the player. Play in the Editor reads the assets directly.
public static class RemoteContent
{
    // Public URL of the R2 bucket (its r2.dev subdomain or a custom domain), no trailing slash.
    public const string BaseUrl = "https://pub-590970a58ce5426989b3bcd2897271b4.r2.dev";
    const string Placeholder = "REPLACE-WITH";

    public const string ServerDataDir = "ServerData";
    // Fixed catalog name: installed apps look for catalog_<name>.hash, so it must not change between builds.
    const string CatalogName = "lumaquarium";
    const string GroupName = "Remote Aquarium";
    const string ScenePath = "Assets/_Project/Scenes/MainAquarium.unity";
    const string SceneAddress = "MainAquarium"; // Lumaquarium.UI.FrontEndScenes.Aquarium
    const string FishRoot = "Assets/_Project/Art/Fish";
    // One bundle each; the scene bundle refers to them, so changing one fish downloads only that fish.
    static readonly string[] SharedFolders = { "Assets/_Project/Art/Betta", "Assets/_Project/Art/Aquarium3D" };

    public static string OutputDir =>
        $"{ServerDataDir}/{EditorUserBuildSettings.activeBuildTarget}/{PlayerSettings.bundleVersion}";

    [MenuItem("Lumaquarium/Remote Content/Set Up Addressables")]
    public static void SetUp()
    {
        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var profiles = settings.profileSettings;
        var profile = settings.activeProfileId;
        profiles.SetValue(profile, AddressableAssetSettings.kRemoteBuildPath,
            ServerDataDir + "/[BuildTarget]/[UnityEditor.PlayerSettings.bundleVersion]");
        profiles.SetValue(profile, AddressableAssetSettings.kRemoteLoadPath,
            BaseUrl + "/[BuildTarget]/[UnityEditor.PlayerSettings.bundleVersion]");

        settings.BuildRemoteCatalog = true;
        settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
        settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
        settings.OverridePlayerVersion = CatalogName;
        settings.DisableCatalogUpdateOnStartup = false;
        // The builders build content themselves, so they know when it failed and what to upload.
        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

        var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, true, null,
            typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        var bundled = group.GetSchema<BundledAssetGroupSchema>();
        bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
        bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
        bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
        bundled.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
        group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
        settings.DefaultGroup = group;

        Add(settings, group, ScenePath, SceneAddress);
        foreach (var folder in SharedFolders) Add(settings, group, folder, Path.GetFileName(folder));
        foreach (var folder in AssetDatabase.GetSubFolders(FishRoot)) Add(settings, group, folder, "Fish/" + Path.GetFileName(folder));
        // The default local group would ship inside the app, where a content update cannot replace it.
        foreach (var other in settings.groups.Where(g => g != null && g != group && g.entries.Count == 0).ToList())
            settings.RemoveGroup(other);

        // A scene in Build Settings would also ship inside the app.
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToArray();

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Lumaquarium] Addressables set up: {group.entries.Count} remote entries, loading from {BaseUrl}");
    }

    static void Add(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
    {
        var guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning("[Lumaquarium] Remote content: missing " + path);
            return;
        }
        var entry = settings.CreateOrMoveEntry(guid, group, false, false);
        entry.address = address;
    }

    [MenuItem("Lumaquarium/Remote Content/Build Content (Active Platform)")]
    static void BuildFromMenu() => Build();

    [MenuItem("Lumaquarium/Remote Content/Build Content (Active Platform)", true)]
    static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static void BuildFromCommandLine() => EditorApplication.Exit(Build() ? 0 : 1);

    // Builds the bundles and the remote catalog for the active platform into OutputDir.
    public static bool Build()
    {
        if (BaseUrl.Contains(Placeholder))
        {
            Debug.LogError("[Lumaquarium] Set RemoteContent.BaseUrl to the R2 bucket's public URL before building.");
            return false;
        }
        SetUp();
        // The content carries the catalog version; the module deployed with it must have the same prices.
        ServerCatalogWriter.Write();
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error))
        {
            Debug.LogError("[Lumaquarium] Remote content build failed: " + result.Error);
            return false;
        }
        Debug.Log($"[Lumaquarium] Remote content built to {OutputDir}. Upload it with Tools/upload_content_r2.sh.");
        return true;
    }
}
