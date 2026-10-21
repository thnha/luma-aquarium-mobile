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
        Ui3D.LoadFonts();
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

    // Start and guest login are one screen; LoginGame keeps only the coming-soon account form.
    private static void BuildStart()
    {
        var canvas = NewScreen(FrontEndScenes.Start);
        var controller = canvas.gameObject.AddComponent<StartGameScreen>();
        var safe = SafeRoot(canvas);
        var subtitle = Header(safe, "Lumaquarium", "Hồ cá của riêng bạn");
        var card = Ui3D.Panel("StartPanel", safe);
        Ui3D.At(card.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 88), new Vector2(1000, 690));
        var title = Ui3D.Text("Title", card.transform, "Một góc hồ dành cho bạn", 60, Ui3D.Ink, true);
        Ui3D.TopStrip(title.rectTransform, 50, 50, 60, 80);
        var description = Ui3D.Text("Description", card.transform, "Bắt đầu ngay, không cần tài khoản", 40, Ui3D.Muted);
        Ui3D.TopStrip(description.rectTransform, 50, 50, 145, 56);
        var start = Ui3D.Button("StartButton", card.transform, "Chơi ngay", Ui3D.Style.Primary, 50);
        Ui3D.TopStrip((RectTransform)start.transform, 60, 60, 230, 150);
        var account = Ui3D.Button("AccountButton", card.transform, "Đăng nhập tài khoản", Ui3D.Style.Secondary);
        Ui3D.TopStrip((RectTransform)account.transform, 60, 60, 400, 150);
        var accountLabel = account.GetComponentInChildren<TMP_Text>();
        accountLabel.rectTransform.offsetMax = new Vector2(-200, -6);
        var soon = Ui3D.Badge("Soon", account.transform, "Sắp có", true, out _);
        Ui3D.At(soon.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-34, 4), new Vector2(180, 66));
        var note = Ui3D.Text("LocalSaveNote", card.transform, "Tiến trình chơi khách lưu trên thiết bị này", 34, Ui3D.Muted);
        Ui3D.TopStrip(note.rectTransform, 50, 50, 575, 56);
        var feedback = Text("Feedback", safe, "", 31, Cream);
        At(feedback.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 800), new Vector2(940, 80));
        Assign(controller, "startButton", start);
        Assign(controller, "accountButton", account);
        Assign(controller, "startLabel", start.GetComponentInChildren<TMP_Text>());
        Assign(controller, "subtitle", subtitle);
        Assign(controller, "feedback", feedback);
        SaveScreen(canvas, FrontEndScenes.Start);
    }

    private static void BuildLogin()
    {
        var canvas = NewScreen(FrontEndScenes.Login);
        var controller = canvas.gameObject.AddComponent<LoginGameScreen>();
        var safe = SafeRoot(canvas);
        var back = Ui3D.RoundButton("BackButton", safe, "utility_back");
        At((RectTransform)back.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -40), new Vector2(122, 122));
        var heading = Ui3D.Text("Heading", safe, "Tài khoản", 56, Cream, true, TextAlignmentOptions.MidlineLeft);
        At(heading.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -40), new Vector2(700, 122));

        var form = Ui3D.Panel("AccountPanel", safe);
        Ui3D.At(form.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 88), new Vector2(1000, 1210));
        var soon = Ui3D.Badge("Soon", form.transform, "SẮP CÓ", true, out _);
        At(soon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -50), new Vector2(210, 66));
        var title = Ui3D.Text("AccountTitle", form.transform, "Đăng nhập tài khoản", 60, Ui3D.Ink, true);
        Ui3D.TopStrip(title.rectTransform, 50, 50, 130, 80);
        var description = Ui3D.Text("AccountAvailability", form.transform, "Đồng bộ hồ giữa nhiều thiết bị sẽ mở trong bản sau", 38, Ui3D.Muted);
        Ui3D.TopStrip(description.rectTransform, 50, 50, 215, 100);
        var emailLabel = Ui3D.Text("EmailLabel", form.transform, "Email", 34, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(emailLabel.rectTransform, 74, 70, 330, 50);
        var email = Input("EmailInput", form.transform, "ban@vidu.com", -445, false);
        email.contentType = TMP_InputField.ContentType.EmailAddress;
        var passwordLabel = Ui3D.Text("PasswordLabel", form.transform, "Mật khẩu", 34, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(passwordLabel.rectTransform, 74, 70, 540, 50);
        var password = Input("PasswordInput", form.transform, "Mật khẩu", -655, true);
        email.interactable = password.interactable = false;
        var submit = Ui3D.Button("AccountSubmitButton", form.transform, "Đăng nhập", Ui3D.Style.Primary);
        Ui3D.TopStrip((RectTransform)submit.transform, 70, 70, 750, 150);
        submit.interactable = false;
        var divider = Ui3D.Solid("Divider", form.transform, Ui3D.Divider);
        Ui3D.TopStrip(divider.rectTransform, 70, 70, 925, 4);
        var guest = Ui3D.Button("ReturnToGuestButton", form.transform, "Chơi với tư cách khách", Ui3D.Style.Primary);
        Ui3D.TopStrip((RectTransform)guest.transform, 70, 70, 955, 150);
        var note = Ui3D.Text("PrivacyNote", form.transform, "Không gửi hay lưu thông tin đăng nhập", 32, Ui3D.Muted);
        Ui3D.TopStrip(note.rectTransform, 50, 50, 1115, 50);

        var feedback = Text("Feedback", safe, "", 30, Cream);
        At(feedback.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 1320), new Vector2(960, 80));
        Assign(controller, "accountPanel", form.gameObject);
        Assign(controller, "returnToGuestButton", guest);
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
        var card = Ui3D.Panel("LoadingPanel", safe);
        Ui3D.At(card.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 88), new Vector2(1000, 500));
        var status = Ui3D.Text("LoadingStatus", card.transform, "Đang chuẩn bị hồ cá...", 46, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(status.rectTransform, 60, 230, 60, 70);
        var percent = Ui3D.Text("ProgressPercent", card.transform, "0%", 44, Ui3D.Forest, true, TextAlignmentOptions.MidlineRight);
        Ui3D.TopStrip(percent.rectTransform, 700, 60, 60, 70);
        var track = Ui3D.Image("ProgressTrack", card.transform, "ui3d_track");
        Ui3D.TopStrip(track.rectTransform, 60, 60, 150, 48);
        var fill = Ui3D.Image("ProgressFill", track.transform, "ui3d_fill", false);
        Stretch(fill.rectTransform, 6);
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        fill.fillAmount = 0;
        var tip = Ui3D.Text("LoadingTip", card.transform, "Bạn có thể xoay góc nhìn để ngắm hồ", 36, Ui3D.Muted);
        Ui3D.TopStrip(tip.rectTransform, 50, 50, 225, 50);
        var actions = Ui3D.Rect("ErrorActions", card.transform);
        Ui3D.TopStrip(actions, 60, 60, 300, 150);
        var back = Ui3D.Button("BackButton", actions, "Quay lại", Ui3D.Style.Secondary);
        Ui3D.Edges((RectTransform)back.transform, Vector2.zero, new Vector2(.41f, 1), Vector2.zero, Vector2.zero);
        var retry = Ui3D.Button("RetryButton", actions, "Thử lại", Ui3D.Style.Primary);
        Ui3D.Edges((RectTransform)retry.transform, new Vector2(.44f, 0), Vector2.one, Vector2.zero, Vector2.zero);
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

    private static TMP_Text Header(Transform parent, string title, string subtitle)
    {
        var label = Text("Title", parent, title, 92, Cream, titleFont);
        At(label.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -135), new Vector2(950, 135));
        var small = Text("Subtitle", parent, subtitle, 36, Cream);
        At(small.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -255), new Vector2(950, 80));
        return small;
    }

    private static TMP_InputField Input(string name, Transform parent, string placeholder, float y, bool password)
    {
        var image = Picture(name, parent, "ui3d_tile_empty");
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.raycastTarget = true;
        At(image.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(860, 132));
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
