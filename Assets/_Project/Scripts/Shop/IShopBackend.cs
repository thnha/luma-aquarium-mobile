using System.Threading.Tasks;
using Lumaquarium.UI;

namespace Lumaquarium.Shop
{
    // Every change to the shop save goes through a backend; reading stays with FishShopInventory. Each call returns
    // ShopError.None when done, or why not. slot is null or "" for a decoration.
    public interface IShopBackend
    {
        Task<string> Buy(string id, int price, string slot);
        Task<string> Select(string slot, string id);
    }

    // The rules run on the device, on the local save: the Editor (unless it uses the server) and the tests.
    public sealed class LocalShopBackend : IShopBackend
    {
        private readonly string storageKey;

        public LocalShopBackend(string storageKey = FishShopInventory.DefaultStorageKey) => this.storageKey = storageKey;

        public Task<string> Buy(string id, int price, string slot) => Task.FromResult(new FishShopInventory(storageKey).Buy(id, price, slot));
        public Task<string> Select(string slot, string id) => Task.FromResult(new FishShopInventory(storageKey).Select(slot, id));
    }
}
