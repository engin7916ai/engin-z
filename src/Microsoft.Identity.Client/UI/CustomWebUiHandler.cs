﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Extensibility;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Identity.Client.UI
{
    internal class CustomWebUiHandler : IWebUI
    {
        private readonly ICustomWebUi _customWebUi;

        public CustomWebUiHandler(ICustomWebUi customWebUi)
        {
            _customWebUi = customWebUi;
        }

        /// <inheritdoc />
        public async Task<AuthorizationResult> AcquireAuthorizationAsync(
            Uri authorizationUri,
            Uri redirectUri,
            RequestContext requestContext,
            CancellationToken cancellationToken)
        {
            requestContext.Logger.Info(LogMessages.CustomWebUiAcquiringAuthorizationCode);

            try
            {
                requestContext.Logger.InfoPii(LogMessages.CustomWebUiCallingAcquireAuthorizationCodePii(authorizationUri, redirectUri),
                                              LogMessages.CustomWebUiCallingAcquireAuthorizationCodeNoPii);
                var uri = await _customWebUi.AcquireAuthorizationCodeAsync(authorizationUri, redirectUri, cancellationToken)
                                            .ConfigureAwait(false);
                if (uri == null || String.IsNullOrWhiteSpace(uri.Query))
                {
                    throw new MsalClientException(
                        MsalError.CustomWebUiReturnedInvalidUri,
                        MsalErrorMessage.CustomWebUiReturnedInvalidUri);
                }

                if (uri.Authority.Equals(redirectUri.Authority, StringComparison.OrdinalIgnoreCase) &&
                    uri.AbsolutePath.Equals(redirectUri.AbsolutePath))
                {
                    IDictionary<string, string> inputQp = CoreHelpers.ParseKeyValueList(
                        authorizationUri.Query.Substring(1),
                        '&',
                        true,
                        null);

                    requestContext.Logger.Info(LogMessages.CustomWebUiRedirectUriMatched);
                    return new AuthorizationResult(AuthorizationStatus.Success, uri.OriginalString);
                }

                throw new MsalClientException(
                    MsalError.CustomWebUiRedirectUriMismatch,
                    MsalErrorMessage.CustomWebUiRedirectUriMismatch(
                        uri.AbsolutePath,
                        redirectUri.AbsolutePath));
            }
            catch (OperationCanceledException)
            {
                requestContext.Logger.Info(LogMessages.CustomWebUiOperationCancelled);
                return new AuthorizationResult(AuthorizationStatus.UserCancel, null);
            }
            catch (Exception ex)
            {
                requestContext.Logger.WarningPiiWithPrefix(ex, MsalErrorMessage.CustomWebUiAuthorizationCodeFailed);
                throw;
            }
        }


        /// <inheritdoc />
        public void ValidateRedirectUri(Uri redirectUri)
        {
            RedirectUriHelper.Validate(redirectUri, usesSystemBrowser: false);
        }
    }
}
