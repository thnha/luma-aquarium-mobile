using System.Net;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Api;

namespace LumaquariumShop;

public static class PlayerDataDeletion
{
    // No player-id parameter from the client: a player can delete only their own data.
    public static async Task Delete(IExecutionContext context, ICloudSaveDataApi api)
    {
        if (string.IsNullOrWhiteSpace(context.PlayerId)) throw new UnauthorizedAccessException("A player is required.");
        foreach (var delete in new Func<Task>[] {
            () => api.DeleteItemsAsync(context, context.ServiceToken, context.ProjectId, context.PlayerId),
            () => api.DeleteProtectedItemsAsync(context, context.ServiceToken, context.ProjectId, context.PlayerId),
            () => api.DeletePublicItemsAsync(context, context.ServiceToken, context.ProjectId, context.PlayerId),
            () => api.DeletePrivateItemsAsync(context, context.ServiceToken, context.ProjectId, context.PlayerId) })
        {
            try { await delete(); }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound) { } // safe to retry
        }
    }
}
