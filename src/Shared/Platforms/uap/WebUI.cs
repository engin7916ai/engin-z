﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
#if WINDOWS_APP

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.UI;
using Microsoft.Identity.Client.Utils;
using Windows.ApplicationModel.Core;
using Windows.Security.Authentication.Web;

namespace Microsoft.Identity.Client.Platforms.uap
{
    internal class WebUI : IWebUI
    {
        private const int WABRetryAttempts = 2;

        private readonly bool _useCorporateNetwork;
        private readonly bool _silentMode;
        private readonly RequestContext _requestContext;
        private bool _ssoMode = false;

        public WebUI(CoreUIParent parent, RequestContext requestContext)
        {
            _useCorporateNetwork = parent.UseCorporateNetwork;
            _silentMode = parent.UseHiddenBrowser;
            _requestContext = requestContext;
        }

        public async Task<AuthorizationResult> AcquireAuthorizationAsync(
            Uri authorizationUri,
            Uri redirectUri,
            RequestContext requestContext,
            CancellationToken cancellationToken)
        {
            WebAuthenticationResult webAuthenticationResult;
            WebAuthenticationOptions options = (_useCorporateNetwork &&
                                                (_ssoMode || redirectUri.Scheme == Constants.MsAppScheme))
                ? WebAuthenticationOptions.UseCorporateNetwork
                : WebAuthenticationOptions.None;

            if (_silentMode)
            {
                options |= WebAuthenticationOptions.SilentMode;
            }

            try
            {
                webAuthenticationResult = await RetryOperationHelper.ExecuteWithRetryAsync(
                    () => InvokeWABOnMainThreadAsync(authorizationUri, redirectUri, options),
                    WABRetryAttempts,
                    onAttemptFailed: (attemptNumber, exception) =>
                    {
                        _requestContext.Logger.Warning($"Attempt {attemptNumber} to call WAB failed");
                        _requestContext.Logger.WarningPii(exception);
                    })
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                requestContext.Logger.ErrorPii(ex);
                throw new MsalClientException(
                    MsalError.AuthenticationUiFailedError,
                    "Web Authentication Broker (WAB) authentication failed. To collect WAB logs, please follow https://aka.ms/msal-net-wab-logs",
                    ex);
            }

            AuthorizationResult result = ProcessAuthorizationResult(webAuthenticationResult);
            return result;
        }

        private async Task<WebAuthenticationResult> InvokeWABOnMainThreadAsync(
            Uri authorizationUri,
            Uri redirectUri,
            WebAuthenticationOptions options)
        {
            return await CoreApplication.MainView.CoreWindow.Dispatcher.RunTaskAsync(
                async () =>
                {
                    if (_ssoMode)
                    {
                        return await
                            WebAuthenticationBroker.AuthenticateAsync(options, authorizationUri)
                                .AsTask()
                                .ConfigureAwait(false);
                    }
                    else
                    {
                        return await WebAuthenticationBroker
                            .AuthenticateAsync(options, authorizationUri, redirectUri)
                            .AsTask()
                            .ConfigureAwait(false);
                    }
                })
                .ConfigureAwait(false);
        }

        private static AuthorizationResult ProcessAuthorizationResult(WebAuthenticationResult webAuthenticationResult)
        {
            AuthorizationResult result;
            switch (webAuthenticationResult.ResponseStatus)
            {
                case WebAuthenticationStatus.Success:
                    result = AuthorizationResult.FromUri(webAuthenticationResult.ResponseData);
                    break;

                case WebAuthenticationStatus.ErrorHttp:
                    result = AuthorizationResult.FromStatus(AuthorizationStatus.ErrorHttp);
                    result.Code = webAuthenticationResult.ResponseErrorDetail.ToString(CultureInfo.InvariantCulture);
                    break;

                case WebAuthenticationStatus.UserCancel:
                    result = AuthorizationResult.FromStatus(AuthorizationStatus.UserCancel);
                    break;

                default:
                    result = AuthorizationResult.FromStatus(
                        AuthorizationStatus.UnknownError,
                        MsalError.WABError,
                        MsalErrorMessage.WABError(
                            webAuthenticationResult.ResponseStatus.ToString(),
                            webAuthenticationResult.ResponseErrorDetail.ToString(CultureInfo.InvariantCulture),
                            webAuthenticationResult.ResponseData));
                    break;
            }

            return result;
        }

        public Uri UpdateRedirectUri(Uri redirectUri)
        {
            if (string.Equals(redirectUri.OriginalString, Constants.UapWEBRedirectUri, StringComparison.OrdinalIgnoreCase))
            {
                _ssoMode = true;
                return WebAuthenticationBroker.GetCurrentApplicationCallbackUri();
            }
            else
            {
                RedirectUriHelper.Validate(redirectUri, usesSystemBrowser: false);
                return redirectUri;
            }
        }
    }
}
#endif
