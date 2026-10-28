using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Lumaquarium.Account
{
    internal static class AppleTokenRevocation
    {
#if UNITY_IOS && !UNITY_EDITOR
        private delegate void Completion(int success);
        private static readonly Completion callback = Complete;
        private static TaskCompletionSource<bool> pending;
        [DllImport("__Internal")] private static extern void LumaRevokeAppleToken(string code, Completion completion);

        [AOT.MonoPInvokeCallback(typeof(Completion))]
        private static void Complete(int success)
        {
            if (success != 0) pending?.TrySetResult(true);
            else pending?.TrySetException(new InvalidOperationException("Apple token revocation failed."));
        }
#endif
        public static Task Revoke(string authorizationCode)
        {
            if (string.IsNullOrEmpty(authorizationCode)) throw new InvalidOperationException("Apple authorization code missing.");
#if UNITY_IOS && !UNITY_EDITOR
            pending = new TaskCompletionSource<bool>();
            LumaRevokeAppleToken(authorizationCode, callback);
            return pending.Task;
#else
            throw new PlatformNotSupportedException("Apple token revocation requires iOS.");
#endif
        }
    }
}
