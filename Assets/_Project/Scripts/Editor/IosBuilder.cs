using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Exports the iOS Xcode project to Builds/iOS for TestFlight. Tools/build_testflight.sh calls
// BuildFromCommandLine in batch mode, then archives and uploads it with xcodebuild.
// Menu: Lumaquarium > Build > iOS Xcode Project (bumps the build number by one).
// Batch: Unity -batchmode -quit -projectPath mobile -executeMethod IosBuilder.BuildFromCommandLine -buildNumber 42
public static class IosBuilder
{
    public const string OutputDir = "Builds/iOS";
    const string AppIconPath = "Assets/_Project/Art/Icons/AppIcon.png";

    [MenuItem("Lumaquarium/Build/iOS Xcode Project")]
    static void BuildFromMenu()
    {
        int.TryParse(PlayerSettings.iOS.buildNumber, out var current);
        Build((current + 1).ToString());
    }

    [MenuItem("Lumaquarium/Build/iOS Xcode Project", true)]
    static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static void BuildFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "-buildNumber");
        var buildNumber = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        if (string.IsNullOrEmpty(buildNumber))
        {
            int.TryParse(PlayerSettings.iOS.buildNumber, out var current);
            buildNumber = (current + 1).ToString();
        }
        EditorApplication.Exit(Build(buildNumber) ? 0 : 1);
    }

    // App Store Connect rejects uploads without a 1024x1024 icon; Unity scales the default icon to every
    // iOS and Android size. Shared with AndroidBuilder.
    public static bool ApplyAppIcon()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
        if (!icon)
        {
            Debug.LogError("[Lumaquarium] Missing " + AppIconPath + " (1024x1024 PNG, no alpha).");
            return false;
        }
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        return true;
    }

    static bool Build(string buildNumber)
    {
        if (string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID))
        {
            Debug.LogError("[Lumaquarium] Set Player > iOS > Signing Team ID before building.");
            return false;
        }

        if (!ApplyAppIcon()) return false;

        PlayerSettings.iOS.buildNumber = buildNumber;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        AssetDatabase.SaveAssets();

        // Remote content (MainAquarium, fish, decor) is built for the active platform; switch back afterwards
        // so a menu build leaves the editor on the platform it was on.
        var previousTarget = EditorUserBuildSettings.activeBuildTarget;
        if (previousTarget != BuildTarget.iOS)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        try
        {
            if (!RemoteContent.Build()) return false;
            return BuildPlayer(buildNumber);
        }
        finally
        {
            if (previousTarget != BuildTarget.iOS && !Application.isBatchMode)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget), previousTarget);
        }
    }

    static bool BuildPlayer(string buildNumber)
    {
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = OutputDir,
            target = BuildTarget.iOS,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var ok = report.summary.result == BuildResult.Succeeded;
        if (ok)
            Debug.Log($"[Lumaquarium] iOS {PlayerSettings.bundleVersion} ({buildNumber}) exported to {OutputDir}");
        else
            Debug.LogError($"[Lumaquarium] iOS build {report.summary.result}: {report.summary.totalErrors} error(s)");
        return ok;
    }
}
