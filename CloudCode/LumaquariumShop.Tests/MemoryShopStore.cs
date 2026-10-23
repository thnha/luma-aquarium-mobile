using LumaquariumShop;

namespace LumaquariumShop.Tests;

// One player's save in memory, behaving like Cloud Save: the write lock is a counter, a write with no lock always
// writes and a write with a lock must match. Conflicts makes the next saves fail as if another call had written
// first; RacingJson is written over the next lockless save, as if another call created the save at the same time.
public sealed class MemoryShopStore : IShopStore
{
    public string? Json, RacingJson;
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
        if (writeLock != null && writeLock != version.ToString()) throw new WriteConflictException();
        Json = json;
        if (writeLock == null && RacingJson != null)
        {
            Json = RacingJson;
            RacingJson = null;
        }
        version++;
        Saves++;
        return Task.CompletedTask;
    }
}
