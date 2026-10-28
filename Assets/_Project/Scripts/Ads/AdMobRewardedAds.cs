using System;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using Lumaquarium.Shop;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Lumaquarium.Ads
{
    // One SDK session across aquarium scenes. Only the earned callback records a pending server claim.
    public sealed class AdMobRewardedAds : MonoBehaviour
    {
        private static AdMobRewardedAds instance;
        private RewardedAd ad;
        private AdMobConfig config;
        private bool consentRunning, initializing, initialized, loading, showing, syncing;
        private float retryLoadAt, nextEligibleAt;
        private float loadedAt;
        private int loadGeneration;
        private ServerShopBackend statusServer;
        public event Action Changed;
        public event Action<string> Feedback;
        public event Action<int> RewardGranted; // coins the server just added
        public bool Busy => showing || syncing || consentRunning;
        public AdRewardStatus Status { get; private set; }
        public string Error { get; private set; } = "";
        public bool PrivacyRequired => ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
        public int WaitSeconds => Mathf.Max(0, Mathf.CeilToInt(nextEligibleAt - Time.realtimeSinceStartup));
        public bool CanShow => !Busy && Status != null && Status.Remaining > 0 && WaitSeconds == 0
            && ConsentInformation.CanRequestAds() && ad != null && ad.CanShowAd();
        private string PendingKey => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn
            && !Account.AccountSignIn.ProgressBlocked
            ? "Lumaquarium.Ads.Pending." + AuthenticationService.Instance.PlayerId : null;
        public bool HasPendingClaim => PendingKey != null && PlayerPrefs.HasKey(PendingKey);

        public static bool Supported
        {
            get
            {
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static AdMobRewardedAds Get()
        {
            if (!Supported) return null;
            if (instance == null)
            {
                instance = new GameObject("AdMobRewardedAds").AddComponent<AdMobRewardedAds>();
                DontDestroyOnLoad(instance.gameObject);
                instance.config = AdMobConfig.Load();
                MobileAdsEventExecutor.Initialize();
            }
            return instance;
        }

        private static void OnMain(Action action) => MobileAdsEventExecutor.ExecuteInUpdate(action);

        public void Prepare()
        {
            if (!initialized && !consentRunning && !initializing) GatherConsent();
            _ = SyncStatus();
        }

        private void GatherConsent()
        {
            consentRunning = true;
            Error = "";
            ConsentInformation.Update(new ConsentRequestParameters(), error => OnMain(() =>
            {
                if (error != null) { ConsentFinished(error.Message); return; }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() => ConsentFinished(formError?.Message)));
            }));
        }

        private void ConsentFinished(string error)
        {
            consentRunning = false;
            if (error != null) Debug.LogWarning("[AdMob] Consent: " + error);
            if (!ConsentInformation.CanRequestAds())
            {
                Error = "Chưa thể tải quảng cáo. Vui lòng thử lại.";
                Changed?.Invoke();
                return;
            }
            if (initialized) { Load(); return; }
            if (initializing) return;
            initializing = true;
            MobileAds.Initialize(_ => OnMain(() =>
            {
                initializing = false;
                initialized = true;
                Load();
            }));
        }

        public void ShowPrivacyOptions()
        {
            if (Busy || !PrivacyRequired) return;
            consentRunning = true;
            loadGeneration++;
            loading = false;
            ad?.Destroy();
            ad = null;
            ConsentForm.ShowPrivacyOptionsForm(error => OnMain(() => ConsentFinished(error?.Message)));
        }

        private void Load()
        {
            if (!initialized || loading || showing || consentRunning || !ConsentInformation.CanRequestAds()) return;
            if (string.IsNullOrWhiteSpace(config.RewardedId))
            {
                Error = "Chưa cấu hình quảng cáo.";
                Changed?.Invoke();
                return;
            }
            ad?.Destroy();
            ad = null;
            loading = true;
            int generation = loadGeneration;
            RewardedAd.Load(config.RewardedId, new AdRequest(), (loaded, error) => OnMain(() =>
            {
                if (this == null || generation != loadGeneration) { loaded?.Destroy(); return; }
                loading = false;
                if (error != null || loaded == null)
                {
                    loaded?.Destroy();
                    Error = "Chưa có quảng cáo. Vui lòng thử lại sau.";
                    retryLoadAt = Time.realtimeSinceStartup + 30;
                    Debug.LogWarning("[AdMob] Rewarded load: " + error);
                }
                else
                {
                    ad = loaded;
                    loadedAt = Time.realtimeSinceStartup;
                    Error = "";
                    retryLoadAt = 0;
                    loaded.OnAdFullScreenContentClosed += () => OnMain(() => Finished(loaded));
                    loaded.OnAdFullScreenContentFailed += failure => OnMain(() =>
                    {
                        Feedback?.Invoke("Không mở được quảng cáo. Vui lòng thử lại.");
                        Finished(loaded);
                    });
                }
                Changed?.Invoke();
            }));
        }

        private void Finished(RewardedAd shown)
        {
            if (ad == shown) ad = null;
            shown.Destroy();
            showing = false;
            Changed?.Invoke();
            Load();
        }

        public async Task SyncStatus()
        {
            if (syncing || showing || !ShopService.UsesServer) return;
            syncing = true;
            try
            {
                await ShopService.Prepare();
                var server = ShopService.Server;
                if (server == null) return;
                if (HasPendingClaim) await ClaimPending(server);
                string error = await server.GetAdRewardStatus();
                if (error == ShopError.None && server.AdStatus != null)
                {
                    statusServer = server;
                    Status = server.AdStatus;
                    nextEligibleAt = Time.realtimeSinceStartup + Status.RetryAfterSeconds;
                }
                else
                {
                    Status = null;
                    Feedback?.Invoke(ShopService.Message(error));
                }
            }
            catch (Exception exception)
            {
                Status = null;
                Debug.LogException(exception);
            }
            finally { syncing = false; Changed?.Invoke(); }
        }

        public void Show()
        {
            if (!CanShow || HasPendingClaim || ShopService.Server != statusServer) return;
            string key = PendingKey;
            if (key == null) return;
            string requestId = Guid.NewGuid().ToString("N");
            bool earned = false;
            showing = true;
            Changed?.Invoke();
            ad.Show(reward => OnMain(() =>
            {
                if (earned) return;
                earned = true;
                PlayerPrefs.SetString(key, requestId);
                PlayerPrefs.Save();
                _ = SyncEarnedReward();
            }));
        }

        private async Task SyncEarnedReward()
        {
            // The callback can arrive before the fullscreen closed event. Claim immediately, even then.
            if (syncing) return;
            syncing = true;
            try
            {
                await ClaimPending(statusServer);
                Status = statusServer.AdStatus;
                if (Status != null) nextEligibleAt = Time.realtimeSinceStartup + Status.RetryAfterSeconds;
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { syncing = false; Changed?.Invoke(); }
        }

        private async Task ClaimPending(ServerShopBackend server)
        {
            string key = PendingKey;
            if (key == null || !PlayerPrefs.HasKey(key) || server == null) return;
            string error = await server.ClaimAdReward(PlayerPrefs.GetString(key));
            if (error == ShopError.None || error == ShopError.AdDailyLimit || error == ShopError.AdCooldown)
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            Feedback?.Invoke(error == ShopError.None ? "Đã nhận thưởng quảng cáo!" : ShopService.Message(error));
            if (error == ShopError.None && Status != null) RewardGranted?.Invoke(Status.Coins);
        }

        private void Update()
        {
            if (!showing && !loading && !consentRunning && initialized &&
                ((ad == null && retryLoadAt > 0 && Time.realtimeSinceStartup >= retryLoadAt) ||
                 (ad != null && Time.realtimeSinceStartup - loadedAt >= 3300))) Load();
        }

        private void OnApplicationPause(bool paused) { if (!paused && initialized) _ = SyncStatus(); }
        private void OnDestroy() { ad?.Destroy(); if (instance == this) instance = null; }
    }
}
