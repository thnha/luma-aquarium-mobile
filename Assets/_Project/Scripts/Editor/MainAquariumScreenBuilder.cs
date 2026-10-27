using System;
using System.IO;
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

// Builds the redesigned 2.5D HUD (docs/ui-redesign.md) over the existing aquarium.
// The first build also frames the starter tank; later builds replace only the HUD and
// rewire the placement dock, lighting switch and shops, keeping the installed 3D aquarium.
public static class MainAquariumScreenBuilder
{
    private const string Root = "Assets/_Project/UI";
    private const string ScenePath = "Assets/_Project/Scenes/MainAquarium.unity";
    private const float Side = 33, DockBottom = 66, DockHeight = 280, SheetBottom = 390;

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
        Ui3D.LoadFonts();
        // The aquarium has no painted backdrop behind it; only the camera's water colour shows there.
        bool firstBuild = UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>(FindObjectsInactive.Include) == null;

        // Retain both the on-disk and any unsaved authored scene before making changes.
        string backupName = "MainAquarium-before-screen-" + SceneBackups.Stamp();
        SceneBackups.CopyFile(ScenePath, backupName + "-saved");
        SceneBackups.SaveCopy(scene, backupName + "-working");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build redesigned MainAquarium screen");

        var old = GameObject.Find("Canvas");
        if (old != null)
        {
            Undo.RecordObject(old, "Preserve starter HUD");
            old.name = "LegacyAquariumHUD";
            old.SetActive(false);
        }
        var existing = GameObject.Find("MainAquariumHUD");
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        if (firstBuild) ConfigurePresentation(fish, orbit);
        BakePortrait(fish);
        var decoration = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>(FindObjectsInactive.Include);
        var lighting = UnityEngine.Object.FindFirstObjectByType<AquariumLighting>(FindObjectsInactive.Include);

        var canvas = Ui3D.Canvas("MainAquariumHUD", 0);
        Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Create MainAquariumHUD");
        var controller = canvas.gameObject.AddComponent<MainAquariumScreen>();
        controller.fish = fish;
        controller.feeding = feeding;
        controller.orbit = orbit;
        controller.decoration = decoration;
        controller.hud = canvas.gameObject.AddComponent<CanvasGroup>();
        var safe = Ui3D.Rect("SafeArea", canvas);
        Ui3D.Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();

        BuildHeader(controller, safe);
        BuildDock(controller, safe);
        BuildCare(controller, safe);
        BuildSettings(controller, safe);
        var lightingSheet = BuildLighting(controller, safe);
        BuildTanks(controller, safe);
        BuildDecorate(controller, safe, decoration);

        var restore = Ui3D.RoundButton("RestoreHudButton", safe, "utility_close");
        Ui3D.At((RectTransform)restore.transform, Vector2.one, Vector2.one, new Vector2(-44, -50), new Vector2(122, 122));
        Wire(restore, controller.ToggleHud);
        controller.restoreButton = restore.gameObject;
        restore.gameObject.SetActive(false);
        // Toasts sit on a dark sage chip that hugs the message, so they read over pale sand.
        var chip = Ui3D.Image("FeedbackChip", safe, "ui3d_hint");
        Ui3D.At(chip.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, SheetBottom + 30), new Vector2(600, 96));
        var chipLayout = chip.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        chipLayout.padding = new RectOffset(48, 48, 22, 24);
        chipLayout.childControlWidth = chipLayout.childControlHeight = true;
        var chipFit = chip.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        chipFit.horizontalFit = chipFit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var chipShadow = chip.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        chipShadow.effectColor = new Color(0, 0, 0, .35f);
        chipShadow.effectDistance = new Vector2(0, -6);
        var feedback = Ui3D.Text("Feedback", chip.transform, "", 40, Ui3D.Cream, true);
        feedback.textWrappingMode = TextWrappingModes.NoWrap;
        controller.feedback = feedback;
        controller.feedbackRoot = chip.gameObject;
        chip.gameObject.SetActive(false);

        if (decoration != null)
        {
            Undo.RecordObject(decoration, "Wire placement dock");
            decoration.screen = controller;
            BuildPlacementDock(decoration, safe);
            Saved(decoration);
        }
        if (lighting != null)
        {
            Undo.RecordObject(lighting, "Wire lighting switch");
            lighting.glowShader = Shader.Find("Lumaquarium/Aquarium Glow");
            Saved(lighting);
            lightingSheet.lighting = lighting;
        }
        controller.ClosePanels();
        FishShopScreenBuilder.Connect(controller);
        ShopScreenBuilder.Connect(controller);

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            Undo.RegisterCreatedObjectUndo(new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)), "Create input");
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Redesigned 2.5D MainAquarium HUD built. Run Lumaquarium/UI/Build Shop Screen next.");
    }

    // Fish chip (portrait, name, fullness) on the left; wallet, settings and hide on the right.
    private static void BuildHeader(MainAquariumScreen controller, RectTransform safe)
    {
        var header = Ui3D.Rect("Header", safe);
        Ui3D.TopStrip(header, 44, 44, 40, 330);
        controller.header = header.gameObject;

        var chip = Ui3D.Image("FishNameplate", header, "ui3d_chip");
        chip.raycastTarget = true;
        Ui3D.At(chip.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(500, 156));
        var chipButton = chip.gameObject.AddComponent<UnityEngine.UI.Button>();
        chipButton.targetGraphic = chip;
        Wire(chipButton, controller.ToggleCare);
        var name = Ui3D.Text("FishName", chip.transform, "Betta", 42, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.Edges(name.rectTransform, new Vector2(0, .48f), Vector2.one, new Vector2(166, 0), new Vector2(-30, -16));
        controller.fishNameLabel = name;
        var caption = Ui3D.Text("FullnessCaption", chip.transform, "No", 30, Ui3D.Muted, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.At(caption.rectTransform, Vector2.zero, Vector2.zero, new Vector2(166, 28), new Vector2(56, 44));
        var track = Ui3D.Image("FullnessTrack", chip.transform, "ui3d_track");
        track.pixelsPerUnitMultiplier = 1.6f; // keep the rounded ends on a short bar
        Ui3D.At(track.rectTransform, Vector2.zero, Vector2.zero, new Vector2(224, 34), new Vector2(170, 34));
        var fill = Filled("FullnessFill", track.transform, 5, 24);
        controller.hudFullnessFill = fill;
        var percent = Ui3D.Text("FullnessPercent", chip.transform, "100%", 30, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.At(percent.rectTransform, Vector2.zero, Vector2.zero, new Vector2(404, 28), new Vector2(80, 44));
        controller.hudFullnessLabel = percent;

        var portrait = Ui3D.Image("BettaPortrait", header, "BettaPortrait", false);
        Ui3D.At(portrait.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(-4, -16), new Vector2(124, 124));
        var ring = Ui3D.Image("PortraitRing", portrait.transform, "ui3d_ring", false);
        Ui3D.Stretch(ring.rectTransform, -10);
        controller.fishPortrait = portrait;

        var settings = HeaderButton("SettingsButton", header, "utility_settings");
        Ui3D.At((RectTransform)settings.transform, Vector2.one, Vector2.one, new Vector2(0, -2), new Vector2(150, 150));
        Wire(settings, controller.ToggleSettings);
        var wallet = Ui3D.Image("WalletButton", header, "ui3d_chip");
        wallet.raycastTarget = true;
        Ui3D.At(wallet.rectTransform, Vector2.one, Vector2.one, new Vector2(-170, -16), new Vector2(290, 122));
        var walletButton = wallet.gameObject.AddComponent<UnityEngine.UI.Button>();
        walletButton.targetGraphic = wallet;
        Wire(walletButton, controller.OpenShop);
        var coin = Ui3D.Icon("Coin", wallet.transform, "utility_coin");
        Ui3D.At(coin.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 3), new Vector2(84, 84));
        controller.walletLabel = Ui3D.Text("Balance", wallet.transform, "1.250", 46, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.Edges(controller.walletLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(106, 12), new Vector2(-22, -6));
        var hide = HeaderButton("HideButton", header, "utility_view");
        Ui3D.At((RectTransform)hide.transform, Vector2.one, Vector2.one, new Vector2(0, -172), new Vector2(150, 150));
        Wire(hide, controller.ToggleHud);
    }

    // Settings and hide: larger than the shared 44-point round button, with a dark edge on the
    // glyph so it reads over the bright water surface.
    private static UnityEngine.UI.Button HeaderButton(string name, RectTransform header, string icon)
    {
        var button = Ui3D.RoundButton(name, header, icon);
        var glyph = button.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>();
        Ui3D.Edges(glyph.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 30), new Vector2(-24, -20));
        var edge = glyph.gameObject.AddComponent<UnityEngine.UI.Outline>();
        edge.effectColor = new Color32(35, 64, 28, 230);
        edge.effectDistance = new Vector2(2, -2);
        var drop = glyph.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        drop.effectColor = new Color32(35, 64, 28, 200);
        drop.effectDistance = new Vector2(0, -5);
        return button;
    }

    // Icon-only dock: each action stands on its own mossy stone over the gravel, no tray or labels.
    private static void BuildDock(MainAquariumScreen controller, RectTransform safe)
    {
        var dock = Ui3D.Rect("BottomBar", safe);
        Ui3D.BottomStrip(dock, Side - 12, Side - 12, DockBottom - 20, DockHeight);
        var layout = dock.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        controller.toolbar = dock.gameObject;
        // Care opens from the fish chip in the header, so the dock keeps four evenly spaced stones.
        var feed = Stone("FeedButton", dock, "action_feed", out controller.feedGlow, out controller.feedLift);
        Wire(feed, controller.Feed);
        controller.feedIcon = (UnityEngine.UI.Image)feed.targetGraphic;
        controller.feedIconOpen = Ui3D.Require("action_feed_open");
        Wire(Stone("DecorateButton", dock, "action_decorate", out controller.decorateGlow, out controller.decorateLift), controller.ToggleDecorate);
        Wire(Stone("ShopButton", dock, "action_shop", out _, out _), controller.OpenShop);
        Wire(Stone("PhotoButton", dock, "action_photo", out _, out _), controller.TakePhoto);
    }

    private static void BuildCare(MainAquariumScreen controller, RectTransform safe)
    {
        var panel = Sheet("CarePanel", safe, "Chăm sóc", 840, controller);
        controller.carePanel = panel.gameObject;
        var portrait = Ui3D.Image("CarePortrait", panel, "BettaPortrait", false);
        Ui3D.At(portrait.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -150), new Vector2(160, 160));
        Ui3D.Stretch(Ui3D.Image("PortraitRing", portrait.transform, "ui3d_ring", false).rectTransform, -10);
        controller.carePortrait = portrait;
        controller.careFishName = Ui3D.Text("FishName", panel, "Betta", 46, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.At(controller.careFishName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, -160), new Vector2(640, 64));
        controller.behaviourLabel = Ui3D.Text("Behaviour", panel, "Đang bơi thong thả", 36, Ui3D.Forest, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.At(controller.behaviourLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, -232), new Vector2(640, 56));

        var card = Ui3D.Image("FullnessCard", panel, "ui3d_tile");
        Ui3D.TopStrip(card.rectTransform, 44, 44, 340, 180);
        controller.hungerLabel = Ui3D.Text("HungerLabel", card.transform, "Độ no", 40, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.Edges(controller.hungerLabel.rectTransform, new Vector2(0, .5f), Vector2.one, new Vector2(36, 0), new Vector2(-220, -12));
        controller.hungerPercent = Ui3D.Text("HungerPercent", card.transform, "100%", 40, Ui3D.Ink, true, TextAlignmentOptions.MidlineRight);
        Ui3D.Edges(controller.hungerPercent.rectTransform, new Vector2(.6f, .5f), Vector2.one, Vector2.zero, new Vector2(-36, -12));
        var track = Ui3D.Image("HungerTrack", card.transform, "ui3d_track");
        Ui3D.Edges(track.rectTransform, Vector2.zero, new Vector2(1, .5f), new Vector2(36, 34), new Vector2(-36, -8));
        controller.hungerFill = Filled("HungerFill", track.transform, 6, 36);

        var water = Ui3D.Text("WaterAvailability", panel, "Nước & thiết bị", 36, Ui3D.Muted, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(water.rectTransform, 56, 300, 550, 70);
        var soon = Ui3D.Badge("Soon", panel, "Sắp có", true, out _);
        Ui3D.At(soon.rectTransform, Vector2.one, Vector2.one, new Vector2(-56, -552), new Vector2(180, 66));
        var change = Ui3D.Button("ChangeFishButton", panel, "Đổi cá", Ui3D.Style.Secondary);
        Ui3D.Edges((RectTransform)change.transform, Vector2.zero, new Vector2(.38f, 0), new Vector2(44, 40), new Vector2(-8, 190));
        Wire(change, controller.OpenFishShop);
        var feed = Ui3D.Button("PanelFeedButton", panel, "Cho Betta ăn", Ui3D.Style.Primary);
        Ui3D.Edges((RectTransform)feed.transform, new Vector2(.38f, 0), new Vector2(1, 0), new Vector2(8, 40), new Vector2(-44, 190));
        Wire(feed, controller.Feed);
    }

    // No day/night or hide-HUD rows: the Đèn hồ sheet and the eye button already cover them.
    private static void BuildSettings(MainAquariumScreen controller, RectTransform safe)
    {
        var panel = Sheet("SettingsPanel", safe, "Cài đặt", SettingsHeight, controller);
        controller.settingsPanel = panel.gameObject;
        var mute = Row(panel, 0, "Âm thanh", "Bật hoặc tắt tiếng hồ", "MuteButton", "Đang bật");
        controller.muteLabel = mute.GetComponentInChildren<TMP_Text>();
        Wire(mute, controller.ToggleMute);
        Wire(Row(panel, 4, "Đèn hồ", "Đèn trên, đèn nền và màu đèn", "LightingButton", "Tùy chỉnh"), controller.ToggleLighting);
        AccountRow(controller, panel);
        TankRow(controller, panel);
        LanguageRow(controller, panel);
        GroupSettings(panel);
    }

    // Rows keep their Row0–7 names (the single-row builders find them by it) and are only moved under headings.
    private static readonly (string heading, int[] rows)[] SettingsGroups =
    {
        ("Hồ của bạn", new[] { 6, 4 }),
        ("Chung", new[] { 0, 7 }),
        ("Tài khoản", new[] { 5 }),
    };
    private const float SettingsRowHeight = 124, SettingsHeadingHeight = 84;

    // A forest chip that hugs the (localized) heading.
    private static RectTransform SettingsHeading(RectTransform settings, string name, string heading)
    {
        var group = Ui3D.Rect(name, settings);
        var chip = Ui3D.Badge("Chip", group, heading, false, out var label);
        chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = chip.rectTransform.pivot = new Vector2(0, .5f);
        chip.rectTransform.anchoredPosition = Vector2.zero;
        var layout = chip.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 8, 10);
        layout.childControlWidth = layout.childControlHeight = true;
        var fit = chip.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        label.enableAutoSizing = false;
        label.fontSize = 32;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return group;
    }
    private const float SettingsHeight = 150 + 5 * SettingsRowHeight + 3 * SettingsHeadingHeight + 40;

    private static void GroupSettings(RectTransform settings)
    {
        Undo.RecordObject(settings, "Fit settings groups");
        settings.sizeDelta = new Vector2(settings.sizeDelta.x, SettingsHeight);
        // Rows dropped from the sheet (day/night, view reset, hide HUD) are removed from older HUDs.
        foreach (int index in new[] { 1, 2, 3 })
        {
            var dropped = settings.Find("Row" + index);
            if (dropped != null) Undo.DestroyObjectImmediate(dropped.gameObject);
        }
        float top = 140;
        for (int g = 0; g < SettingsGroups.Length; g++)
        {
            var (heading, rows) = SettingsGroups[g];
            var existing = settings.Find("Group" + g);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            var group = SettingsHeading(settings, "Group" + g, heading);
            Undo.RegisterCreatedObjectUndo(group.gameObject, "Create settings heading");
            Ui3D.TopStrip(group, 56, 56, top + 24, SettingsHeadingHeight - 36);
            top += SettingsHeadingHeight;
            for (int r = 0; r < rows.Length; r++)
            {
                var row = (RectTransform)settings.Find("Row" + rows[r]);
                if (row == null) continue;
                Undo.RecordObject(row, "Move settings row");
                Ui3D.TopStrip(row, 56, 56, top, SettingsRowHeight);
                top += SettingsRowHeight;
                // Lines only between rows of a group; the next heading already separates groups.
                var divider = row.Find("Divider");
                if (divider == null) continue;
                Undo.RecordObject(divider.gameObject, "Toggle settings divider");
                divider.gameObject.SetActive(r < rows.Length - 1);
            }
        }
    }

    // Groups the rows of an already built Settings sheet without rebuilding the HUD.
    [MenuItem("Lumaquarium/UI/Group Settings Rows")]
    public static void BuildSettingsGroups()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before grouping the settings rows.");
        var scene = SceneManager.GetActiveScene();
        var controller = UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>(FindObjectsInactive.Include);
        if (scene.path != ScenePath || controller == null || controller.settingsPanel == null)
            throw new InvalidOperationException("Open MainAquarium with its built HUD first.");
        Ui3D.LoadFonts();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        GroupSettings((RectTransform)controller.settingsPanel.transform);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Lumaquarium] Settings rows grouped.");
    }

    // The label is replaced at runtime by the language in use (MainAquariumScreen.ToggleLanguage).
    private static void LanguageRow(MainAquariumScreen controller, RectTransform settings)
    {
        var language = Row(settings, 7, "Ngôn ngữ", "Tiếng Việt hoặc English", "LanguageButton", "Tiếng Việt");
        controller.languageLabel = language.GetComponentInChildren<TMP_Text>();
        Wire(language, controller.ToggleLanguage);
    }

    // Adds the language row to a HUD built before it existed, or rebuilds it.
    [MenuItem("Lumaquarium/UI/Build Language Row")]
    public static void BuildLanguageRow()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building the language row.");
        var scene = SceneManager.GetActiveScene();
        var controller = UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>(FindObjectsInactive.Include);
        if (scene.path != ScenePath || controller == null || controller.settingsPanel == null)
            throw new InvalidOperationException("Open MainAquarium with its built HUD first.");
        Ui3D.LoadFonts();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var settings = (RectTransform)controller.settingsPanel.transform;
        Undo.RecordObject(settings, "Fit language row");
        settings.sizeDelta = new Vector2(settings.sizeDelta.x, SettingsHeight);
        var oldRow = settings.Find("Row7");
        if (oldRow != null) Undo.DestroyObjectImmediate(oldRow.gameObject);
        Undo.RecordObject(controller, "Wire language row");
        LanguageRow(controller, settings);
        Undo.RegisterCreatedObjectUndo(settings.Find("Row7").gameObject, "Create language row");
        GroupSettings(settings);
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Lumaquarium] Language row built.");
    }

    // Texts are swapped at runtime when the player is signed in (MainAquariumScreen.UpdateAccountRow).
    private static void AccountRow(MainAquariumScreen controller, RectTransform settings)
    {
        var account = Row(settings, 5, "Hồ sơ khách", "Lưu trên thiết bị này", "AccountButton", "Đăng nhập");
        var row = account.transform.parent;
        controller.accountTitle = row.Find("Title").GetComponent<TMP_Text>();
        controller.accountDetail = row.Find("Detail").GetComponent<TMP_Text>();
        controller.accountLabel = account.GetComponentInChildren<TMP_Text>();
        Wire(account, controller.AccountAction);
    }

    // Replaces the "Đăng nhập · Sắp có" profile row of an existing HUD with the sign-in / sign-out row.
    [MenuItem("Lumaquarium/UI/Build Account Row")]
    public static void BuildAccountRow()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building the account row.");
        var scene = SceneManager.GetActiveScene();
        var controller = UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>(FindObjectsInactive.Include);
        if (scene.path != ScenePath || controller == null || controller.settingsPanel == null)
            throw new InvalidOperationException("Open MainAquarium with its built HUD first.");
        Ui3D.LoadFonts();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var settings = (RectTransform)controller.settingsPanel.transform;
        var oldRow = settings.Find("Row5");
        int sibling = oldRow != null ? oldRow.GetSiblingIndex() : -1;
        if (oldRow != null) Undo.DestroyObjectImmediate(oldRow.gameObject);
        Undo.RecordObject(controller, "Wire account row");
        AccountRow(controller, settings);
        var row = settings.Find("Row5");
        if (sibling >= 0) row.SetSiblingIndex(sibling);
        Undo.RegisterCreatedObjectUndo(row.gameObject, "Create account row");
        GroupSettings(settings);
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Lumaquarium] Account row built.");
    }

    private static void TankRow(MainAquariumScreen controller, RectTransform settings) =>
        Wire(Row(settings, 6, "Hồ cá", "Mua hoặc đổi kích thước hồ", "TanksButton", "Đổi hồ"), controller.ToggleTanks);

    // Adds the tank sheet and its Settings row to a HUD built before tanks existed, or rebuilds them.
    [MenuItem("Lumaquarium/UI/Build Tank Sheet")]
    public static void BuildTankSheet()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building the tank sheet.");
        var scene = SceneManager.GetActiveScene();
        var controller = UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>(FindObjectsInactive.Include);
        if (scene.path != ScenePath || controller == null || controller.settingsPanel == null)
            throw new InvalidOperationException("Open MainAquarium with its built HUD first.");
        Ui3D.LoadFonts();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var safe = (RectTransform)controller.settingsPanel.transform.parent;
        var old = safe.Find("TankPanel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var settings = (RectTransform)controller.settingsPanel.transform;
        Undo.RecordObject(settings, "Fit tank row");
        settings.sizeDelta = new Vector2(settings.sizeDelta.x, SettingsHeight);
        var oldRow = settings.Find("Row6");
        if (oldRow != null) Undo.DestroyObjectImmediate(oldRow.gameObject);
        TankRow(controller, settings);
        Undo.RegisterCreatedObjectUndo(settings.Find("Row6").gameObject, "Create tank row");
        GroupSettings(settings);
        Undo.RecordObject(controller, "Wire tank sheet");
        BuildTanks(controller, safe);
        Undo.RegisterCreatedObjectUndo(controller.tankPanel, "Create tank sheet");
        controller.tankPanel.SetActive(false);
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Lumaquarium] Tank sheet built.");
    }

    // One row per tank size (AquariumTank.Specs) with its buy / use button, and a line of feedback.
    private static void BuildTanks(MainAquariumScreen controller, RectTransform safe)
    {
        var specs = AquariumTank.Specs;
        var panel = Sheet("TankPanel", safe, "Hồ cá", 220 + specs.Length * 140 + 120, controller);
        controller.tankPanel = panel.gameObject;
        var sheet = panel.gameObject.AddComponent<TankShopPanel>();
        sheet.aquarium = controller;
        sheet.buttons = new UnityEngine.UI.Button[specs.Length];
        for (int i = 0; i < specs.Length; i++)
            sheet.buttons[i] = Row(panel, i, specs[i].displayName, specs[i].description, "Tank" + i, "Dùng");
        sheet.feedback = Ui3D.Text("Feedback", panel, "", 32, Ui3D.Forest, false, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(sheet.feedback.rectTransform, 56, 56, 170 + specs.Length * 140, 70);
    }

    // Light from above (daylight or the lamp), the lamp's colour and the backlight behind the tank.
    private static LightingPanel BuildLighting(MainAquariumScreen controller, RectTransform safe)
    {
        var panel = Sheet("LightingPanel", safe, "Ánh sáng", 860, controller);
        controller.lightingPanel = panel.gameObject;
        var sheet = panel.gameObject.AddComponent<LightingPanel>();
        sheet.topButtons = Tabs(panel, 0, "Chiếu từ trên xuống", "Nắng tự nhiên hoặc đèn treo trên hồ", "Top", "Ban ngày", "Đèn");
        sheet.colorButtons = Tabs(panel, 1, "Màu đèn trên", "Chọn màu là bật đèn", "Color", "Trắng", "Xanh", "Đỏ");
        sheet.backButtons = Tabs(panel, 2, "Đèn nền phía sau", "Viền sáng cây, đá và cá từ phía sau", "Back", "Tắt", "Trắng", "Xanh", "Đỏ");
        sheet.labelOn = Ui3D.Cream;
        sheet.labelOff = Ui3D.Hex("4A503F");
        return sheet;
    }

    // A titled row with a tray of tabs under it, as in the decorate sheet; LightingPanel lights the selected tab.
    private static UnityEngine.UI.Button[] Tabs(RectTransform panel, int index, string title, string subtitle, string name, params string[] labels)
    {
        var row = Ui3D.Rect(name + "Row", panel);
        Ui3D.TopStrip(row, 56, 56, 150 + index * 220, 220);
        var heading = Ui3D.Text("Title", row, title, 40, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(heading.rectTransform, 0, 0, 0, 52);
        var detail = Ui3D.Text("Detail", row, subtitle, 30, Ui3D.Muted, false, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(detail.rectTransform, 0, 0, 52, 42);
        var tray = Ui3D.Image("Tabs", row, "ui3d_tab_tray_light");
        Ui3D.TopStrip(tray.rectTransform, -12, -12, 98, 112);
        var layout = tray.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 16);
        layout.spacing = 6;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        var buttons = new UnityEngine.UI.Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            var tab = Ui3D.Image(name + i, tray.transform, "ui3d_button_primary");
            tab.color = Color.clear;
            tab.raycastTarget = true;
            var button = tab.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = tab;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            var label = Ui3D.Text("Label", tab.transform, labels[i], 40, Ui3D.Hex("4A503F"), true);
            Ui3D.Edges(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 12), new Vector2(-6, -4));
            buttons[i] = button;
        }
        return buttons;
    }

    // The Trang trí sheet runs from above the bottom bar to just under the header, so taller phones show more rows.
    private const float DecorateTop = 390;

    private static void FitDecorateGrid(UnityEngine.UI.GridLayoutGroup layout)
    {
        layout.cellSize = new Vector2(176, 224);
        layout.spacing = new Vector2(12, 12);
        layout.constraintCount = 5;
    }

    // Stretches a Trang trí sheet built at the old fixed height and gives it the five-column grid.
    [MenuItem("Lumaquarium/UI/Fit Decorate Sheet")]
    public static void FitDecorateSheet()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before fitting the decorate sheet.");
        var scene = SceneManager.GetActiveScene();
        var controller = UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>(FindObjectsInactive.Include);
        if (scene.path != ScenePath || controller == null || controller.decoratePanel == null)
            throw new InvalidOperationException("Open MainAquarium with its built HUD first.");
        var panel = (RectTransform)controller.decoratePanel.transform;
        var layout = panel.GetComponentInChildren<UnityEngine.UI.GridLayoutGroup>(true);
        Undo.RecordObjects(new UnityEngine.Object[] { panel, layout }, "Fit decorate sheet");
        Ui3D.Edges(panel, Vector2.zero, Vector2.one, new Vector2(Side, SheetBottom), new Vector2(-Side, -DecorateTop));
        FitDecorateGrid(layout);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Lumaquarium] Decorate sheet fitted.");
    }

    // Owned decorations by group. Cards follow the catalog order so their index is the offer index.
    public static void RefreshDecoratePanel(MainAquariumScreen controller)
    {
        var old=controller.decoratePanel;
        var parent=(RectTransform)old.transform.parent;
        int index=old.transform.GetSiblingIndex();
        UnityEngine.Object.DestroyImmediate(old);
        BuildDecorate(controller,parent,controller.decoration);
        controller.decoratePanel.transform.SetSiblingIndex(index);
        controller.decoratePanel.SetActive(false);
        EditorUtility.SetDirty(controller);
    }

    private static void BuildDecorate(MainAquariumScreen controller, RectTransform safe, AquariumDecoration decoration)
    {
        var panel = Sheet("DecoratePanel", safe, "Trang trí", 1150, controller);
        Ui3D.Edges(panel, Vector2.zero, Vector2.one, new Vector2(Side, SheetBottom), new Vector2(-Side, -DecorateTop));
        controller.decoratePanel = panel.gameObject;
        var inventory = panel.gameObject.AddComponent<DecorInventoryPanel>();
        inventory.aquarium = controller;
        inventory.summary = Ui3D.Text("Summary", panel, "Kho của bạn", 32, Ui3D.Muted, false, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(inventory.summary.rectTransform, 56, 180, 122, 46);

        var tray = Ui3D.Image("Tabs", panel, "ui3d_tab_tray_light");
        Ui3D.TopStrip(tray.rectTransform, 44, 44, 190, 120);
        var row = tray.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        row.padding = new RectOffset(10, 10, 10, 16);
        row.spacing = 6;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = true;
        string[] groups = { "Cây", "Lũa", "Đá", "San hô" }; // AquariumDecorCategory order: Plants, Wood, Rocks, Coral
        inventory.tabButtons = new UnityEngine.UI.Button[groups.Length];
        inventory.tabSurfaces = new UnityEngine.UI.Image[groups.Length];
        inventory.tabLabels = new TMP_Text[groups.Length];
        for (int i = 0; i < groups.Length; i++)
        {
            var tab = Ui3D.Image("Tab" + i, tray.transform, "ui3d_button_primary");
            tab.color = i == 0 ? Color.white : Color.clear;
            tab.raycastTarget = true;
            var button = tab.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = tab;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            inventory.tabButtons[i] = button;
            inventory.tabSurfaces[i] = tab;
            inventory.tabLabels[i] = Ui3D.Text("Label", tab.transform, groups[i], 40, i == 0 ? Ui3D.Cream : Ui3D.Hex("4A503F"), true);
            Ui3D.Edges(inventory.tabLabels[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 12), new Vector2(-6, -4));
        }
        inventory.tabLabelOn = Ui3D.Cream;
        inventory.tabLabelOff = Ui3D.Hex("4A503F");

        var items = Ui3D.Rect("Items", panel);
        Ui3D.Edges(items, Vector2.zero, Vector2.one, new Vector2(44, 236), new Vector2(-44, -330));
        items.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var grid = Ui3D.Rect("Content", items);
        Ui3D.Edges(grid, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
        grid.pivot = new Vector2(.5f, 1);
        var layout = grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        FitDecorateGrid(layout);
        grid.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var scroll = items.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.content = grid;
        scroll.viewport = items;
        scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

        var offers = decoration != null && decoration.catalog != null ? decoration.catalog.offers : new AquariumDecorOffer[0];
        inventory.cards = new GameObject[offers.Length];
        inventory.cardButtons = new UnityEngine.UI.Button[offers.Length];
        inventory.cardSurfaces = new UnityEngine.UI.Image[offers.Length];
        inventory.countLabels = new TMP_Text[offers.Length];
        inventory.countBadges = new UnityEngine.UI.Image[offers.Length];
        inventory.cardNormal = Ui3D.Require("ui3d_tile");
        inventory.cardEmpty = Ui3D.Require("ui3d_tile_empty");
        inventory.badgeStock = Ui3D.Require("ui3d_badge");
        inventory.badgeEmpty = Ui3D.Require("ui3d_badge_light");
        for (int i = 0; i < offers.Length; i++)
        {
            var offer = offers[i];
            var card = Ui3D.Image("Item_" + offer.id, grid, "ui3d_tile");
            card.raycastTarget = true;
            var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = card;
            button.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            button.spriteState = new UnityEngine.UI.SpriteState { pressedSprite = Ui3D.Require("ui3d_tile_pressed") };
            var image = Ui3D.Image("Portrait", card.transform, null, false);
            image.sprite = offer.portrait;
            image.preserveAspect = true;
            Ui3D.Edges(image.rectTransform, new Vector2(0, .3f), Vector2.one, new Vector2(14, 0), new Vector2(-14, -14));
            var label = Ui3D.Text("Name", card.transform, offer.displayName, 28, Ui3D.Ink, true);
            Ui3D.Edges(label.rectTransform, Vector2.zero, new Vector2(1, .32f), new Vector2(6, 14), new Vector2(-6, 0));
            var badge = Ui3D.Badge("Count", card.transform, "x1", false, out var count);
            Ui3D.At(badge.rectTransform, Vector2.one, Vector2.one, new Vector2(-10, -10), new Vector2(86, 56));
            inventory.cards[i] = card.gameObject;
            inventory.cardButtons[i] = button;
            inventory.cardSurfaces[i] = card;
            inventory.countLabels[i] = count;
            inventory.countBadges[i] = badge;
        }

        inventory.arrangeButton = Ui3D.Button("ArrangeButton", panel, "Sắp xếp lại", Ui3D.Style.Secondary);
        Ui3D.Edges((RectTransform)inventory.arrangeButton.transform, Vector2.zero, new Vector2(.5f, 0), new Vector2(44, 44), new Vector2(-8, 194));
        inventory.shopButton = Ui3D.Button("ShopButton", panel, "Mua thêm", Ui3D.Style.Primary);
        Ui3D.Edges((RectTransform)inventory.shopButton.transform, new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(8, 44), new Vector2(-44, 194));
    }

    // Instruction chip above a tray of Xoay / Cất / Hủy and the primary Đặt, or Thêm / Hoàn tác / Xong while nothing is selected.
    private static void BuildPlacementDock(AquariumDecoration decoration, RectTransform safe)
    {
        var dock = Ui3D.Image("PlacementDock", safe, "ui3d_dock");
        dock.raycastTarget = true;
        Ui3D.BottomStrip(dock.rectTransform, Side, Side, DockBottom, DockHeight);
        var layout = dock.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 40, 52);
        layout.spacing = 16;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        string[] labels = { "Xoay", "Cất", "Hủy", "Đặt" };
        UnityAction[] actions = { decoration.Rotate, decoration.Delete, decoration.Cancel, decoration.Confirm };
        for (int i = 0; i < labels.Length; i++)
        {
            var button = Ui3D.Button("PlacementAction" + i, dock.transform, labels[i], i == 3 ? Ui3D.Style.Primary : Ui3D.Style.Secondary, 44);
            button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = i == 3 ? 1.6f : 1;
            Wire(button, actions[i]);
        }
        // Nghiêng (driftwood) and Tỉa / Mọc (rooted plants) sit after Xoay and show only for those pieces.
        decoration.tiltButton = ContextButton(dock.transform, "PlacementTilt", "Nghiêng", decoration.Tilt, 1);
        decoration.trimButton = ContextButton(dock.transform, "PlacementTrim", "Tỉa", decoration.Trim, 2);
        decoration.growButton = ContextButton(dock.transform, "PlacementGrow", "Mọc", decoration.Grow, 3);
        // Thêm / Hoàn tác / Xong take the dock while nothing is selected.
        decoration.addButton = ContextButton(dock.transform, "PlacementAdd", "Thêm", decoration.Add, 7);
        decoration.undoButton = ContextButton(dock.transform, "PlacementUndo", "Hoàn tác", decoration.UndoLast, 8);
        var done = Ui3D.Button("PlacementDone", dock.transform, "Xong", Ui3D.Style.Primary, 44);
        done.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1.6f;
        Wire(done, decoration.Finish);
        done.gameObject.SetActive(false);
        decoration.doneButton = done.gameObject;
        var chip = Ui3D.Image("InstructionChip", dock.transform, "ui3d_hint");
        chip.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        Ui3D.At(chip.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(940, 104));
        var hint = Ui3D.Text("Instruction", chip.transform, "Kéo trên cát • Xoay • Đặt", 34, Ui3D.Cream, true);
        Ui3D.Stretch(hint.rectTransform, 18);
        decoration.dock = dock.gameObject;
        decoration.instruction = hint;
        dock.gameObject.SetActive(false);
    }

    private static GameObject ContextButton(Transform dock, string name, string label, UnityAction action, int slot)
    {
        var button = Ui3D.Button(name, dock, label, Ui3D.Style.Secondary, 44);
        button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
        button.transform.SetSiblingIndex(slot);
        Wire(button, action);
        button.gameObject.SetActive(false);
        return button.gameObject;
    }

    private static UnityEngine.UI.Button Stone(string name, Transform parent, string icon, out GameObject glow, out RectTransform lift)
    {
        var hit = Ui3D.Solid(name, parent, Color.clear);
        hit.raycastTarget = true;
        var halo = Ui3D.Image("Glow", hit.transform, "ui3d_glow", false);
        Ui3D.At(halo.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, -14), new Vector2(250, 96));
        halo.gameObject.SetActive(false);
        glow = halo.gameObject;
        var stone = Ui3D.Image("Stone", hit.transform, "ui3d_rock", false);
        stone.preserveAspect = true;
        Ui3D.Edges(stone.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(-8, 0), new Vector2(8, 64));
        lift = Ui3D.Rect("Lift", hit.transform);
        Ui3D.Stretch(lift);
        var artwork = Ui3D.Icon("Icon", lift, icon);
        Ui3D.At(artwork.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 30), new Vector2(178, 178));
        var drop = artwork.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        drop.effectColor = new Color(0, 0, 0, .3f);
        drop.effectDistance = new Vector2(0, -8);
        var button = hit.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = artwork;
        var colors = button.colors;
        colors.pressedColor = new Color(.78f, .78f, .78f);
        button.colors = colors;
        return button;
    }

    private static RectTransform Sheet(string name, RectTransform safe, string title, float height, MainAquariumScreen controller)
    {
        var panel = Ui3D.Panel(name, safe);
        Ui3D.BottomStrip(panel.rectTransform, Side, Side, SheetBottom, height);
        var heading = Ui3D.Text("Title", panel.transform, title, 58, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(heading.rectTransform, 56, 180, 40, 90);
        var close = Ui3D.RoundButton("CloseButton", panel.transform, "utility_close", true);
        Ui3D.At((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-30, -28), new Vector2(116, 116));
        Wire(close, controller.ClosePanels);
        return panel.rectTransform;
    }

    private static RectTransform RowFrame(RectTransform panel, int index, string title, string subtitle, bool divider = true)
    {
        var row = Ui3D.Rect("Row" + index, panel);
        Ui3D.TopStrip(row, 56, 56, 150 + index * 140, 140);
        var heading = Ui3D.Text("Title", row, title, 40, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.Edges(heading.rectTransform, new Vector2(0, .5f), Vector2.one, Vector2.zero, new Vector2(-340, -12));
        var detail = Ui3D.Text("Detail", row, subtitle, 30, Ui3D.Muted, false, TextAlignmentOptions.MidlineLeft);
        Ui3D.Edges(detail.rectTransform, Vector2.zero, new Vector2(1, .5f), new Vector2(0, 14), new Vector2(-340, 0));
        if (divider) Ui3D.Edges(Ui3D.Solid("Divider", row, Ui3D.Divider).rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
        return row;
    }

    private static UnityEngine.UI.Button Row(RectTransform panel, int index, string title, string subtitle, string button, string label)
    {
        var row = RowFrame(panel, index, title, subtitle);
        var control = Ui3D.Button(button, row, label, Ui3D.Style.Secondary, 38);
        Ui3D.At((RectTransform)control.transform, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(300, 116));
        return control;
    }

    // A sliced pill whose width follows the value (MainAquariumScreen.SetFill). A Filled image
    // would stretch the whole rounded sprite and pinch both ends on a short bar.
    private static UnityEngine.UI.Image Filled(string name, Transform track, float inset, float height)
    {
        var area = Ui3D.Rect(name + "Area", track);
        Ui3D.Stretch(area, inset);
        var fill = Ui3D.Image(name, area, "ui3d_fill");
        Ui3D.Stretch(fill.rectTransform);
        fill.pixelsPerUnitMultiplier = 40f / height; // 20 px caps = half the bar height
        return fill;
    }

    private static void Wire(UnityEngine.UI.Button button, UnityAction action) => UnityEventTools.AddPersistentListener(button.onClick, action);

    private static void Saved(Component component)
    {
        EditorUtility.SetDirty(component);
        if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
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
        orbit.minPitch = 0;
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
        // The tank fish shows its model only once BettaAppearance applies the variant.
        var appearance = clone.GetComponent<BettaAppearance>();
        if (appearance != null) appearance.Apply(appearance.variant);
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
}
