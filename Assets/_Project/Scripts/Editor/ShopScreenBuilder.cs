using System;
using System.IO;
using System.Linq;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The one shop (layout 3: search, sliding tabs, price sort, 4-column grid, details bar). Installs it in
// MainAquarium and removes the fish and decoration shop canvases; their prefabs and scripts stay in the project.
public static class ShopScreenBuilder
{
    private const string PrefabPath = Ui3D.Root + "/Prefabs/Shop.prefab";
    private const string FishPrefabPath = Ui3D.Root + "/Prefabs/FishShop.prefab";
    private const string ScenePath = "Assets/_Project/Scenes/MainAquarium.unity";
    // ShopTab order.
    public static readonly string[] TabNames = { "Tất cả", "Cá", "Lũa", "Đá", "San hô", "Cây", "Rong rêu", "Bèo", "Thành hồ", "Cát nền" };

    [MenuItem("Lumaquarium/UI/Build Shop Screen")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open MainAquarium first.");
        var aquarium = UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>();
        if (aquarium == null || aquarium.fish == null || aquarium.decoration == null) throw new InvalidOperationException("The aquarium HUD, fish and decoration are required.");
        var decoration = aquarium.decoration;
        if (decoration.catalog == null || decoration.surfaces == null)
            throw new InvalidOperationException("Run Lumaquarium/UI/Build Decoration Shop and Lumaquarium/Build Surface Catalog first.");
        var fishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FishPrefabPath);
        var fishShop = fishPrefab != null ? fishPrefab.GetComponent<FishShopScreen>() : null;
        if (fishShop == null || fishShop.offers == null || fishShop.offers.Length == 0)
            throw new InvalidOperationException("Run Lumaquarium/UI/Build Fish Shop Screen first (it bakes the fish portraits).");
        Ui3D.LoadFonts();
        string stamp = SceneBackups.Stamp();
        SceneBackups.CopyFile(scene.path, "MainAquarium-before-shop-" + stamp + "-saved");
        SceneBackups.SaveCopy(scene, "MainAquarium-before-shop-" + stamp + "-working");

        var built = Create(fishShop.offers, decoration);
        PrefabUtility.SaveAsPrefabAsset(built.gameObject, PrefabPath); // keeps the GUID, so a scene instance follows
        UnityEngine.Object.DestroyImmediate(built.gameObject);
        if (UnityEngine.Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include) == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            Undo.RegisterCreatedObjectUndo(instance, "Add shop");
        }
        Connect(aquarium);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Shop installed: " + TabNames.Length + " tabs; the old fish and decoration shops were removed from the scene.");
    }

    // Called by the HUD builder too, so rebuilding the HUD reconnects the shop.
    public static void Connect(MainAquariumScreen aquarium)
    {
        var shop = UnityEngine.Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
        if (shop == null) return;
        Undo.RecordObject(shop, "Connect shop");
        shop.aquarium = aquarium;
        shop.fish = aquarium.fish.GetComponent<BettaAppearance>();
        Saved(shop);
        Undo.RecordObject(aquarium, "Connect shop");
        aquarium.shop = shop;
        aquarium.fishShop = null;
        Saved(aquarium);
        var inventory = aquarium.decoratePanel != null ? aquarium.decoratePanel.GetComponent<DecorInventoryPanel>() : null;
        if (inventory != null)
        {
            Undo.RecordObject(inventory, "Connect shop");
            inventory.shop = shop;
            inventory.decorShop = null;
            Saved(inventory);
        }
        foreach (var old in UnityEngine.Object.FindObjectsByType<FishShopScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(old.gameObject);
        foreach (var old in UnityEngine.Object.FindObjectsByType<DecorShopScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(old.gameObject);
    }

    private static void Saved(UnityEngine.Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    // Layout on the 1080 × 1920 canvas, measured down from the safe area's top: header 40–172, search 196–308,
    // tabs 330–480, sort 490–594, grid 614 to 440 above the bottom, details bar the bottom 24–424.
    private static ShopScreen Create(FishShopOffer[] offers, AquariumDecoration decoration)
    {
        var root = Ui3D.Canvas("ShopHUD", 30);
        var shop = root.gameObject.AddComponent<ShopScreen>();
        shop.fishOffers = offers;
        shop.cardNormal = Ui3D.Require("ui3d_tile");
        shop.cardSelected = Ui3D.Require("ui3d_tile_selected");
        var content = Ui3D.Rect("ShopScreen", root);
        Ui3D.Stretch(content);
        shop.content = content.gameObject;
        var environment = Ui3D.Image("Environment", content, "AquariumEnvironment", false);
        Ui3D.Stretch(environment.rectTransform);
        var fitter = environment.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = environment.sprite.rect.width / environment.sprite.rect.height;
        var scrim = Ui3D.Solid("Scrim", content, new Color(.04f, .09f, .06f, .45f));
        scrim.raycastTarget = true;
        Ui3D.Stretch(scrim.rectTransform);
        var safe = Ui3D.Rect("SafeArea", content);
        Ui3D.Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();

        Header(shop, safe);
        Search(shop, safe);
        Tabs(shop, safe, offers, decoration);
        shop.sortButton = Ui3D.Button("Sort", safe, "Mặc định", Ui3D.Style.Secondary, 38);
        Ui3D.Edges((RectTransform)shop.sortButton.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -594), new Vector2(524, -490));
        shop.sortLabel = shop.sortButton.GetComponentInChildren<TMP_Text>();
        Grid(shop, safe);
        Details(shop, safe);
        Confirmation(shop, content);
        content.gameObject.SetActive(false);
        return shop;
    }

    private static void Header(ShopScreen shop, RectTransform safe)
    {
        var header = Ui3D.Rect("Header", safe);
        Ui3D.TopStrip(header, 44, 44, 40, 132);
        shop.backButton = Ui3D.RoundButton("BackButton", header, "utility_back");
        Ui3D.At((RectTransform)shop.backButton.transform, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(122, 122));
        var title = Ui3D.Text("Title", header, "Shop", 62, Ui3D.Cream, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.Edges(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(150, 0), new Vector2(-320, 0));
        var wallet = Ui3D.Image("Wallet", header, "ui3d_chip");
        Ui3D.At(wallet.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(300, 122));
        var coin = Ui3D.Icon("Coin", wallet.transform, "utility_coin");
        Ui3D.At(coin.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 3), new Vector2(84, 84));
        shop.balanceLabel = Ui3D.Text("Balance", wallet.transform, "1.250", 46, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.Edges(shop.balanceLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(110, 12), new Vector2(-20, -6));
    }

    private static void Search(ShopScreen shop, RectTransform safe)
    {
        var field = Ui3D.Image("Search", safe, "ui3d_chip");
        field.raycastTarget = true;
        Ui3D.TopStrip(field.rectTransform, 44, 44, 196, 112);
        var area = Ui3D.Rect("TextArea", field.transform);
        Ui3D.Edges(area, Vector2.zero, Vector2.one, new Vector2(40, 14), new Vector2(-130, -8));
        area.gameObject.AddComponent<RectMask2D>();
        var placeholder = Ui3D.Text("Placeholder", area, "Tìm vật phẩm...", 40, Ui3D.Muted, false, TextAlignmentOptions.MidlineLeft);
        Ui3D.Stretch(placeholder.rectTransform);
        var text = Ui3D.Text("Text", area, "", 40, Ui3D.Ink, false, TextAlignmentOptions.MidlineLeft);
        text.enableAutoSizing = false;
        Ui3D.Stretch(text.rectTransform);
        var input = field.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = field;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 40;
        input.fontAsset = Ui3D.Body;
        input.pointSize = 40;
        shop.search = input;
        shop.clearSearch = Ui3D.RoundButton("Clear", field.transform, "utility_close", true);
        Ui3D.At((RectTransform)shop.clearSearch.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-14, 2), new Vector2(92, 92));
    }

    private static void Tabs(ShopScreen shop, RectTransform safe, FishShopOffer[] offers, AquariumDecoration decoration)
    {
        var tray = Ui3D.Image("Tabs", safe, "ui3d_tab_tray");
        tray.raycastTarget = true;
        Ui3D.TopStrip(tray.rectTransform, 44, 44, 330, 150);
        var viewport = Ui3D.Rect("Viewport", tray.transform);
        Ui3D.Edges(viewport, Vector2.zero, Vector2.one, new Vector2(12, 18), new Vector2(-12, -10));
        viewport.gameObject.AddComponent<RectMask2D>();
        var row = Ui3D.Rect("Row", viewport);
        row.anchorMin = Vector2.zero;
        row.anchorMax = new Vector2(0, 1);
        row.pivot = new Vector2(0, .5f);
        row.offsetMin = row.offsetMax = Vector2.zero;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = tray.gameObject.AddComponent<ScrollRect>();
        scroll.content = row;
        scroll.viewport = viewport;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var icons = TabIcons(offers, decoration);
        int count = TabNames.Length;
        shop.tabButtons = new Button[count];
        shop.tabSurfaces = new Image[count];
        shop.tabLabels = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            var tab = Ui3D.Image("Tab" + i, row, "ui3d_tab_on");
            tab.color = i == 0 ? Color.white : Color.clear;
            tab.raycastTarget = true;
            tab.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            var button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = tab;
            button.transition = Selectable.Transition.None;
            var icon = Ui3D.Rect("Icon", tab.transform).gameObject.AddComponent<Image>();
            icon.sprite = icons[i];
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Ui3D.At(icon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -4), new Vector2(100, 84));
            var label = Ui3D.Text("Label", tab.transform, TabNames[i], 28, i == 0 ? Ui3D.Ink : Ui3D.Cream, true);
            Ui3D.Edges(label.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(4, 4), new Vector2(-4, 36));
            shop.tabButtons[i] = button;
            shop.tabSurfaces[i] = tab;
            shop.tabLabels[i] = label;
        }
    }

    // Each tab shows one of its own items; Tất cả uses the shop icon.
    private static Sprite[] TabIcons(FishShopOffer[] offers, AquariumDecoration decoration)
    {
        Sprite Decor(AquariumDecorCategory category) =>
            decoration.catalog.offers.FirstOrDefault(o => o.category == category && o.portrait != null)?.portrait;
        var surfaces = decoration.surfaces;
        return new[]
        {
            Ui3D.Require("action_shop"), offers[0].portrait,
            Decor(AquariumDecorCategory.Wood), Decor(AquariumDecorCategory.Rocks), Decor(AquariumDecorCategory.Coral),
            Decor(AquariumDecorCategory.Plants), Decor(AquariumDecorCategory.Moss), Decor(AquariumDecorCategory.Floating),
            surfaces.walls.Length > 1 ? surfaces.walls[1].portrait : null,
            surfaces.substrates.Length > 1 ? surfaces.substrates[1].portrait : null
        };
    }

    private static void Grid(ShopScreen shop, RectTransform safe)
    {
        var area = Ui3D.Rect("Grid", safe);
        Ui3D.Edges(area, Vector2.zero, Vector2.one, new Vector2(44, 440), new Vector2(-44, -614));
        area.gameObject.AddComponent<RectMask2D>();
        var hit = area.gameObject.AddComponent<Image>();
        hit.color = Color.clear; // drags between cards still scroll
        var cells = Ui3D.Rect("Cards", area);
        cells.anchorMin = new Vector2(0, 1);
        cells.anchorMax = Vector2.one;
        cells.pivot = new Vector2(.5f, 1);
        cells.offsetMin = cells.offsetMax = Vector2.zero;
        var grid = cells.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(232, 312);
        grid.spacing = new Vector2(14, 16);
        grid.padding = new RectOffset(0, 0, 4, 24);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperCenter;
        cells.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.content = cells;
        scroll.viewport = area;
        scroll.horizontal = false;
        scroll.vertical = true;
        shop.scroll = scroll;
        shop.cardTemplate = Card(cells);
        var empty = Ui3D.Text("Empty", area, "Không tìm thấy vật phẩm", 42, Ui3D.Cream, true);
        Ui3D.TopStrip(empty.rectTransform, 0, 0, 80, 80);
        shop.emptyLabel = empty.gameObject;
    }

    private static ShopCard Card(Transform parent)
    {
        var surface = Ui3D.Image("Card", parent, "ui3d_tile");
        surface.raycastTarget = true;
        var card = surface.gameObject.AddComponent<ShopCard>();
        card.surface = surface;
        card.button = surface.gameObject.AddComponent<Button>();
        card.button.targetGraphic = surface;
        card.button.transition = Selectable.Transition.None;
        card.portrait = Ui3D.Rect("Portrait", surface.transform).gameObject.AddComponent<Image>();
        card.portrait.preserveAspect = true;
        card.portrait.raycastTarget = false;
        Ui3D.Edges(card.portrait.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(14, -184), new Vector2(-14, -12));
        card.title = Ui3D.Text("Name", surface.transform, "", 30, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        card.title.textWrappingMode = TextWrappingModes.NoWrap;
        card.title.overflowMode = TextOverflowModes.Ellipsis;
        Ui3D.Edges(card.title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(18, -236), new Vector2(-14, -190));
        var coin = Ui3D.Icon("Coin", surface.transform, "utility_coin");
        Ui3D.At(coin.rectTransform, Vector2.zero, Vector2.zero, new Vector2(16, 22), new Vector2(48, 48));
        card.coin = coin.gameObject;
        card.price = Ui3D.Text("Price", surface.transform, "", 32, Ui3D.PriceInk, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.Edges(card.price.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(70, 20), new Vector2(-14, 72));
        var badge = Ui3D.Badge("Status", surface.transform, "", true, out var status);
        Ui3D.At(badge.rectTransform, Vector2.one, Vector2.one, new Vector2(-8, -8), new Vector2(150, 52));
        card.status = status;
        card.statusBadge = badge.gameObject;
        return card;
    }

    private static void Details(ShopScreen shop, RectTransform safe)
    {
        var panel = Ui3D.Panel("Details", safe);
        Ui3D.BottomStrip(panel.rectTransform, 24, 24, 24, 400);
        shop.details = panel.gameObject;
        shop.portrait = Ui3D.Rect("Portrait", panel.transform).gameObject.AddComponent<Image>();
        shop.portrait.preserveAspect = true;
        shop.portrait.raycastTarget = false;
        Ui3D.At(shop.portrait.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(36, 8), new Vector2(270, 270));
        shop.title = Ui3D.Text("Title", panel.transform, "", 48, Ui3D.Ink, true, TextAlignmentOptions.TopLeft);
        Ui3D.Edges(shop.title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(330, -100), new Vector2(-36, -36));
        shop.description = Ui3D.Text("Description", panel.transform, "", 32, Ui3D.Muted, false, TextAlignmentOptions.TopLeft);
        shop.description.enableAutoSizing = false;
        shop.description.overflowMode = TextOverflowModes.Ellipsis;
        Ui3D.Edges(shop.description.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(330, -186), new Vector2(-36, -104));
        var coin = Ui3D.Icon("Coin", panel.transform, "utility_coin");
        Ui3D.At(coin.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(330, -196), new Vector2(56, 56));
        shop.priceCoin = coin.gameObject;
        shop.priceLabel = Ui3D.Text("Price", panel.transform, "", 42, Ui3D.PriceInk, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.Edges(shop.priceLabel.rectTransform, new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(396, -254), new Vector2(0, -194));
        shop.feedback = Ui3D.Text("Feedback", panel.transform, "", 30, Ui3D.Forest, false, TextAlignmentOptions.MidlineRight);
        Ui3D.Edges(shop.feedback.rectTransform, new Vector2(.5f, 1), Vector2.one, new Vector2(0, -254), new Vector2(-36, -194));
        shop.secondaryButton = Ui3D.Button("Buy", panel.transform, "Mua 1", Ui3D.Style.Secondary, 40);
        Ui3D.At((RectTransform)shop.secondaryButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-400, 28), new Vector2(300, 116));
        shop.secondaryLabel = shop.secondaryButton.GetComponentInChildren<TMP_Text>();
        shop.primaryButton = Ui3D.Button("Action", panel.transform, "Mua", Ui3D.Style.Primary, 40);
        Ui3D.At((RectTransform)shop.primaryButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 28), new Vector2(350, 116));
        shop.primaryLabel = shop.primaryButton.GetComponentInChildren<TMP_Text>();
    }

    private static void Confirmation(ShopScreen shop, RectTransform content)
    {
        var modal = Ui3D.Solid("PurchaseConfirmation", content, new Color(.05f, .09f, .06f, .62f));
        modal.raycastTarget = true;
        Ui3D.Stretch(modal.rectTransform);
        var dialog = Ui3D.Panel("Dialog", modal.transform);
        Ui3D.At(dialog.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 560));
        var heading = Ui3D.Text("Heading", dialog.transform, "Xác nhận mua", 52, Ui3D.Ink, true);
        Ui3D.TopStrip(heading.rectTransform, 40, 40, 44, 70);
        shop.confirmationText = Ui3D.Text("Text", dialog.transform, "", 44, Ui3D.Ink);
        Ui3D.TopStrip(shop.confirmationText.rectTransform, 40, 40, 130, 200);
        shop.cancelButton = Ui3D.Button("Cancel", dialog.transform, "Huỷ", Ui3D.Style.Secondary);
        Ui3D.At((RectTransform)shop.cancelButton.transform, Vector2.zero, Vector2.zero, new Vector2(50, 44), new Vector2(370, 128));
        shop.confirmButton = Ui3D.Button("Confirm", dialog.transform, "Mua", Ui3D.Style.Primary);
        Ui3D.At((RectTransform)shop.confirmButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50, 44), new Vector2(370, 128));
        shop.confirmation = modal.gameObject;
        modal.gameObject.SetActive(false);
    }
}
