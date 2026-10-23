using System.Net;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace LumaquariumShop;

// The save is the player's protected Cloud Save item "shop": the player can read it, only the server writes it.
public sealed class CloudSaveShopStore : IShopStore
{
    public const string Key = "shop";
    private readonly IExecutionContext context;
    private readonly IGameApiClient api;

    public CloudSaveShopStore(IExecutionContext context, IGameApiClient api)
    {
        this.context = context;
        this.api = api;
    }

    public async Task<StoredSave> Load()
    {
        var response = await api.CloudSaveData.GetProtectedItemsAsync(context, context.AccessToken, context.ProjectId,
            context.PlayerId!, new List<string> { Key });
        var item = response.Data.Results.FirstOrDefault(x => x.Key == Key);
        return new StoredSave { Json = item?.Value?.ToString(), WriteLock = item?.WriteLock };
    }

    public async Task Save(string json, string? writeLock)
    {
        try
        {
            await api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId,
                context.PlayerId!, new SetItemBody(Key, json, writeLock!));
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict)
        {
            throw new WriteConflictException();
        }
    }
}
