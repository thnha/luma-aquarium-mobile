using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One source image for the native launch, Unity splash and interactive start screen.
public sealed class LaunchBrandingBuilder : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public const string ArtworkPath = "Assets/_Project/UI/Backgrounds/AquariumLaunch.png";
    const string StartPrefab = "Assets/_Project/UI/Prefabs/StartGame.prefab";
    const string LoadingPrefab = "Assets/_Project/UI/Prefabs/LoadingGame.prefab";
    const string PreviewDir = "docs/previews/LaunchBranding";
    public int callbackOrder => -100;

    [MenuItem("Lumaquarium/UI/Apply Splash and Start Branding")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ApplyPlatformSettings();
        FrontEndScreenBuilder.BuildBrandedStart();
        Verify();
        CapturePreviews();
        Debug.Log("[Lumaquarium] Branded splash (iOS + Android) and StartGame applied and verified.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        ApplyPlatformSettings();
        Verify();
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;
        ConfigureIosLaunchScreens(report.summary.outputPath);
    }

    public static void ConfigureIosLaunchScreens(string outputPath)
    {
        FillIosLaunchStoryboard(Path.Combine(outputPath, "LaunchScreen-iPhone.storyboard"), "LaunchScreen-iPhonePortrait.png");
        FillIosLaunchStoryboard(Path.Combine(outputPath, "LaunchScreen-iPad.storyboard"), "LaunchScreen-iPad.png");
    }

    static void FillIosLaunchStoryboard(string path, string imageName)
    {
        var document = XDocument.Load(path);
        var root = document.Descendants("view").Single(view => (string)view.Attribute("key") == "view");
        var rootId = (string)root.Attribute("id");
        const string imageId = "Lumaquarium-LaunchArtwork";
        var image = new XElement("imageView",
            new XAttribute("id", imageId),
            new XAttribute("image", imageName),
            new XAttribute("contentMode", "scaleAspectFill"),
            new XAttribute("clipsSubviews", "YES"),
            new XAttribute("userInteractionEnabled", "NO"),
            new XAttribute("translatesAutoresizingMaskIntoConstraints", "NO"),
            new XElement(root.Element("rect")));

        // Unity's relative-image template uses aspect-fit inside oversized scale views.
        // Pin the artwork directly to the root (including notch/home indicator areas)
        // so it fills every aspect ratio, matching StartGame's EnvelopeParent background.
        root.Element("subviews").ReplaceNodes(image);
        root.Elements("variation").Remove();
        root.SetAttributeValue("clipsSubviews", "YES");
        var constraints = new XElement("constraints");
        foreach (string edge in new[] { "leading", "trailing", "top", "bottom" })
            constraints.Add(new XElement("constraint",
                new XAttribute("firstItem", imageId), new XAttribute("firstAttribute", edge),
                new XAttribute("secondItem", rootId), new XAttribute("secondAttribute", edge),
                new XAttribute("id", "Lumaquarium-Launch-" + edge)));
        root.Element("constraints").ReplaceWith(constraints);
        document.Save(path);
    }

    public static void ApplyPlatformSettings()
    {
        var art = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtworkPath);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
        if (art == null || sprite == null)
            throw new BuildFailedException("Missing launch artwork: " + ArtworkPath);
        PlayerSettings.productName = "Betta Fish Aquarium 3D";
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
        PlayerSettings.SplashScreen.logos = Array.Empty<PlayerSettings.SplashScreenLogo>();
        PlayerSettings.SplashScreen.background = sprite;
        PlayerSettings.SplashScreen.backgroundPortrait = sprite;
        PlayerSettings.SplashScreen.backgroundColor = new Color(.025f, .13f, .14f, 1);
        PlayerSettings.SplashScreen.overlayOpacity = 0;
        PlayerSettings.SplashScreen.blurBackgroundImage = false;
        PlayerSettings.Android.splashScreenScale = AndroidSplashScreenScale.ScaleToFill;
        PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
        PlayerSettings.iOS.SetiPadLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
        PlayerSettings.iOS.SetLaunchScreenImage(art, iOSLaunchScreenImageType.iPhonePortraitImage);
        PlayerSettings.iOS.SetLaunchScreenImage(art, iOSLaunchScreenImageType.iPhoneLandscapeImage);
        PlayerSettings.iOS.SetLaunchScreenImage(art, iOSLaunchScreenImageType.iPadImage);
        // The legacy Android splash is still used on older Android releases.
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        settings.FindProperty("androidSplashScreen").objectReferenceValue = art;
        settings.FindProperty("iOSLaunchScreenFillPct").floatValue = 100;
        settings.FindProperty("iOSLaunchScreeniPadFillPct").floatValue = 100;
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Lumaquarium/UI/Verify Splash and Start Branding")]
    public static void Verify()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartPrefab);
        if (prefab == null) throw new BuildFailedException("StartGame prefab is missing.");
        var background = prefab.transform.Find("AquariumBackground").GetComponent<UnityEngine.UI.Image>();
        if (AssetDatabase.GetAssetPath(background.sprite) != ArtworkPath)
            throw new BuildFailedException("StartGame and splash must share AquariumLaunch.png.");
        var loading = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingPrefab);
        if (loading == null || loading.transform.Find("AquariumBackground").GetComponent<UnityEngine.UI.Image>().sprite != background.sprite)
            throw new BuildFailedException("LoadingGame and StartGame must share AquariumLaunch.png.");
        var loadingController = new SerializedObject(loading.GetComponent<GameLoadingScreen>());
        foreach (string field in new[] { "progressFill", "progressLabel", "statusLabel", "errorActions", "retryButton", "backButton" })
            if (loadingController.FindProperty(field).objectReferenceValue == null)
                throw new BuildFailedException("LoadingGame is missing " + field);
        foreach (var screen in new[] { prefab, loading })
        {
            var scrim = screen.transform.Find("ControlsScrim") as RectTransform;
            if (scrim == null || scrim.anchorMin != Vector2.zero || scrim.anchorMax != Vector2.one
                || scrim.offsetMin != Vector2.zero || scrim.offsetMax != Vector2.zero)
                throw new BuildFailedException(screen.name + " scrim must cover the full canvas, outside SafeArea.");
        }
        var controller = prefab.GetComponent<StartGameScreen>();
        var serialized = new SerializedObject(controller);
        foreach (string field in new[] { "startButton", "accountButton", "startLabel", "feedback", "startControls" })
            if (serialized.FindProperty(field).objectReferenceValue == null)
                throw new BuildFailedException("StartGame is missing " + field);
        if (!prefab.GetComponent<Canvas>() || !prefab.GetComponent<UnityEngine.UI.GraphicRaycaster>())
            throw new BuildFailedException("StartGame canvas cannot receive button input.");
        if (EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled)?.path != "Assets/_Project/Scenes/StartGame.unity")
            throw new BuildFailedException("StartGame must be the first enabled scene.");
        if (PlayerSettings.SplashScreen.showUnityLogo || !PlayerSettings.SplashScreen.show
            || PlayerSettings.SplashScreen.backgroundPortrait != background.sprite)
            throw new BuildFailedException("Native splash settings are not in sync with StartGame.");
        var native = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        foreach (string field in new[] { "androidSplashScreen", "iOSLaunchScreenPortrait", "iOSLaunchScreenLandscape", "iOSLaunchScreeniPadImage" })
            if (AssetDatabase.GetAssetPath(native.FindProperty(field).objectReferenceValue) != ArtworkPath)
                throw new BuildFailedException("Launch image is not configured: " + field);
        if (native.FindProperty("iOSLaunchScreenType").intValue != (int)iOSLaunchScreenType.ImageAndBackgroundRelative
            || native.FindProperty("iOSLaunchScreeniPadType").intValue != (int)iOSLaunchScreenType.ImageAndBackgroundRelative)
            throw new BuildFailedException("iOS launch screens must use the branded relative image.");
        Directory.CreateDirectory(PreviewDir);
        File.WriteAllText(PreviewDir + "/verification.txt",
            "Branding verified: " + DateTime.UtcNow.ToString("O") + "\n"
            + "Product: " + PlayerSettings.productName + "\n"
            + "Shared artwork: " + ArtworkPath + "\n"
            + "Unity splash: enabled, static, no Unity logo, no blur/overlay\n"
            + "iOS: iPhone portrait/landscape and iPad launch image configured\n"
            + "Android: launch image configured, scale-to-fill\n"
            + "Start: canvas, raycaster, controls and controller references valid\n"
            + "Loading: shared launch artwork and controller references valid\n"
            + "Entry scene: StartGame\n"
            + "Device builds have not been installed or tested by this check.\n");
    }

    [MenuItem("Lumaquarium/UI/Capture Splash and Start Previews")]
    public static void CapturePreviews()
    {
        Directory.CreateDirectory(PreviewDir);
        Capture(540, 1170, true, "Splash-9x19.png");
        Capture(540, 1170, false, "StartGame-9x19.png");
        Capture(540, 960, false, "StartGame-9x16.png");
        Capture(768, 1024, false, "StartGame-iPad.png");
        Capture(540, 1170, false, "StartGame-returning.png", true);
        Capture(540, 1170, false, "LoadingGame-9x19.png", loading: true);
        Capture(540, 960, false, "LoadingGame-9x16.png", loading: true);
        Capture(768, 1024, false, "LoadingGame-iPad.png", loading: true);
        Capture(540, 1170, false, "LoadingGame-error.png", loading: true, error: true);
        Capture(540, 1170, false, "StartGame-safe-area.png", returning: true, safeInsets: true);
        Capture(540, 1170, false, "LoadingGame-safe-area.png", loading: true, safeInsets: true);
    }

    static void Capture(int width, int height, bool splash, string file, bool returning = false, bool loading = false, bool error = false, bool safeInsets = false)
    {
        var previousScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewPreviewScene();
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(loading ? LoadingPrefab : StartPrefab));
        SceneManager.MoveGameObjectToScene(root, scene);
        // Render without running gameplay, creating a profile or contacting remote content.
        if (loading) root.GetComponent<GameLoadingScreen>().enabled = false;
        else root.GetComponent<StartGameScreen>().enabled = false;
        foreach (var safe in root.GetComponentsInChildren<SafeArea>(true)) safe.enabled = false;
        if (safeInsets)
        {
            var safe = (RectTransform)root.transform.Find("SafeArea");
            safe.anchorMin = new Vector2(0, .038f);
            safe.anchorMax = new Vector2(1, .945f);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var controls = root.transform.Find("SafeArea/StartControls");
        if (controls != null) controls.gameObject.SetActive(!splash);
        root.transform.Find("ControlsScrim").gameObject.SetActive(!splash);
        if (loading && error)
        {
            var controller = new SerializedObject(root.GetComponent<GameLoadingScreen>());
            ((GameObject)controller.FindProperty("errorActions").objectReferenceValue).SetActive(true);
            ((TMP_Text)controller.FindProperty("statusLabel").objectReferenceValue).text = "Chưa tải được hồ cá. Kiểm tra kết nối mạng rồi thử lại.";
        }
        if (returning)
            controls.Find("StartButton/Label").GetComponent<TMP_Text>().text = "Tiếp tục hồ của bạn";
        var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .13f, .14f, 1);
        camera.cullingMask = 1 << 30;
        camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.orthographic = true;
        var target = RenderTexture.GetTemporary(width, height, 24);
        var previousTarget = RenderTexture.active;
        camera.targetTexture = target;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        try
        {
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(PreviewDir + "/" + file, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.ClosePreviewScene(scene);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
        }
    }
}
