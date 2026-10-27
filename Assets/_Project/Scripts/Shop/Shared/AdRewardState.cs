#nullable disable
using System;
using System.Collections.Generic;

namespace Lumaquarium.Shop
{
    [Serializable]
    public sealed class AdRewardSettings
    {
        public int coins = 20;
        public int dailyLimit = 10;
        public int cooldownSeconds = 60;
    }

    // Stored with the wallet in one protected Cloud Save write. Unix seconds are always server UTC.
    [Serializable]
    public sealed class AdRewardProgress
    {
        public string day = "";
        public int claimed;
        public long lastClaimUtc;
        public List<string> recentClaims = new List<string>();

        public AdRewardProgress Copy() => new AdRewardProgress
        {
            day = day, claimed = claimed, lastClaimUtc = lastClaimUtc,
            recentClaims = new List<string>(recentClaims ?? new List<string>())
        };
    }

    public sealed class AdRewardStatus
    {
        public int Coins { get; set; }
        public int Remaining { get; set; }
        public int RetryAfterSeconds { get; set; }
    }
}
