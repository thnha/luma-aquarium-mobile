#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// GoogleMobileAds' own postprocessor writes App ID, ATT description and SKAdNetwork IDs.
// UMP's configured IDFA message uses Apple's ATT framework before ads are initialized.
public static class AdMobIosPostprocessor
{
    [PostProcessBuild(10)]
    public static void Process(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        string path = PBXProject.GetPBXProjectPath(output);
        var project = new PBXProject();
        project.ReadFromFile(path);
        project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);
        project.WriteToFile(path);
    }
}
#endif
