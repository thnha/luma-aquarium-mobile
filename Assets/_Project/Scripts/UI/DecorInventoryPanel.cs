using TMPro;
using UnityEngine;

namespace Lumaquarium.UI
{
    // The Trang trí sheet: lists the decorations still in storage by group; tapping one starts
    // placement. Items not bought yet, or all placed, stay in the shop.
    public sealed class DecorInventoryPanel : MonoBehaviour
    {
        public MainAquariumScreen aquarium;
        public DecorShopScreen decorShop;
        public ShopScreen shop;
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
        private bool picking;

        private AquariumDecoration Decoration => aquarium != null ? aquarium.decoration : null;

        private void Awake()
        {
            AddMissingCards();
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

        private void OnEnable()
        {
            if (arrangeButton != null) arrangeButton.gameObject.SetActive(!picking);
            Refresh();
        }

        // Opened from Thêm in the arrange screen: a card adds that piece to the session, so Sắp xếp lại is hidden.
        // The button is hidden here too in case the sheet is already open (OnEnable then does not run).
        public void Pick()
        {
            picking = true;
            if (arrangeButton != null) arrangeButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        // The button is not touched here: Unity refuses to activate children while the sheet is deactivating.
        private void OnDisable() => picking = false;

        // A sheet built before newer catalog items gets their cards copied from the first card.
        private void AddMissingCards()
        {
            var catalog = Decoration != null ? Decoration.catalog : null;
            if (catalog == null || cards.Length == 0 || cards.Length >= catalog.offers.Length) return;
            int count = catalog.offers.Length;
            int old = cards.Length;
            System.Array.Resize(ref cards, count);
            System.Array.Resize(ref cardButtons, count);
            System.Array.Resize(ref cardSurfaces, count);
            System.Array.Resize(ref countLabels, count);
            if (countBadges != null && countBadges.Length == old) System.Array.Resize(ref countBadges, count);
            for (int i = old; i < count; i++)
            {
                var offer = catalog.offers[i];
                var card = Instantiate(cards[0], cards[0].transform.parent);
                card.name = "Item_" + offer.id;
                cards[i] = card;
                cardButtons[i] = card.GetComponent<UnityEngine.UI.Button>();
                cardSurfaces[i] = card.GetComponent<UnityEngine.UI.Image>();
                var portrait = card.transform.Find("Portrait");
                if (portrait != null) portrait.GetComponent<UnityEngine.UI.Image>().sprite = offer.portrait;
                var title = card.transform.Find("Name");
                if (title != null) title.GetComponent<TMP_Text>().text = Loc.T(offer.displayName);
                var badge = card.transform.Find("Count");
                if (countBadges != null && countBadges.Length == count && badge != null) countBadges[i] = badge.GetComponent<UnityEngine.UI.Image>();
                countLabels[i] = badge != null ? badge.GetComponentInChildren<TMP_Text>() : card.GetComponentInChildren<TMP_Text>();
            }
        }

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
            int shown = 0;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool on = i == (int)category;
                if (i < tabSurfaces.Length) tabSurfaces[i].color = on ? Color.white : Color.clear;
                if (i < tabLabels.Length) tabLabels[i].color = on ? tabLabelOn : tabLabelOff;
            }
            for (int i = 0; i < cards.Length; i++)
            {
                var offer = catalog != null && i < catalog.offers.Length ? catalog.offers[i] : null;
                int available = offer != null && offer.prefab != null && AquariumDecorCatalog.InventoryGroup(offer.category) == category
                    ? decoration.Available(offer.prefab.kind) : 0;
                cards[i].SetActive(available > 0);
                if (available <= 0) continue;
                shown++;
                countLabels[i].text = "x" + available;
                cardSurfaces[i].sprite = cardNormal;
                if (countBadges != null && i < countBadges.Length) countBadges[i].sprite = badgeStock;
                countLabels[i].color = tabLabelOn;
            }
            if (summary != null && decoration != null)
                summary.text = shown > 0 ? Loc.F("Kho của bạn · trong hồ {0}/{1} điểm", decoration.Points, decoration.capacity)
                    : Loc.T("Kho trống ở mục này · chọn Mua thêm");
        }

        public void Use(int index)
        {
            var decoration = Decoration;
            if (decoration == null || decoration.catalog == null || index < 0 || index >= decoration.catalog.offers.Length) return;
            var offer = decoration.catalog.offers[index];
            if (offer.prefab == null) return;
            if (decoration.Available(offer.prefab.kind) <= 0) { OpenShop(index); return; }
            if (picking) aquarium.ClosePanels();
            decoration.Begin(offer.prefab.kind);
        }

        public void Arrange()
        {
            if (Decoration != null) Decoration.EditExisting();
        }

        // Opens the shop on the current group, and on one item when given.
        public void OpenShop(int index)
        {
            var catalog = Decoration != null ? Decoration.catalog : null;
            if (shop != null)
            {
                string id = catalog != null && index >= 0 && index < catalog.offers.Length ? catalog.offers[index].id : null;
                shop.OpenTab(id != null ? ShopEntry.TabOf(catalog.offers[index].category) : ShopEntry.TabOf(category), id);
                return;
            }
            if (decorShop == null) return;
            decorShop.Open();
            decorShop.SelectCategory((int)category);
            if (index >= 0) decorShop.SelectItem(index);
        }
    }
}
