using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lumaquarium.UI;
using UnityEngine;

namespace Lumaquarium.Shop
{
    // Thrown by IShopCloud when the server cannot be reached.
    public sealed class ShopOfflineException : Exception
    {
        public ShopOfflineException(string message, Exception inner = null) : base(message, inner) { }
    }

    // One call to a function of the Cloud Code module LumaquariumShop (CloudCodeShopCloud; a fake in the tests).
    public interface IShopCloud
    {
        Task<ShopReply> Call(string function, Dictionary<string, object> args);
    }

    // The server decides; its answer (the save as it now is) becomes the local save, which every screen reads.
    // The price passed to Buy is ignored: the server takes its own, for the catalog version the game shows.
    public sealed class ServerShopBackend : IShopBackend
    {
        private readonly IShopCloud cloud;

        public string StorageKey { get; }
        // AquariumDecorCatalog.serverCatalogVersion, set when the aquarium opens.
        public string CatalogVersion { get; set; } = "";

        public ServerShopBackend(IShopCloud cloud, string storageKey = FishShopInventory.DefaultStorageKey)
        {
            this.cloud = cloud;
            StorageKey = storageKey;
        }

        public Task<string> Buy(string id, int price, string slot) =>
            Send("BuyItem", new Dictionary<string, object> { ["id"] = id, ["catalogVersion"] = CatalogVersion });

        public Task<string> Select(string slot, string id) =>
            Send("SelectItem", new Dictionary<string, object> { ["slot"] = slot, ["id"] = id });

        public Task<string> ApplyGifts() => Send("ApplyGifts", new Dictionary<string, object>());

        public Task<string> Import(string save) => Send("ImportLocalSave", new Dictionary<string, object> { ["save"] = save });

        private async Task<string> Send(string function, Dictionary<string, object> args)
        {
            ShopReply reply;
            try { reply = await cloud.Call(function, args); }
            catch (ShopOfflineException e)
            {
                Debug.LogWarning($"[Shop] {function}: offline ({e.Message}).");
                return ShopError.Offline;
            }
            if (reply == null) return ShopError.Rejected;
            if (!string.IsNullOrEmpty(reply.State) && !new FishShopInventory(StorageKey).ReplaceWith(reply.State)) return ShopError.Rejected;
            return reply.Error ?? ShopError.None;
        }
    }
}
