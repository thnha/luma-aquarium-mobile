using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lumaquarium.Shop;
using Lumaquarium.UI;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;
#if LUMA_SOCIAL_SIGNIN
using Firebase;
using Firebase.Auth;
#endif

namespace Lumaquarium.Account
{
    public enum DeleteAccountResult { Success, Canceled, Offline, Unavailable, WrongAccount, Failed }

    public static partial class AccountSignIn
    {
        public const string DeletionStageKey = "Lumaquarium.Account.DeletionStage";
        private const string DeletionPlayerKey = "Lumaquarium.Account.DeletionPlayer";
        private const string DeletionIdentityKey = "Lumaquarium.Account.DeletionIdentity";
        private const string DeletionProviderKey = "Lumaquarium.Account.DeletionProvider";
        public static bool DeletionPending => PlayerPrefs.GetInt(DeletionStageKey, 0) > 0;
        // Remains true while the old scene unloads: OnDestroy and delayed shop replies must not restore saves.
        private static bool localProgressDeleted;
        public static bool ProgressBlocked => DeletionPending || localProgressDeleted;
        private static Task<DeleteAccountResult> deleting;
#if LUMA_SOCIAL_SIGNIN
        private static string appleAuthorizationCode;
        private static FirebaseUser deletionUser;
#endif

        public static void BeginNewSession()
        {
            if (!DeletionPending) localProgressDeleted = false;
        }

        public static Task<DeleteAccountResult> DeleteAccount()
        {
            if (deleting != null) return deleting;
            var task = DeleteAccountOnce();
            if (!task.IsCompleted) deleting = task;
            return task;
        }

        private static async Task<DeleteAccountResult> DeleteAccountOnce()
        {
            try
            {
                var stage = (AccountDeletionStage)PlayerPrefs.GetInt(DeletionStageKey, 0);
                if (stage == AccountDeletionStage.AccountDeleted)
                {
                    ClearLocalProgress();
                    return DeleteAccountResult.Success;
                }
                if (Application.internetReachability == NetworkReachability.NotReachable) return DeleteAccountResult.Offline;
#if LUMA_SOCIAL_SIGNIN
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                var auth = AuthenticationService.Instance;
                if (!auth.IsSignedIn)
                {
                    // Never create a new guest in order to delete an account.
                    if (!DeletionPending || !auth.SessionTokenExists) return DeleteAccountResult.Failed;
                    try { await auth.SignInAnonymouslyAsync(); }
                    catch (RequestFailedException e) when (stage >= AccountDeletionStage.AccountDeletionStarted && e.ErrorCode == CommonErrorCodes.NotFound)
                    {
                        ClearLocalProgress();
                        return DeleteAccountResult.Success;
                    }
                }
                string player = auth.PlayerId;
                if (DeletionPending && PlayerPrefs.GetString(DeletionPlayerKey) != player) return DeleteAccountResult.WrongAccount;
                string identity = PlayerPrefs.GetString(DeletionIdentityKey);
                if (!DeletionPending)
                {
                    await auth.GetPlayerInfoAsync();
                    identity = auth.PlayerInfo?.GetOpenIdConnectId(UnityOidcProvider);
                    if (string.IsNullOrEmpty(identity)) return DeleteAccountResult.Unavailable;
                }
                string expectedIdentity = identity;
                await AccountDeletionFlow.Run(stage,
                    () => PrepareIdentityDeletion(expectedIdentity),
                    async () =>
                    {
                        if (ShopService.Server != null) await ShopService.Server.WaitForPendingRequests();
                        bool removed = await CloudCodeService.Instance.CallModuleEndpointAsync<bool>(
                            CloudCodeShopCloud.Module, "DeletePlayerData", new Dictionary<string, object>());
                        if (!removed) throw new InvalidOperationException("Cloud data deletion was not acknowledged.");
                    },
                    () => DeleteFirebaseIdentity(expectedIdentity),
                    async () =>
                    {
                        try { await auth.DeleteAccountAsync(); }
                        catch (RequestFailedException e) when (e.ErrorCode == CommonErrorCodes.NotFound) { }
                    },
                    next =>
                    {
                        PlayerPrefs.SetString(DeletionPlayerKey, player);
                        PlayerPrefs.SetString(DeletionIdentityKey, expectedIdentity);
                        PlayerPrefs.SetInt(DeletionStageKey, (int)next);
                        PlayerPrefs.Save();
                    }, ClearLocalProgress);
                return DeleteAccountResult.Success;
#else
                await Task.CompletedTask;
                return DeleteAccountResult.Unavailable;
#endif
            }
            catch (OperationCanceledException) { return DeleteAccountResult.Canceled; }
            catch (IdentityMismatchException) { return DeleteAccountResult.WrongAccount; }
            catch (RequestFailedException e) when (e.ErrorCode == CommonErrorCodes.TransportError) { return DeleteAccountResult.Offline; }
            catch (Exception e)
            {
                // Do not log credentials, provider responses or identity IDs.
                Debug.LogWarning($"[Account] Deletion incomplete ({e.GetType().Name}); retry is available.");
                return DeleteAccountResult.Failed;
            }
            finally { deleting = null; }
        }

        private sealed class IdentityMismatchException : Exception { }

#if LUMA_SOCIAL_SIGNIN
        private static async Task DeleteFirebaseIdentity(string expectedIdentity)
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available) throw new InvalidOperationException("Firebase unavailable.");
            // Credential absence is not evidence of deletion. Keep the checkpoint and refuse to report
            // success unless Firebase confirms deletion or returns UserNotFound for the original user.
            var user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null) throw new InvalidOperationException("Original Firebase session unavailable.");
            if (user.UserId != expectedIdentity) throw new IdentityMismatchException();
            try
            {
                await user.ReloadAsync();
                try { await user.DeleteAsync(); }
                catch (FirebaseException e) when ((AuthError)e.ErrorCode == AuthError.RequiresRecentLogin)
                {
                    await PrepareIdentityDeletion(expectedIdentity);
                    await deletionUser.DeleteAsync();
                }
            }
            catch (FirebaseException e) when ((AuthError)e.ErrorCode == AuthError.UserNotFound) { }
        }

        private static async Task PrepareIdentityDeletion(string expectedIdentity)
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available) throw new InvalidOperationException("Firebase unavailable.");
            var firebase = FirebaseAuth.DefaultInstance;
            var user = firebase.CurrentUser;
            string provider = PlayerPrefs.GetString(DeletionProviderKey);
            if (user != null)
            {
                if (user.UserId != expectedIdentity) throw new IdentityMismatchException();
                provider = user.ProviderData.Any(p => p.ProviderId == "apple.com") ? "apple.com" : "google.com";
            }
            // Never guess Apple for a Google-linked account on iOS. Normal sign-in records the provider;
            // older installations can recover it from Firebase's existing user.
            if (string.IsNullOrEmpty(provider)) throw new InvalidOperationException("Original provider unavailable.");
            bool withApple = provider == "apple.com";
            if (withApple ? !AppleSupported : !GoogleSupported) throw new InvalidOperationException("Provider unavailable.");
            var credential = withApple ? await AppleCredential() : await GoogleCredential();
            if (credential == null) throw new OperationCanceledException();
            if (user == null) user = await firebase.SignInWithCredentialAsync(credential);
            else await user.ReauthenticateAsync(credential);
            if (user.UserId != expectedIdentity)
            {
                firebase.SignOut();
                throw new IdentityMismatchException();
            }
            PlayerPrefs.SetString(DeletionProviderKey, withApple ? "apple.com" : "google.com");
            PlayerPrefs.Save();
            if (withApple) await AppleTokenRevocation.Revoke(appleAuthorizationCode);
            deletionUser = user;
        }
#endif

        private static void ClearLocalProgress()
        {
            localProgressDeleted = true;
#if LUMA_SOCIAL_SIGNIN
            try { FirebaseAuth.DefaultInstance.SignOut(); } catch (Exception) { }
            try { if (GoogleSupported) { ConfigureGoogle(); Google.GoogleSignIn.DefaultInstance.SignOut(); } } catch (Exception) { }
            deletionUser = null;
            appleAuthorizationCode = null;
#endif
            var language = Loc.Current;
            // All gameplay records, old migration backups, per-fish mood, layouts, ad claims, guest and SDK
            // sessions live in PlayerPrefs. Photos are user-created exports, not account progress.
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt(Loc.PrefsKey, (int)language);
            PlayerPrefs.Save();
            ShopService.Use(null);
        }
    }
}
