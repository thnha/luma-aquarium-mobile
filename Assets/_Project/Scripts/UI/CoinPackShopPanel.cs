using Lumaquarium.Ads;
using Lumaquarium.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumaquarium.UI
{
    // Real store packs live in the wallet sheet, leaving the catalog free of payment controls. The packs are three
    // cards side by side: picture (Resources/CoinPacks/pack_<size>), size name, coins, and Apple's price in the
    // player's currency on a primary pill; Medium and Big carry a badge, Big a green frame. A pack Apple has not sent
    // yet stays off.
    public sealed class CoinPackShopPanel : MonoBehaviour
    {
        private ShopScreen shop;
        private IAPManager iap;
        private CoinRewardEffect coinEffect;
        private static readonly string[] Pictures = { "pack_small", "pack_medium", "pack_big" };
        private static readonly string[] Sizes = { "Nhỏ", "Vừa", "Lớn" };
        private static readonly string[] Badges = { null, "Phổ biến", "Lợi nhất" };
        private static readonly Color Dark = new Color32(44, 50, 38, 255), Muted = new Color32(128, 116, 92, 255),
            Gold = new Color32(236, 176, 52, 255), TileTint = new Color32(250, 234, 190, 255);
        private Button[] buttons;
        private TMP_Text[] sizeLabels, kindLabels, coinLabels, badgeLabels, priceLabels;
        private Image[] pills;
        private Sprite coinSprite;
        private bool wasOpen;
        private bool wasWalletOpen;
        public bool Active => iap != null;
        public bool Busy => iap != null && iap.Busy;

        public void Initialize(ShopScreen screen)
        {
            shop = screen;
            iap = IAPManager.Get();
            if (iap == null) return;
            shop.coinPackArea.Find("Unavailable").gameObject.SetActive(false);
            var row = new GameObject("Packs", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(shop.coinPackArea, false);
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.offsetMin = Vector2.zero;
            row.offsetMax = new Vector2(0, -26); // room for the badges over the cards' top edge
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 22;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var packs = IAPManager.Packs;
            buttons = new Button[packs.Count];
            sizeLabels = new TMP_Text[packs.Count];
            kindLabels = new TMP_Text[packs.Count];
            coinLabels = new TMP_Text[packs.Count];
            badgeLabels = new TMP_Text[packs.Count];
            priceLabels = new TMP_Text[packs.Count];
            pills = new Image[packs.Count];
            var walletChip = (RectTransform)shop.walletBalanceLabel.transform.parent;
            var walletCoin = walletChip.Find("Coin")?.GetComponent<Image>();
            coinSprite = walletCoin != null ? walletCoin.sprite : null;
            for (int i = 0; i < packs.Count; i++)
            {
                string productId = packs[i].productId;
                buttons[i] = CreateCard(i, packs[i], row);
                buttons[i].onClick.AddListener(() =>
                {
                    if (shop.WalletOpen && shop.coinPackArea.gameObject.activeInHierarchy && !shop.TransactionBusy)
                        iap.Buy(productId);
                });
            }
            iap.Changed += Refresh;
            iap.Feedback += ShowFeedback;
            iap.CoinsGranted += PlayReward;
            Loc.Changed += Refresh;
            if (walletCoin != null)
            {
                coinEffect = gameObject.AddComponent<CoinRewardEffect>();
                coinEffect.Setup(walletChip, shop.walletBalanceLabel, walletCoin.sprite);
            }
            iap.Prepare();
            Refresh();
        }

        private void Update()
        {
            if (iap == null) return;
            if (shop.IsOpen && !wasOpen) iap.Prepare(); // reconnects, or sends again a purchase the server has not taken
            if (shop.WalletOpen && !wasWalletOpen) Refresh();
            if (!shop.WalletOpen && coinEffect != null && coinEffect.Playing) coinEffect.Finish();
            wasOpen = shop.IsOpen;
            wasWalletOpen = shop.WalletOpen;
        }

        private void Refresh()
        {
            if (this == null || shop == null) return;
            var packs = IAPManager.Packs;
            for (int i = 0; i < packs.Count; i++)
            {
                string price = iap.Price(packs[i].productId);
                bool on = price != null && !iap.Busy && !shop.TransactionBusy;
                int k = Mathf.Min(i, Sizes.Length - 1);
                sizeLabels[i].text = Loc.T(Sizes[k]);
                kindLabels[i].text = Loc.T("Gói xu");
                coinLabels[i].text = Loc.Money(packs[i].coins);
                if (badgeLabels[i] != null) badgeLabels[i].text = Loc.T(Badges[k]);
                priceLabels[i].text = price ?? "…";
                buttons[i].interactable = on;
                pills[i].color = on ? Color.white : new Color(1, 1, 1, .45f);
            }
            if (shop.IsOpen) { shop.aquarium.RefreshWallet(); shop.Refresh(); }
        }

        private Button CreateCard(int index, CoinPack pack, Transform parent)
        {
            int k = Mathf.Min(index, Sizes.Length - 1);
            bool best = k == Sizes.Length - 1;
            // The root takes the touches; the surface (and Big's green frame behind it) are its children.
            var card = Child("Pack" + pack.coins, parent, typeof(Image), typeof(Button));
            card.GetComponent<Image>().color = Color.clear;
            if (best)
            {
                var frame = Child("Frame", card, typeof(Image)).GetComponent<Image>();
                Place(frame.rectTransform, Vector2.zero, Vector2.one, new Vector2(-6, -6), new Vector2(6, 6));
                frame.sprite = shop.cardSelected;
                frame.type = Image.Type.Sliced;
                frame.raycastTarget = false;
            }
            var surface = Child("Surface", card, typeof(Image)).GetComponent<Image>();
            Place(surface.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            surface.sprite = shop.cardNormal;
            surface.type = Image.Type.Sliced;
            surface.raycastTarget = false;
            var button = card.GetComponent<Button>();
            button.targetGraphic = surface;

            // Picture on a light gold tile, filling what the text below leaves.
            var tile = Child("Tile", card, typeof(Image)).GetComponent<Image>();
            Place(tile.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 246), new Vector2(-18, -34)); // below the badge
            tile.sprite = shop.cardNormal;
            tile.type = Image.Type.Sliced;
            tile.color = TileTint;
            tile.raycastTarget = false;
            var picture = Child("Picture", tile.transform, typeof(RawImage)).GetComponent<RawImage>();
            Place(picture.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            var fit = picture.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1;
            picture.texture = Resources.Load<Texture2D>("CoinPacks/" + Pictures[k]);
            picture.raycastTarget = false;

            // From the bottom: price pill, coins, "Coins Pack", size name.
            var pill = Child("Price", card, typeof(Image));
            Place(pill, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 16), new Vector2(-16, 88));
            pills[index] = pill.GetComponent<Image>();
            var primary = shop.primaryButton.GetComponent<Image>();
            pills[index].sprite = primary.sprite;
            pills[index].type = primary.type;
            pills[index].raycastTarget = false;
            RoundEnds(pills[index], 72);
            priceLabels[index] = Text("Label", pill, shop.primaryLabel.color, 32, TextAlignmentOptions.Center);
            Place(priceLabels[index].rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -4));

            var coins = Child("Coins", card, typeof(HorizontalLayoutGroup));
            Place(coins, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 100), new Vector2(-10, 152));
            var coinsLayout = coins.GetComponent<HorizontalLayoutGroup>();
            coinsLayout.childAlignment = TextAnchor.MiddleCenter;
            coinsLayout.spacing = 8;
            coinsLayout.childControlWidth = coinsLayout.childControlHeight = true;
            coinsLayout.childForceExpandWidth = coinsLayout.childForceExpandHeight = false;
            if (coinSprite != null)
            {
                var icon = Child("Coin", coins, typeof(Image), typeof(LayoutElement));
                icon.GetComponent<Image>().sprite = coinSprite;
                icon.GetComponent<Image>().raycastTarget = false;
                var size = icon.GetComponent<LayoutElement>();
                size.preferredWidth = size.preferredHeight = 40;
            }
            coinLabels[index] = Text("Amount", coins, Dark, 44, TextAlignmentOptions.Center);
            coinLabels[index].enableAutoSizing = false; // the layout sizes the row from the text's own width

            kindLabels[index] = Text("Kind", card, Muted, 24, TextAlignmentOptions.Center);
            Place(kindLabels[index].rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 152), new Vector2(-10, 184));
            sizeLabels[index] = Text("Size", card, Muted, 34, TextAlignmentOptions.Center);
            Place(sizeLabels[index].rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 184), new Vector2(-10, 232));

            // Badge across the top edge.
            if (Badges[k] != null)
            {
                var badge = Child("Badge", card, typeof(Image)).GetComponent<Image>();
                Place(badge.rectTransform, new Vector2(.08f, 1), new Vector2(.92f, 1), new Vector2(0, -22), new Vector2(0, 24));
                badge.sprite = best ? shop.chipSelected : shop.chipNormal;
                badge.type = Image.Type.Sliced;
                badge.color = best ? Color.white : Gold;
                badge.raycastTarget = false;
                RoundEnds(badge, 46);
                badgeLabels[index] = Text("Label", badge.transform, best ? shop.primaryLabel.color : Color.white, 26, TextAlignmentOptions.Center);
                Place(badgeLabels[index].rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            }
            return button;
        }

        // A sliced pill shorter than its sprite's caps squeezes them into points: shrink the caps to half its height.
        private static void RoundEnds(Image image, float height)
        {
            var border = image.sprite != null ? image.sprite.border : Vector4.zero;
            if (border.y + border.w > height) image.pixelsPerUnitMultiplier = (border.y + border.w) / height;
        }

        private static RectTransform Child(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (var c in components) go.AddComponent(c);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private TMP_Text Text(string name, Transform parent, Color color, float size, TextAlignmentOptions align)
        {
            var text = Child(name, parent, typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.font = shop.balanceLabel.font;
            text.color = color;
            text.fontSize = size;
            text.alignment = align;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * .6f;
            text.fontSizeMax = size;
            text.raycastTarget = false;
            return text;
        }

        private void PlayReward(string productId, int coins)
        {
            if (coinEffect == null || !shop.WalletOpen || !shop.coinPackArea.gameObject.activeInHierarchy || coins <= 0) return;
            int index = IAPManager.Packs.FindIndex(p => p.productId == productId);
            int balance = shop.Balance;
            coinEffect.Play((RectTransform)buttons[Mathf.Max(0, index)].transform, balance - coins, balance);
        }

        private void ShowFeedback(string message)
        {
            if (shop != null) shop.ShowWalletFeedback(message);
        }

        private void OnDestroy()
        {
            if (iap == null) return;
            iap.Changed -= Refresh;
            iap.Feedback -= ShowFeedback;
            iap.CoinsGranted -= PlayReward;
            Loc.Changed -= Refresh;
        }
    }
}
