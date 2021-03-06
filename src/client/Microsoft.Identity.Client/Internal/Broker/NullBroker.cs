// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Instance.Discovery;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client.Internal.Broker
{
    /// <summary>
    /// For platforms that do not support a broker
    /// </summary>
    internal class NullBroker : IBroker
    {
        public bool IsBrokerInstalledAndInvokable()
        {
            return false;
        }      

        public Task<MsalTokenResponse> AcquireTokenInteractiveAsync(AuthenticationRequestParameters authenticationRequestParameters, AcquireTokenInteractiveParameters acquireTokenInteractiveParameters)
        {
            throw new PlatformNotSupportedException();
        }

        public Task<MsalTokenResponse> AcquireTokenSilentAsync(AuthenticationRequestParameters authenticationRequestParameters, AcquireTokenSilentParameters acquireTokenSilentParameters)
        {
            throw new PlatformNotSupportedException();
        }

        public void HandleInstallUrl(string appLink)
        {
            throw new PlatformNotSupportedException();
        }     

        public Task RemoveAccountAsync(ApplicationConfiguration appConfig, IAccount account)
        {
            throw new PlatformNotSupportedException();
        }

        public Task<MsalTokenResponse> AcquireTokenSilentDefaultUserAsync(AuthenticationRequestParameters authenticationRequestParameters, AcquireTokenSilentParameters acquireTokenSilentParameters)
        {
            throw new PlatformNotSupportedException();
        }

        Task<IReadOnlyList<IAccount>> IBroker.GetAccountsAsync(
            string clientID, 
            string redirectUri,             
            AuthorityInfo authorityInfo,
            ICacheSessionManager cacheSessionManager, 
            IInstanceDiscoveryManager instanceDiscoveryManager)
        {
            throw new NotImplementedException();
        }
    }
}
