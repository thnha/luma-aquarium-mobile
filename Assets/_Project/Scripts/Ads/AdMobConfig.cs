using System;
using UnityEngine;

namespace Lumaquarium.Ads
{
    [Serializable]
    public sealed class AdMobConfig
    {
        public const string IosTestRewardedId = "ca-app-pub-3940256099942544/1712485313";
        public bool useTestAds = true;
        public string iosRewardedAdUnitId = "";

        public string RewardedId => useTestAds ? IosTestRewardedId : iosRewardedAdUnitId;

        public static AdMobConfig Load()
        {
            var json = Resources.Load<TextAsset>("AdMobConfig");
            return json != null ? JsonUtility.FromJson<AdMobConfig>(json.text) : new AdMobConfig();
        }
    }
}
