using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumaquarium.UI
{
    public sealed class DecorShopScreen : MonoBehaviour
    {
        public AquariumDecorCatalog catalog;
        public MainAquariumScreen aquarium;
        public FishShopScreen fishShop;
        public GameObject content, confirmation;
        public UnityEngine.UI.Button backButton, buyButton, placeButton, confirmButton, cancelButton, fishButton, stockButton;
        public UnityEngine.UI.Button[] categoryButtons, optionButtons;
        public GameObject[] cards;
        public UnityEngine.UI.Image[] cardSurfaces;
        public TMP_Text[] stockLabels;
        public TMP_Text[] priceLabels; // layout 4: the price sits in its own pill, stock stays in stockLabels
        public UnityEngine.UI.Image portrait;
        public TMP_Text title, description, balanceLabel, priceLabel, feedback, confirmationText, stockButtonLabel;
        public Sprite normalSurface, selectedSurface;
        // Shared shop tabs: categoryButtons are Cây/Lũa/Đá/San hô, fishButton is the Cá tab.
        public UnityEngine.UI.Image[] categorySurfaces;
        public TMP_Text[] categoryLabels;
        public Color tabLabelOn = new Color32(52, 60, 45, 255);
        public Color tabLabelOff = new Color32(255, 248, 230, 255);
        public int layoutVersion;
        private FishShopInventory inventory;
        private AquariumDecorCategory category;
        private int selectedIndex, pendingIndex = -1;
        private bool stockOnly, previousOrbit;
        private float previousAlpha;
        private bool previousInteractable, previousRaycasts;
        public bool IsOpen => content != null && content.activeSelf;
        public int SelectedIndex => selectedIndex;

        private void Awake()
        {
            for (int i = 0; i < optionButtons.Length; i++)
            { int index = i; optionButtons[i].onClick.AddListener(() => SelectItem(index)); }
            for (int i = 0; i < categoryButtons.Length; i++)
            { int index = i; categoryButtons[i].onClick.AddListener(() => SelectCategory(index)); }
            backButton.onClick.AddListener(Close);
            buyButton.onClick.AddListener(RequestPurchase);
            placeButton.onClick.AddListener(PlaceSelected);
            confirmButton.onClick.AddListener(ConfirmPurchase);
            cancelButton.onClick.AddListener(CancelPurchase);
            fishButton.onClick.AddListener(() => { Close(); fishShop.Open(); });
            stockButton.onClick.AddListener(() => { stockOnly = !stockOnly; Refresh(); });
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { if (confirmation.activeSelf) CancelPurchase(); else Close(); }
        }

        public void Open()
        {
            if (IsOpen) return;
            if (fishShop != null && fishShop.IsOpen) fishShop.Close();
            aquarium.decoration.Cancel();
            aquarium.ClosePanels();
            previousAlpha = aquarium.hud.alpha;
            previousInteractable = aquarium.hud.interactable;
            previousRaycasts = aquarium.hud.blocksRaycasts;
            previousOrbit = aquarium.orbit.enabled;
            aquarium.hud.alpha = 0;
            aquarium.hud.interactable = aquarium.hud.blocksRaycasts = false;
            aquarium.orbit.enabled = false;
            content.SetActive(true);
            confirmation.SetActive(false);
            pendingIndex = -1;
            feedback.text = "Mỗi lần mua nhận 1 món · Có thể mua nhiều lần";
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            CancelPurchase();
            content.SetActive(false);
            if (aquarium != null)
            {
                if (aquarium.hud != null)
                {
                    aquarium.hud.alpha = previousAlpha;
                    aquarium.hud.interactable = previousInteractable;
                    aquarium.hud.blocksRaycasts = previousRaycasts;
                }
                if (aquarium.orbit != null) aquarium.orbit.enabled = previousOrbit;
            }
        }

        public void SelectCategory(int index)
        {
            if (confirmation.activeSelf || index < 0 || index > (int)AquariumDecorCategory.Coral) return;
            category = (AquariumDecorCategory)index;
            selectedIndex = Array.FindIndex(catalog.offers, x => x.category == category);
            Refresh();
            var scroll = cards[0].GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        public void SelectItem(int index)
        {
            if (confirmation.activeSelf || index < 0 || index >= catalog.offers.Length) return;
            selectedIndex = index;
            Refresh();
        }

        public void RequestPurchase()
        {
            if (!IsOpen || confirmation.activeSelf || selectedIndex < 0) return;
            var offer = catalog.offers[selectedIndex];
            RefreshInventory();
            if (!offer.IsValid || inventory.Balance < offer.price) return;
            pendingIndex = selectedIndex;
            confirmationText.text = "1 × " + offer.displayName + "\n" + offer.price + " xu\nMua thêm sẽ cộng thêm 1 món vào kho.";
            confirmation.SetActive(true);
        }

        public void CancelPurchase()
        {
            pendingIndex = -1;
            if (confirmation != null) confirmation.SetActive(false);
        }

        public void ConfirmPurchase()
        {
            if (!IsOpen || !confirmation.activeSelf || pendingIndex < 0) return;
            int index = pendingIndex;
            CancelPurchase();
            var offer = catalog.offers[index];
            try
            {
                RefreshInventory();
                feedback.text = offer.IsValid && inventory.TryPurchaseDecor(offer.id, offer.price)
                    ? "Đã thêm 1 " + offer.displayName + " vào kho. Chọn Đặt vào hồ để sử dụng."
                    : "Chưa đủ xu để mua món này.";
            }
            catch (Exception exception)
            {
                Debug.LogError("[Lumaquarium] Decor purchase failed: " + exception.Message);
                feedback.text = "Chưa lưu được giao dịch. Vui lòng thử lại.";
            }
            Refresh();
        }

        public void PlaceSelected()
        {
            if (!IsOpen || confirmation.activeSelf || selectedIndex < 0) return;
            int kind = catalog.offers[selectedIndex].prefab.kind;
            if (aquarium.decoration.Available(kind) <= 0) return;
            if (aquarium.decoration.Count >= aquarium.decoration.capacity)
            { feedback.text = "Hồ đã đầy. Cất bớt đồ trước khi đặt thêm."; return; }
            Close();
            aquarium.decoration.Begin(kind);
        }

        private void RefreshInventory() => inventory = new FishShopInventory(aquarium.decoration.inventoryStorageKey);

        public void Refresh()
        {
            RefreshInventory();
            balanceLabel.text = inventory.Balance.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + (layoutVersion >= 2 ? "" : " xu");
            stockButtonLabel.text = stockOnly ? "Hiện tất cả" : "Chỉ món trong kho";
            for (int i = 0; i < catalog.offers.Length; i++)
            {
                var offer = catalog.offers[i];
                int total = inventory.DecorCount(offer.id);
                int available = aquarium.decoration.Available(offer.prefab.kind);
                cards[i].SetActive(offer.category == category && (!stockOnly || total > 0));
                cardSurfaces[i].sprite = i == selectedIndex ? selectedSurface : normalSurface;
                bool pills = priceLabels != null && i < priceLabels.Length;
                if (pills) priceLabels[i].text = Money(offer.price);
                stockLabels[i].text = pills ? $"Kho {available} · Hồ {total - available}"
                    : layoutVersion >= 2 ? $"{offer.price} xu · Kho {available} · Hồ {total - available}"
                    : $"{offer.price} xu / món\nKho: {available} · Trong hồ: {total - available}";
            }
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                bool on = i == (int)category;
                if (categorySurfaces != null && i < categorySurfaces.Length)
                {
                    categorySurfaces[i].color = on ? Color.white : Color.clear;
                    if (categoryLabels != null && i < categoryLabels.Length) categoryLabels[i].color = on ? tabLabelOn : tabLabelOff;
                }
                else categoryButtons[i].GetComponent<UnityEngine.UI.Image>().sprite = on ? selectedSurface : normalSurface;
            }
            if (selectedIndex < 0) return;
            var selected = catalog.offers[selectedIndex];
            portrait.sprite = selected.portrait;
            title.text = selected.displayName;
            description.text = selected.description;
            int remaining = aquarium.decoration.Available(selected.prefab.kind);
            priceLabel.text = layoutVersion >= 4 ? Money(selected.price) : layoutVersion >= 2 ? $"{selected.price} xu" : $"{selected.price} xu / món · Kho: {remaining}";
            buyButton.interactable = selected.IsValid && inventory.Balance >= selected.price;
            buyButton.GetComponentInChildren<TMP_Text>().text = buyButton.interactable ? (layoutVersion >= 2 ? "Mua 1" : "Mua 1 món") : "Chưa đủ xu";
            placeButton.interactable = remaining > 0;
            placeButton.GetComponentInChildren<TMP_Text>().text = layoutVersion >= 2
                ? (remaining > 0 ? $"Đặt ({remaining})" : "Kho trống")
                : (remaining > 0 ? "Đặt vào hồ" : "Chưa có trong kho");
        }

        private static string Money(int amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

        private void OnDisable() { if (IsOpen) Close(); }
    }
}
