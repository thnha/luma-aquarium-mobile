using System;
using System.Collections.Generic;
using Lumaquarium.Shop;
using Lumaquarium.Ads;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumaquarium.UI
{
    // The grouped shop: compact text tabs, contextual categories, search/sort and two-column cards
    // made from a template. A tapped card opens a details sheet, whose buttons buy, place a decoration,
    // or put a fish, wall or floor style into use. Purchases with a price ask for confirmation.
    public sealed class ShopScreen : MonoBehaviour
    {
        public MainAquariumScreen aquarium;
        public BettaAppearance fish;
        public FishShopOffer[] fishOffers;
        public GameObject content, confirmation, details, emptyLabel, priceCoin;
        public TMP_InputField search;
        public UnityEngine.UI.Button clearSearch, backButton, sortButton, primaryButton, secondaryButton, confirmButton, cancelButton;
        public UnityEngine.UI.Button topUpButton, walletCloseButton, detailsCloseButton, buyCoinsTab, rewardCoinsTab;
        public GameObject walletContent;
        public RectTransform coinPackArea, rewardedAdArea;
        public TMP_Text walletBalanceLabel, walletFeedback;
        public TMP_Text balanceLabel, sortLabel, title, description, priceLabel, feedback, confirmationText, primaryLabel, secondaryLabel;
        public UnityEngine.UI.Image portrait;
        public UnityEngine.UI.Button[] groupButtons;
        public UnityEngine.UI.Image[] groupSurfaces;
        public TMP_Text[] groupLabels;
        public RectTransform categoryTray, searchRow;
        public UnityEngine.UI.Image sortIcon, sortDownIcon;
        public UnityEngine.UI.Button[] tabButtons;
        public UnityEngine.UI.Image[] tabSurfaces;
        public TMP_Text[] tabLabels;
        public ShopCard cardTemplate;
        public UnityEngine.UI.ScrollRect scroll;
        public Sprite cardNormal, cardSelected;
        // Category chips: secondary / primary button surfaces. Shops built before these were set light the chip with
        // the card sprites and keep one label colour.
        public Sprite chipNormal, chipSelected;
        public Color tabLabelOn = new Color32(52, 60, 45, 255);
        public Color tabLabelOff = new Color32(255, 248, 230, 255);

        private static readonly string[] SortNames = { "Mặc định", "Giá: Thấp → Cao", "Giá: Cao → Thấp" };
        public static readonly ShopTab[][] CategoryTabs =
        {
            new[] { ShopTab.Fish },
            new[] { ShopTab.All, ShopTab.Plants, ShopTab.Wood, ShopTab.Rocks, ShopTab.Moss, ShopTab.Coral },
            new[] { ShopTab.Tanks, ShopTab.Walls, ShopTab.Substrates }
        };
        public static readonly string[][] CategoryNames =
        {
            new[] { "Cá" },
            new[] { "Tất cả", "Cây", "Lũa", "Đá", "Rêu & bèo", "San hô" },
            new[] { "Tank", "Thành hồ", "Nền hồ" }
        };
        private static readonly float[] DecorCategoryWidths = { 155, 110, 110, 100, 235, 150 };
        private static readonly float[] AquariumCategoryWidths = { 210, 290, 260 };
        private readonly List<ShopCard> cards = new List<ShopCard>();
        private List<ShopEntry> entries, visible = new List<ShopEntry>();
        private FishShopInventory inventory;
        private Dictionary<int, int> placed = new Dictionary<int, int>();
        private ShopTab tab;
        private ShopGroup group;
        private ShopSort sort;
        private ShopEntry selected, pending;
        private float previousAlpha;
        private bool previousInteractable, previousRaycasts, previousOrbit;
        private bool busy; // waiting for the server: buying and using are locked
        private bool detailsVisible;

        private RewardedAdShopPanel rewardedPanel;
        private CoinPackShopPanel coinPanel;
        public bool TransactionBusy => busy || confirmation.activeSelf || (coinPanel != null && coinPanel.Busy)
            || (rewardedPanel != null && rewardedPanel.Busy);

        public bool IsOpen => content != null && content.activeSelf;
        public bool WalletOpen => IsOpen && walletContent != null && walletContent.activeSelf;
        public int Balance { get { inventory.Reload(); return inventory.Balance; } }
        private AquariumDecoration Decoration => aquarium != null ? aquarium.decoration : null;
        private List<ShopEntry> Entries => entries ??= ShopEntry.Build(fishOffers,
            Decoration != null ? Decoration.catalog : null, Decoration != null ? Decoration.surfaces : null);

        private void Awake()
        {
            inventory = new FishShopInventory(Decoration != null ? Decoration.inventoryStorageKey : FishShopInventory.DefaultStorageKey);
            for (int i = 0; i < groupButtons.Length; i++)
            {
                var target = (ShopGroup)i;
                groupButtons[i].onClick.AddListener(() => SelectGroup(target));
            }
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => SelectTab(CategoryTabs[(int)group][index]));
            }
            search.onValueChanged.AddListener(_ => Rebuild(true));
            clearSearch.onClick.AddListener(() => search.text = "");
            backButton.onClick.AddListener(GoBack);
            topUpButton.onClick.AddListener(OpenWallet);
            walletCloseButton.onClick.AddListener(CloseWallet);
            detailsCloseButton.onClick.AddListener(CloseDetails);
            buyCoinsTab.onClick.AddListener(() => SelectWalletTab(false));
            rewardCoinsTab.onClick.AddListener(() => SelectWalletTab(true));
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
            rewardedPanel = gameObject.AddComponent<RewardedAdShopPanel>();
            rewardedPanel.Initialize(this);
            coinPanel = gameObject.AddComponent<CoinPackShopPanel>();
            coinPanel.Initialize(this);
        }

        private void ShowActiveTab()
        {
            bool hasCategories = group != ShopGroup.Fish;
            categoryTray.gameObject.SetActive(hasCategories);
            float top = hasCategories ? 410 : 302;
            searchRow.offsetMin = new Vector2(searchRow.offsetMin.x, -top - 116);
            searchRow.offsetMax = new Vector2(searchRow.offsetMax.x, -top);
            var grid = (RectTransform)scroll.transform;
            grid.offsetMax = new Vector2(grid.offsetMax.x, -top - 144);
            if (search.placeholder is TMP_Text placeholder)
                placeholder.text = Loc.T(group == ShopGroup.Fish ? "Tìm cá..." : group == ShopGroup.Decor ? "Tìm tiểu cảnh..." : "Tìm hồ, thành, nền...");
        }

        private void Update()
        {
            if (!IsOpen || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            GoBack();
        }

        public void GoBack()
        {
            if (confirmation.activeSelf) CancelPurchase();
            else if (WalletOpen) CloseWallet();
            else if (detailsVisible) CloseDetails();
            else Close();
        }

        public void OpenWallet()
        {
            if (!IsOpen || TransactionBusy) return;
            walletFeedback.text = "";
            walletContent.SetActive(true);
            SelectWalletTab(false);
            Refresh();
        }

        public void CloseWallet()
        {
            walletContent.SetActive(false);
            walletFeedback.text = "";
            Refresh();
        }

        public void SelectWalletTab(bool rewards)
        {
            coinPackArea.gameObject.SetActive(!rewards);
            rewardedAdArea.gameObject.SetActive(rewards);
            var buttons = new[] { buyCoinsTab, rewardCoinsTab };
            for (int i = 0; i < buttons.Length; i++)
            {
                bool active = rewards == (i == 1);
                var label = buttons[i].GetComponent<Ui3DButtonLabel>();
                if (label == null)
                {
                    // Segment tab, as in the shop groups: the selected tab shows its primary surface.
                    buttons[i].GetComponent<UnityEngine.UI.Image>().color = active ? Color.white : Color.clear;
                    buttons[i].GetComponentInChildren<TMP_Text>().color = active ? tabLabelOn : tabLabelOff;
                    continue;
                }
                buttons[i].GetComponent<UnityEngine.UI.Image>().color = active ? new Color32(69, 93, 63, 255) : Color.white;
                label.normal = active ? tabLabelOff : tabLabelOn;
                label.label.color = label.normal;
            }
            walletFeedback.text = "";
        }

        public void ShowWalletFeedback(string message)
        {
            if (IsOpen) walletFeedback.text = message;
        }

        public void CloseDetails()
        {
            if (TransactionBusy) return;
            detailsVisible = false;
            details.SetActive(false);
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
            walletContent.SetActive(false);
            detailsVisible = false;
            pending = null;
            feedback.text = "";
            group = ShopGroup.Fish;
            tab = ShopTab.Fish;
            sort = ShopSort.Default;
            selected = null;
            search.SetTextWithoutNotify("");
            Rebuild(true);
            ShowActiveTab();
            Sync();
        }

        // The server may have changed the save since the aquarium opened (gift packs, a missed import): its answer
        // shows when it comes.
        private async void Sync()
        {
            if (!ShopService.UsesServer) return;
            try { await ShopService.Prepare(); }
            catch (Exception exception) { Debug.LogException(exception); }
            if (this == null || !IsOpen) return;
            aquarium.RefreshWallet();
            Refresh();
        }

        // Opens on one tab, and on one item when its id is given (the Trang trí sheet's Mua thêm); showDetails false
        // only selects it, without the details sheet (Đổi cá).
        public void OpenTab(ShopTab target, string id = null, bool showDetails = true)
        {
            Open();
            group = ShopQuery.GroupOf(target);
            tab = target;
            selected = id != null ? Entries.Find(e => e.id == id) : null;
            detailsVisible = showDetails && selected != null;
            Rebuild(true);
            ShowActiveTab();
        }

        public void Close()
        {
            if (!IsOpen) return;
            CancelPurchase();
            walletContent.SetActive(false);
            detailsVisible = false;
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
            detailsVisible = false;
            Rebuild(true);
            ShowActiveTab();
        }

        public void SelectGroup(ShopGroup target)
        {
            if (confirmation.activeSelf) return;
            group = target;
            detailsVisible = false;
            tab = CategoryTabs[(int)group][0];
            search.SetTextWithoutNotify("");
            feedback.text = "";
            Rebuild(true);
            ShowActiveTab();
        }

        private void Select(int index)
        {
            if (confirmation.activeSelf || index >= visible.Count) return;
            selected = visible[index];
            detailsVisible = true;
            feedback.text = "";
            Refresh();
        }

        // Re-runs tab / search / sort; keeps the selection while it is still listed, else selects the first card.
        private void Rebuild(bool toTop)
        {
            visible = ShopQuery.Filter(Entries, group, tab, search.text, sort);
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
            walletBalanceLabel.text = Money(inventory.Balance);
            topUpButton.interactable = !TransactionBusy;
            if (sortLabel != null) sortLabel.text = Loc.T(SortNames[(int)sort]);
            RefreshSortIcon();
            clearSearch.gameObject.SetActive(!string.IsNullOrEmpty(search.text));
            for (int i = 0; i < groupButtons.Length; i++)
            {
                bool on = i == (int)group;
                groupSurfaces[i].color = on ? Color.white : Color.clear;
                groupLabels[i].color = on ? tabLabelOn : tabLabelOff;
            }
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool shown = i < CategoryTabs[(int)group].Length;
                tabButtons[i].gameObject.SetActive(shown);
                if (!shown) continue;
                bool on = CategoryTabs[(int)group][i] == tab || (tab == ShopTab.Floating && CategoryTabs[(int)group][i] == ShopTab.Moss);
                tabLabels[i].text = Loc.T(CategoryNames[(int)group][i]);
                if (chipSelected != null)
                {
                    tabSurfaces[i].sprite = on ? chipSelected : chipNormal;
                    // Ui3DButtonLabel repaints the label every frame, so the chip colour goes through it.
                    var tint = tabButtons[i].GetComponent<Ui3DButtonLabel>();
                    var color = on ? tabLabelOn : tabLabelOff;
                    if (tint != null) tint.normal = color;
                    tabLabels[i].color = color;
                }
                else
                {
                    tabSurfaces[i].sprite = on ? cardSelected : cardNormal;
                    tabLabels[i].color = tabLabelOn;
                }
                float[] widths = group == ShopGroup.Decor ? DecorCategoryWidths : AquariumCategoryWidths;
                tabButtons[i].GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = widths[i];
            }
            for (int i = 0; i < cards.Count; i++)
            {
                bool shown = i < visible.Count;
                cards[i].gameObject.SetActive(shown);
                if (!shown) continue;
                var entry = visible[i];
                cards[i].Show(entry.portrait, Loc.T(entry.name), entry.price == 0 ? Loc.T("Miễn phí") : Money(entry.price), entry.price > 0,
                    Status(entry), detailsVisible && entry == selected ? cardSelected : cardNormal, Active(entry),
                    entry.kind == ShopKind.Tank ? AquariumTank.Find(entry.id) : null);
            }
            emptyLabel.SetActive(visible.Count == 0);
            details.SetActive(detailsVisible && selected != null);
            if (selected != null) ShowDetails(selected);
            if (busy) primaryButton.interactable = secondaryButton.interactable = false;
        }

        private void RefreshSortIcon()
        {
            if (sortIcon == null || sortDownIcon == null) return;
            bool defaultOrder = sort == ShopSort.Default;
            bool showUp = sort != ShopSort.PriceDescending;
            bool showDown = sort != ShopSort.PriceAscending;
            Color color = defaultOrder ? UiInk : new Color32(69, 112, 58, 255);
            SetSortDirection(sortIcon, showUp, defaultOrder ? 15 : 3, defaultOrder ? 38 : 52, color);
            SetSortDirection(sortDownIcon, showDown, defaultOrder ? -11 : 3, defaultOrder ? 38 : 52, color);
        }

        private static void SetSortDirection(UnityEngine.UI.Image image, bool shown, float y, float size, Color color)
        {
            image.gameObject.SetActive(shown);
            image.color = color;
            image.rectTransform.anchoredPosition = new Vector2(23, y);
            image.rectTransform.sizeDelta = new Vector2(size, size);
        }

        private void ShowDetails(ShopEntry entry)
        {
            portrait.sprite = entry.portrait;
            title.text = Loc.T(entry.name);
            description.text = Loc.T(entry.description);
            bool owned = Owned(entry), afford = inventory.Balance >= entry.price;
            priceCoin.SetActive(!owned && entry.price > 0);
            priceLabel.text = owned ? Loc.T("Đã có") : entry.price == 0 ? Loc.T("Miễn phí") : Money(entry.price);
            if (entry.kind == ShopKind.Decor)
            {
                int stock = Stock(entry);
                secondaryButton.gameObject.SetActive(true);
                secondaryButton.interactable = true;
                secondaryLabel.text = Loc.T(afford ? "Mua 1" : "Chưa đủ xu · Nạp xu");
                primaryButton.interactable = stock > 0;
                primaryLabel.text = stock > 0 ? Loc.F("Đặt ({0})", stock) : Loc.T("Kho trống");
                return;
            }
            secondaryButton.gameObject.SetActive(false);
            bool active = Active(entry);
            primaryButton.interactable = !active;
            primaryLabel.text = Loc.T(active ? "Đang dùng" : owned ? "Dùng" : entry.price == 0 ? "Nhận" : afford ? "Mua" : "Chưa đủ xu · Nạp xu");
        }

        private string Status(ShopEntry entry)
        {
            if (entry.kind == ShopKind.Decor)
            {
                int stock = Stock(entry);
                return stock > 0 ? Loc.F("Kho {0}", stock) : "";
            }
            return Active(entry) ? "" : Owned(entry) ? Loc.T("Đã có") : "";
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
            ShopKind.Tank => (Decoration.GetComponent<AquariumTank>()?.Current?.id
                ?? (string.IsNullOrEmpty(inventory.ActiveTank) ? AquariumTank.Specs[0].id : inventory.ActiveTank)) == entry.id,
            _ => false
        };

        private void Primary()
        {
            if (selected == null || TransactionBusy || WalletOpen) return;
            if (selected.kind == ShopKind.Decor) Place(selected);
            else if (Owned(selected)) Use(selected);
            else RequestPurchase(selected);
        }

        private void RequestPurchase(ShopEntry entry)
        {
            if (TransactionBusy || WalletOpen) return;
            inventory.Reload();
            if (inventory.Balance < entry.price) { OpenWallet(); return; }
            pending = entry;
            if (entry.price == 0) { ConfirmPurchase(); return; } // free gifts need no confirmation
            confirmationText.text = (entry.kind == ShopKind.Decor ? "1 × " : "") + Loc.T(entry.name) + "\n" + Loc.F("{0} xu", Money(entry.price));
            confirmation.SetActive(true);
        }

        public void CancelPurchase()
        {
            pending = null;
            if (confirmation != null) confirmation.SetActive(false);
        }

        public async void ConfirmPurchase()
        {
            var entry = pending;
            CancelPurchase(); // consume the pending purchase before saving or a repeated tap
            if (!IsOpen || entry == null || busy) return;
            busy = true;
            feedback.text = Loc.T("Đang xử lý...");
            Refresh();
            string error;
            try
            {
                if (ShopService.UsesServer) await ShopService.Prepare(); // the old save goes first
                error = await ShopService.Backend.Buy(entry.id, entry.price, Slot(entry.kind));
            }
            catch (Exception exception) { error = null; SaveFailed(exception); }
            busy = false;
            if (this == null) return; // the scene closed while waiting
            if (error == ShopError.None)
            {
                if (entry.kind == ShopKind.Decor) feedback.text = Loc.F("Đã thêm 1 {0} vào kho.", Loc.T(entry.name));
                else { Apply(entry); feedback.text = Loc.F("Đã mua và dùng {0}.", Loc.T(entry.name)); }
            }
            else if (error != null) feedback.text = ShopService.Message(error);
            aquarium.RefreshWallet();
            Refresh();
        }

        private async void Use(ShopEntry entry)
        {
            if (busy) return;
            busy = true;
            feedback.text = Loc.T("Đang xử lý...");
            Refresh();
            string error;
            try
            {
                if (ShopService.UsesServer) await ShopService.Prepare();
                error = await ShopService.Backend.Select(Slot(entry.kind), entry.id);
            }
            catch (Exception exception) { error = null; SaveFailed(exception); }
            busy = false;
            if (this == null) return;
            if (error == ShopError.None) { Apply(entry); feedback.text = Loc.F("Đã dùng {0}.", Loc.T(entry.name)); }
            else if (error != null) feedback.text = ShopService.Message(error);
            Refresh();
        }

        private static string Slot(ShopKind kind) => kind switch
        {
            ShopKind.Fish => ShopSave.FishSlot,
            ShopKind.Wall => ShopSave.WallSlot,
            ShopKind.Substrate => ShopSave.FloorSlot,
            ShopKind.Tank => ShopSave.TankSlot,
            _ => null
        };

        private void Apply(ShopEntry entry)
        {
            var tank = Decoration != null ? Decoration.GetComponent<AquariumTank>() : null;
            switch (entry.kind)
            {
                case ShopKind.Fish: ApplyFish(fishOffers[entry.source]); break;
                case ShopKind.Wall: if (tank != null) tank.ApplyWall(entry.id); break;
                case ShopKind.Substrate: if (tank != null) tank.ApplySubstrate(entry.id); break;
                case ShopKind.Tank: if (tank != null) tank.Switch(entry.id); break;
            }
        }

        private void ApplyFish(FishShopOffer offer)
        {
            if (fish != null) fish.Apply(offer.variant);
            if (aquarium.fishNameLabel != null) aquarium.fishNameLabel.text = Loc.T(offer.variant.displayName);
            if (aquarium.fishPortrait != null) aquarium.fishPortrait.sprite = offer.portrait;
        }

        private void Place(ShopEntry entry)
        {
            var decoration = Decoration;
            int kind = DecorKind(entry);
            if (decoration.Available(kind) <= 0) return;
            if (!decoration.HasRoomFor(kind))
            {
                feedback.text = Loc.F("Hồ không đủ điểm cho món này ({0}/{1}). Cất bớt đồ trước khi đặt thêm.", decoration.Points, decoration.capacity);
                return;
            }
            Close();
            decoration.Begin(kind);
        }

        private void SaveFailed(Exception exception)
        {
            Debug.LogError("[Lumaquarium] Shop purchase failed: " + exception.Message);
            feedback.text = Loc.T("Chưa lưu được giao dịch. Vui lòng thử lại.");
        }

        private static string Money(int amount) => Loc.Money(amount);
        private static readonly Color UiInk = new Color32(52, 60, 45, 255);

        private void OnDisable() { if (IsOpen) Close(); }
    }
}
