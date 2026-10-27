#if UNITY_IOS && LUMA_SOCIAL_SIGNIN
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Sign in with Apple needs its entitlement and AuthenticationServices (AppleAuth's native code sits in
// UnityFramework). Google Sign-In returns to the app through the REVERSED_CLIENT_ID URL scheme from
// GoogleService-Info.plist; it is added here when Firebase's own postprocessor has not done so.
public static class SignInIosPostprocessor
{
    [PostProcessBuild(20)]
    public static void Process(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        string path = PBXProject.GetPBXProjectPath(output);
        var project = new PBXProject();
        project.ReadFromFile(path);
        project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AuthenticationServices.framework", true);
        project.WriteToFile(path);

        var capabilities = new ProjectCapabilityManager(path, "Unity-iPhone/Unity-iPhone.entitlements", null, project.GetUnityMainTargetGuid());
        capabilities.AddSignInWithApple();
        capabilities.WriteToFile();

        AddGoogleUrlScheme(output);
    }

    private static void AddGoogleUrlScheme(string output)
    {
        var config = Directory.GetFiles("Assets", "GoogleService-Info.plist", SearchOption.AllDirectories).FirstOrDefault();
        if (config == null) return;
        var google = new PlistDocument();
        google.ReadFromFile(config);
        if (!google.root.values.TryGetValue("REVERSED_CLIENT_ID", out var reversed)) return;
        var scheme = reversed.AsString();

        string infoPath = Path.Combine(output, "Info.plist");
        var info = new PlistDocument();
        info.ReadFromFile(infoPath);
        var types = info.root.values.TryGetValue("CFBundleURLTypes", out var existing) ? existing.AsArray() : info.root.CreateArray("CFBundleURLTypes");
        bool present = types.values.Any(t => t.AsDict().values.TryGetValue("CFBundleURLSchemes", out var schemes)
            && schemes.AsArray().values.Any(s => s.AsString() == scheme));
        if (present) return;
        var entry = types.AddDict();
        entry.SetString("CFBundleURLName", "google-sign-in");
        entry.CreateArray("CFBundleURLSchemes").AddString(scheme);
        info.WriteToFile(infoPath);
    }
}
#endif
