using System;
using System.IO;
using System.Linq;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds a full-screen shop above the existing tank. Does not rebuild the aquarium or variant presets.
public static class FishShopScreenBuilder
{
    private const string Root = "Assets/_Project/UI";
    private const string PrefabPath = Root + "/Prefabs/FishShop.prefab";
    private static readonly Color Ink = new Color32(52, 60, 45, 255);
    private static readonly Color Cream = new Color32(255, 245, 221, 255);
    private static readonly Color Muted = new Color32(101, 113, 90, 255);
    private static TMP_FontAsset bodyFont;
    private static TMP_FontAsset titleFont;

    [MenuItem("Lumaquarium/UI/Build Fish Shop Screen")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/MainAquarium.unity") throw new InvalidOperationException("Open MainAquarium first.");
        var aquarium = UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>();
        if (aquarium == null || aquarium.fish == null) throw new InvalidOperationException("The existing aquarium HUD and fish are required.");
        LoadFonts();
        var offers = LoadOffers();
        string backupDir = "Assets/_Project/Scenes/Backups";
        Directory.CreateDirectory(backupDir);
        string backup = backupDir + "/MainAquarium-before-fish-shop-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        File.Copy(scene.path, backup + "-saved.unity");
        EditorSceneManager.SaveScene(scene, backup + "-working.unity", true);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add fish shop");

        // The prefab is rebuilt only when it is missing or its fish list differs from LoadOffers;
        // otherwise hand edits to it are kept. Rebuilding keeps the prefab GUID, so the scene instance follows.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var built = prefab != null ? prefab.GetComponent<FishShopScreen>() : null;
        if (built == null || built.offers == null || !built.offers.Select(o => o.id).SequenceEqual(offers.Select(o => o.id)))
        {
            BakePortraits(aquarium.fish.gameObject, offers);
            var screen = CreateScreen(offers);
            PrefabUtility.SaveAsPrefabAsset(screen.gameObject, PrefabPath);
            UnityEngine.Object.DestroyImmediate(screen.gameObject);
        }
        var shop = UnityEngine.Object.FindFirstObjectByType<FishShopScreen>(FindObjectsInactive.Include);
        if (shop == null)
        {
            shop = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene)).GetComponent<FishShopScreen>();
            Undo.RegisterCreatedObjectUndo(shop.gameObject, "Add fish shop canvas");
        }
        Connect(aquarium);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = shop.gameObject;
        Debug.Log("[Lumaquarium] Fish shop integrated. Open it with Mua cá in the aquarium header.");
    }

    // Called by the HUD builder too, so rebuilding that HUD reconnects the existing shop.
    public static void Connect(MainAquariumScreen aquarium)
    {
        var shop = UnityEngine.Object.FindFirstObjectByType<FishShopScreen>(FindObjectsInactive.Include);
        if (shop == null) return;
        LoadFonts();
        Undo.RecordObject(shop, "Connect fish shop");
        shop.aquarium = aquarium;
        shop.fish = aquarium.fish.GetComponent<BettaAppearance>();
        if (shop.fish == null) shop.fish = Undo.AddComponent<BettaAppearance>(aquarium.fish.gameObject);
        shop.aquariumFishName = aquarium.header.transform.Find("FishNameplate/FishName").GetComponent<TMP_Text>();
        shop.aquariumPortrait = aquarium.header.transform.Find("BettaPortrait").GetComponent<UnityEngine.UI.Image>();
        Undo.RecordObject(shop.aquariumFishName, "Fit fish name");
        shop.aquariumFishName.enableAutoSizing = true;
        shop.aquariumFishName.fontSizeMin = 20;
        shop.aquariumFishName.fontSizeMax = 39;
        var existing = aquarium.header.transform.Find("FishShopButton");
        UnityEngine.UI.Button entry;
        if (existing != null) entry = existing.GetComponent<UnityEngine.UI.Button>();
        else
        {
            entry = Button("FishShopButton", aquarium.header.transform, "Mua cá", "button_normal_wide", Ink, 34);
            At((RectTransform)entry.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -122), new Vector2(248, 88));
            Undo.RegisterCreatedObjectUndo(entry.gameObject, "Add fish shop entry");
        }
        Undo.RecordObject(entry, "Wire fish shop entry");
        for (int i = entry.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if ((entry.onClick.GetPersistentTarget(i) is FishShopScreen || entry.onClick.GetPersistentTarget(i) == null)
                && entry.onClick.GetPersistentMethodName(i) == "Open") // null: a target lost when the prefab was rebuilt
                UnityEventTools.RemovePersistentListener(entry.onClick, i);
        UnityEventTools.AddPersistentListener(entry.onClick, shop.Open);
        DecorShopScreenBuilder.Connect(aquarium);
        EditorUtility.SetDirty(shop);
        EditorUtility.SetDirty(entry);
        if (PrefabUtility.IsPartOfPrefabInstance(shop)) PrefabUtility.RecordPrefabInstancePropertyModifications(shop);
    }

    private static FishShopOffer[] LoadOffers()
    {
        string[] ids = { "royal", "mustard", "koi", "orchid", "pearl", "aqua-fire", "koi-nemo" };
        string[] assets = { "RoyalBlueHalfmoon", "MustardGasVeiltail", "KoiGalaxyPlakat", "BlackOrchidCrowntail", "AquaPearlRosetail", "AquaFireBetta", "KoiNemoBetta" };
        string[] descriptions = { "Xanh hoàng gia · Đuôi xòe bán nguyệt", "Xanh sẫm, vây vàng · Đuôi rủ mềm", "Trắng, cam, đen · Hoa văn Koi", "Tím phong lan · Đuôi tưa cá tính",
            "Trắng ngọc, vây xanh ngọc · Mép vây gợn sóng", "Xanh ngọc, đỏ lửa · Model và màu sắc nguyên bản", "Đỏ, xanh, trắng · Koi Nemo đuôi tròn" };
        int[] prices = { 350, 450, 650, 900, 0, 0, 0 };
        return ids.Select((id, i) => new FishShopOffer
        {
            id = id,
            variant = AssetDatabase.LoadAssetAtPath<BettaVariant>("Assets/_Project/Data/Betta/Variants/" + assets[i] + ".asset")
                ?? throw new InvalidOperationException("Missing variant " + assets[i] + ". Build the Betta variant presets first."),
            description = descriptions[i], price = prices[i]
        }).ToArray();
    }

    private static FishShopScreen CreateScreen(FishShopOffer[] offers)
    {
        var root = Rect("FishShopHUD", null);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var shop = root.gameObject.AddComponent<FishShopScreen>();
        shop.offers = offers;
        shop.normalSurface = Sprite("Surfaces/inventory_slot");
        shop.selectedSurface = Sprite("Surfaces/inventory_slot_selected");
        var content = Rect("ShopScreen", root);
        Stretch(content);
        shop.content = content.gameObject;
        var environment = Picture("Environment", content, Sprite("Backgrounds/AquariumEnvironment"));
        Stretch(environment.rectTransform);
        var fitter = environment.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
        fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = environment.sprite.rect.width / environment.sprite.rect.height;
        var scrim = Picture("Scrim", content, null);
        scrim.color = new Color(.025f, .13f, .13f, .35f);
        scrim.raycastTarget = true;
        Stretch(scrim.rectTransform);
        var bottomFill = Picture("BottomFill", content, null);
        bottomFill.color = new Color32(238, 228, 200, 255);
        AnchorEdges(bottomFill.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 400));
        var safe = Rect("SafeArea", content);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();

        var header = Rect("Header", safe);
        AnchorEdges(header, new Vector2(0, 1), Vector2.one, new Vector2(52, -180), new Vector2(-52, -38));
        shop.backButton = Button("BackButton", header, "", "utility_badge", Ink);
        At((RectTransform)shop.backButton.transform, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(112, 112));
        var backIcon = Picture("Icon", shop.backButton.transform, Sprite("Icons/utility_back"));
        Stretch(backIcon.rectTransform, 27);
        var title = Label("Title", header, "Tiệm cá nhỏ", 57, Cream, true);
        AnchorEdges(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(146, 0), new Vector2(-310, 0));
        title.alignment = TextAlignmentOptions.MidlineLeft;
        var wallet = Picture("Wallet", header, Sprite("Surfaces/button_normal_wide"), true);
        At(wallet.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(282, 102));
        var walletCoin = Coin(wallet.transform, "Coin");
        At(walletCoin, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(24, 0), new Vector2(52, 52));
        shop.balanceLabel = Label("Balance", wallet.transform, "1.250", 45, Ink, true);
        AnchorEdges(shop.balanceLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(83, 10), new Vector2(-16, -10));

        var hero = Rect("SelectedFish", safe);
        AnchorEdges(hero, Vector2.zero, Vector2.one, new Vector2(45, 1160), new Vector2(-45, -198));
        var overline = Label("Overline", hero, "MỘT NGƯỜI BẠN MỚI CHO HỒ", 30, Cream, true);
        At(overline.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(960, 55));
        shop.heroPortrait = Picture("Portrait", hero, offers[0].portrait);
        shop.heroPortrait.preserveAspect = true;
        AnchorEdges(shop.heroPortrait.rectTransform, Vector2.zero, Vector2.one, new Vector2(40, 224), new Vector2(-40, -58));
        shop.heroName = Label("FishName", hero, offers[0].variant.displayName, 65, Cream, true);
        At(shop.heroName.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 110), new Vector2(970, 98));
        shop.description = Label("Description", hero, offers[0].description, 34, Cream);
        At(shop.description.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 35), new Vector2(940, 70));

        var sheet = Picture("SelectionSheet", safe, Sprite("Surfaces/panel_care"), true);
        sheet.color = new Color32(255, 250, 235, 255);
        sheet.raycastTarget = true;
        AnchorEdges(sheet.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1175));
        var heading = Label("Heading", sheet.transform, "Chọn cá của bạn", 48, Ink, true);
        At(heading.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(55, -48), new Vector2(590, 70));
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        var count = Label("FishCount", sheet.transform, offers.Length + " người bạn đang chờ", 30, Muted);
        At(count.rectTransform, Vector2.one, Vector2.one, new Vector2(-55, -48), new Vector2(385, 70));
        count.alignment = TextAlignmentOptions.MidlineRight;
        // Two cards per row at the approved size; past two rows the list scrolls inside the same area.
        var viewport = Rect("FishOptions", sheet.transform);
        AnchorEdges(viewport, new Vector2(0, 1), Vector2.one, new Vector2(52, -824), new Vector2(-52, -145));
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var grid = Rect("Content", viewport);
        AnchorEdges(grid, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
        grid.pivot = new Vector2(.5f, 1);
        var columns = grid.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        columns.spacing = 24;
        columns.childControlHeight = columns.childControlWidth = true;
        columns.childForceExpandWidth = true;
        columns.childForceExpandHeight = false;
        grid.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.content = grid;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        const float rowHeight = (679f - 24f) / 2f;
        shop.optionButtons = new UnityEngine.UI.Button[offers.Length];
        shop.optionSurfaces = new UnityEngine.UI.Image[offers.Length];
        shop.optionChecks = new GameObject[offers.Length];
        shop.optionPrices = new TMP_Text[offers.Length];
        for (int row = 0; row < (offers.Length + 1) / 2; row++)
        {
            var rowRect = Rect("Row" + row, grid);
            rowRect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = rowHeight;
            var horizontal = rowRect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            horizontal.spacing = 24;
            horizontal.childControlHeight = horizontal.childControlWidth = true;
            horizontal.childForceExpandHeight = horizontal.childForceExpandWidth = true;
            for (int col = 0; col < 2; col++)
            {
                int i = row * 2 + col;
                if (i >= offers.Length)
                {
                    Rect("Empty", rowRect); // keeps a lone last card at half width
                    continue;
                }
                var offer = offers[i];
                var surface = Picture("FishOption_" + offer.id, rowRect, i == 0 ? shop.selectedSurface : shop.normalSurface, true);
                surface.raycastTarget = true;
                var option = surface.gameObject.AddComponent<UnityEngine.UI.Button>();
                option.transition = UnityEngine.UI.Selectable.Transition.None;
                option.targetGraphic = surface;
                var image = Picture("Portrait", surface.transform, offer.portrait);
                image.preserveAspect = true;
                AnchorEdges(image.rectTransform, new Vector2(0, .37f), Vector2.one, new Vector2(12, 0), new Vector2(-12, -14));
                var name = Label("Name", surface.transform, offer.variant.displayName, 40, Ink, true);
                At(name.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 64), new Vector2(410, 92));
                name.alignment = TextAlignmentOptions.MidlineLeft;
                var price = Label("Price", surface.transform, offer.price == 0 ? "Miễn phí" : offer.price + " xu", 38, Muted, true);
                At(price.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(27, 17), new Vector2(354, 47));
                price.alignment = TextAlignmentOptions.MidlineLeft;
                var coin = Coin(surface.transform, "Coin");
                At(coin, Vector2.zero, Vector2.zero, new Vector2(29, 23), new Vector2(36, 36));
                var check = Picture("Selected", surface.transform, Sprite("Surfaces/utility_badge"));
                var tick = Picture("Tick", check.transform, Sprite("Icons/utility_confirm"));
                Stretch(tick.rectTransform, 12);
                At(check.rectTransform, Vector2.one, Vector2.one, new Vector2(-20, -20), new Vector2(56, 56));
                check.gameObject.SetActive(i == 0);
                shop.optionButtons[i] = option;
                shop.optionSurfaces[i] = surface;
                shop.optionChecks[i] = check.gameObject;
                shop.optionPrices[i] = price;
            }
        }
        var line = Picture("Divider", sheet.transform, null);
        line.color = new Color32(186, 189, 151, 255);
        AnchorEdges(line.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(55, 303), new Vector2(-55, 306));
        var caption = Label("PriceCaption", sheet.transform, "Giá cá đã chọn", 32, Muted);
        At(caption.rectTransform, Vector2.zero, Vector2.zero, new Vector2(55, 231), new Vector2(420, 56));
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        shop.priceLabel = Label("SelectedPrice", sheet.transform, "350 xu", 56, Ink, true);
        At(shop.priceLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(116, 153), new Vector2(412, 83));
        shop.priceLabel.alignment = TextAlignmentOptions.MidlineLeft;
        var totalCoin = Coin(sheet.transform, "TotalCoin");
        At(totalCoin, Vector2.zero, Vector2.zero, new Vector2(55, 171), new Vector2(49, 49));
        shop.buyButton = Button("BuyButton", sheet.transform, "Mua cá", "button_selected_wide", Ink, 47);
        At((RectTransform)shop.buyButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-55, 166), new Vector2(450, 137));
        shop.buyLabel = shop.buyButton.GetComponentInChildren<TMP_Text>();
        var note = Label("LocalSaveNote", sheet.transform, "Xu trong game · Cá đã mua được lưu trên thiết bị", 28, Muted);
        At(note.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 99), new Vector2(960, 52));
        shop.feedback = Label("Feedback", sheet.transform, "", 31, Ink);
        At(shop.feedback.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 16), new Vector2(940, 77));

        var modal = Picture("PurchaseConfirmation", content, null);
        modal.color = new Color(.035f, .13f, .11f, .78f);
        modal.raycastTarget = true;
        Stretch(modal.rectTransform);
        shop.confirmation = modal.gameObject;
        var modalSafe = Rect("SafeArea", modal.transform);
        Stretch(modalSafe);
        modalSafe.gameObject.AddComponent<SafeArea>();
        var dialog = Picture("Dialog", modalSafe, Sprite("Surfaces/panel_care"), true);
        dialog.raycastTarget = true;
        At(dialog.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(960, 650));
        var dialogTitle = Label("Title", dialog.transform, "Đón bạn mới về hồ?", 57, Ink, true);
        At(dialogTitle.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -66), new Vector2(850, 96));
        shop.confirmationText = Label("Summary", dialog.transform, "", 44, Ink);
        At(shop.confirmationText.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -195), new Vector2(850, 190));
        shop.cancelButton = Button("CancelButton", dialog.transform, "Chọn lại", "button_normal_wide", Ink, 40);
        At((RectTransform)shop.cancelButton.transform, Vector2.zero, Vector2.zero, new Vector2(58, 76), new Vector2(390, 136));
        shop.confirmButton = Button("ConfirmButton", dialog.transform, "Xác nhận mua", "button_selected_wide", Ink, 40);
        At((RectTransform)shop.confirmButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-58, 76), new Vector2(414, 136));
        modal.gameObject.SetActive(false);
        content.gameObject.SetActive(false);
        return shop;
    }

    private static void BakePortraits(GameObject fish, FishShopOffer[] offers)
    {
        string directory = Root + "/Portraits/Shop";
        Directory.CreateDirectory(directory);
        foreach (var offer in offers)
        {
            string path = directory + "/" + offer.id + ".png";
            if (!File.Exists(path)) BakePortrait(fish, offer.variant, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            offer.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    private static void BakePortrait(GameObject fish, BettaVariant variant, string path)
    {
        var clone = UnityEngine.Object.Instantiate(fish);
        var cameraObject = new GameObject("ShopPortraitBakeCamera");
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(640, 400, 24, RenderTextureFormat.ARGB32);
        var bakedMeshes = new System.Collections.Generic.List<Mesh>();
        Texture2D texture = null;
        try
        {
            clone.name = "ShopPortraitBakeOnly";
            clone.transform.position = new Vector3(1000, 0, 0);
            clone.transform.rotation = Quaternion.Euler(0, 90, 0);
            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            var animator = clone.GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;
            var appearance = clone.GetComponent<BettaAppearance>();
            if (appearance == null) appearance = clone.AddComponent<BettaAppearance>();
            appearance.Apply(variant);
            foreach (var t in clone.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
            var renderers = clone.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            // Skinned renderer bounds include every tail form and over-frame short-finned fish.
            // Measure the currently enabled, deformed meshes for an accurately fitted portrait.
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy))
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            foreach (var renderer in renderers)
            {
                var mesh = new Mesh();
                bakedMeshes.Add(mesh);
                renderer.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                {
                    var world = renderer.transform.TransformPoint(vertex);
                    if (first) { bounds = new Bounds(world, Vector3.zero); first = false; }
                    else bounds.Encapsulate(world);
                }
                // Render the CPU-skinned pose, including eye anchors changed in this call.
                // The SkinnedMeshRenderer's GPU pose may still be from the previous frame.
                var snapshot = new GameObject(renderer.name + "Portrait", typeof(MeshFilter), typeof(MeshRenderer));
                snapshot.layer = 31;
                snapshot.transform.SetParent(renderer.transform, false);
                snapshot.GetComponent<MeshFilter>().sharedMesh = mesh;
                var snapshotRenderer = snapshot.GetComponent<MeshRenderer>();
                snapshotRenderer.sharedMaterials = renderer.sharedMaterials;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                snapshotRenderer.SetPropertyBlock(block);
                renderer.enabled = false;
            }
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << 31;
            camera.orthographic = true;
            camera.aspect = 1.6f;
            camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x / camera.aspect) * .56f;
            camera.transform.position = bounds.center + Vector3.back * 3;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture = new Texture2D(640, 400, TextureFormat.RGBA32, false);
            texture.ReadPixels(new UnityEngine.Rect(0, 0, 640, 400), 0, 0);
            // The transparent camera target contains premultiplied colour. PNG sprites
            // use straight alpha; undo the blend so the UI won't darken the new membranes twice.
            var pixels = texture.GetPixels();
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int i = 0; i < pixels.Length; i++)
            {
                float alpha = pixels[i].a;
                if (alpha <= 0.001f || alpha >= 0.999f) continue;
                Color colour = linear ? pixels[i].linear : pixels[i];
                colour.r /= alpha; colour.g /= alpha; colour.b /= alpha;
                pixels[i] = linear ? colour.gamma : colour;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = null;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(clone);
            foreach (var mesh in bakedMeshes) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    private static void LoadFonts()
    {
        bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Nunito-SemiBold SDF.asset");
        titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Nunito-ExtraBold SDF.asset");
        if (bodyFont == null || titleFont == null) throw new InvalidOperationException("Prepare the existing Nunito UI fonts first.");
    }

    private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/" + path + ".png")
        ?? throw new InvalidOperationException("Missing UI sprite: " + path);

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }

    private static UnityEngine.UI.Image Picture(string name, Transform parent, Sprite sprite, bool sliced = false)
    {
        var image = Rect(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        if (sliced)
        {
            image.type = UnityEngine.UI.Image.Type.Sliced;
            // Preserve the supplied surface corner radii at the 1080px canvas reference width.
            image.pixelsPerUnitMultiplier = 1f;
        }
        return image;
    }

    private static TMP_Text Label(string name, Transform parent, string value, float size, Color color, bool title = false)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = title ? titleFont : bodyFont;
        label.text = value;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * .8f;
        label.fontSizeMax = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static UnityEngine.UI.Button Button(string name, Transform parent, string text, string sprite, Color color, float fontSize = 40)
    {
        var surface = Picture(name, parent, Sprite("Surfaces/" + sprite), true);
        surface.raycastTarget = true;
        var button = surface.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = surface;
        var colors = button.colors;
        colors.disabledColor = new Color(.65f, .65f, .65f, .8f);
        button.colors = colors;
        var label = Label("Label", surface.transform, text, fontSize, color, true);
        Stretch(label.rectTransform, 15);
        return button;
    }

    private static RectTransform Coin(Transform parent, string name)
    {
        return Picture(name, parent, Sprite("Icons/utility_coin")).rectTransform;
    }

    private static void At(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset = 0) => AnchorEdges(rect, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
    private static void AnchorEdges(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
