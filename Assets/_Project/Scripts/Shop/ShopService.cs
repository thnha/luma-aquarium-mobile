using System.Threading.Tasks;
using Lumaquarium.UI;
using UnityEngine;

namespace Lumaquarium.Shop
{
    // Which backend changes the shop save: the server in a build; in the Editor only with
    // Lumaquarium > Shop > Use Server in Editor (the local rules otherwise).
    public static class ShopService
    {
        public const string EditorServerKey = "Lumaquarium.Shop.UseServerInEditor";
        private static IShopBackend backend;

        public static IShopBackend Backend => backend ??= UseServer ? new ServerShopBackend(new CloudCodeShopCloud()) : new LocalShopBackend();
        public static ServerShopBackend Server => Backend as ServerShopBackend;
        public static bool UsesServer => Server != null;

        private static bool UseServer
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(EditorServerKey, false);
#else
                return true;
#endif
            }
        }

        // For the tests; null goes back to the default.
        public static void Use(IShopBackend next) => backend = next;

        // Play Mode without a domain reload keeps statics: start each session with the current setting.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => backend = null;

        // Before the aquarium opens and when the shop opens: a save kept on this device from before the server is
        // sent once (kept under <key>.imported.bak whatever the server says), then the gift packs. The server's
        // answer becomes the local save. Offline it stays as it is and the import waits for the next time.
        public static async Task Prepare()
        {
            var server = Server;
            if (server == null) return;
            string importedKey = server.StorageKey + ".imported";
            if (PlayerPrefs.GetInt(importedKey, 0) == 0)
            {
                string local = new FishShopInventory(server.StorageKey).StoredJson();
                if (local != null)
                {
                    if (await server.Import(local) == ShopError.Offline) return;
                    PlayerPrefs.SetString(server.StorageKey + ".imported.bak", local);
                }
                PlayerPrefs.SetInt(importedKey, 1);
                PlayerPrefs.Save();
            }
            await server.ApplyGifts();
        }

        // What the player reads when a change is not made.
        public static string Message(string error) => error switch
        {
            ShopError.NotEnoughCoins => "Chưa đủ xu để mua món này.",
            ShopError.Owned => "Bạn đã có món này.",
            ShopError.Offline => "Cần kết nối mạng để mua và đổi đồ.",
            ShopError.PriceChanged => "Giá vừa được cập nhật. Mở lại game để xem giá mới.",
            _ => "Chưa lưu được giao dịch. Vui lòng thử lại."
        };
    }
}
