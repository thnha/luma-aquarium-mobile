using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
#if LUMA_SOCIAL_SIGNIN
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;
using Firebase;
using Firebase.Auth;
using Google;
#endif

namespace Lumaquarium.Account
{
    public enum SignInResult { Success, Canceled, Unavailable, Offline, Failed }

    // Apple / Google sign-in through Firebase, then into Unity Authentication with the Firebase ID token
    // (OpenID Connect provider "oidc-firebase", issuer https://securetoken.google.com/<firebase project id>).
    // The first sign-in links the current anonymous player, so coins and fish stay; if that Firebase account
    // already belongs to another player (reinstall, new device), the device switches to that player instead.
    // Unity Authentication caches the session token, so CloudCodeShopCloud's SignInAnonymouslyAsync keeps
    // restoring the signed-in player on later launches.
    // The native SDKs (Firebase Auth, AppleAuth, Google Sign-In) compile in only with LUMA_SOCIAL_SIGNIN.
    public static class AccountSignIn
    {
        public const string UnityOidcProvider = "oidc-firebase";
        // OAuth client of type 3 ("Web client") in google-services.json / the Google Cloud console.
        public const string GoogleWebClientId = "";

        public static bool AppleSupported =>
#if LUMA_SOCIAL_SIGNIN
            Application.platform == RuntimePlatform.IPhonePlayer && AppleAuthManager.IsCurrentPlatformSupported;
#else
            false;
#endif

        public static bool GoogleSupported =>
#if LUMA_SOCIAL_SIGNIN
            !Application.isEditor && !string.IsNullOrEmpty(GoogleWebClientId);
#else
            false;
#endif

        // True once the current Unity player is tied to a Firebase account.
        public static bool IsLinked =>
            UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn
            && AuthenticationService.Instance.PlayerInfo?.GetOpenIdConnectId(UnityOidcProvider) != null;

#if LUMA_SOCIAL_SIGNIN
        private static AppleAuthManager apple;
#endif

        // AppleAuthManager delivers its callbacks from Update; LoginGameScreen pumps it every frame.
        public static void Update()
        {
#if LUMA_SOCIAL_SIGNIN
            apple?.Update();
#endif
        }

        public static Task<SignInResult> SignInWithApple() => SignIn(true);
        public static Task<SignInResult> SignInWithGoogle() => SignIn(false);

        private static async Task<SignInResult> SignIn(bool withApple)
        {
            if (withApple ? !AppleSupported : !GoogleSupported) return SignInResult.Unavailable;
            if (Application.internetReachability == NetworkReachability.NotReachable) return SignInResult.Offline;
#if LUMA_SOCIAL_SIGNIN
            try
            {
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (status != DependencyStatus.Available)
                {
                    Debug.LogError($"[Account] Firebase unavailable: {status}");
                    return SignInResult.Unavailable;
                }

                var credential = withApple ? await AppleCredential() : await GoogleCredential();
                if (credential == null) return SignInResult.Canceled;
                var user = await FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential);
                await SignInUnity(await user.TokenAsync(false));
                return SignInResult.Success;
            }
            catch (RequestFailedException e) when (e.ErrorCode == CommonErrorCodes.TransportError)
            {
                return SignInResult.Offline;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Account] {(withApple ? "Apple" : "Google")} sign-in failed: {e}");
                return SignInResult.Failed;
            }
#else
            await Task.CompletedTask;
            return SignInResult.Unavailable;
#endif
        }

        private static async Task SignInUnity(string firebaseIdToken)
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            var auth = AuthenticationService.Instance;
            // Restore the cached guest first so a first sign-in links it rather than starting a new player.
            if (!auth.IsSignedIn && auth.SessionTokenExists) await auth.SignInAnonymouslyAsync();
            if (auth.IsSignedIn) await auth.GetPlayerInfoAsync();

            if (auth.IsSignedIn && auth.PlayerInfo?.GetOpenIdConnectId(UnityOidcProvider) == null)
            {
                try
                {
                    await auth.LinkWithOpenIdConnectAsync(UnityOidcProvider, firebaseIdToken);
                    return;
                }
                catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // This Firebase account already has a player; switch to it below.
                }
            }

            if (auth.IsSignedIn) auth.SignOut(true);
            await auth.SignInWithOpenIdConnectAsync(UnityOidcProvider, firebaseIdToken);
        }

#if LUMA_SOCIAL_SIGNIN
        // Apple gets the SHA-256 of the nonce, Firebase the raw nonce, so a stolen Apple token cannot be replayed.
        private static Task<Credential> AppleCredential()
        {
            apple ??= new AppleAuthManager(new PayloadDeserializer());
            var rawNonce = Nonce();
            var done = new TaskCompletionSource<Credential>();
            apple.LoginWithAppleId(
                new AppleAuthLoginArgs(LoginOptions.None, Sha256(rawNonce)),
                credential =>
                {
                    if (credential is IAppleIDCredential id && id.IdentityToken != null)
                    {
                        var idToken = Encoding.UTF8.GetString(id.IdentityToken);
                        var code = id.AuthorizationCode != null ? Encoding.UTF8.GetString(id.AuthorizationCode) : null;
                        done.TrySetResult(OAuthProvider.GetCredential("apple.com", idToken, rawNonce, code));
                    }
                    else done.TrySetException(new InvalidOperationException("Apple returned no identity token"));
                },
                error =>
                {
                    if (error.GetAuthorizationErrorCode() == AuthorizationErrorCode.Canceled) done.TrySetResult(null);
                    else done.TrySetException(new InvalidOperationException($"Apple sign-in error {error.Code}: {error.LocalizedDescription}"));
                });
            return done.Task;
        }

        private static async Task<Credential> GoogleCredential()
        {
            GoogleSignIn.Configuration ??= new GoogleSignInConfiguration
            {
                WebClientId = GoogleWebClientId,
                RequestIdToken = true,
                UseGameSignIn = false,
            };
            try
            {
                var user = await GoogleSignIn.DefaultInstance.SignIn();
                return GoogleAuthProvider.GetCredential(user.IdToken, null);
            }
            catch (GoogleSignIn.SignInException e) when (e.Status == GoogleSignInStatusCode.Canceled)
            {
                return null;
            }
        }
#endif

        private static string Nonce()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string Sha256(string value)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }
}
