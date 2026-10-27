#nullable disable
namespace Lumaquarium.Shop
{
    // Why a shop change was not made. None is the empty string.
    public static class ShopError
    {
        public const string None = "";
        public const string NotEnoughCoins = "NotEnoughCoins";
        public const string Owned = "Owned";
        public const string PriceChanged = "PriceChanged";
        public const string Offline = "Offline";
        public const string Rejected = "Rejected";
        public const string SaveUnreadable = "SaveUnreadable";
        public const string AlreadyImported = "AlreadyImported";
        public const string AdCooldown = "AdCooldown";
        public const string AdDailyLimit = "AdDailyLimit";
        // Apple says the purchase is not one this game can pay out (unknown, refunded, another app or product):
        // final, unlike Rejected or Offline, so the game finishes the transaction without coins.
        public const string PurchaseInvalid = "PurchaseInvalid";
    }

    // What every LumaquariumShop function returns: the error (None when done) and the player's save as it now is
    // (ShopSave JSON), or null when it could not be read. Properties, as Cloud Code serializes the reply.
    public sealed class ShopReply
    {
        public string Error { get; set; } = ShopError.None;
        public string State { get; set; }
        public AdRewardStatus Ads { get; set; }
    }
}
