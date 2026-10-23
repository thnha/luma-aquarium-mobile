using Lumaquarium.Shop;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace LumaquariumShop;

// The functions the game calls (ServerShopBackend). Each one works on the calling player's save only.
public sealed class ShopModule
{
    private static ShopLogic Logic(IExecutionContext context, IGameApiClient api) =>
        new(new CloudSaveShopStore(context, api), CatalogResource.Catalog);

    [CloudCodeFunction("GetState")]
    public Task<ShopReply> GetState(IExecutionContext context, IGameApiClient gameApiClient) =>
        Logic(context, gameApiClient).GetState();

    [CloudCodeFunction("BuyItem")]
    public Task<ShopReply> BuyItem(IExecutionContext context, IGameApiClient gameApiClient, string id, string catalogVersion) =>
        Logic(context, gameApiClient).Buy(id, catalogVersion);

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
