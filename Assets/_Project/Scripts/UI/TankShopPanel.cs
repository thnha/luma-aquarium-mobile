using System.Globalization;
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
            if (feedback != null) feedback.text = "Cá, cây và đèn phủ kín cả hồ";
            Refresh();
        }

        public void Choose(int index)
        {
            var tank = Tank;
            if (tank == null || index < 0 || index >= AquariumTank.Specs.Length) return;
            var spec = AquariumTank.Specs[index];
            if (spec == tank.Current) return;
            var inventory = Inventory;
            bool owned = spec.price == 0 || inventory.OwnsTank(spec.id);
            if (!owned && pending != index)
            {
                pending = index;
                feedback.text = inventory.Balance >= spec.price ? $"Chạm lần nữa để mua {spec.displayName}" : "Chưa đủ xu để mua hồ này";
                Refresh();
                return;
            }
            pending = -1;
            bool done = owned ? inventory.SelectTank(spec.id, spec.price == 0) : inventory.TryPurchaseTank(spec.id, spec.price);
            if (done)
            {
                tank.Switch(spec.id);
                feedback.text = owned ? $"Đã chuyển sang {spec.displayName}" : $"Đã mua {spec.displayName}. Đồ không vừa hồ được cất vào kho.";
                aquarium.RefreshWallet();
            }
            else feedback.text = "Chưa đủ xu để mua hồ này";
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
                bool current = spec == tank.Current, owned = spec.price == 0 || inventory.OwnsTank(spec.id);
                buttons[i].interactable = !current && (owned || inventory.Balance >= spec.price);
                var label = buttons[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = current ? "Đang dùng" : owned ? "Dùng" : pending == i ? "Xác nhận" :
                        inventory.Balance >= spec.price ? spec.price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " xu" : "Chưa đủ xu";
            }
        }
    }
}
