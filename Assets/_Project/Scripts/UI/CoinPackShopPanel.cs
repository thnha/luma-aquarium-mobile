using Lumaquarium.Ads;
using Lumaquarium.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumaquarium.UI
{
    // The App Store coin packs as a row of buttons under the ad row, built at runtime like RewardedAdShopPanel.
    // Each shows its coins and Apple's price in the player's currency; a pack Apple has not sent yet stays off.
    public sealed class CoinPackShopPanel : MonoBehaviour
    {
        public const float RowHeight = 104;
        private ShopScreen shop;
        private IAPManager iap;
        private CoinRewardEffect coinEffect;
        private Button[] buttons;
        private TMP_Text[] labels;
        private bool wasOpen;
        public bool Active => iap != null;

        // below: how far down the rows added before this one reach.
        public void Initialize(ShopScreen screen, float below)
        {
            shop = screen;
            iap = IAPManager.Get();
            if (iap == null) return;
            var row = new GameObject("CoinPacks", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(shop.searchRow.parent, false);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(.5f, 1);
            row.offsetMin = new Vector2(44, -286 - below);
            row.offsetMax = new Vector2(-44, -194 - below);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var packs = IAPManager.Packs;
            buttons = new Button[packs.Count];
            labels = new TMP_Text[packs.Count];
            for (int i = 0; i < packs.Count; i++)
            {
                string productId = packs[i].productId;
                buttons[i] = RewardedAdShopPanel.CreateButton(shop, "CoinPack" + packs[i].coins, row, out labels[i]);
                buttons[i].onClick.AddListener(() => { if (!shop.TransactionBusy) iap.Buy(productId); });
            }
            var groups = (RectTransform)shop.groupButtons[0].transform.parent.parent;
            groups.anchoredPosition -= new Vector2(0, RowHeight);
            shop.categoryTray.anchoredPosition -= new Vector2(0, RowHeight);
            iap.Changed += Refresh;
            iap.Feedback += ShowFeedback;
            iap.CoinsGranted += PlayReward;
            Loc.Changed += Refresh;
            var walletChip = (RectTransform)shop.balanceLabel.transform.parent;
            var walletCoin = walletChip.Find("Coin")?.GetComponent<Image>();
            if (walletCoin != null)
            {
                coinEffect = gameObject.AddComponent<CoinRewardEffect>();
                coinEffect.Setup(walletChip, shop.balanceLabel, walletCoin.sprite);
            }
            iap.Prepare();
            Refresh();
        }

        private void Update()
        {
            if (iap == null) return;
            if (shop.IsOpen && !wasOpen) iap.Prepare(); // reconnects, or sends again a purchase the server has not taken
            wasOpen = shop.IsOpen;
        }

        private void Refresh()
        {
            if (this == null || shop == null) return;
            var packs = IAPManager.Packs;
            for (int i = 0; i < packs.Count; i++)
            {
                string price = iap.Price(packs[i].productId);
                labels[i].text = Loc.F("{0} xu", Loc.Money(packs[i].coins)) + "\n<size=80%>" + (price ?? "…") + "</size>";
                buttons[i].interactable = price != null && !iap.Busy && !shop.TransactionBusy;
            }
            if (shop.IsOpen) { shop.aquarium.RefreshWallet(); shop.Refresh(); }
        }

        private void PlayReward(string productId, int coins)
        {
            if (coinEffect == null || !shop.IsOpen || coins <= 0) return;
            int index = IAPManager.Packs.FindIndex(p => p.productId == productId);
            int balance = shop.Balance;
            coinEffect.Play((RectTransform)buttons[Mathf.Max(0, index)].transform, balance - coins, balance);
        }

        private void ShowFeedback(string message)
        {
            if (shop != null && shop.IsOpen) shop.feedback.text = message;
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
