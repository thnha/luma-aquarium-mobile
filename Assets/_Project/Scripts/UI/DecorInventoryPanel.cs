using TMPro;
using UnityEngine;

namespace Lumaquarium.UI
{
    // The Trang trí sheet: lists owned decorations by group. A card with stock starts
    // placement; an empty one opens the shop on that item.
    public sealed class DecorInventoryPanel : MonoBehaviour
    {
        public MainAquariumScreen aquarium;
        public DecorShopScreen decorShop;
        public UnityEngine.UI.Button[] tabButtons;
        public UnityEngine.UI.Image[] tabSurfaces;
        public TMP_Text[] tabLabels;
        public Color tabLabelOn = new Color32(255, 248, 230, 255);
        public Color tabLabelOff = new Color32(74, 80, 63, 255);
        public GameObject[] cards;
        public UnityEngine.UI.Button[] cardButtons;
        public UnityEngine.UI.Image[] cardSurfaces;
        public TMP_Text[] countLabels;
        public UnityEngine.UI.Image[] countBadges;
        public Sprite cardNormal, cardEmpty, badgeStock, badgeEmpty;
        public TMP_Text summary;
        public UnityEngine.UI.Button arrangeButton, shopButton;
        private AquariumDecorCategory category;

        private AquariumDecoration Decoration => aquarium != null ? aquarium.decoration : null;

        private void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => SelectCategory(index));
            }
            for (int i = 0; i < cardButtons.Length; i++)
            {
                int index = i;
                cardButtons[i].onClick.AddListener(() => Use(index));
            }
            if (arrangeButton != null) arrangeButton.onClick.AddListener(Arrange);
            if (shopButton != null) shopButton.onClick.AddListener(() => OpenShop(-1));
        }

        private void OnEnable() => Refresh();

        public void SelectCategory(int index)
        {
            if (index < 0 || index > (int)AquariumDecorCategory.Coral) return;
            category = (AquariumDecorCategory)index;
            Refresh();
            var scroll = GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        public void Refresh()
        {
            var decoration = Decoration;
            var catalog = decoration != null ? decoration.catalog : null;
            if (summary != null && decoration != null)
                summary.text = $"Kho của bạn · trong hồ {decoration.Count}/{decoration.capacity} món";
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool on = i == (int)category;
                if (i < tabSurfaces.Length) tabSurfaces[i].color = on ? Color.white : Color.clear;
                if (i < tabLabels.Length) tabLabels[i].color = on ? tabLabelOn : tabLabelOff;
            }
            for (int i = 0; i < cards.Length; i++)
            {
                var offer = catalog != null && i < catalog.offers.Length ? catalog.offers[i] : null;
                bool visible = offer != null && offer.prefab != null && offer.category == category;
                cards[i].SetActive(visible);
                if (!visible) continue;
                int available = decoration.Available(offer.prefab.kind);
                countLabels[i].text = available > 0 ? "x" + available : "Mua";
                cardSurfaces[i].sprite = available > 0 ? cardNormal : cardEmpty;
                if (countBadges != null && i < countBadges.Length)
                    countBadges[i].sprite = available > 0 ? badgeStock : badgeEmpty;
                countLabels[i].color = available > 0 ? tabLabelOn : tabLabelOff;
            }
        }

        public void Use(int index)
        {
            var decoration = Decoration;
            if (decoration == null || decoration.catalog == null || index < 0 || index >= decoration.catalog.offers.Length) return;
            var offer = decoration.catalog.offers[index];
            if (offer.prefab == null) return;
            if (decoration.Available(offer.prefab.kind) > 0) decoration.Begin(offer.prefab.kind);
            else OpenShop(index);
        }

        public void Arrange()
        {
            if (Decoration != null) Decoration.EditExisting();
        }

        // Opens the shop on the current group, and on one item when given.
        public void OpenShop(int index)
        {
            if (decorShop == null) return;
            decorShop.Open();
            decorShop.SelectCategory((int)category);
            if (index >= 0) decorShop.SelectItem(index);
        }
    }
}
