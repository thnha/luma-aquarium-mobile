using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.U2D;

public static class FrontEndScreenBuilder
{
    private const string Root = "Assets/_Project/UI";
    private const string SceneRoot = "Assets/_Project/Scenes/";
    private static readonly Color Ink = Hex("343C2D");
    private static readonly Color Cream = Hex("FFF5DD");
    private static readonly Color Muted = Hex("65715A");
    private static TMP_FontAsset bodyFont;
    private static TMP_FontAsset titleFont;

    [MenuItem("Lumaquarium/UI/Prepare Sprite Assets")]
    public static void PrepareAssets()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string file in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            if (importer == null || importer.textureType != TextureImporterType.Sprite
                || importer.spriteBorder != FrontEndAssetImporter.BorderFor(Path.GetFileNameWithoutExtension(file)))
                AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] UI sprites prepared. TMP essentials imported without a modal dialog.");
    }

    [MenuItem("Lumaquarium/UI/Build Front End Screens")]
    public static void BuildScreens()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building UI scenes.");
        var entryPaths = new[] { FrontEndScenes.Start, FrontEndScenes.Login, FrontEndScenes.Loading }
            .Select(n => SceneRoot + n + ".unity").ToArray();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty && entryPaths.Contains(SceneManager.GetSceneAt(i).path))
                throw new InvalidOperationException("Save the front end scene changes before rebuilding it.");
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            throw new InvalidOperationException("Run Lumaquarium/UI/Prepare Sprite Assets first.");

        bodyFont = FontAsset("Nunito-SemiBold");
        titleFont = FontAsset("Nunito-ExtraBold");
        CreateScrim();
        CreateAtlas();
        var previousActive = SceneManager.GetActiveScene();
        var originalHandles = Enumerable.Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i).handle).ToHashSet();
        try
        {
            BuildStart();
            BuildLogin();
            BuildLoading();
            var scenes = entryPaths.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => !entryPaths.Contains(s.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
        finally
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!originalHandles.Contains(scene.handle)) EditorSceneManager.CloseScene(scene, true);
            }
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
        Debug.Log("[Lumaquarium] StartGame, LoginGame and LoadingGame built. MainAquarium preserved.");
    }

    private static void BuildStart()
    {
        var canvas = NewScreen(FrontEndScenes.Start);
        var controller = canvas.gameObject.AddComponent<StartGameScreen>();
        var safe = SafeRoot(canvas);
        Header(safe, "Lumaquarium", "Hồ cá của riêng bạn");
        var button = Button("StartButton", safe, "Bắt đầu", "button_primary", new Vector2(0, 290), new Vector2(760, 146));
        var arrow = Picture("Arrow", button.transform, "utility_back");
        At(arrow.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-43, 0), new Vector2(55, 55));
        arrow.rectTransform.localScale = new Vector3(-1, 1, 1);
        var feedback = Text("Feedback", safe, "", 31, Cream);
        At(feedback.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 145), new Vector2(900, 90));
        var caption = Text("Caption", safe, "Ngắm cá bơi, chăm chút góc hồ của mình", 33, Cream);
        At(caption.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 95), new Vector2(940, 60));
        Assign(controller, "startButton", button);
        Assign(controller, "feedback", feedback);
        SaveScreen(canvas, FrontEndScenes.Start);
    }

    private static void BuildLogin()
    {
        var canvas = NewScreen(FrontEndScenes.Login);
        var controller = canvas.gameObject.AddComponent<LoginGameScreen>();
        var safe = SafeRoot(canvas);
        Header(safe, "Lumaquarium", "Chào mừng bạn trở lại");
        var back = IconButton("BackButton", safe, "utility_back");
        At((RectTransform)back.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -55), new Vector2(96, 96));

        var guest = Panel("GuestPanel", safe, new Vector2(0, 110), new Vector2(900, 600));
        PanelText("GuestTitle", guest, "Một góc hồ dành cho bạn", 55, -82, 790, titleFont);
        PanelText("GuestDescription", guest, "Bắt đầu nhẹ nhàng, không cần tài khoản", 33, -166, 790, bodyFont, Muted);
        var playGuest = Button("GuestButton", guest, "Chơi với tư cách khách", "button_primary", new Vector2(0, -290), new Vector2(760, 136), true);
        var account = Button("AccountButton", guest, "Đăng nhập tài khoản", "button_normal", new Vector2(0, -430), new Vector2(760, 118), true);
        PanelText("LocalSaveNote", guest, "Hành trình chơi khách lưu trên thiết bị này", 29, -525, 790, bodyFont, Muted);

        var accountForm = Panel("AccountPanel", safe, new Vector2(0, 110), new Vector2(900, 820));
        PanelText("AccountTitle", accountForm, "Đăng nhập tài khoản", 55, -83, 790, titleFont);
        PanelText("AccountAvailability", accountForm, "Sắp có · Bạn vẫn có thể chơi khách ngay", 31, -158, 790, bodyFont, Muted);
        var email = Input("EmailInput", accountForm, "Email", -282, false);
        email.contentType = TMP_InputField.ContentType.EmailAddress;
        var password = Input("PasswordInput", accountForm, "Mật khẩu", -430, true);
        email.interactable = password.interactable = false;
        var submit = Button("AccountSubmitButton", accountForm, "Đăng nhập", "button_primary", new Vector2(0, -578), new Vector2(760, 126), true);
        submit.interactable = false;
        var returnGuest = Button("ReturnToGuestButton", accountForm, "Quay lại chơi khách", "button_normal", new Vector2(0, -720), new Vector2(760, 114), true);
        accountForm.gameObject.SetActive(false);

        var feedback = Text("Feedback", safe, "", 30, Cream);
        At(feedback.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 15), new Vector2(960, 80));
        Assign(controller, "guestPanel", guest.gameObject);
        Assign(controller, "accountPanel", accountForm.gameObject);
        Assign(controller, "guestButton", playGuest);
        Assign(controller, "accountButton", account);
        Assign(controller, "returnToGuestButton", returnGuest);
        Assign(controller, "backButton", back);
        Assign(controller, "password", password);
        Assign(controller, "feedback", feedback);
        SaveScreen(canvas, FrontEndScenes.Login);
    }

    private static void BuildLoading()
    {
        var canvas = NewScreen(FrontEndScenes.Loading);
        var controller = canvas.gameObject.AddComponent<GameLoadingScreen>();
        var safe = SafeRoot(canvas);
        Header(safe, "Lumaquarium", "Góc hồ đang chờ bạn");
        var status = Text("LoadingStatus", safe, "Đang chuẩn bị hồ cá...", 44, Cream);
        At(status.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 365), new Vector2(920, 90));
        var track = Picture("ProgressTrack", safe, "progress_track");
        track.type = UnityEngine.UI.Image.Type.Sliced;
        At(track.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 300), new Vector2(760, 42));
        var fill = Picture("ProgressFill", track.transform, "progress_fill");
        Stretch(fill.rectTransform, 3);
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        fill.fillAmount = 0;
        var percent = Text("ProgressPercent", safe, "0%", 35, Cream);
        At(percent.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 220), new Vector2(760, 60));
        var tip = Text("LoadingTip", safe, "Bạn có thể xoay góc nhìn để ngắm hồ", 32, Cream);
        At(tip.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 145), new Vector2(920, 60));
        var actions = Rect("ErrorActions", safe);
        At(actions, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 80), new Vector2(800, 140));
        var retry = Button("RetryButton", actions, "Thử lại", "button_normal", new Vector2(-200, 0), new Vector2(365, 130));
        var back = Button("BackButton", actions, "Quay lại", "button_normal", new Vector2(200, 0), new Vector2(365, 130));
        actions.gameObject.SetActive(false);
        Assign(controller, "progressFill", fill);
        Assign(controller, "progressLabel", percent);
        Assign(controller, "statusLabel", status);
        Assign(controller, "errorActions", actions.gameObject);
        Assign(controller, "retryButton", retry);
        Assign(controller, "backButton", back);
        SaveScreen(canvas, FrontEndScenes.Loading);
    }

    private static RectTransform NewScreen(string name)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var camera = new GameObject("MainCamera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Hex("173F38");
        camera.transform.position = new Vector3(0, 0, -10);
        camera.gameObject.AddComponent<AudioListener>();
        new GameObject("MobileSettings").AddComponent<MobileSettings>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var root = Rect(name + "Screen", null);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var background = Picture("AquariumBackground", root, "AquariumFrontEnd");
        Stretch(background.rectTransform);
        var fitter = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
        fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = background.sprite.rect.width / background.sprite.rect.height;
        var scrim = Picture("BottomScrim", root, "FrontEndScrim");
        Stretch(scrim.rectTransform);
        var tint = Picture("SoftTint", root, null);
        tint.color = new Color(.025f, .07f, .05f, .12f);
        Stretch(tint.rectTransform);
        return root;
    }

    private static RectTransform SafeRoot(RectTransform canvas)
    {
        var safe = Rect("SafeArea", canvas);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();
        return safe;
    }

    private static void Header(Transform parent, string title, string subtitle)
    {
        var label = Text("Title", parent, title, 92, Cream, titleFont);
        At(label.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -135), new Vector2(950, 135));
        var small = Text("Subtitle", parent, subtitle, 36, Cream);
        At(small.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -255), new Vector2(950, 80));
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var image = Picture(name, parent, "panel_care");
        image.type = UnityEngine.UI.Image.Type.Sliced;
        At(image.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), position, size);
        return image.rectTransform;
    }

    private static TMP_Text PanelText(string name, Transform parent, string value, float size, float y,
        float width, TMP_FontAsset font, Color? color = null)
    {
        var label = Text(name, parent, value, size, color ?? Ink, font);
        At(label.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(width, 80));
        return label;
    }

    private static UnityEngine.UI.Button Button(string name, Transform parent, string label,
        string surface, Vector2 position, Vector2 size, bool fromTop = false)
    {
        // Square action tiles compress their vertical gradient in short button rows.
        string spriteName = surface == "button_normal" || surface == "button_selected"
            || surface == "button_pressed" || surface == "button_disabled" ? surface + "_wide" : surface;
        var image = Picture(name, parent, spriteName);
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.raycastTarget = true;
        At(image.rectTransform, new Vector2(.5f, fromTop ? 1 : 0), new Vector2(.5f, .5f), position, size);
        // Keep both border pairs inside the rect so uGUI never squashes just one axis.
        var border = image.sprite.border / image.pixelsPerUnit;
        image.pixelsPerUnitMultiplier = Mathf.Max(1, Mathf.Max(
            (border.x + border.z) / size.x, (border.y + border.w) / size.y));
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(.98f, 1, .95f);
        colors.pressedColor = new Color(.84f, .88f, .78f);
        colors.disabledColor = new Color(.85f, .85f, .8f, .66f);
        button.colors = colors;
        var text = Text("Label", image.transform, label, 42, surface == "button_primary" ? Cream : Ink, titleFont);
        Stretch(text.rectTransform, 22);
        return button;
    }

    private static UnityEngine.UI.Button IconButton(string name, Transform parent, string icon)
    {
        var background = Picture(name, parent, "utility_badge");
        background.raycastTarget = true;
        var button = background.gameObject.AddComponent<UnityEngine.UI.Button>();
        var glyph = Picture("Icon", background.transform, icon);
        Stretch(glyph.rectTransform, 22);
        return button;
    }

    private static TMP_InputField Input(string name, Transform parent, string placeholder, float y, bool password)
    {
        var image = Picture(name, parent, "inventory_slot");
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.raycastTarget = true;
        At(image.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(760, 124));
        var input = image.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        var viewport = Rect("Viewport", image.transform);
        Stretch(viewport, 24);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var text = Text("Text", viewport, "", 36, Ink);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        var hint = Text("Placeholder", viewport, placeholder, 36, Muted);
        Stretch(hint.rectTransform);
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = hint;
        input.characterLimit = 128;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        return input;
    }

    private static TMP_Text Text(string name, Transform parent, string value, float size, Color color, TMP_FontAsset font = null)
    {
        var rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font ?? bodyFont;
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static UnityEngine.UI.Image Picture(string name, Transform parent, string spriteName)
    {
        var image = Rect(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>();
        if (spriteName != null) image.sprite = Sprite(spriteName);
        image.raycastTarget = false;
        return image;
    }

    private static Sprite Sprite(string name)
    {
        foreach (string sub in new[] { "Surfaces", "Icons", "Backgrounds" })
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{sub}/{name}.png");
            if (sprite != null) return sprite;
        }
        throw new InvalidOperationException("Missing UI sprite: " + name);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }

    private static void At(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("Missing serialized field: " + field);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SaveScreen(RectTransform root, string name)
    {
        string prefab = $"{Root}/Prefabs/{name}.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, prefab, InteractionMode.AutomatedAction);
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, SceneRoot + name + ".unity");
        EditorSceneManager.CloseScene(scene, true);
    }

    private static TMP_FontAsset FontAsset(string name)
    {
        string path = $"{Root}/Fonts/{name} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;
        var font = AssetDatabase.LoadAssetAtPath<Font>($"{Root}/Fonts/{name}.ttf");
        if (font == null) throw new InvalidOperationException("Missing static font: " + name);
        var asset = TMP_FontAsset.CreateFontAsset(font, 70, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = name + " SDF";
        AssetDatabase.CreateAsset(asset, path);
        string characters = new string(Enumerable.Range(32, 95)
            .Concat(Enumerable.Range(160, 352)).Concat(Enumerable.Range(0x1EA0, 90))
            .Select(v => (char)v).ToArray());
        asset.TryAddCharacters(characters, out string missing);
        if (missing.Any(c => c >= 0x1EA0 && c <= 0x1EF9) || !asset.HasCharacter('Đ') || !asset.HasCharacter('ư'))
            throw new InvalidOperationException("Vietnamese font glyphs are incomplete: " + name);
        if (!AssetDatabase.Contains(asset.material)) AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var atlas in asset.atlasTextures)
            if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    private static void CreateScrim()
    {
        string path = Root + "/Backgrounds/FrontEndScrim.png";
        if (File.Exists(path)) return;
        var texture = new Texture2D(4, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 4; x++)
            {
                float t = y / 127f;
                float opacity = Mathf.Lerp(.74f, .02f, Mathf.SmoothStep(0, 1, t * 1.4f));
                texture.SetPixel(x, y, new Color(.025f, .075f, .055f, opacity));
            }
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void CreateAtlas()
    {
        string path = Root + "/LumaquariumUI.spriteatlas";
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, path);
        }
        atlas.SetIncludeInBuild(true);
        atlas.SetPackingSettings(new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 });
        atlas.SetTextureSettings(new SpriteAtlasTextureSettings { readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear });
        atlas.Remove(atlas.GetPackables());
        atlas.Add(new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(Root + "/Icons"), AssetDatabase.LoadAssetAtPath<DefaultAsset>(Root + "/Surfaces") });
        EditorUtility.SetDirty(atlas);
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
}
