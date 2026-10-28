using Lumaquarium.Shop;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace LumaquariumShop;

// The functions the game calls (ServerShopBackend). Each one works on the calling player's save only.
public sealed class ShopModule
{
    private readonly ILogger<ShopModule> logger;

    // Cloud Code injects the logger; what it writes shows in the dashboard under Cloud Code > Logs.
    public ShopModule(ILogger<ShopModule> logger) => this.logger = logger;

    private static ShopLogic Logic(IExecutionContext context, IGameApiClient api) =>
        new(new CloudSaveShopStore(context, api), CatalogResource.Catalog);

    [CloudCodeFunction("DeletePlayerData")]
    public async Task<bool> DeletePlayerData(IExecutionContext context, IGameApiClient gameApiClient)
    {
        await PlayerDataDeletion.Delete(context, gameApiClient.CloudSaveData);
        return true;
    }

    [CloudCodeFunction("GetState")]
    public Task<ShopReply> GetState(IExecutionContext context, IGameApiClient gameApiClient) =>
        Logic(context, gameApiClient).GetState();

    [CloudCodeFunction("GetAdRewardStatus")]
    public Task<ShopReply> GetAdRewardStatus(IExecutionContext context, IGameApiClient gameApiClient) =>
        Logic(context, gameApiClient).GetAdRewardStatus();

    [CloudCodeFunction("ClaimAdReward")]
    public Task<ShopReply> ClaimAdReward(IExecutionContext context, IGameApiClient gameApiClient, string requestId) =>
        Logic(context, gameApiClient).ClaimAdReward(requestId);

    [CloudCodeFunction("ClaimAppStorePurchase")]
    public Task<ShopReply> ClaimAppStorePurchase(IExecutionContext context, IGameApiClient gameApiClient, string productId, string transactionId) =>
        new ShopLogic(new CloudSaveShopStore(context, gameApiClient), CatalogResource.Catalog, appStore: new AppStoreApi(context, gameApiClient, logger),
                stats: new CloudSavePurchaseStats(context, gameApiClient))
            .ClaimAppStorePurchase(productId, transactionId);

    [CloudCodeFunction("ClaimGooglePlayPurchase")]
    public Task<ShopReply> ClaimGooglePlayPurchase(IExecutionContext context, IGameApiClient gameApiClient, string productId, string purchaseToken) =>
        new ShopLogic(new CloudSaveShopStore(context, gameApiClient), CatalogResource.Catalog, googlePlay: new GooglePlayApi(context, gameApiClient, logger),
                stats: new CloudSavePurchaseStats(context, gameApiClient))
            .ClaimGooglePlayPurchase(productId, purchaseToken);

    [CloudCodeFunction("BuyItem")]
    public Task<ShopReply> BuyItem(IExecutionContext context, IGameApiClient gameApiClient, string id, string catalogVersion) =>
        new ShopLogic(new CloudSaveShopStore(context, gameApiClient), CatalogResource.Catalog,
                stats: new CloudSavePurchaseStats(context, gameApiClient))
            .Buy(id, catalogVersion);

    [CloudCodeFunction("SelectItem")]
    public Task<ShopReply> SelectItem(IExecutionContext context, IGameApiClient gameApiClient, string slot, string id) =>
        Logic(context, gameApiClient).Select(slot, id);

    [CloudCodeFunction("ApplyGifts")]
    public Task<ShopReply> ApplyGifts(IExecutionContext context, IGameApiClient gameApiClient) =>
        Logic(context, gameApiClient).ApplyGifts();

    [CloudCodeFunction("ImportLocalSave")]
    public Task<ShopReply> ImportLocalSave(IExecutionContext context, IGameApiClient gameApiClient, string save) =>
        Logic(context, gameApiClient).Import(save);
}
