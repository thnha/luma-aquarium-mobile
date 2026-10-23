using System;
using Lumaquarium.Shop;
using TMPro;
using UnityEngine;

namespace Lumaquarium.UI
{
    // The tank sheet: one row per AquariumTank.Specs entry with its size and a button that buys it,
    // switches to it or shows that it is in use. Buying asks for a second tap, as tanks cost the most.
    public sealed class TankShopPanel : MonoBehaviour
    {
        public MainAquariumScreen aquarium;
        public UnityEngine.UI.Button[] buttons; // AquariumTank.Specs order
        public TMP_Text feedback;
        private int pending = -1;
        private bool busy; // waiting for the server

        private AquariumTank Tank => aquarium != null && aquarium.decoration != null ? aquarium.decoration.GetComponent<AquariumTank>() : null;
        private FishShopInventory Inventory => new FishShopInventory(aquarium.decoration.inventoryStorageKey);

        private void Start()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                buttons[i].onClick.AddListener(() => Choose(index));
            }
            Refresh();
        }

        private void OnEnable()
        {
            pending = -1;
            if (feedback != null) feedback.text = Loc.T("Cá, cây và đèn phủ kín cả hồ");
            Refresh();
        }

        public async void Choose(int index)
        {
            var tank = Tank;
            if (busy || tank == null || index < 0 || index >= AquariumTank.Specs.Length) return;
            var spec = AquariumTank.Specs[index];
            if (spec == tank.Current) return;
            var inventory = Inventory;
            bool owned = inventory.OwnsTank(spec.id);
            // A free tank is taken at the first tap.
            if (!owned && spec.price > 0 && pending != index)
            {
                pending = index;
                feedback.text = inventory.Balance >= spec.price ? Loc.F("Chạm lần nữa để mua {0}", Loc.T(spec.displayName)) : Loc.T("Chưa đủ xu để mua hồ này");
                Refresh();
                return;
            }
            pending = -1;
            busy = true;
            feedback.text = Loc.T("Đang xử lý...");
            Refresh();
            string error;
            try
            {
                if (ShopService.UsesServer) await ShopService.Prepare(); // the old save goes first
                error = owned ? await ShopService.Backend.Select(ShopSave.TankSlot, spec.id)
                    : await ShopService.Backend.Buy(spec.id, spec.price, ShopSave.TankSlot);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Lumaquarium] Tank purchase failed: " + exception.Message);
                error = ShopError.Rejected;
            }
            busy = false;
            if (this == null) return; // the scene closed while waiting
            if (error == ShopError.None)
            {
                tank.Switch(spec.id);
                feedback.text = owned ? Loc.F("Đã chuyển sang {0}", Loc.T(spec.displayName)) : Loc.F("Đã mua {0}. Đồ không vừa hồ được cất vào kho.", Loc.T(spec.displayName));
                aquarium.RefreshWallet();
            }
            else feedback.text = error == ShopError.NotEnoughCoins ? Loc.T("Chưa đủ xu để mua hồ này") : ShopService.Message(error);
            Refresh();
        }

        public void Refresh()
        {
            var tank = Tank;
            if (tank == null || buttons == null) return;
            var inventory = Inventory;
            for (int i = 0; i < buttons.Length && i < AquariumTank.Specs.Length; i++)
            {
                var spec = AquariumTank.Specs[i];
                // The row's name and line were baked with the built-in values; the price sheet may have changed them.
                var row = buttons[i].transform.parent;
                SetText(row.Find("Title"), Loc.T(spec.displayName));
                SetText(row.Find("Detail"), Loc.T(spec.description));
                bool current = spec == tank.Current, owned = inventory.OwnsTank(spec.id);
                buttons[i].interactable = !busy && !current && (owned || inventory.Balance >= spec.price);
                var label = buttons[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = current ? Loc.T("Đang dùng") : owned ? Loc.T("Dùng") : spec.price == 0 ? Loc.T("Nhận") : pending == i ? Loc.T("Xác nhận") :
                        inventory.Balance >= spec.price ? Loc.F("{0} xu", Loc.Money(spec.price)) : Loc.T("Chưa đủ xu");
            }
        }

        private static void SetText(Transform target, string text)
        {
            var label = target != null ? target.GetComponent<TMP_Text>() : null;
            if (label != null && label.text != text) label.text = text;
        }
    }
}
