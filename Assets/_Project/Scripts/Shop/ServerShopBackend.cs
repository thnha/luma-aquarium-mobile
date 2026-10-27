using System;
using System.Collections.Generic;
using System.Threading;
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
        private readonly SemaphoreSlim requests = new SemaphoreSlim(1, 1);
        public AdRewardStatus AdStatus { get; private set; }

        public string StorageKey { get; }
        // AquariumDecorCatalog.serverCatalogVersion, set when the aquarium opens.
        public string CatalogVersion { get; set; } = "";

        public ServerShopBackend(IShopCloud cloud, string storageKey = FishShopInventory.DefaultStorageKey)
        {
            this.cloud = cloud;
            StorageKey = storageKey;
        }

        public Task<string> Buy(string id, int price, string slot) =>
            ImportPending ? Task.FromResult(ShopError.Offline)
                : Send("BuyItem", new Dictionary<string, object> { ["id"] = id, ["catalogVersion"] = CatalogVersion });

        public Task<string> Select(string slot, string id) =>
            ImportPending ? Task.FromResult(ShopError.Offline)
                : Send("SelectItem", new Dictionary<string, object> { ["slot"] = slot, ["id"] = id });

        // The old local save has not been sent yet (ShopService.Prepare): a server with no save would start from
        // an empty one and the old coins and items would be lost, so nothing is bought or changed meanwhile.
        private bool ImportPending =>
            PlayerPrefs.GetInt(StorageKey + ".imported", 0) == 0 && new FishShopInventory(StorageKey).StoredJson() != null;

        public Task<string> ApplyGifts() => Send("ApplyGifts", new Dictionary<string, object>());

        public Task<string> GetAdRewardStatus() => ImportPending ? Task.FromResult(ShopError.Offline)
            : Send("GetAdRewardStatus", new Dictionary<string, object>());

        public Task<string> ClaimAdReward(string requestId) => ImportPending ? Task.FromResult(ShopError.Offline)
            : Send("ClaimAdReward", new Dictionary<string, object> { ["requestId"] = requestId });

        // An App Store coin pack: the server asks Apple about the transaction and adds the pack's coins once.
        public Task<string> ClaimPurchase(string productId, string transactionId) => ImportPending ? Task.FromResult(ShopError.Offline)
            : Send("ClaimAppStorePurchase", new Dictionary<string, object> { ["productId"] = productId, ["transactionId"] = transactionId });

        public Task<string> Import(string save) => Send("ImportLocalSave", new Dictionary<string, object> { ["save"] = save });

        private async Task<string> Send(string function, Dictionary<string, object> args)
        {
            // Keep responses in order: a slower status read must not overwrite a newer purchase/reward save.
            await requests.WaitAsync();
            try { return await SendOnce(function, args); }
            finally { requests.Release(); }
        }

        private async Task<string> SendOnce(string function, Dictionary<string, object> args)
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
            if (reply.Ads != null) AdStatus = reply.Ads;
            return reply.Error ?? ShopError.None;
        }
    }
}
