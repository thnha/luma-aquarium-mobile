using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the Android app to Builds/Android: an .aab for Google Play or an .apk to sideload.
// Tools/build_android.sh calls BuildFromCommandLine in batch mode.
// Menu: Lumaquarium > Build > Android App Bundle / Android APK (bumps the version code by one).
// Batch: Unity -batchmode -quit -projectPath mobile -buildTarget Android -executeMethod AndroidBuilder.BuildFromCommandLine
//        [-format apk] [-buildNumber 42]
// Upload signing comes from ANDROID_KEYSTORE, ANDROID_KEYSTORE_PASS, ANDROID_KEY_ALIAS, ANDROID_KEY_PASS (Unity never
// stores keystore passwords). Without them the build falls back to Player Settings, else to the debug key, which is
// fine for an APK on a test device but rejected by Google Play.
public static class AndroidBuilder
{
    public const string OutputDir = "Builds/Android";

    [MenuItem("Lumaquarium/Build/Android App Bundle (Play)")]
    static void BuildBundleFromMenu() => Build(true, NextVersionCode());

    [MenuItem("Lumaquarium/Build/Android APK (device)")]
    static void BuildApkFromMenu() => Build(false, NextVersionCode());

    [MenuItem("Lumaquarium/Build/Android App Bundle (Play)", true)]
    [MenuItem("Lumaquarium/Build/Android APK (device)", true)]
    static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static void BuildFromCommandLine()
    {
        var bundle = Arg("-format") != "apk";
        var versionCode = int.TryParse(Arg("-buildNumber"), out var n) ? n : NextVersionCode();
        EditorApplication.Exit(Build(bundle, versionCode) ? 0 : 1);
    }

    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static int NextVersionCode() => PlayerSettings.Android.bundleVersionCode + 1;

    static bool Build(bool bundle, int versionCode)
    {
        if (!IosBuilder.ApplyAppIcon()) return false;
        if (!ApplySigning(bundle)) return false;

        PlayerSettings.Android.bundleVersionCode = versionCode;
        EditorUserBuildSettings.buildAppBundle = bundle;
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory(OutputDir);
        var name = $"BettaFishAquarium3D-{PlayerSettings.bundleVersion}-{versionCode}.{(bundle ? "aab" : "apk")}";
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(OutputDir, name),
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var ok = report.summary.result == BuildResult.Succeeded;
        if (ok)
            Debug.Log($"[Lumaquarium] Android {PlayerSettings.bundleVersion} ({versionCode}) built to {options.locationPathName}");
        else
            Debug.LogError($"[Lumaquarium] Android build {report.summary.result}: {report.summary.totalErrors} error(s)");
        return ok;
    }

    static bool ApplySigning(bool bundle)
    {
        var keystore = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE");
        if (!string.IsNullOrEmpty(keystore))
        {
            if (!File.Exists(keystore))
            {
                Debug.LogError("[Lumaquarium] ANDROID_KEYSTORE not found: " + keystore);
                return false;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("ANDROID_KEY_PASS")
                ?? PlayerSettings.Android.keystorePass;
        }

        if (PlayerSettings.Android.useCustomKeystore)
        {
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            {
                Debug.LogError("[Lumaquarium] Keystore passwords missing: set ANDROID_KEYSTORE_PASS / ANDROID_KEY_PASS or enter them in Player > Publishing Settings.");
                return false;
            }
        }
        else if (bundle)
        {
            Debug.LogWarning("[Lumaquarium] No upload keystore: the .aab is signed with the debug key and Google Play will reject it.");
        }
        return true;
    }
}
