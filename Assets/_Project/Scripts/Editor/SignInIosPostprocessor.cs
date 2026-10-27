#if UNITY_IOS && LUMA_SOCIAL_SIGNIN
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Sign in with Apple needs its entitlement and AuthenticationServices (AppleAuth's native code sits in
// UnityFramework). Google Sign-In's own PListProcessor adds GIDClientID and the REVERSED_CLIENT_ID URL scheme
// from GoogleService-Info.plist.
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
    }
}
#endif
