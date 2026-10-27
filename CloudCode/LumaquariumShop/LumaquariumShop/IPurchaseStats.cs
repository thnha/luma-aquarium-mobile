namespace LumaquariumShop;

// How many times each item was bought, per month, across all players: Cloud Save in the module, memory in the tests.
public interface IPurchaseStats
{
    Task Add(string month, string itemId, int count);
}
