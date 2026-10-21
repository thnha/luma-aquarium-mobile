using System;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Shared widgets for the 2.5D redesign (docs/ui-redesign.md). Sizes are 1080-wide canvas pixels.
public static class Ui3D
{
    public const string Root = "Assets/_Project/UI";
    public static readonly Color Ink = Hex("343C2D");
    public static readonly Color Muted = Hex("5E6352");
    public static readonly Color Cream = Hex("FFF8E6");
    public static readonly Color Forest = Hex("455D3F");
    public static readonly Color Divider = Hex("E6D9B8");
    public static readonly Color Disabled = Hex("6E6A5C");
    public static TMP_FontAsset Body { get; private set; }
    public static TMP_FontAsset Title { get; private set; }

    public enum Style { Primary, Secondary }

    public static void LoadFonts()
    {
        Body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Nunito-SemiBold SDF.asset");
        Title = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Nunito-ExtraBold SDF.asset");
        if (Body == null || Title == null) throw new InvalidOperationException("Run Lumaquarium/UI/Prepare Sprite Assets first.");
        if (Sprite("ui3d_panel") == null) throw new InvalidOperationException("Missing 2.5D surfaces. Run Tools/UI/make_ui3d_sprites.py, then Prepare Sprite Assets.");
    }

    public static Sprite Sprite(string name)
    {
        foreach (string folder in new[] { "Surfaces", "Icons", "Backgrounds", "Portraits", "Portraits/Shop" })
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{folder}/{name}.png");
            if (sprite != null) return sprite;
        }
        return null;
    }

    public static Sprite Require(string name) => Sprite(name) ?? throw new InvalidOperationException("Missing UI sprite: " + name);

    public static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }

    public static void At(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Edges(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public static void Stretch(RectTransform rect, float inset = 0) =>
        Edges(rect, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));

    // Top-anchored strip spanning the parent width, `top` and `height` measured down from the parent top.
    public static void TopStrip(RectTransform rect, float left, float right, float top, float height) =>
        Edges(rect, new Vector2(0, 1), Vector2.one, new Vector2(left, -top - height), new Vector2(-right, -top));

    public static void BottomStrip(RectTransform rect, float left, float right, float bottom, float height) =>
        Edges(rect, Vector2.zero, new Vector2(1, 0), new Vector2(left, bottom), new Vector2(-right, bottom + height));

    public static Image Image(string name, Transform parent, string sprite, bool sliced = true)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite != null ? Require(sprite) : null;
        image.type = sliced && image.sprite != null && image.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1;
        image.raycastTarget = false;
        return image;
    }

    public static Image Solid(string name, Transform parent, Color color)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Image Icon(string name, Transform parent, string sprite)
    {
        var image = Image(name, parent, sprite, false);
        image.preserveAspect = true;
        return image;
    }

    public static TMP_Text Text(string name, Transform parent, string value, float size, Color color, bool title = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = title ? Title : Body;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * .75f;
        text.fontSizeMax = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    public static Button Button(string name, Transform parent, string label, Style style, float fontSize = 46)
    {
        bool primary = style == Style.Primary;
        var surface = Image(name, parent, primary ? "ui3d_button_primary" : "ui3d_button_secondary");
        surface.raycastTarget = true;
        var button = surface.gameObject.AddComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            pressedSprite = Require(primary ? "ui3d_button_primary_pressed" : "ui3d_button_secondary_pressed"),
            disabledSprite = Require("ui3d_button_disabled")
        };
        var text = Text("Label", surface.transform, label, fontSize, primary ? Cream : Ink, true);
        // The face sits above the bottom edge; centre the label on the face, not the whole sprite.
        Edges(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 14), new Vector2(-18, -6));
        var tint = surface.gameObject.AddComponent<Ui3DButtonLabel>();
        tint.button = button;
        tint.label = text;
        tint.normal = text.color;
        tint.disabled = Disabled;
        return button;
    }

    // A 44-point round button: green with a cream glyph, or light with an ink glyph.
    public static Button RoundButton(string name, Transform parent, string icon, bool light = false)
    {
        var surface = Image(name, parent, light ? "ui3d_round_light" : "ui3d_round", false);
        surface.raycastTarget = true;
        var button = surface.gameObject.AddComponent<Button>();
        button.targetGraphic = surface;
        if (!light)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = Require("ui3d_round_pressed") };
        }
        var glyph = Icon("Icon", surface.transform, icon);
        Edges(glyph.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 34), new Vector2(-30, -26));
        if (light) glyph.color = Ink;
        return button;
    }

    public static readonly Color PriceInk = Hex("5A3A12");

    // Compact price pill for shop cards, anchored bottom-right and sized to its content:
    // a coin and the number only (gold), or a short status on the green ui3d_price_free.
    public static Image PricePill(string name, Transform parent, out TMP_Text label, out GameObject coin)
    {
        var pill = Image(name, parent, "ui3d_price");
        pill.pixelsPerUnitMultiplier = 96f / 84; // keep both rounded ends on an 84 px pill
        At(pill.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 22), new Vector2(200, 84));
        var row = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(10, 26, 8, 14);
        row.spacing = 8;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        pill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var icon = Icon("Coin", pill.transform, "utility_coin");
        var size = icon.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = size.preferredHeight = 62;
        coin = icon.gameObject;
        label = Text("Price", pill.transform, "", 50, PriceInk, true, TextAlignmentOptions.MidlineLeft);
        label.enableAutoSizing = false; // the pill grows with the text instead
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return pill;
    }

    public static Image Panel(string name, Transform parent)
    {
        var panel = Image(name, parent, "ui3d_panel");
        panel.raycastTarget = true;
        return panel;
    }

    public static Image Badge(string name, Transform parent, string value, bool light, out TMP_Text label)
    {
        var badge = Image(name, parent, light ? "ui3d_badge_light" : "ui3d_badge");
        label = Text("Label", badge.transform, value, 30, light ? Ink : Cream, true);
        Stretch(label.rectTransform, 6);
        return badge;
    }

    public static RectTransform Canvas(string name, int sortingOrder)
    {
        var root = Rect(name, null);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        root.gameObject.AddComponent<GraphicRaycaster>();
        return root;
    }

    public static void Wire(Button button, UnityAction action) => UnityEventTools.AddPersistentListener(button.onClick, action);

    public static void ClearPersistent(Button button)
    {
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
    }

    // Shared frame of the one shop: header, Cá/Cây/Lũa/Đá/San hô tabs, preview area, sheet and footer.
    public sealed class ShopFrame
    {
        public RectTransform content, safe, hero, sheet, footer, modal, dialog;
        public Button back;
        public TMP_Text balance;
        public Button[] tabs;
        public Image[] tabSurfaces;
        public TMP_Text[] tabLabels;
    }

    public static readonly string[] ShopTabs = { "Cá", "Cây", "Lũa", "Đá", "San hô" };

    public static ShopFrame BuildShopFrame(Transform root, int selectedTab, string heading)
    {
        var frame = new ShopFrame();
        var content = Rect("ShopScreen", root);
        Stretch(content);
        frame.content = content;
        var environment = Image("Environment", content, "AquariumEnvironment", false);
        Stretch(environment.rectTransform);
        var fitter = environment.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = environment.sprite.rect.width / environment.sprite.rect.height;
        var scrim = Solid("Scrim", content, new Color(.04f, .09f, .06f, .35f));
        scrim.raycastTarget = true;
        Stretch(scrim.rectTransform);
        var safe = Rect("SafeArea", content);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();
        frame.safe = safe;

        var header = Rect("Header", safe);
        TopStrip(header, 44, 44, 40, 132);
        frame.back = RoundButton("BackButton", header, "utility_back");
        At((RectTransform)frame.back.transform, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(122, 122));
        var title = Text("Title", header, heading, 62, Cream, true, TextAlignmentOptions.MidlineLeft);
        Edges(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(150, 0), new Vector2(-320, 0));
        var wallet = Image("Wallet", header, "ui3d_chip");
        At(wallet.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(300, 122));
        var coin = Icon("Coin", wallet.transform, "utility_coin");
        At(coin.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 3), new Vector2(84, 84));
        frame.balance = Text("Balance", wallet.transform, "1.250", 46, Ink, true, TextAlignmentOptions.MidlineLeft);
        Edges(frame.balance.rectTransform, Vector2.zero, Vector2.one, new Vector2(110, 12), new Vector2(-20, -6));

        var tray = Image("Tabs", safe, "ui3d_tab_tray");
        TopStrip(tray.rectTransform, 44, 44, 196, 132);
        var row = tray.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(14, 14, 12, 20);
        row.spacing = 8;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = true;
        frame.tabs = new Button[ShopTabs.Length];
        frame.tabSurfaces = new Image[ShopTabs.Length];
        frame.tabLabels = new TMP_Text[ShopTabs.Length];
        for (int i = 0; i < ShopTabs.Length; i++)
        {
            bool on = i == selectedTab;
            var tab = Image("Tab" + i, tray.transform, "ui3d_tab_on");
            tab.color = on ? Color.white : Color.clear;
            tab.raycastTarget = true;
            frame.tabs[i] = tab.gameObject.AddComponent<Button>();
            frame.tabs[i].targetGraphic = tab;
            frame.tabs[i].transition = Selectable.Transition.None;
            frame.tabSurfaces[i] = tab;
            frame.tabLabels[i] = Text("Label", tab.transform, ShopTabs[i], 38, on ? Ink : Cream, true);
            Stretch(frame.tabLabels[i].rectTransform, 6);
        }

        var hero = Rect("Selected", safe);
        Edges(hero, new Vector2(0, 1), Vector2.one, new Vector2(44, -760), new Vector2(-44, -350));
        frame.hero = hero;

        var sheet = Panel("SelectionSheet", content);
        Edges(sheet.rectTransform, Vector2.zero, new Vector2(1, .5f), new Vector2(-14, -90), new Vector2(14, 200));
        frame.sheet = sheet.rectTransform;
        var footer = Solid("Footer", content, Hex("FFFAEC"));
        footer.raycastTarget = true;
        BottomStrip(footer.rectTransform, 0, 0, 0, 290);
        var line = Solid("Line", footer.transform, Divider);
        Edges(line.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -4), Vector2.zero);
        frame.footer = footer.rectTransform;

        var modal = Solid("PurchaseConfirmation", content, new Color(.05f, .09f, .06f, .62f));
        modal.raycastTarget = true;
        Stretch(modal.rectTransform);
        frame.modal = modal.rectTransform;
        var dialog = Panel("Dialog", modal.transform);
        At(dialog.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(960, 700));
        frame.dialog = dialog.rectTransform;
        modal.gameObject.SetActive(false);
        return frame;
    }

    public static Color Hex(string value) => ColorUtility.TryParseHtmlString("#" + value, out var color) ? color : Color.magenta;
}
