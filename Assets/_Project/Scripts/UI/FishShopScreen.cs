using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Lumaquarium.UI
{
    [Serializable]
    public sealed class FishShopOffer
    {
        public string id;
        public BettaVariant variant;
        public Sprite portrait;
        public string description;
        public int price;
        public bool IsValid => !string.IsNullOrWhiteSpace(id) && variant != null && portrait != null && price >= 0; // 0 = free gift
    }

    public sealed class FishShopScreen : MonoBehaviour
    {
        public MainAquariumScreen aquarium;
        public BettaAppearance fish;
        public GameObject content;
        public FishShopOffer[] offers;
        public UnityEngine.UI.Button[] optionButtons;
        public UnityEngine.UI.Image[] optionSurfaces;
        public GameObject[] optionChecks;
        public TMP_Text[] optionPrices;
        // Layout 4: card price pills (gold with a coin for a price, green for free or owned).
        public UnityEngine.UI.Image[] optionPricePills;
        public GameObject[] optionPriceCoins;
        public Sprite priceSprite;
        public Sprite freePriceSprite;
        public GameObject priceCoin;
        public Sprite normalSurface;
        public Sprite selectedSurface;
        public UnityEngine.UI.Image heroPortrait;
        public TMP_Text heroName;
        public TMP_Text description;
        public TMP_Text balanceLabel;
        public TMP_Text priceLabel;
        public TMP_Text feedback;
        public UnityEngine.UI.Button buyButton;
        public TMP_Text buyLabel;
        public UnityEngine.UI.Button backButton;
        public GameObject confirmation;
        public TMP_Text confirmationText;
        public UnityEngine.UI.Button confirmButton;
        public UnityEngine.UI.Button cancelButton;
        public TMP_Text aquariumFishName;
        public UnityEngine.UI.Image aquariumPortrait;
        // One shop with Cá/Cây/Lũa/Đá tabs: the last three hand over to the decoration shop.
        public DecorShopScreen decorShop;
        public UnityEngine.UI.Button[] decorTabs;
        public int layoutVersion;

        private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
        private FishShopInventory inventory;
        private int selectedIndex;
        private int pendingIndex = -1;
        private float previousHudAlpha;
        private bool previousHudInteractable;
        private bool previousHudRaycasts;
        private bool previousOrbitEnabled;
        public bool IsOpen => content != null && content.activeSelf;

        private void Awake()
        {
            inventory = new FishShopInventory();
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                optionButtons[i].onClick.AddListener(() => SelectFish(index));
            }
            buyButton.onClick.AddListener(BuyOrEquip);
            backButton.onClick.AddListener(Close);
            confirmButton.onClick.AddListener(ConfirmPurchase);
            cancelButton.onClick.AddListener(CancelPurchase);
            if (decorTabs != null)
                for (int i = 0; i < decorTabs.Length; i++)
                {
                    int category = i;
                    decorTabs[i].onClick.AddListener(() => OpenDecor(category));
                }
        }

        public void OpenDecor(int category)
        {
            if (decorShop == null || confirmation.activeSelf) return;
            decorShop.Open(); // closes this shop first and keeps the HUD state it restored
            decorShop.SelectCategory(category);
        }

        private void Start()
        {
            // The saved fish; with none (new player) or one no longer sold, the tank's default fish.
            int active = Array.FindIndex(offers, o => o.id == inventory.ActiveId);
            if (active < 0 && fish != null) active = Array.FindIndex(offers, o => o.variant == fish.variant);
            if (active >= 0)
            {
                selectedIndex = active;
                ApplyToAquarium(offers[active]);
            }
            Refresh();
        }

        private void Update()
        {
            if (!IsOpen || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (confirmation.activeSelf) CancelPurchase();
            else Close();
        }

        public void Open()
        {
            if (IsOpen) return;
            aquarium.decoration?.Leave();
            aquarium.ClosePanels();
            previousHudAlpha = aquarium.hud.alpha;
            previousHudInteractable = aquarium.hud.interactable;
            previousHudRaycasts = aquarium.hud.blocksRaycasts;
            previousOrbitEnabled = aquarium.orbit != null && aquarium.orbit.enabled;
            aquarium.hud.alpha = 0;
            aquarium.hud.interactable = aquarium.hud.blocksRaycasts = false;
            if (aquarium.orbit != null) aquarium.orbit.enabled = false;
            content.SetActive(true);
            confirmation.SetActive(false);
            feedback.text = "";
            pendingIndex = -1;
            Refresh();
            Focus(optionButtons[selectedIndex].gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            confirmation.SetActive(false);
            pendingIndex = -1;
            content.SetActive(false);
            aquarium.hud.alpha = previousHudAlpha;
            aquarium.hud.interactable = previousHudInteractable;
            aquarium.hud.blocksRaycasts = previousHudRaycasts;
            if (aquarium.orbit != null) aquarium.orbit.enabled = previousOrbitEnabled;
            Focus(null);
        }

        public void SelectFish(int index)
        {
            if (index < 0 || index >= offers.Length || !offers[index].IsValid || confirmation.activeSelf) return;
            selectedIndex = index;
            feedback.text = "";
            Refresh();
        }

        public void BuyOrEquip()
        {
            if (!IsOpen || confirmation.activeSelf) return;
            var offer = offers[selectedIndex];
            if (!offer.IsValid) return;
            if (inventory.Owns(offer.id))
            {
                try
                {
                    if (!inventory.SelectOwned(offer.id)) return;
                    ApplyToAquarium(offer);
                    feedback.text = "Đã chọn " + offer.variant.displayName + " cho hồ của bạn.";
                }
                catch (Exception exception) { SaveFailed(exception); }
                Refresh();
                return;
            }
            if (inventory.Balance < offer.price) return;
            pendingIndex = selectedIndex;
            confirmationText.text = offer.variant.displayName + "\n" + PriceText(offer.price);
            confirmation.SetActive(true);
            Focus(confirmButton.gameObject);
        }

        public void CancelPurchase()
        {
            confirmation.SetActive(false);
            pendingIndex = -1;
            Focus(buyButton.gameObject);
        }

        public void ConfirmPurchase()
        {
            if (!IsOpen || !confirmation.activeSelf || pendingIndex < 0 || pendingIndex >= offers.Length) return;
            var offer = offers[pendingIndex];
            pendingIndex = -1; // Consume the pending action before any save or repeated click.
            confirmation.SetActive(false);
            try
            {
                if (offer.IsValid && inventory.TryPurchase(offer.id, offer.price))
                {
                    ApplyToAquarium(offer);
                    feedback.text = "Đã đón " + offer.variant.displayName + " về hồ!";
                }
            }
            catch (Exception exception) { SaveFailed(exception); }
            Refresh();
            Focus(optionButtons[selectedIndex].gameObject);
        }

        private void ApplyToAquarium(FishShopOffer offer)
        {
            if (!offer.IsValid) return;
            if (fish != null) fish.Apply(offer.variant);
            if (aquariumFishName != null) aquariumFishName.text = offer.variant.displayName;
            if (aquariumPortrait != null) aquariumPortrait.sprite = offer.portrait;
        }

        private void Refresh()
        {
            inventory.Reload();
            balanceLabel.text = Money(inventory.Balance);
            for (int i = 0; i < offers.Length; i++)
            {
                bool selected = i == selectedIndex;
                optionSurfaces[i].sprite = selected ? selectedSurface : normalSurface;
                optionChecks[i].SetActive(selected);
                optionButtons[i].interactable = offers[i].IsValid;
                if (optionPricePills != null && i < optionPricePills.Length) ShowPill(i);
                else optionPrices[i].text = inventory.Owns(offers[i].id) ? "Đã sở hữu" : PriceText(offers[i].price);
            }
            var offer = offers[selectedIndex];
            heroPortrait.sprite = offer.portrait;
            heroName.text = offer.variant != null ? offer.variant.displayName : "Cá chưa sẵn sàng";
            description.text = offer.description;
            bool priced = !inventory.Owns(offer.id) && offer.price > 0;
            if (priceCoin != null)
            {
                priceCoin.SetActive(priced);
                priceLabel.text = priced ? Money(offer.price) : inventory.Owns(offer.id) ? "Đã sở hữu" : "Miễn phí";
            }
            else priceLabel.text = inventory.Owns(offer.id) ? "Đã sở hữu" : PriceText(offer.price);
            bool active = inventory.ActiveId == offer.id;
            bool canAfford = inventory.Balance >= offer.price;
            buyButton.interactable = offer.IsValid && !active && (inventory.Owns(offer.id) || canAfford);
            buyLabel.text = active ? "Đang trong hồ" : inventory.Owns(offer.id) ? "Dùng cá này" : offer.price == 0 ? "Nhận cá" : canAfford ? "Mua cá" : "Chưa đủ xu";
        }

        private void ShowPill(int i)
        {
            var offer = offers[i];
            bool owned = inventory.Owns(offer.id);
            bool priced = !owned && offer.price > 0;
            optionPricePills[i].sprite = priced ? priceSprite : freePriceSprite;
            optionPriceCoins[i].SetActive(!owned);
            optionPrices[i].text = inventory.ActiveId == offer.id ? "Trong hồ" : owned ? "Đã có" : priced ? Money(offer.price) : "Miễn phí";
        }

        private void SaveFailed(Exception exception)
        {
            Debug.LogError("[Lumaquarium] Could not save fish shop: " + exception.Message);
            feedback.text = "Chưa lưu được giao dịch. Vui lòng thử lại.";
        }

        private static string Money(int amount) => amount.ToString("N0", Vietnamese);
        private static string PriceText(int price) => price == 0 ? "Miễn phí" : Money(price) + " xu";
        private static void Focus(GameObject target)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
        }

        private void OnDisable() { if (IsOpen) Close(); }
    }
}
