using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumaquarium.UI
{
    // The one shop (layout 3): search, a sliding row of tabs (ShopTab order), a price sort and a grid of cards
    // made from a template. A tapped card shows in the details bar, whose buttons buy, place a decoration,
    // or put a fish, wall or floor style into use. Purchases with a price ask for confirmation.
    public sealed class ShopScreen : MonoBehaviour
    {
        public MainAquariumScreen aquarium;
        public BettaAppearance fish;
        public FishShopOffer[] fishOffers;
        public GameObject content, confirmation, details, emptyLabel, priceCoin;
        public TMP_InputField search;
        public UnityEngine.UI.Button clearSearch, backButton, sortButton, primaryButton, secondaryButton, confirmButton, cancelButton;
        public TMP_Text balanceLabel, sortLabel, title, description, priceLabel, feedback, confirmationText, primaryLabel, secondaryLabel;
        public UnityEngine.UI.Image portrait;
        public UnityEngine.UI.Button[] tabButtons;
        public UnityEngine.UI.Image[] tabSurfaces;
        public TMP_Text[] tabLabels;
        public ShopCard cardTemplate;
        public UnityEngine.UI.ScrollRect scroll;
        public Sprite cardNormal, cardSelected;
        public Color tabLabelOn = new Color32(52, 60, 45, 255);
        public Color tabLabelOff = new Color32(255, 248, 230, 255);

        private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
        private static readonly string[] SortNames = { "Mặc định", "Giá: Thấp → Cao", "Giá: Cao → Thấp" };
        private readonly List<ShopCard> cards = new List<ShopCard>();
        private List<ShopEntry> entries, visible = new List<ShopEntry>();
        private FishShopInventory inventory;
        private Dictionary<int, int> placed = new Dictionary<int, int>();
        private ShopTab tab;
        private ShopSort sort;
        private ShopEntry selected, pending;
        private float previousAlpha;
        private bool previousInteractable, previousRaycasts, previousOrbit;

        public bool IsOpen => content != null && content.activeSelf;
        private AquariumDecoration Decoration => aquarium != null ? aquarium.decoration : null;
        private List<ShopEntry> Entries => entries ??= ShopEntry.Build(fishOffers,
            Decoration != null ? Decoration.catalog : null, Decoration != null ? Decoration.surfaces : null);

        private void Awake()
        {
            inventory = new FishShopInventory(Decoration != null ? Decoration.inventoryStorageKey : FishShopInventory.DefaultStorageKey);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                var target = (ShopTab)i;
                tabButtons[i].onClick.AddListener(() => SelectTab(target));
            }
            search.onValueChanged.AddListener(_ => Rebuild(true));
            clearSearch.onClick.AddListener(() => search.text = "");
            backButton.onClick.AddListener(Close);
            sortButton.onClick.AddListener(() => { if (!confirmation.activeSelf) { sort = (ShopSort)(((int)sort + 1) % 3); Rebuild(true); } });
            primaryButton.onClick.AddListener(Primary);
            secondaryButton.onClick.AddListener(() => { if (selected != null && selected.kind == ShopKind.Decor) RequestPurchase(selected); });
            confirmButton.onClick.AddListener(ConfirmPurchase);
            cancelButton.onClick.AddListener(CancelPurchase);
            cardTemplate.gameObject.SetActive(false);
        }

        // The saved fish swims in the tank from the start; with none, the tank keeps its default fish.
        private void Start()
        {
            inventory.Reload(); // AquariumDecoration.Awake may have given the gift packs since Awake
            var active = fishOffers != null ? Array.Find(fishOffers, o => o != null && o.IsValid && o.id == inventory.ActiveId) : null;
            if (active == null && fish != null && fishOffers != null)
                active = Array.Find(fishOffers, o => o != null && o.IsValid && o.variant == fish.variant);
            if (active != null) ApplyFish(active);
        }

        // Centres the active tab in the sliding strip.
        private void ShowActiveTab()
        {
            var strip = tabButtons[0].GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (strip == null) return;
            Canvas.ForceUpdateCanvases();
            var current = (RectTransform)tabButtons[(int)tab].transform;
            float room = strip.content.rect.width - strip.viewport.rect.width;
            if (room <= 0) return;
            strip.horizontalNormalizedPosition = Mathf.Clamp01((current.anchoredPosition.x - current.pivot.x * current.rect.width + current.rect.width / 2 - strip.viewport.rect.width / 2) / room);
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
            if (Decoration != null) Decoration.Cancel();
            aquarium.ClosePanels();
            previousAlpha = aquarium.hud.alpha;
            previousInteractable = aquarium.hud.interactable;
            previousRaycasts = aquarium.hud.blocksRaycasts;
            previousOrbit = aquarium.orbit != null && aquarium.orbit.enabled;
            aquarium.hud.alpha = 0;
            aquarium.hud.interactable = aquarium.hud.blocksRaycasts = false;
            if (aquarium.orbit != null) aquarium.orbit.enabled = false;
            content.SetActive(true);
            AquariumAudio.Muffle(true);
            confirmation.SetActive(false);
            pending = null;
            feedback.text = "";
            tab = ShopTab.All;
            sort = ShopSort.Default;
            selected = null;
            search.SetTextWithoutNotify("");
            Rebuild(true);
            ShowActiveTab();
        }

        // Opens on one tab, and on one item when its id is given (the Trang trí sheet's Mua thêm).
        public void OpenTab(ShopTab target, string id = null)
        {
            Open();
            tab = target;
            selected = id != null ? Entries.Find(e => e.id == id) : null;
            Rebuild(true);
            ShowActiveTab();
        }

        public void Close()
        {
            if (!IsOpen) return;
            CancelPurchase();
            content.SetActive(false);
            AquariumAudio.Muffle(false);
            if (aquarium == null) return;
            if (aquarium.hud != null)
            {
                aquarium.hud.alpha = previousAlpha;
                aquarium.hud.interactable = previousInteractable;
                aquarium.hud.blocksRaycasts = previousRaycasts;
            }
            if (aquarium.orbit != null) aquarium.orbit.enabled = previousOrbit;
        }

        private void SelectTab(ShopTab target)
        {
            if (confirmation.activeSelf) return;
            tab = target;
            Rebuild(true);
            ShowActiveTab();
        }

        private void Select(int index)
        {
            if (confirmation.activeSelf || index >= visible.Count) return;
            selected = visible[index];
            feedback.text = "";
            Refresh();
        }

        // Re-runs tab / search / sort; keeps the selection while it is still listed, else selects the first card.
        private void Rebuild(bool toTop)
        {
            visible = ShopQuery.Filter(Entries, tab, search.text, sort);
            if (selected == null || !visible.Contains(selected)) selected = visible.Count > 0 ? visible[0] : null;
            while (cards.Count < visible.Count)
            {
                var card = Instantiate(cardTemplate, cardTemplate.transform.parent);
                int index = cards.Count;
                card.button.onClick.AddListener(() => Select(index));
                cards.Add(card);
            }
            if (toTop && scroll != null) scroll.verticalNormalizedPosition = 1;
            Refresh();
        }

        public void Refresh()
        {
            inventory.Reload();
            placed = Decoration != null ? Decoration.PlacedCounts() : placed;
            balanceLabel.text = Money(inventory.Balance);
            sortLabel.text = SortNames[(int)sort];
            clearSearch.gameObject.SetActive(!string.IsNullOrEmpty(search.text));
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool on = i == (int)tab;
                if (i < tabSurfaces.Length) tabSurfaces[i].color = on ? Color.white : Color.clear;
                if (i < tabLabels.Length) tabLabels[i].color = on ? tabLabelOn : tabLabelOff;
            }
            for (int i = 0; i < cards.Count; i++)
            {
                bool shown = i < visible.Count;
                cards[i].gameObject.SetActive(shown);
                if (!shown) continue;
                var entry = visible[i];
                bool owned = Owned(entry), priced = !owned && entry.price > 0;
                cards[i].Show(entry.portrait, entry.name, owned ? "" : priced ? Money(entry.price) : "Miễn phí", priced,
                    Status(entry), entry == selected ? cardSelected : cardNormal);
            }
            emptyLabel.SetActive(visible.Count == 0);
            details.SetActive(selected != null);
            if (selected != null) ShowDetails(selected);
        }

        private void ShowDetails(ShopEntry entry)
        {
            portrait.sprite = entry.portrait;
            title.text = entry.name;
            description.text = entry.description;
            bool owned = Owned(entry), afford = inventory.Balance >= entry.price;
            priceCoin.SetActive(!owned && entry.price > 0);
            priceLabel.text = owned ? "Đã có" : entry.price == 0 ? "Miễn phí" : Money(entry.price);
            if (entry.kind == ShopKind.Decor)
            {
                int stock = Stock(entry);
                secondaryButton.gameObject.SetActive(true);
                secondaryButton.interactable = afford;
                secondaryLabel.text = afford ? "Mua 1" : "Chưa đủ xu";
                primaryButton.interactable = stock > 0;
                primaryLabel.text = stock > 0 ? $"Đặt ({stock})" : "Kho trống";
                return;
            }
            secondaryButton.gameObject.SetActive(false);
            bool active = Active(entry);
            primaryButton.interactable = !active && (owned || afford);
            primaryLabel.text = active ? "Đang dùng" : owned ? "Dùng" : entry.price == 0 ? "Nhận" : afford ? "Mua" : "Chưa đủ xu";
        }

        private string Status(ShopEntry entry)
        {
            if (entry.kind == ShopKind.Decor)
            {
                int stock = Stock(entry);
                return stock > 0 ? "Kho " + stock : "";
            }
            return Active(entry) ? "Đang dùng" : Owned(entry) ? "Đã có" : "";
        }

        // Stock = bought minus placed; the inventory and placed counts are read once per Refresh.
        private int Stock(ShopEntry entry) =>
            Mathf.Max(0, inventory.DecorCount(entry.id) - (placed.TryGetValue(DecorKind(entry), out int n) ? n : 0));

        private int DecorKind(ShopEntry entry) => Decoration.catalog.offers[entry.source].prefab.kind;

        // Decorations are bought again and again, so they never count as owned. A free item is owned once taken.
        private bool Owned(ShopEntry entry) => entry.kind != ShopKind.Decor && inventory.Owns(entry.id);

        // Style 0 is in use while nothing else has been chosen (Wall / Substrate fall back to it).
        private bool Active(ShopEntry entry) => entry.kind switch
        {
            ShopKind.Fish => inventory.ActiveId == entry.id,
            ShopKind.Wall => Decoration.surfaces.Wall(inventory.ActiveWall)?.id == entry.id,
            ShopKind.Substrate => Decoration.surfaces.Substrate(inventory.ActiveSubstrate)?.id == entry.id,
            _ => false
        };

        private void Primary()
        {
            if (selected == null || confirmation.activeSelf) return;
            if (selected.kind == ShopKind.Decor) Place(selected);
            else if (Owned(selected)) Use(selected);
            else RequestPurchase(selected);
        }

        private void RequestPurchase(ShopEntry entry)
        {
            inventory.Reload();
            if (inventory.Balance < entry.price) return;
            pending = entry;
            if (entry.price == 0) { ConfirmPurchase(); return; } // free gifts need no confirmation
            confirmationText.text = (entry.kind == ShopKind.Decor ? "1 × " : "") + entry.name + "\n" + Money(entry.price) + " xu";
            confirmation.SetActive(true);
        }

        public void CancelPurchase()
        {
            pending = null;
            if (confirmation != null) confirmation.SetActive(false);
        }

        public void ConfirmPurchase()
        {
            var entry = pending;
            CancelPurchase(); // consume the pending purchase before saving or a repeated tap
            if (!IsOpen || entry == null) return;
            try
            {
                bool bought = entry.kind switch
                {
                    ShopKind.Decor => inventory.TryPurchaseDecor(entry.id, entry.price),
                    ShopKind.Fish => inventory.TryPurchase(entry.id, entry.price),
                    ShopKind.Wall => inventory.TryPurchaseWall(entry.id, entry.price),
                    _ => inventory.TryPurchaseSubstrate(entry.id, entry.price)
                };
                if (!bought) feedback.text = "Chưa đủ xu để mua món này.";
                else if (entry.kind == ShopKind.Decor) feedback.text = "Đã thêm 1 " + entry.name + " vào kho.";
                else { Apply(entry); feedback.text = "Đã mua và dùng " + entry.name + "."; }
            }
            catch (Exception exception) { SaveFailed(exception); }
            aquarium.RefreshWallet();
            Refresh();
        }

        private void Use(ShopEntry entry)
        {
            try
            {
                bool done = entry.kind switch
                {
                    ShopKind.Fish => inventory.SelectOwned(entry.id),
                    ShopKind.Wall => inventory.SelectWall(entry.id),
                    _ => inventory.SelectSubstrate(entry.id)
                };
                if (done) { Apply(entry); feedback.text = "Đã dùng " + entry.name + "."; }
            }
            catch (Exception exception) { SaveFailed(exception); }
            Refresh();
        }

        private void Apply(ShopEntry entry)
        {
            var tank = Decoration != null ? Decoration.GetComponent<AquariumTank>() : null;
            switch (entry.kind)
            {
                case ShopKind.Fish: ApplyFish(fishOffers[entry.source]); break;
                case ShopKind.Wall: if (tank != null) tank.ApplyWall(entry.id); break;
                case ShopKind.Substrate: if (tank != null) tank.ApplySubstrate(entry.id); break;
            }
        }

        private void ApplyFish(FishShopOffer offer)
        {
            if (fish != null) fish.Apply(offer.variant);
            if (aquarium.fishNameLabel != null) aquarium.fishNameLabel.text = offer.variant.displayName;
            if (aquarium.fishPortrait != null) aquarium.fishPortrait.sprite = offer.portrait;
        }

        private void Place(ShopEntry entry)
        {
            var decoration = Decoration;
            int kind = DecorKind(entry);
            if (decoration.Available(kind) <= 0) return;
            if (!decoration.HasRoomFor(kind))
            {
                feedback.text = $"Hồ không đủ điểm cho món này ({decoration.Points}/{decoration.capacity}). Cất bớt đồ trước khi đặt thêm.";
                return;
            }
            Close();
            decoration.Begin(kind);
        }

        private void SaveFailed(Exception exception)
        {
            Debug.LogError("[Lumaquarium] Shop purchase failed: " + exception.Message);
            feedback.text = "Chưa lưu được giao dịch. Vui lòng thử lại.";
        }

        private static string Money(int amount) => amount.ToString("N0", Vietnamese);

        private void OnDisable() { if (IsOpen) Close(); }
    }
}
