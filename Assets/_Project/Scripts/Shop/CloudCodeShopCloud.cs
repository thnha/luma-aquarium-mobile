using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

namespace Lumaquarium.Shop
{
    // Calls the Cloud Code module, signing in anonymously first (the player types nothing; the session is kept on
    // the device). A failed connection is ShopOfflineException; any other failure is a refused change.
    public sealed class CloudCodeShopCloud : IShopCloud
    {
        public const string Module = "LumaquariumShop";

        public async Task<ShopReply> Call(string function, Dictionary<string, object> args)
        {
            if (Account.AccountSignIn.ProgressBlocked) return new ShopReply { Error = ShopError.Rejected };
            if (Application.internetReachability == NetworkReachability.NotReachable) throw new ShopOfflineException("no network");
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                if (Account.AccountSignIn.ProgressBlocked) return new ShopReply { Error = ShopError.Rejected };
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return await CloudCodeService.Instance.CallModuleEndpointAsync<ShopReply>(Module, function, args);
            }
            catch (RequestFailedException e) when (e.ErrorCode == CommonErrorCodes.TransportError)
            {
                throw new ShopOfflineException(e.Message, e);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Shop] {function} failed: {e}");
                return new ShopReply { Error = ShopError.Rejected };
            }
        }
    }
}
