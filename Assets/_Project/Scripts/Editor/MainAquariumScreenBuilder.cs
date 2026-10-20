using System;
using System.IO;
using System.Linq;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Adds the approved HUD to the existing aquarium without rebuilding its fish or rig.
public static class MainAquariumScreenBuilder
{
    private const string Root = "Assets/_Project/UI";
    private const string ScenePath = "Assets/_Project/Scenes/MainAquarium.unity";
    private static readonly Color Ink = new Color32(52, 60, 45, 255);
    private static readonly Color Muted = new Color32(101, 113, 90, 255);
    private static TMP_FontAsset bodyFont;
    private static TMP_FontAsset titleFont;

    [MenuItem("Lumaquarium/UI/Build Main Aquarium Screen")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building MainAquarium.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Open MainAquarium before building its HUD.");

        var fish = UnityEngine.Object.FindFirstObjectByType<BettaFishAI>();
        var feeding = UnityEngine.Object.FindFirstObjectByType<FeedingSystem>();
        var orbit = UnityEngine.Object.FindFirstObjectByType<OrbitTouchCamera>();
        if (fish == null || feeding == null || orbit == null)
            throw new InvalidOperationException("MainAquarium requires its existing fish, feeding system and camera.");
        bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Nunito-SemiBold SDF.asset");
        titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Nunito-ExtraBold SDF.asset");
        if (bodyFont == null || titleFont == null || Sprite("AquariumEnvironment") == null)
            throw new InvalidOperationException("Prepare the UI fonts and clean environment first.");

        // Retain both the on-disk and any unsaved authored scene before making changes.
        string backupDir = "Assets/_Project/Scenes/Backups";
        Directory.CreateDirectory(backupDir);
        string backupName = $"{backupDir}/MainAquarium-before-screen-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}";
        File.Copy(ScenePath, backupName + "-saved.unity");
        EditorSceneManager.SaveScene(scene, backupName + "-working.unity", true);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build approved MainAquarium screen");

        var old = GameObject.Find("Canvas");
        if (old != null)
        {
            Undo.RecordObject(old, "Preserve starter HUD");
            old.name = "LegacyAquariumHUD";
            old.SetActive(false);
        }
        foreach (string name in new[] { "MainAquariumHUD", "AquariumBackdrop" })
        {
            var existing = GameObject.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
        }

        ConfigurePresentation(fish, orbit);
        BuildBackdrop(orbit.GetComponent<Camera>());
        BakePortrait(fish);
        var canvas = NewCanvas("MainAquariumHUD");
        var controller = canvas.gameObject.AddComponent<MainAquariumScreen>();
        controller.fish = fish;
        controller.feeding = feeding;
        controller.orbit = orbit;
        controller.hud = canvas.gameObject.AddComponent<CanvasGroup>();
        var safe = Rect("SafeArea", canvas);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();

        var header = Rect("Header", safe);
        header.anchorMin = new Vector2(0, 1);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(.5f, 1);
        header.sizeDelta = new Vector2(-96, 144);
        header.anchoredPosition = new Vector2(0, -50);
        controller.header = header.gameObject;
        var plate = Picture("FishNameplate", header, "fish_nameplate", true);
        At(plate.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -19), new Vector2(255, 87));
        var nameLabel = Text("FishName", plate.transform, "Betta", 39, Ink, true);
        Stretch(nameLabel.rectTransform);
        nameLabel.rectTransform.offsetMin = new Vector2(46, 5);
        var portrait = Picture("BettaPortrait", header, "BettaPortrait");
        At(portrait.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(124, 124));
        var frame = Picture("PortraitFrame", portrait.transform, "fish_portrait_frame");
        Stretch(frame.rectTransform);
        var settings = IconButton("SettingsButton", header, "utility_settings");
        At((RectTransform)settings.transform, Vector2.one, Vector2.one, new Vector2(-100, -7), new Vector2(86, 86));
        Wire(settings, controller.ToggleSettings);
        var hide = IconButton("HideButton", header, "utility_hide");
        At((RectTransform)hide.transform, Vector2.one, Vector2.one, new Vector2(0, -7), new Vector2(86, 86));
        Wire(hide, controller.ToggleHud);

        var toolbar = Rect("BottomBar", safe);
        toolbar.anchorMin = Vector2.zero;
        toolbar.anchorMax = new Vector2(1, 0);
        toolbar.pivot = new Vector2(.5f, 0);
        toolbar.sizeDelta = new Vector2(-96, 222);
        toolbar.anchoredPosition = new Vector2(0, 75);
        var layout = toolbar.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 24;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        controller.toolbar = toolbar.gameObject;
        Wire(ActionButton("FeedButton", toolbar, "Cho ăn", "action_feed", true), controller.Feed);
        Wire(ActionButton("CareButton", toolbar, "Chăm sóc", "action_care"), controller.ToggleCare);
        Wire(ActionButton("DecorateButton", toolbar, "Trang trí", "action_decorate"), controller.ToggleDecorate);
        Wire(ActionButton("PhotoButton", toolbar, "Ảnh", "action_photo"), controller.TakePhoto);

        var care = Panel("CarePanel", safe, "Chăm sóc hồ", "panel_care", 590, controller);
        controller.carePanel = care.gameObject;
        PanelText(care, "StatusHeading", "BETTA CỦA BẠN", 28, -140, Muted);
        controller.behaviourLabel = PanelText(care, "Behaviour", "Đang bơi thong thả", 37, -196, Ink);
        controller.hungerLabel = PanelText(care, "HungerLabel", "Độ no", 32, -266, Ink);
        var track = Picture("HungerTrack", care, "progress_track", true);
        At(track.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -320), new Vector2(760, 30));
        var fill = Picture("HungerFill", track.transform, "progress_fill");
        Stretch(fill.rectTransform);
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        controller.hungerFill = fill;
        PanelText(care, "WaterAvailability", "Nước & thiết bị: chưa có dữ liệu mô phỏng", 29, -399, Muted);
        var feed = TextButton("PanelFeedButton", care, "Cho Betta ăn", new Vector2(0, 48), new Vector2(760, 103));
        Wire(feed, controller.Feed);

        var decorate = Panel("DecoratePanel", safe, "Trang trí hồ", "panel_decorate", 510, controller);
        controller.decoratePanel = decorate.gameObject;
        string[] kinds = { "Cây", "Đá", "Lũa" };
        string[] icons = { "utility_leaf", "utility_rock", "utility_driftwood" };
        for (int i = 0; i < kinds.Length; i++)
        {
            var slot = Picture("Inventory" + i, decorate, "inventory_slot", true);
            At(slot.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2((i - 1) * 242, -225), new Vector2(200, 174));
            var icon = Picture("Icon", slot.transform, icons[i]);
            At(icon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 8), new Vector2(90, 90));
            var label = Text("Label", slot.transform, kinds[i], 29, Ink, true);
            At(label.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 10), new Vector2(150, 40));
        }
        PanelText(decorate, "Availability", "Bố cục hồ đang dùng nền theo mẫu đã duyệt.\nĐặt vật phẩm 3D sẽ được bổ sung sau.", 30, -390, Muted, 122);

        var settingsPanel = Panel("SettingsPanel", safe, "Cài đặt", "panel_care", 440, controller);
        controller.settingsPanel = settingsPanel.gameObject;
        var mute = TextButton("MuteButton", settingsPanel, "Âm thanh: Bật", new Vector2(0, 198), new Vector2(760, 100));
        controller.muteLabel = mute.GetComponentInChildren<TMP_Text>();
        Wire(mute, controller.ToggleMute);
        Wire(TextButton("ResetViewButton", settingsPanel, "Góc nhìn ban đầu", new Vector2(0, 74), new Vector2(760, 100)), controller.ResetView);

        var restore = IconButton("RestoreHudButton", safe, "utility_hide");
        At((RectTransform)restore.transform, Vector2.one, Vector2.one, new Vector2(-48, -57), new Vector2(86, 86));
        Wire(restore, controller.ToggleHud);
        controller.restoreButton = restore.gameObject;
        restore.gameObject.SetActive(false);
        var feedback = Text("Feedback", safe, "", 30, new Color32(255, 248, 228, 255), true);
        At(feedback.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 330), new Vector2(900, 110));
        var outline = feedback.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0, .06f, .04f, .9f);
        outline.effectDistance = new Vector2(2, -2);
        controller.feedback = feedback;
        feedback.gameObject.SetActive(false);
        controller.ClosePanels();
        FishShopScreenBuilder.Connect(controller);

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            Undo.RegisterCreatedObjectUndo(new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)), "Create input");
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Approved MainAquarium HUD built with the existing animated Betta.");
    }

    private static void ConfigurePresentation(BettaFishAI fish, OrbitTouchCamera orbit)
    {
        // Hide blockout geometry, keeping its bounds and feeding transforms intact.
        var aquarium = GameObject.Find("AquariumRoot");
        if (aquarium != null)
            foreach (var renderer in aquarium.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(renderer, "Preserve blockout renderer");
                renderer.enabled = false;
            }
        Undo.RecordObject(orbit, "Frame aquarium");
        orbit.fitHalfWidth = .85f;
        orbit.fitDepthOffset = .15f;
        orbit.pitch = 0;
        orbit.yaw = 0;
        orbit.maxYaw = 15;
        orbit.minPitch = -8;
        orbit.maxPitch = 8;
        orbit.minDistance = 2.2f;
        orbit.FitToScreen();
        Undo.RecordObject(orbit.transform, "Position camera");
        orbit.transform.position = orbit.target.position + Vector3.back * orbit.distance;
        orbit.transform.rotation = Quaternion.identity;
        Undo.RecordObject(fish.transform, "Present existing Betta");
        fish.transform.localScale = Vector3.one * 1.45f;
        fish.transform.rotation = Quaternion.Euler(0, 90, 0);
        Undo.RecordObject(fish.swimBounds.transform, "Frame the open swimming area");
        fish.swimBounds.transform.localScale = new Vector3(1.35f, 1.65f, .42f);
        var light = UnityEngine.Object.FindFirstObjectByType<Light>();
        if (light != null)
        {
            Undo.RecordObject(light, "Warm aquarium light");
            light.color = new Color(1, .96f, .83f);
            light.intensity = 1.5f;
        }
    }

    private static void BuildBackdrop(Camera camera)
    {
        var canvas = NewCanvas("AquariumBackdrop");
        var component = canvas.GetComponent<Canvas>();
        component.renderMode = RenderMode.ScreenSpaceCamera;
        component.worldCamera = camera;
        component.planeDistance = 15;
        UnityEngine.Object.DestroyImmediate(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>());
        var image = Picture("Environment", canvas, "AquariumEnvironment");
        Stretch(image.rectTransform);
        var fitter = image.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
        fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
    }

    private static void BakePortrait(BettaFishAI fish)
    {
        string directory = Root + "/Portraits";
        Directory.CreateDirectory(directory);
        string path = directory + "/BettaPortrait.png";
        if (File.Exists(path)) return;
        var clone = UnityEngine.Object.Instantiate(fish.gameObject);
        clone.name = "PortraitBakeOnly";
        clone.transform.position = new Vector3(1000, 0, 0);
        clone.transform.rotation = Quaternion.Euler(0, 90, 0);
        foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        foreach (var t in clone.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
        var renderers = clone.GetComponentsInChildren<SkinnedMeshRenderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var cameraObject = new GameObject("PortraitBakeCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.cullingMask = 1 << 31;
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x) * .59f;
        camera.transform.position = bounds.center + Vector3.back * 2;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .23f, .28f, 1);
        var target = RenderTexture.GetTemporary(256, 256, 24);
        var previous = RenderTexture.active;
        Texture2D texture = null;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            texture.Apply();
            // A round portrait avoids a square outside the supplied oval frame.
            var rounded = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    Color c = texture.GetPixel(x, y);
                    c.a = Mathf.Clamp01((1f - new Vector2((x - 127.5f) / 119, (y - 127.5f) / 119).magnitude) * 90);
                    rounded.SetPixel(x, y, c);
                }
            rounded.Apply();
            File.WriteAllBytes(path, rounded.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(rounded);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(clone);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static RectTransform NewCanvas(string name)
    {
        var root = Rect(name, null);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Create " + name);
        root.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return root;
    }

    private static RectTransform Panel(string name, Transform parent, string title, string surface, float height, MainAquariumScreen controller)
    {
        var panel = Picture(name, parent, surface, true);
        panel.raycastTarget = true;
        At(panel.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 326), new Vector2(960, height));
        PanelText(panel.transform, "Title", title, 47, -73, Ink);
        var close = IconButton("CloseButton", panel.transform, "utility_close");
        At((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-30, -27), new Vector2(72, 72));
        Wire(close, controller.ClosePanels);
        return panel.rectTransform;
    }

    private static TMP_Text PanelText(Transform parent, string name, string value, float size, float y, Color color, float height = 64)
    {
        var text = Text(name, parent, value, size, color, name == "Title");
        At(text.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(790, height));
        return text;
    }

    private static UnityEngine.UI.Button ActionButton(string name, Transform parent, string label, string icon, bool selected = false)
    {
        var background = Picture(name, parent, selected ? "button_selected" : "button_normal", true);
        background.raycastTarget = true;
        var element = background.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        element.flexibleWidth = 1;
        var button = background.gameObject.AddComponent<UnityEngine.UI.Button>();
        ButtonStates(button);
        var artwork = Picture("Icon", background.transform, icon);
        artwork.preserveAspect = true;
        Stretch(artwork.rectTransform);
        artwork.rectTransform.offsetMin = new Vector2(10, 51);
        artwork.rectTransform.offsetMax = new Vector2(-10, -8);
        var text = Text("Label", background.transform, label, 31, Ink, true);
        At(text.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 19), new Vector2(195, 49));
        return button;
    }

    private static UnityEngine.UI.Button TextButton(string name, Transform parent, string label, Vector2 position, Vector2 size)
    {
        var image = Picture(name, parent, "button_primary", true);
        image.raycastTarget = true;
        At(image.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), position, size);
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        var text = Text("Label", image.transform, label, 36, new Color32(255, 248, 228, 255), true);
        Stretch(text.rectTransform, 12);
        return button;
    }

    private static UnityEngine.UI.Button IconButton(string name, Transform parent, string icon)
    {
        var image = Picture(name, parent, "utility_badge");
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        var glyph = Picture("Icon", image.transform, icon);
        Stretch(glyph.rectTransform, 21);
        return button;
    }

    private static void ButtonStates(UnityEngine.UI.Button button)
    {
        button.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
        button.spriteState = new UnityEngine.UI.SpriteState
        {
            highlightedSprite = Sprite("button_selected"),
            pressedSprite = Sprite("button_pressed"),
            selectedSprite = Sprite("button_selected"),
            disabledSprite = Sprite("button_disabled")
        };
    }

    private static void Wire(UnityEngine.UI.Button button, UnityAction action) => UnityEventTools.AddPersistentListener(button.onClick, action);

    private static TMP_Text Text(string name, Transform parent, string value, float size, Color color, bool title = false)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = title ? titleFont : bodyFont;
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static UnityEngine.UI.Image Picture(string name, Transform parent, string sprite, bool sliced = false)
    {
        var image = Rect(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = Sprite(sprite);
        image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite Sprite(string name)
    {
        foreach (string folder in new[] { "Surfaces", "Icons", "Backgrounds", "Portraits" })
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{folder}/{name}.png");
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
}
