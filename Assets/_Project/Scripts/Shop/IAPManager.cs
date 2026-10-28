using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lumaquarium.UI;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Lumaquarium.Shop
{
    // The App Store / Google Play coin packs (ShopCatalog.DefaultCoinPacks), one store session across aquarium scenes. A paid
    // purchase is only finished (ConfirmPurchase) once the server has added its coins or said Apple denies it;
    // offline or refused, it stays pending: retried when the shop opens, and Apple delivers it again next launch.
    // In the Editor (Unity IAP Fake Store) with the local rules the coins are added on this device instead, to try
    // the shop; with Use Server in Editor the server asks Apple, which knows no Fake Store purchase.
    public sealed class IAPManager : MonoBehaviour
    {
        private static IAPManager instance;
        private StoreController store;
        private readonly List<PendingOrder> unclaimed = new List<PendingOrder>();
        private readonly HashSet<string> claiming = new HashSet<string>();
        private bool connecting, purchasing, fetchingProducts;
        public event Action Changed;
        public event Action<string> Feedback;
        public event Action<string, int> CoinsGranted; // product id, coins the server just added
        public bool Connected { get; private set; }
        public bool Busy => purchasing || claiming.Count > 0;
        public bool HasUnclaimed => unclaimed.Count > 0;

        public static bool Supported
        {
            get
            {
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static IAPManager Get()
        {
            if (!Supported || !(ShopService.UsesServer || Application.isEditor)) return null;
            if (instance == null)
            {
                instance = new GameObject("IAPManager").AddComponent<IAPManager>();
                DontDestroyOnLoad(instance.gameObject);
            }
            return instance;
        }

        public static List<CoinPack> Packs => ShopCatalog.DefaultCoinPacks();

        public void Prepare()
        {
            if (!Connected) Connect();
            else
            {
                if (Packs.Any(p => string.IsNullOrEmpty(Price(p.productId)))) FetchProducts();
                RetryUnclaimed();
            }
        }

        // The store's price in the player's currency, or null before the products arrive.
        public string Price(string productId)
        {
            var product = Connected ? store.GetProductById(productId) : null;
            return product != null && product.availableToPurchase ? product.metadata.localizedPriceString : null;
        }

        public void Buy(string productId)
        {
            if (Busy || Price(productId) == null) return;
            purchasing = true;
            Changed?.Invoke();
            store.PurchaseProduct(store.GetProductById(productId));
        }

        private async void Connect()
        {
            if (connecting) return;
            connecting = true;
            if (store == null)
            {
                store = UnityIAPServices.StoreController();
                // Every event before Connect: a purchase left pending last session can arrive at once.
                store.OnStoreConnected += OnConnected;
                store.OnStoreDisconnected += failure => { Connected = false; fetchingProducts = false; Debug.LogWarning($"[IAP] Store disconnected: {failure.Message}"); Changed?.Invoke(); };
                store.OnProductsFetched += OnProductsFetched;
                store.OnProductsFetchFailed += OnProductsFetchFailed;
                store.OnPurchasesFetched += orders => Debug.Log($"[IAP] {orders.PendingOrders.Count} pending purchases");
                store.OnPurchasesFetchFailed += failure => Debug.LogWarning($"[IAP] Purchases not fetched: {failure.Message}");
                store.OnPurchasePending += order => _ = Claim(order);
                store.OnPurchaseConfirmed += OnConfirmed;
                store.OnPurchaseFailed += OnFailed;
                store.OnPurchaseDeferred += _ => Finish("Đang chờ phụ huynh duyệt giao dịch.");
            }
            try { await store.Connect(); }
            catch (Exception e) { Debug.LogWarning($"[IAP] Connect failed: {e.Message}"); }
            finally { connecting = false; }
        }

        private void OnConnected()
        {
            Connected = true;
            FetchProducts();
            store.FetchPurchases();
            Changed?.Invoke();
        }

        private void FetchProducts()
        {
            if (fetchingProducts) return;
            fetchingProducts = true;
            try
            {
                store.FetchProducts(Packs.Select(p => new ProductDefinition(p.productId, ProductType.Consumable)).ToList());
            }
            catch (Exception e)
            {
                fetchingProducts = false;
                Debug.LogWarning($"[IAP] Products not fetched: {e.Message}");
                Changed?.Invoke();
            }
        }

        private void OnProductsFetched(List<Product> products)
        {
            fetchingProducts = false;
            Debug.Log($"[IAP] {products.Count} products");
            Changed?.Invoke();
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            fetchingProducts = false;
            Debug.LogWarning($"[IAP] Products not fetched: {failure.FailureReason}");
            Changed?.Invoke();
        }

        private async Task Claim(PendingOrder order)
        {
            purchasing = false;
            string productId = order.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id;
            string transactionId = order.Info.TransactionID;
            if (string.IsNullOrEmpty(transactionId) || !claiming.Add(transactionId)) { Changed?.Invoke(); return; }
            unclaimed.RemoveAll(x => x.Info.TransactionID == transactionId);
            Changed?.Invoke();
            string error;
            try
            {
                var server = ShopService.Server;
                string storageKey = server != null ? server.StorageKey : FishShopInventory.DefaultStorageKey;
                int before = new FishShopInventory(storageKey).Balance;
                if (server != null)
                {
                    await ShopService.Prepare(); // the old local save goes first
                    error = await server.ClaimPurchase(productId, transactionId);
                }
                else
                {
                    var pack = Packs.Find(p => p.productId == productId);
                    error = pack == null ? ShopError.PurchaseInvalid : new FishShopInventory(storageKey).AddCoins(pack.coins);
                }
                int added = new FishShopInventory(storageKey).Balance - before;
                if (error == ShopError.None && added > 0) CoinsGranted?.Invoke(productId, added);
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAP] Claim {productId} failed: {e}");
                error = ShopError.Rejected;
            }
            claiming.Remove(transactionId);
            if (this == null) return;
            if (error == ShopError.None || error == ShopError.PurchaseInvalid)
            {
                store.ConfirmPurchase(order);
                Finish(error == ShopError.None ? "Đã nhận xu. Cảm ơn bạn!" : ShopService.Message(error), translated: error != ShopError.None);
            }
            else
            {
                unclaimed.Add(order);
                Finish(error == ShopError.Offline ? "Đã thanh toán. Xu sẽ được cộng khi có mạng." : "Đã thanh toán. Xu sẽ được cộng ở lần thử sau.");
            }
        }

        private void RetryUnclaimed()
        {
            foreach (var order in unclaimed.ToList()) _ = Claim(order);
        }

        private void OnConfirmed(Order order)
        {
            if (order is FailedOrder failed) Debug.LogWarning($"[IAP] Finish failed: {failed.FailureReason} {failed.Details}");
        }

        private void OnFailed(FailedOrder order)
        {
            Debug.LogWarning($"[IAP] Purchase failed: {order.FailureReason} {order.Details}");
            Finish(order.FailureReason == PurchaseFailureReason.UserCancelled ? "Đã hủy giao dịch." : "Chưa mua được. Vui lòng thử lại.");
        }

        private void Finish(string message, bool translated = false)
        {
            purchasing = false;
            Feedback?.Invoke(translated ? message : Loc.T(message));
            Changed?.Invoke();
        }
    }
}
