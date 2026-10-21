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

    [MenuItem("Lumaquarium/UI/Build Fish Shop Screen")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/MainAquarium.unity") throw new InvalidOperationException("Open MainAquarium first.");
        var aquarium = UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>();
        if (aquarium == null || aquarium.fish == null) throw new InvalidOperationException("The existing aquarium HUD and fish are required.");
        Ui3D.LoadFonts();
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
        if (built == null || built.layoutVersion < LayoutVersion || built.offers == null
            || !built.offers.Select(o => o.id).SequenceEqual(offers.Select(o => o.id)))
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
        Debug.Log("[Lumaquarium] Fish shop integrated. Open it with Cửa hàng in the dock or the wallet chip.");
    }

    // Called by the HUD builder too, so rebuilding that HUD reconnects the existing shop.
    public static void Connect(MainAquariumScreen aquarium)
    {
        var shop = UnityEngine.Object.FindFirstObjectByType<FishShopScreen>(FindObjectsInactive.Include);
        if (shop == null) return;
        Ui3D.LoadFonts();
        Undo.RecordObject(shop, "Connect fish shop");
        shop.aquarium = aquarium;
        shop.fish = aquarium.fish.GetComponent<BettaAppearance>();
        if (shop.fish == null) shop.fish = Undo.AddComponent<BettaAppearance>(aquarium.fish.gameObject);
        shop.aquariumFishName = aquarium.header.transform.Find("FishNameplate/FishName").GetComponent<TMP_Text>();
        shop.aquariumPortrait = aquarium.header.transform.Find("BettaPortrait").GetComponent<UnityEngine.UI.Image>();
        Undo.RecordObject(shop.aquariumFishName, "Fit fish name");
        shop.aquariumFishName.enableAutoSizing = true;
        shop.aquariumFishName.fontSizeMin = 20;
        shop.aquariumFishName.fontSizeMax = 42;
        // The redesigned HUD opens the shop from the dock, wallet chip and Đổi cá through OpenShop.
        Undo.RecordObject(aquarium, "Connect fish shop");
        aquarium.fishShop = shop;
        aquarium.fishNameLabel = shop.aquariumFishName;
        aquarium.fishPortrait = shop.aquariumPortrait;
        EditorUtility.SetDirty(aquarium);
        var legacy = aquarium.header.transform.Find("FishShopButton");
        if (legacy != null) Undo.DestroyObjectImmediate(legacy.gameObject);
        DecorShopScreenBuilder.Connect(aquarium);
        EditorUtility.SetDirty(shop);
        if (PrefabUtility.IsPartOfPrefabInstance(shop)) PrefabUtility.RecordPrefabInstancePropertyModifications(shop);
    }

    private static FishShopOffer[] LoadOffers()
    {
        string[] ids = { "royal", "mustard", "koi", "orchid", "pearl" };
        string[] assets = { "RoyalBlueHalfmoon", "MustardGasVeiltail", "KoiGalaxyPlakat", "BlackOrchidCrowntail", "AquaPearlRosetail" };
        string[] descriptions = { "Xanh hoàng gia · Đuôi xòe bán nguyệt", "Xanh sẫm, vây vàng · Đuôi rủ mềm", "Trắng, cam, đen · Hoa văn Koi", "Tím phong lan · Đuôi tưa cá tính",
            "Trắng ngọc, vây xanh ngọc · Mép vây gợn sóng" };
        int[] prices = { 350, 450, 650, 900, 0 }; // Aqua Pearl Rosetail is a free gift
        return ids.Select((id, i) => new FishShopOffer
        {
            id = id,
            variant = AssetDatabase.LoadAssetAtPath<BettaVariant>("Assets/_Project/Data/Betta/Variants/" + assets[i] + ".asset")
                ?? throw new InvalidOperationException("Missing variant " + assets[i] + ". Build the Betta variant presets first."),
            description = descriptions[i], price = prices[i]
        }).ToArray();
    }

    public const int LayoutVersion = 2; // 2 = shared 2.5D shop with Cá/Cây/Lũa/Đá tabs

    private static FishShopScreen CreateScreen(FishShopOffer[] offers)
    {
        var root = Ui3D.Canvas("FishShopHUD", 30);
        var shop = root.gameObject.AddComponent<FishShopScreen>();
        shop.layoutVersion = LayoutVersion;
        shop.offers = offers;
        shop.normalSurface = Ui3D.Require("ui3d_tile");
        shop.selectedSurface = Ui3D.Require("ui3d_tile_selected");
        var frame = Ui3D.BuildShopFrame(root, 0, "Cửa hàng");
        shop.content = frame.content.gameObject;
        shop.backButton = frame.back;
        shop.balanceLabel = frame.balance;
        shop.decorTabs = new[] { frame.tabs[1], frame.tabs[2], frame.tabs[3] };

        shop.heroPortrait = Ui3D.Image("Portrait", frame.hero, null, false);
        shop.heroPortrait.sprite = offers[0].portrait;
        shop.heroPortrait.preserveAspect = true;
        Ui3D.Edges(shop.heroPortrait.rectTransform, Vector2.zero, Vector2.one, new Vector2(60, 170), new Vector2(-60, -20));
        shop.heroName = Ui3D.Text("FishName", frame.hero, offers[0].variant.displayName, 62, Ui3D.Cream, true);
        Ui3D.At(shop.heroName.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 80), new Vector2(980, 90));
        shop.description = Ui3D.Text("Description", frame.hero, offers[0].description, 38, Ui3D.Cream);
        Ui3D.At(shop.description.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 10), new Vector2(980, 64));

        var sheet = frame.sheet;
        var heading = Ui3D.Text("Heading", sheet, "Chọn cá cho hồ", 50, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.TopStrip(heading.rectTransform, 64, 420, 44, 76);
        var hint = Ui3D.Text("FishCount", sheet, "Mỗi loại mua 1 lần", 34, Ui3D.Muted, false, TextAlignmentOptions.MidlineRight);
        Ui3D.TopStrip(hint.rectTransform, 600, 64, 44, 76);
        // Two cards per row; past two rows the list scrolls inside the same area.
        var viewport = Ui3D.Rect("FishOptions", sheet);
        Ui3D.Edges(viewport, Vector2.zero, Vector2.one, new Vector2(54, 470), new Vector2(-54, -140));
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var grid = Ui3D.Rect("Content", viewport);
        Ui3D.Edges(grid, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
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
        shop.optionButtons = new UnityEngine.UI.Button[offers.Length];
        shop.optionSurfaces = new UnityEngine.UI.Image[offers.Length];
        shop.optionChecks = new GameObject[offers.Length];
        shop.optionPrices = new TMP_Text[offers.Length];
        for (int row = 0; row < (offers.Length + 1) / 2; row++)
        {
            var rowRect = Ui3D.Rect("Row" + row, grid);
            rowRect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 340;
            var horizontal = rowRect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            horizontal.spacing = 24;
            horizontal.childControlHeight = horizontal.childControlWidth = true;
            horizontal.childForceExpandHeight = horizontal.childForceExpandWidth = true;
            for (int col = 0; col < 2; col++)
            {
                int i = row * 2 + col;
                if (i >= offers.Length)
                {
                    Ui3D.Rect("Empty", rowRect); // keeps a lone last card at half width
                    continue;
                }
                var offer = offers[i];
                var surface = Ui3D.Image("FishOption_" + offer.id, rowRect, "ui3d_tile");
                surface.sprite = i == 0 ? shop.selectedSurface : shop.normalSurface;
                surface.raycastTarget = true;
                var option = surface.gameObject.AddComponent<UnityEngine.UI.Button>();
                option.transition = UnityEngine.UI.Selectable.Transition.None;
                option.targetGraphic = surface;
                var image = Ui3D.Image("Portrait", surface.transform, null, false);
                image.sprite = offer.portrait;
                image.preserveAspect = true;
                Ui3D.Edges(image.rectTransform, new Vector2(0, .4f), Vector2.one, new Vector2(16, 0), new Vector2(-16, -14));
                var name = Ui3D.Text("Name", surface.transform, offer.variant.displayName, 38, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
                Ui3D.Edges(name.rectTransform, new Vector2(0, .2f), new Vector2(1, .42f), new Vector2(28, 0), new Vector2(-24, 0));
                var coin = Ui3D.Icon("Coin", surface.transform, "utility_coin");
                Ui3D.At(coin.rectTransform, Vector2.zero, Vector2.zero, new Vector2(26, 26), new Vector2(42, 42));
                var price = Ui3D.Text("Price", surface.transform, offer.price == 0 ? "Miễn phí" : offer.price + " xu", 34, Ui3D.Forest, true, TextAlignmentOptions.MidlineLeft);
                Ui3D.At(price.rectTransform, Vector2.zero, Vector2.zero, new Vector2(76, 20), new Vector2(380, 54));
                var check = Ui3D.Image("Selected", surface.transform, "ui3d_badge");
                var tick = Ui3D.Icon("Tick", check.transform, "utility_confirm");
                Ui3D.Stretch(tick.rectTransform, 10);
                Ui3D.At(check.rectTransform, Vector2.one, Vector2.one, new Vector2(-16, -16), new Vector2(60, 52));
                check.gameObject.SetActive(i == 0);
                shop.optionButtons[i] = option;
                shop.optionSurfaces[i] = surface;
                shop.optionChecks[i] = check.gameObject;
                shop.optionPrices[i] = price;
            }
        }
        shop.feedback = Ui3D.Text("Feedback", sheet, "", 32, Ui3D.Ink);
        Ui3D.Edges(shop.feedback.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(54, 390), new Vector2(-54, 460));

        var footer = frame.footer;
        var caption = Ui3D.Text("PriceCaption", footer, "GIÁ", 30, Ui3D.Muted, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.At(caption.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(54, -40), new Vector2(420, 44));
        var totalCoin = Ui3D.Icon("TotalCoin", footer, "utility_coin");
        Ui3D.At(totalCoin.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -94), new Vector2(60, 60));
        shop.priceLabel = Ui3D.Text("SelectedPrice", footer, "350 xu", 52, Ui3D.Ink, true, TextAlignmentOptions.MidlineLeft);
        Ui3D.At(shop.priceLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -88), new Vector2(420, 72));
        shop.buyButton = Ui3D.Button("BuyButton", footer, "Mua cá", Ui3D.Style.Primary, 48);
        Ui3D.At((RectTransform)shop.buyButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-48, -36), new Vector2(440, 150));
        shop.buyLabel = shop.buyButton.GetComponentInChildren<TMP_Text>();

        shop.confirmation = frame.modal.gameObject;
        var dialog = frame.dialog;
        var dialogTitle = Ui3D.Text("Title", dialog, "Đón bạn mới về hồ?", 56, Ui3D.Ink, true);
        Ui3D.TopStrip(dialogTitle.rectTransform, 50, 50, 60, 90);
        shop.confirmationText = Ui3D.Text("Summary", dialog, "", 44, Ui3D.Ink);
        Ui3D.TopStrip(shop.confirmationText.rectTransform, 50, 50, 170, 230);
        shop.cancelButton = Ui3D.Button("CancelButton", dialog, "Chọn lại", Ui3D.Style.Secondary);
        Ui3D.Edges((RectTransform)shop.cancelButton.transform, Vector2.zero, new Vector2(.42f, 0), new Vector2(56, 60), new Vector2(-8, 210));
        shop.confirmButton = Ui3D.Button("ConfirmButton", dialog, "Xác nhận mua", Ui3D.Style.Primary);
        Ui3D.Edges((RectTransform)shop.confirmButton.transform, new Vector2(.42f, 0), new Vector2(1, 0), new Vector2(8, 60), new Vector2(-56, 210));
        frame.content.gameObject.SetActive(false);
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
            foreach (var t in clone.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
            var animator = clone.GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;
            var appearance = clone.GetComponent<BettaAppearance>();
            if (appearance == null) appearance = clone.AddComponent<BettaAppearance>();
            appearance.Apply(variant);
            var renderers = clone.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray();
            // Skinned renderer bounds include every tail form and over-frame short-finned fish.
            // Measure the currently enabled, deformed meshes for an accurately fitted portrait.
            var bounds = new Bounds();
            bool first = true;
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

}
