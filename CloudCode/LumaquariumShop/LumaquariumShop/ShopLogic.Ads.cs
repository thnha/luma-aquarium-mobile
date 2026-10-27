using System.Globalization;
using Lumaquarium.Shop;

namespace LumaquariumShop;

public sealed partial class ShopLogic
{
    public async Task<ShopReply> GetAdRewardStatus() => WithAdStatus(await GetState(), utcNow());

    public async Task<ShopReply> ClaimAdReward(string requestId)
    {
        var now = utcNow();
        bool validId = Guid.TryParseExact(requestId, "N", out _);
        var reply = await Change(save =>
        {
            var settings = catalog.adRewards ?? new AdRewardSettings();
            if (!validId || settings.coins <= 0 || settings.dailyLimit <= 0 || settings.cooldownSeconds < 0)
                return ShopError.Rejected;
            if (save.ads.recentClaims.Contains(requestId)) return ShopError.None;
            var status = AdStatus(save.ads, now);
            if (status.Remaining == 0) return ShopError.AdDailyLimit;
            if (status.RetryAfterSeconds > 0) return ShopError.AdCooldown;
            string day = AdDay(now);
            if (save.ads.day != day) { save.ads.day = day; save.ads.claimed = 0; }
            save.ads.claimed++;
            save.ads.lastClaimUtc = now.ToUnixTimeSeconds();
            save.ads.recentClaims.Add(requestId);
            // Keep retry IDs across midnight, bounded independently of the day's counter.
            if (save.ads.recentClaims.Count > 100) save.ads.recentClaims.RemoveAt(0);
            ShopSave.Add(save.wallet, ShopSave.Coins, settings.coins);
            return ShopError.None;
        }, requireExisting: true);
        return WithAdStatus(reply, now);
    }

    private ShopReply WithAdStatus(ShopReply reply, DateTimeOffset now)
    {
        var save = Parse(reply.State);
        if (save != null) reply.Ads = AdStatus(save.ads, now);
        return reply;
    }

    private AdRewardStatus AdStatus(AdRewardProgress progress, DateTimeOffset now)
    {
        var settings = catalog.adRewards ?? new AdRewardSettings();
        int remaining = Math.Max(0, settings.dailyLimit - (progress.day == AdDay(now) ? progress.claimed : 0));
        long wait = progress.lastClaimUtc == 0 ? 0 : Math.Max(0, progress.lastClaimUtc + settings.cooldownSeconds - now.ToUnixTimeSeconds());
        if (remaining == 0)
        {
            var local = now.ToOffset(TimeSpan.FromHours(7));
            wait = Math.Max(wait, (long)Math.Ceiling((local.Date.AddDays(1) - local.DateTime).TotalSeconds));
        }
        return new AdRewardStatus { Coins = settings.coins, Remaining = remaining, RetryAfterSeconds = (int)Math.Min(int.MaxValue, wait) };
    }

    private static string AdDay(DateTimeOffset now) => now.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
