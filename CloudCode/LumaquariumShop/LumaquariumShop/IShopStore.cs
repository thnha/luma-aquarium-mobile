namespace LumaquariumShop;

// The player's save as stored: null Json when the player has none yet. WriteLock is what a write must send back
// to prove nothing changed the save in between.
public sealed class StoredSave
{
    public string? Json { get; init; }
    public string? WriteLock { get; init; }
}

// Thrown by IShopStore.Save when the save changed since it was read.
public sealed class WriteConflictException : Exception { }

// Where one player's shop save lives: Cloud Save in the module, memory in the tests.
public interface IShopStore
{
    Task<StoredSave> Load();
    Task Save(string json, string? writeLock);
}
