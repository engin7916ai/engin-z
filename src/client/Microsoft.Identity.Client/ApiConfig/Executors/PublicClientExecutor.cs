﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.UI;

namespace Microsoft.Identity.Client.ApiConfig.Executors
{
    internal class PublicClientExecutor : AbstractExecutor, IPublicClientApplicationExecutor
    {
        private readonly PublicClientApplication _publicClientApplication;

        public PublicClientExecutor(IServiceBundle serviceBundle, PublicClientApplication publicClientApplication)
            : base(serviceBundle, publicClientApplication)
        {
            _publicClientApplication = publicClientApplication;
        }

        public async Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenInteractiveParameters interactiveParameters,
            CancellationToken cancellationToken)
        {
            var requestContext = CreateRequestContextAndLogVersionInfo(commonParameters.CorrelationId);

            AuthenticationRequestParameters requestParams = _publicClientApplication.CreateRequestParameters(
                commonParameters,
                requestContext,
                _publicClientApplication.UserTokenCacheInternal);

            requestParams.LoginHint = interactiveParameters.LoginHint;
            requestParams.Account = interactiveParameters.Account;

            InteractiveRequest interactiveRequest = 
                new InteractiveRequest(requestParams, interactiveParameters);

            return await interactiveRequest.RunAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenWithDeviceCodeParameters deviceCodeParameters,
            CancellationToken cancellationToken)
        {
            var requestContext = CreateRequestContextAndLogVersionInfo(commonParameters.CorrelationId);

            var requestParams = _publicClientApplication.CreateRequestParameters(
                commonParameters,
                requestContext,
                _publicClientApplication.UserTokenCacheInternal);

            var handler = new DeviceCodeRequest(
                ServiceBundle,
                requestParams,
                deviceCodeParameters);

            return await handler.RunAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenByIntegratedWindowsAuthParameters integratedWindowsAuthParameters,
            CancellationToken cancellationToken)
        {
#if NET_CORE
            if (string.IsNullOrWhiteSpace(integratedWindowsAuthParameters.Username))
            {
                throw new PlatformNotSupportedException("AcquireTokenByIntegratedWindowsAuth is not supported on .net core without adding .WithUsername() because " +
                    "MSAL cannot determine the username (UPN) of the currently logged in user. Please use .WithUsername() before calling ExecuteAsync(). " +
                    "For more details see https://aka.ms/msal-net-iwa");
            }
#endif
            var requestContext = CreateRequestContextAndLogVersionInfo(commonParameters.CorrelationId);

            var requestParams = _publicClientApplication.CreateRequestParameters(
                commonParameters,
                requestContext,
                _publicClientApplication.UserTokenCacheInternal);

            var handler = new IntegratedWindowsAuthRequest(
                ServiceBundle,
                requestParams,
                integratedWindowsAuthParameters);

            return await handler.RunAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenByUsernamePasswordParameters usernamePasswordParameters,
            CancellationToken cancellationToken)
        {
            var requestContext = CreateRequestContextAndLogVersionInfo(commonParameters.CorrelationId);

            var requestParams = _publicClientApplication.CreateRequestParameters(
                commonParameters,
                requestContext,
                _publicClientApplication.UserTokenCacheInternal);

            var handler = new UsernamePasswordRequest(
                ServiceBundle,
                requestParams,
                usernamePasswordParameters);

            return await handler.RunAsync(cancellationToken).ConfigureAwait(false);
        }

      

       
    }
}
