using System.Net;
using System.Text.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace LumaquariumShop;

// The counts are the private custom item "purchase-stats" (only the server reads or writes it), one key per month
// holding { itemId: count }. Every purchase writes the same key, so a write sends back the lock it read with and a
// conflict reads again. The first write of a month has no lock: two purchases at that exact moment may count once.
public sealed class CloudSavePurchaseStats : IPurchaseStats
{
    public const string CustomId = "purchase-stats";
    private const int Attempts = 5;
    private readonly IExecutionContext context;
    private readonly IGameApiClient api;

    public CloudSavePurchaseStats(IExecutionContext context, IGameApiClient api)
    {
        this.context = context;
        this.api = api;
    }

    public async Task Add(string month, string itemId, int count)
    {
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var response = await api.CloudSaveData.GetPrivateCustomItemsAsync(context, context.ServiceToken, context.ProjectId,
                CustomId, new List<string> { month });
            var item = response.Data.Results.FirstOrDefault(x => x.Key == month);
            var counts = item?.Value == null
                ? new Dictionary<string, int>()
                : JsonSerializer.Deserialize<Dictionary<string, int>>(item.Value.ToString()!) ?? new Dictionary<string, int>();
            counts[itemId] = counts.GetValueOrDefault(itemId) + count;
            try
            {
                await api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId,
                    CustomId, new SetItemBody(month, counts, item?.WriteLock!));
                return;
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict) { }
        }
    }
}
