using Lumaquarium.Shop;
using Lumaquarium.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumaquarium.Ads
{
    // Built from the shop's existing sprites/fonts at runtime, including already-published shop prefabs.
    public sealed class RewardedAdShopPanel : MonoBehaviour
    {
        public const float RowHeight = 104;
        private ShopScreen shop;
        private AdMobRewardedAds ads;
        private CoinRewardEffect coinEffect;
        private Button watch, privacy;
        private TMP_Text label, privacyLabel;
        private bool wasOpen;
        private float refreshStatusAt;
        public bool Active => ads != null;

        public void Initialize(ShopScreen screen)
        {
            shop = screen;
            if (!AdMobRewardedAds.Supported || !ShopService.UsesServer) return;
            ads = AdMobRewardedAds.Get();
            var row = new GameObject("RewardedAds", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(shop.searchRow.parent, false);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(.5f, 1);
            row.offsetMin = new Vector2(44, -286);
            row.offsetMax = new Vector2(-44, -194);
            watch = CreateButton("WatchRewarded", row, out label);
            var rect = (RectTransform)watch.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            privacy = CreateButton("AdPrivacy", row, out privacyLabel);
            privacyLabel.text = Loc.T("Quyền riêng tư");
            var privacyRect = (RectTransform)privacy.transform;
            privacyRect.anchorMin = new Vector2(1, 0);
            privacyRect.anchorMax = Vector2.one;
            privacyRect.offsetMin = new Vector2(-260, 0);
            privacyRect.offsetMax = Vector2.zero;
            watch.onClick.AddListener(Watch);
            privacy.onClick.AddListener(ads.ShowPrivacyOptions);
            var groups = (RectTransform)shop.groupButtons[0].transform.parent.parent;
            groups.anchoredPosition -= new Vector2(0, RowHeight);
            shop.categoryTray.anchoredPosition -= new Vector2(0, RowHeight);
            ads.Changed += Refresh;
            Loc.Changed += Refresh;
            ads.Feedback += ShowFeedback;
            var walletChip = (RectTransform)shop.balanceLabel.transform.parent;
            var walletCoin = walletChip.Find("Coin")?.GetComponent<Image>();
            if (walletCoin != null)
            {
                coinEffect = gameObject.AddComponent<CoinRewardEffect>();
                coinEffect.Setup(walletChip, shop.balanceLabel, walletCoin.sprite);
                ads.RewardGranted += PlayReward;
            }
            ads.Prepare();
            Refresh();
        }

        private Button CreateButton(string name, Transform parent, out TMP_Text text) => CreateButton(shop, name, parent, out text);

        // A secondary-style shop button with a centred label, for the rows added at runtime (ads, coin packs).
        public static Button CreateButton(ShopScreen shop, string name, Transform parent, out TMP_Text text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            var original = shop.secondaryButton.GetComponent<Image>();
            image.sprite = original.sprite;
            image.type = original.type;
            image.color = original.color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = shop.secondaryButton.colors;
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            text = textGo.GetComponent<TMP_Text>();
            text.font = shop.balanceLabel.font;
            text.fontSize = 30;
            text.color = shop.balanceLabel.color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 22;
            text.fontSizeMax = 30;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 6);
            textRect.offsetMax = new Vector2(-12, -6);
            return button;
        }

        private void Update()
        {
            if (ads == null) return;
            if (shop.IsOpen)
            {
                if (!wasOpen) { ads.Prepare(); refreshStatusAt = Time.realtimeSinceStartup + 30; }
                else if (!ads.Busy && Time.realtimeSinceStartup >= refreshStatusAt)
                {
                    refreshStatusAt = Time.realtimeSinceStartup + 30;
                    _ = ads.SyncStatus();
                }
                RefreshLabel();
            }
            else if (coinEffect != null && coinEffect.Playing) coinEffect.Finish();
            wasOpen = shop.IsOpen;
        }

        // A reward claimed again on launch, with the shop closed, only updates the numbers.
        private void PlayReward(int coins)
        {
            if (!shop.IsOpen || coins <= 0) return;
            int balance = shop.Balance;
            coinEffect.Play((RectTransform)watch.transform, balance - coins, balance);
        }

        private void Refresh()
        {
            if (this == null || shop == null) return;
            privacyLabel.text = Loc.T("Quyền riêng tư");
            privacy.gameObject.SetActive(ads.PrivacyRequired);
            ((RectTransform)watch.transform).offsetMax = new Vector2(ads.PrivacyRequired ? -276 : 0, 0);
            privacy.interactable = !ads.Busy && !shop.TransactionBusy;
            if (shop.IsOpen) { shop.aquarium.RefreshWallet(); shop.Refresh(); }
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            watch.interactable = !shop.TransactionBusy && (ads.HasPendingClaim ? !ads.Busy : ads.CanShow);
            string message;
            if (ads.HasPendingClaim) message = ads.Busy ? "Đang nhận thưởng..." : "Thử nhận lại thưởng";
            else if (ads.Busy) message = "Đang chuẩn bị quảng cáo...";
            else if (ads.Status == null) message = "Kết nối server để xem quảng cáo";
            else if (ads.Status.Remaining == 0) message = "Đã hết lượt hôm nay";
            else if (ads.WaitSeconds > 0) { label.text = Loc.F("Xem tiếp sau {0} giây", ads.WaitSeconds); return; }
            else if (!string.IsNullOrEmpty(ads.Error)) message = ads.Error;
            else if (!ads.CanShow) message = "Đang tải quảng cáo...";
            else { label.text = Loc.F("Xem video +{0} xu · Còn {1} lượt", ads.Status.Coins, ads.Status.Remaining); return; }
            label.text = Loc.T(message);
        }

        private async void Watch()
        {
            if (shop.TransactionBusy) return;
            if (ads.HasPendingClaim) { await ads.SyncStatus(); return; }
            ads.Show();
        }

        private void ShowFeedback(string message)
        {
            if (shop != null && shop.IsOpen) shop.feedback.text = Loc.T(message);
        }

        private void OnDestroy()
        {
            if (ads == null) return;
            ads.Changed -= Refresh;
            Loc.Changed -= Refresh;
            ads.Feedback -= ShowFeedback;
            ads.RewardGranted -= PlayReward;
        }
    }
}
