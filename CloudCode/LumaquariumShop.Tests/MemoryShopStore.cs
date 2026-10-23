using LumaquariumShop;

namespace LumaquariumShop.Tests;

// One player's save in memory. The write lock is a counter; Conflicts makes the next saves fail as if another
// call had written first.
public sealed class MemoryShopStore : IShopStore
{
    public string? Json;
    public int Conflicts, Saves;
    private int version;

    public Task<StoredSave> Load() =>
        Task.FromResult(new StoredSave { Json = Json, WriteLock = Json == null ? null : version.ToString() });

    public Task Save(string json, string? writeLock)
    {
        if (Conflicts > 0)
        {
            Conflicts--;
            version++;
            throw new WriteConflictException();
        }
        if (Json != null && writeLock != version.ToString()) throw new WriteConflictException();
        Json = json;
        version++;
        Saves++;
        return Task.CompletedTask;
    }
}
