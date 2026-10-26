using System;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The approved compact shop: three text groups, contextual categories, search/sort and two-column cards.
// MainAquarium and removes the fish and decoration shop canvases; their prefabs and scripts stay in the project.
public static class ShopScreenBuilder
{
    private const string PrefabPath = Ui3D.Root + "/Prefabs/Shop.prefab";
    private const string FishPrefabPath = Ui3D.Root + "/Prefabs/FishShop.prefab";
    private const string ScenePath = "Assets/_Project/Scenes/MainAquarium.unity";
    public static readonly string[] TabNames = { "Cá", "Tiểu cảnh", "Hồ" };

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

        var current = UnityEngine.Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
        var built = Create(current != null && current.fishOffers != null && current.fishOffers.Length > 0 ? current.fishOffers : fishShop.offers, decoration);
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

    // 1080-wide canvas: compact header/groups, optional categories, shared search/sort row and a scrollable grid.
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
        Tabs(shop, safe);
        Search(shop, safe);
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
        var title = Ui3D.Text("Title", header, "Cửa hàng", 62, Ui3D.Cream, true, TextAlignmentOptions.MidlineLeft);
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
        var row = Ui3D.Rect("SearchRow", safe);
        Ui3D.TopStrip(row, 44, 44, 330, 116);
        shop.searchRow = row;
        var field = Ui3D.Image("Search", row, "ui3d_chip");
        field.raycastTarget = true;
        Ui3D.Edges(field.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-142, 0));
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
        shop.sortButton = Ui3D.Button("Sort", row, "", Ui3D.Style.Secondary);
        Ui3D.At((RectTransform)shop.sortButton.transform, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(116, 116));
        foreach (var label in shop.sortButton.GetComponentsInChildren<TMP_Text>()) UnityEngine.Object.DestroyImmediate(label.gameObject);
        var priceCoin = Ui3D.Icon("PriceSortCoin", shop.sortButton.transform, "utility_coin");
        Ui3D.At(priceCoin.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-22, 3), new Vector2(54, 54));
        var up = Ui3D.Icon("Ascending", shop.sortButton.transform, "utility_back");
        up.color = Ui3D.Ink;
        Ui3D.At(up.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(23, 15), new Vector2(38, 38));
        up.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
        shop.sortIcon = up;
        var down = Ui3D.Icon("Descending", shop.sortButton.transform, "utility_back");
        down.color = Ui3D.Ink;
        Ui3D.At(down.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(23, -11), new Vector2(38, 38));
        down.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
        shop.sortDownIcon = down;
    }

    private static void Tabs(ShopScreen shop, RectTransform safe)
    {
        var tray = Ui3D.Image("Groups", safe, "ui3d_tab_tray");
        Ui3D.TopStrip(tray.rectTransform, 44, 44, 194, 116);
        var row = Ui3D.Rect("Row", tray.transform);
        Ui3D.Stretch(row, 12);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        shop.groupButtons = new Button[3];
        shop.groupSurfaces = new Image[3];
        shop.groupLabels = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            var surface = Ui3D.Image("Group" + i, row, "ui3d_tab_on");
            surface.color = i == 0 ? Color.white : Color.clear;
            surface.raycastTarget = true;
            surface.gameObject.AddComponent<LayoutElement>().preferredWidth = 300;
            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.None;
            var label = Ui3D.Text("Label", surface.transform, TabNames[i], 42, i == 0 ? Ui3D.Ink : Ui3D.Cream, true);
            Ui3D.Edges(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-8, 0));
            shop.groupButtons[i] = button;
            shop.groupSurfaces[i] = surface;
            shop.groupLabels[i] = label;
        }
        var categories = Ui3D.Rect("Categories", safe);
        Ui3D.TopStrip(categories, 44, 44, 330, 88);
        shop.categoryTray = categories;
        var categoryLayout = categories.gameObject.AddComponent<HorizontalLayoutGroup>();
        categoryLayout.spacing = 10;
        categoryLayout.childControlWidth = categoryLayout.childControlHeight = true;
        categoryLayout.childForceExpandWidth = false;
        categoryLayout.childForceExpandHeight = true;
        shop.tabButtons = new Button[6];
        shop.tabSurfaces = new Image[6];
        shop.tabLabels = new TMP_Text[6];
        for (int i = 0; i < 6; i++)
        {
            var button = Ui3D.Button("Category" + i, categories, ShopScreen.CategoryNames[1][i], Ui3D.Style.Secondary, 32);
            button.transition = Selectable.Transition.None;
            button.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            shop.tabButtons[i] = button;
            shop.tabSurfaces[i] = button.GetComponent<Image>();
            shop.tabLabels[i] = button.GetComponentInChildren<TMP_Text>();
        }
    }

    private static void Grid(ShopScreen shop, RectTransform safe)
    {
        var area = Ui3D.Rect("Grid", safe);
        Ui3D.Edges(area, Vector2.zero, Vector2.one, new Vector2(44, 480), new Vector2(-44, -474));
        area.gameObject.AddComponent<RectMask2D>();
        var hit = area.gameObject.AddComponent<Image>();
        hit.color = Color.clear; // drags between cards still scroll
        var cells = Ui3D.Rect("Cards", area);
        cells.anchorMin = new Vector2(0, 1);
        cells.anchorMax = Vector2.one;
        cells.pivot = new Vector2(.5f, 1);
        cells.offsetMin = cells.offsetMax = Vector2.zero;
        var grid = cells.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(482, 470);
        grid.spacing = new Vector2(28, 28);
        grid.padding = new RectOffset(0, 0, 4, 24);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
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
        Ui3D.Edges(card.portrait.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -296), new Vector2(-24, -18));
        card.title = Ui3D.Text("Name", surface.transform, "", 40, Ui3D.Ink, true, TextAlignmentOptions.TopLeft);
        card.title.textWrappingMode = TextWrappingModes.Normal;
        card.title.overflowMode = TextOverflowModes.Ellipsis;
        Ui3D.Edges(card.title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(30, -390), new Vector2(-30, -306));
        var priceRow = Ui3D.Rect("PriceRow", surface.transform);
        Ui3D.At(priceRow, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 28), new Vector2(180, 62));
        var priceLayout = priceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        priceLayout.spacing = 10;
        priceLayout.childAlignment = TextAnchor.MiddleRight;
        priceLayout.childControlWidth = priceLayout.childControlHeight = true;
        priceLayout.childForceExpandWidth = priceLayout.childForceExpandHeight = false;
        priceRow.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var coin = Ui3D.Icon("Coin", priceRow, "utility_coin");
        var coinSize = coin.gameObject.AddComponent<LayoutElement>();
        coinSize.preferredWidth = coinSize.preferredHeight = 62;
        card.coin = coin.gameObject;
        card.price = Ui3D.Text("Price", priceRow, "", 44, Ui3D.PriceInk, true, TextAlignmentOptions.MidlineRight);
        card.price.enableAutoSizing = false;
        card.price.textWrappingMode = TextWrappingModes.NoWrap;
        var shadow = card.price.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(1, .97f, .9f, .9f);
        shadow.effectDistance = new Vector2(0, -2);
        var badge = Ui3D.Badge("Status", surface.transform, "", true, out var status);
        Ui3D.At(badge.rectTransform, Vector2.one, Vector2.one, new Vector2(-8, -8), new Vector2(150, 52));
        card.status = status;
        card.statusBadge = badge.gameObject;
        var check = Ui3D.Image("ActiveCheck", surface.transform, "ui3d_round", false);
        Ui3D.At(check.rectTransform, Vector2.one, Vector2.one, new Vector2(-16, -16), new Vector2(96, 96));
        var glyph = Ui3D.Icon("Check", check.transform, "utility_confirm");
        Ui3D.Stretch(glyph.rectTransform, 21);
        card.activeCheck = check.gameObject;
        var tankPreview = Ui3D.Rect("TankPreview", surface.transform);
        Ui3D.Edges(tankPreview, new Vector2(0, 1), Vector2.one, new Vector2(24, -296), new Vector2(-24, -18));
        var glass = Ui3D.Solid("Glass", tankPreview, new Color(.66f, .86f, .8f, .38f));
        Ui3D.At(glass.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(290, 190));
        var outline = glass.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.35f, .57f, .49f, 1);
        outline.effectDistance = new Vector2(5, -5);
        var sand = Ui3D.Solid("Sand", glass.transform, new Color(.76f, .66f, .44f, .95f));
        Ui3D.Edges(sand.rectTransform, Vector2.zero, new Vector2(1, .16f), Vector2.zero, Vector2.zero);
        card.tankPreview = tankPreview.gameObject;
        card.tankShape = glass.rectTransform;
        check.transform.SetAsLastSibling();
        return card;
    }

    private static void Details(ShopScreen shop, RectTransform safe)
    {
        var panel = Ui3D.Panel("Details", safe);
        Ui3D.BottomStrip(panel.rectTransform, 24, 24, 24, 430);
        shop.details = panel.gameObject;
        shop.portrait = Ui3D.Rect("Portrait", panel.transform).gameObject.AddComponent<Image>();
        shop.portrait.gameObject.SetActive(false);
        shop.title = Ui3D.Text("Title", panel.transform, "", 48, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(shop.title.rectTransform, 48, 280, 30, 68);
        var coin = Ui3D.Icon("Coin", panel.transform, "utility_coin");
        Ui3D.At(coin.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, -38), new Vector2(56, 56));
        shop.priceCoin = coin.gameObject;
        shop.priceLabel = Ui3D.Text("Price", panel.transform, "", 42, Ui3D.PriceInk, true, TextAlignmentOptions.MidlineRight);
        Ui3D.At(shop.priceLabel.rectTransform, Vector2.one, Vector2.one, new Vector2(-48, -36), new Vector2(220, 60));
        shop.description = Ui3D.Text("Description", panel.transform, "", 34, Ui3D.Muted, false, TextAlignmentOptions.TopLeft);
        shop.description.enableAutoSizing = false;
        shop.description.overflowMode = TextOverflowModes.Ellipsis;
        Ui3D.TopStrip(shop.description.rectTransform, 48, 48, 108, 100);
        shop.feedback = Ui3D.Text("Feedback", panel.transform, "", 30, Ui3D.Forest, false, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(shop.feedback.rectTransform, 48, 48, 212, 44);
        var actions = Ui3D.Rect("Actions", panel.transform);
        Ui3D.BottomStrip(actions, 48, 48, 32, 132);
        var layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        shop.secondaryButton = Ui3D.Button("Buy", actions, "Mua 1", Ui3D.Style.Secondary, 42);
        shop.secondaryButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 440;
        shop.secondaryLabel = shop.secondaryButton.GetComponentInChildren<TMP_Text>();
        shop.primaryButton = Ui3D.Button("Action", actions, "Mua", Ui3D.Style.Primary, 42);
        shop.primaryButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 440;
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
