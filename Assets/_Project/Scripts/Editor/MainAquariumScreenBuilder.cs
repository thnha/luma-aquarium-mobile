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
        bool firstBuild = GameObject.Find("AquariumBackdrop") == null;
        if (firstBuild && Sprite("AquariumEnvironment") == null)
            throw new InvalidOperationException("Prepare the clean environment first.");

        // Retain both the on-disk and any unsaved authored scene before making changes.
        string backupDir = "Assets/_Project/Scenes/Backups";
        Directory.CreateDirectory(backupDir);
        string backupName = $"{backupDir}/MainAquarium-before-screen-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}";
        File.Copy(ScenePath, backupName + "-saved.unity");
        EditorSceneManager.SaveScene(scene, backupName + "-working.unity", true);
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

        if (firstBuild)
        {
            ConfigurePresentation(fish, orbit);
            BuildBackdrop(orbit.GetComponent<Camera>());
        }
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
        var dayNight = BuildSettings(controller, safe);
        BuildDecorate(controller, safe, decoration);

        var restore = Ui3D.RoundButton("RestoreHudButton", safe, "utility_hide");
        Ui3D.At((RectTransform)restore.transform, Vector2.one, Vector2.one, new Vector2(-44, -50), new Vector2(122, 122));
        Wire(restore, controller.ToggleHud);
        controller.restoreButton = restore.gameObject;
        restore.gameObject.SetActive(false);
        var feedback = Ui3D.Text("Feedback", safe, "", 34, Ui3D.Cream, true);
        Ui3D.At(feedback.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, SheetBottom + 10), new Vector2(940, 110));
        var outline = feedback.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(.05f, .12f, .06f, .95f);
        outline.effectDistance = new Vector2(2, -2);
        controller.feedback = feedback;
        feedback.gameObject.SetActive(false);

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
            lighting.label = dayNight.GetComponentInChildren<TMP_Text>();
            Wire(dayNight, lighting.Toggle);
            Saved(lighting);
        }
        else dayNight.interactable = false;
        controller.ClosePanels();
        FishShopScreenBuilder.Connect(controller);

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            Undo.RegisterCreatedObjectUndo(new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)), "Create input");
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Redesigned 2.5D MainAquarium HUD built. Rebuild the fish and decoration shops next.");
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
        Ui3D.At(portrait.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -14), new Vector2(124, 124));
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
        var hide = HeaderButton("HideButton", header, "utility_hide");
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
        Wire(Stone("FeedButton", dock, "action_feed", out _, out _), controller.Feed);
        Wire(Stone("CareButton", dock, "action_care", out controller.careGlow, out controller.careLift), controller.ToggleCare);
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
        Wire(change, controller.OpenShop);
        var feed = Ui3D.Button("PanelFeedButton", panel, "Cho Betta ăn", Ui3D.Style.Primary);
        Ui3D.Edges((RectTransform)feed.transform, new Vector2(.38f, 0), new Vector2(1, 0), new Vector2(8, 40), new Vector2(-44, 190));
        Wire(feed, controller.Feed);
    }

    // Returns the day/night button so the lighting component can drive its label.
    private static UnityEngine.UI.Button BuildSettings(MainAquariumScreen controller, RectTransform safe)
    {
        var panel = Sheet("SettingsPanel", safe, "Cài đặt", 900, controller);
        controller.settingsPanel = panel.gameObject;
        var mute = Row(panel, 0, "Âm thanh", "Bật hoặc tắt tiếng hồ", "MuteButton", "Đang bật");
        controller.muteLabel = mute.GetComponentInChildren<TMP_Text>();
        Wire(mute, controller.ToggleMute);
        var dayNight = Row(panel, 1, "Ánh sáng hồ", "Đổi ngày và đêm cho hồ", "DayNightButton", "Ban ngày");
        Wire(Row(panel, 2, "Góc nhìn", "Đưa camera về vị trí đầu", "ResetViewButton", "Đặt lại"), controller.ResetView);
        Wire(Row(panel, 3, "Ẩn giao diện", "Chỉ giữ nút hiện lại ở góc", "HideUiButton", "Ẩn"), controller.ToggleHud);
        var profile = RowFrame(panel, 4, "Hồ sơ khách", "Lưu trên thiết bị này", false);
        var soon = Ui3D.Badge("Soon", profile, "Đăng nhập · Sắp có", true, out _);
        Ui3D.At(soon.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(340, 66));
        return dayNight;
    }

    // Owned decorations by group. Cards follow the catalog order so their index is the offer index.
    private static void BuildDecorate(MainAquariumScreen controller, RectTransform safe, AquariumDecoration decoration)
    {
        var panel = Sheet("DecoratePanel", safe, "Trang trí", 1150, controller);
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
            inventory.tabLabels[i] = Ui3D.Text("Label", tab.transform, groups[i], 36, i == 0 ? Ui3D.Cream : Ui3D.Ink, true);
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
        layout.cellSize = new Vector2(221, 262);
        layout.spacing = new Vector2(14, 14);
        layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
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

    // Instruction chip above a tray of Xoay / Cất / Hủy and the primary Đặt.
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
        var chip = Ui3D.Image("InstructionChip", dock.transform, "ui3d_hint");
        chip.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        Ui3D.At(chip.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(940, 104));
        var hint = Ui3D.Text("Instruction", chip.transform, "Kéo trên cát • Xoay • Đặt", 34, Ui3D.Cream, true);
        Ui3D.Stretch(hint.rectTransform, 18);
        decoration.dock = dock.gameObject;
        decoration.instruction = hint;
        dock.gameObject.SetActive(false);
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

    private static void Stretch(RectTransform rect, float inset = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
