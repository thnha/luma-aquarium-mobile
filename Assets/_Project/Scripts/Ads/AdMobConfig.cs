using System;
using UnityEngine;

namespace Lumaquarium.Ads
{
    [Serializable]
    public sealed class AdMobConfig
    {
        public const string IosTestRewardedId = "ca-app-pub-3940256099942544/1712485313";
        public const string AndroidTestRewardedId = "ca-app-pub-3940256099942544/5224354917";
        public bool useTestAds = true;
        public string iosRewardedAdUnitId = "";
        public string androidRewardedAdUnitId = "";

#if UNITY_ANDROID
        public string RewardedId => useTestAds ? AndroidTestRewardedId : androidRewardedAdUnitId;
#else
        public string RewardedId => useTestAds ? IosTestRewardedId : iosRewardedAdUnitId;
#endif

        public static AdMobConfig Load()
        {
            var json = Resources.Load<TextAsset>("AdMobConfig");
            return json != null ? JsonUtility.FromJson<AdMobConfig>(json.text) : new AdMobConfig();
        }
    }
}
