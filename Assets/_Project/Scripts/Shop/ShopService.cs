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
        private static void ResetOnPlay()
        {
            backend = null;
            preparing = null;
        }

        private static Task preparing;

        // Before the aquarium opens and when the shop opens: a save kept on this device from before the server is
        // sent once, then the gift packs. The old save is first written to <key>.imported.bak, before the import is
        // sent, and kept from the first attempt (the server's answer replaces the local save, even a refusal that
        // carries one). The server's answer becomes the local save. The import counts as done only on None,
        // AlreadyImported or SaveUnreadable; after anything else Prepare stops before the gift packs and tries
        // again next time. One Prepare runs at a time: a call made meanwhile gets the same task.
        public static Task Prepare()
        {
            if (preparing != null) return preparing;
            var task = PrepareOnce();
            // One that finished without waiting has already cleared itself, before this line.
            if (!task.IsCompleted) preparing = task;
            return task;
        }

        private static async Task PrepareOnce()
        {
            try
            {
                var server = Server;
                if (server == null) return;
                string importedKey = server.StorageKey + ".imported";
                if (PlayerPrefs.GetInt(importedKey, 0) == 0)
                {
                    string local = new FishShopInventory(server.StorageKey).StoredJson();
                    if (local != null)
                    {
                        string backupKey = server.StorageKey + ".imported.bak";
                        if (!PlayerPrefs.HasKey(backupKey)) PlayerPrefs.SetString(backupKey, local);
                        string result = await server.Import(local);
                        if (result != ShopError.None && result != ShopError.AlreadyImported && result != ShopError.SaveUnreadable) return;
                    }
                    PlayerPrefs.SetInt(importedKey, 1);
                    PlayerPrefs.Save();
                }
                await server.ApplyGifts();
            }
            finally { preparing = null; }
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
