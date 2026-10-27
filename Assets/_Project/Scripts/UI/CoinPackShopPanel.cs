using Lumaquarium.Ads;
using Lumaquarium.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumaquarium.UI
{
    // Real store packs live in the wallet sheet, leaving the catalog free of payment controls.
    // Each shows its coins and Apple's price in the player's currency; a pack Apple has not sent yet stays off.
    public sealed class CoinPackShopPanel : MonoBehaviour
    {
        private ShopScreen shop;
        private IAPManager iap;
        private CoinRewardEffect coinEffect;
        private Button[] buttons;
        private TMP_Text[] labels;
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
            var row = new GameObject("Packs", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(shop.coinPackArea, false);
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.offsetMin = row.offsetMax = Vector2.zero;
            var layout = row.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 20;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var packs = IAPManager.Packs;
            buttons = new Button[packs.Count];
            labels = new TMP_Text[packs.Count];
            for (int i = 0; i < packs.Count; i++)
            {
                string productId = packs[i].productId;
                buttons[i] = RewardedAdShopPanel.CreateButton(shop, "CoinPack" + packs[i].coins, row, out labels[i]);
                labels[i].fontSizeMin = 34;
                labels[i].fontSizeMax = 46;
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
            var walletChip = (RectTransform)shop.walletBalanceLabel.transform.parent;
            var walletCoin = walletChip.Find("Coin")?.GetComponent<Image>();
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
                labels[i].text = Loc.F("{0} xu", Loc.Money(packs[i].coins)) + "\n<size=80%>" + (price ?? Loc.T("Đang kết nối cửa hàng...")) + "</size>";
                buttons[i].interactable = price != null && !iap.Busy && !shop.TransactionBusy;
            }
            if (shop.IsOpen) { shop.aquarium.RefreshWallet(); shop.Refresh(); }
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
