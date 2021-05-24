﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.OAuth2;

namespace Microsoft.Identity.Client.Internal.Requests.Silent
{
    internal class SilentClientAuthStretegy : ISilentAuthRequestStrategy
    {
        private AuthenticationRequestParameters AuthenticationRequestParameters { get; }
        private ICacheSessionManager CacheManager => AuthenticationRequestParameters.CacheSessionManager;
        protected IServiceBundle ServiceBundle { get; }
        private readonly AcquireTokenSilentParameters _silentParameters;
        private const string TheOnlyFamilyId = "1";
        private readonly SilentRequest _silentRequest;

        public SilentClientAuthStretegy(
            SilentRequest request,
            IServiceBundle serviceBundle,
            AuthenticationRequestParameters authenticationRequestParameters,
            AcquireTokenSilentParameters silentParameters)
        {
            AuthenticationRequestParameters = authenticationRequestParameters;
            _silentParameters = silentParameters;
            ServiceBundle = serviceBundle;
            _silentRequest = request;
        }

        public async Task PreRunAsync()
        {
            IAccount account = await GetAccountFromParamsOrLoginHintAsync(_silentParameters).ConfigureAwait(false);
            AuthenticationRequestParameters.Account = account;

            AuthenticationRequestParameters.Authority = Authority.CreateAuthorityForRequest(
                ServiceBundle.Config.AuthorityInfo,
                AuthenticationRequestParameters.AuthorityOverride,
                account.HomeAccountId?.TenantId);
        }

        public async Task<AuthenticationResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            var logger = AuthenticationRequestParameters.RequestContext.Logger;
            MsalAccessTokenCacheItem cachedAccessTokenItem = null;

            ThrowIfNoScopesOnB2C();

            if (!_silentParameters.ForceRefresh && string.IsNullOrEmpty(AuthenticationRequestParameters.Claims))
            {
                cachedAccessTokenItem = await CacheManager.FindAccessTokenAsync().ConfigureAwait(false);

                if (cachedAccessTokenItem != null && !cachedAccessTokenItem.NeedsRefresh())
                {
                    logger.Info("Returning access token found in cache. RefreshOn exists ? "
                        + cachedAccessTokenItem.RefreshOn.HasValue);
                    AuthenticationRequestParameters.RequestContext.ApiEvent.IsAccessTokenCacheHit = true;
                    return await CreateAuthenticationResultAsync(cachedAccessTokenItem).ConfigureAwait(false);
                }
            }
            else
            {
                logger.Info("Skipped looking for an Access Token because ForceRefresh or Claims were set");
            }

            // No AT or AT.RefreshOn > Now --> refresh the RT
            try
            {
                return await RefreshRtOrFailAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MsalServiceException e)
            {
                //Remove the account from cache in case of bad_token sub error
                if (MsalError.BadToken.Equals(e.SubError, StringComparison.OrdinalIgnoreCase))
                {
                    await CacheManager.TokenCacheInternal.RemoveAccountAsync(AuthenticationRequestParameters.Account, AuthenticationRequestParameters.RequestContext).ConfigureAwait(false);
                    logger.Warning("Failed to refresh access token because the refresh token is invalid, removing account from cache.");
                    throw;
                }

                bool isAadUnavailable = e.IsAadUnavailable();

                logger.Warning($"Refreshing the RT failed. Is AAD down? {isAadUnavailable}. Is there an AT in the cache that is usable? {cachedAccessTokenItem != null}");

                if (cachedAccessTokenItem != null && isAadUnavailable)
                {
                    logger.Info("Returning existing access token. It is not expired, but should be refreshed.");
                    return await CreateAuthenticationResultAsync(cachedAccessTokenItem).ConfigureAwait(false);
                }

                logger.Warning("Failed to refresh the RT and cannot use existing AT (expired or missing).");
                throw;
            }
        }

        private async Task<AuthenticationResult> RefreshRtOrFailAsync(CancellationToken cancellationToken)
        {
            // Try FOCI first
            MsalTokenResponse msalTokenResponse = await TryGetTokenUsingFociAsync(cancellationToken)
                .ConfigureAwait(false);

            // Normal, non-FOCI flow
            if (msalTokenResponse == null)
            {
                // Look for a refresh token
                MsalRefreshTokenCacheItem appRefreshToken = await FindRefreshTokenOrFailAsync()
                    .ConfigureAwait(false);

                msalTokenResponse = await RefreshAccessTokenAsync(appRefreshToken, cancellationToken)
                    .ConfigureAwait(false);
            }
            return await _silentRequest.CacheTokenResponseAndCreateAuthenticationResultAsync(msalTokenResponse).ConfigureAwait(false);
        }

        private async Task<AuthenticationResult> CreateAuthenticationResultAsync(MsalAccessTokenCacheItem cachedAccessTokenItem)
        {
            var msalIdTokenItem = await CacheManager.GetIdTokenCacheItemAsync(cachedAccessTokenItem.GetIdTokenItemKey()).ConfigureAwait(false);
            return new AuthenticationResult(
                cachedAccessTokenItem,
                msalIdTokenItem,
                AuthenticationRequestParameters.AuthenticationScheme,
                AuthenticationRequestParameters.RequestContext.CorrelationId,
                TokenSource.Cache);
        }

        private void ThrowIfNoScopesOnB2C()
        {
            // During AT Silent with no scopes, Unlike AAD, B2C will not issue an access token if no scopes are requested
            // And we don't want to refresh the RT on every ATS call
            // See https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/715 for details

            if (!AuthenticationRequestParameters.HasScopes &&
                AuthenticationRequestParameters.AuthorityInfo.AuthorityType == AuthorityType.B2C)
            {
                throw new MsalUiRequiredException(
                    MsalError.ScopesRequired,
                    MsalErrorMessage.ScopesRequired,
                    null,
                    UiRequiredExceptionClassification.AcquireTokenSilentFailed);
            }
        }

        private async Task<MsalTokenResponse> TryGetTokenUsingFociAsync(CancellationToken cancellationToken)
        {
            if (!ServiceBundle.PlatformProxy.GetFeatureFlags().IsFociEnabled)
            {
                return null;
            }

            var logger = AuthenticationRequestParameters.RequestContext.Logger;

            // If the app was just added to the family, the app metadata will reflect this
            // after the first RT exchanged.
            bool? isFamilyMember = await CacheManager.IsAppFociMemberAsync(TheOnlyFamilyId).ConfigureAwait(false);

            if (isFamilyMember.HasValue && !isFamilyMember.Value)
            {
                AuthenticationRequestParameters.RequestContext.Logger.Verbose(
                    "[FOCI] App is not part of the family, skipping FOCI.");

                return null;
            }

            logger.Verbose("[FOCI] App is part of the family or unknown, looking for FRT");
            var familyRefreshToken = await CacheManager.FindFamilyRefreshTokenAsync(TheOnlyFamilyId).ConfigureAwait(false);
            logger.Verbose("[FOCI] FRT found? " + (familyRefreshToken != null));

            if (familyRefreshToken != null)
            {
                try
                {
                    MsalTokenResponse frtTokenResponse = await RefreshAccessTokenAsync(familyRefreshToken, cancellationToken)
                        .ConfigureAwait(false);

                    logger.Verbose("[FOCI] FRT refresh succeeded");
                    return frtTokenResponse;
                }
                catch (MsalServiceException ex)
                {
                    // Hack: STS does not yet send back the suberror on these platforms because they are not in an allowed list,
                    // so the best thing we can do is to consider all errors as client_mismatch.
#if NETSTANDARD || WINDOWS_APP || MAC
                    logger.Error("[FOCI] FRT refresh failed - " + ex.ErrorCode + " " + ex.SubError);
                    ex?.GetType();  // avoid the "variable 'ex' is declared but never used" in this code path.
                    return null;
#else
                    if (MsalError.InvalidGrantError.Equals(ex?.ErrorCode, StringComparison.OrdinalIgnoreCase) &&
                        MsalError.ClientMismatch.Equals(ex?.SubError, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Error("[FOCI] FRT refresh failed - client mismatch");
                        return null;
                    }

                    // Rethrow failures to refresh the FRT, other than client_mismatch, because
                    // apps need to handle them in the same way they handle exceptions from refreshing the RT.
                    // For example, some apps have special handling for MFA errors.
                    logger.Error("[FOCI] FRT refresh failed - other error");
                    throw;
#endif
                }
            }

            return null;
        }

        private async Task<MsalTokenResponse> RefreshAccessTokenAsync(MsalRefreshTokenCacheItem msalRefreshTokenItem, CancellationToken cancellationToken)
        {
            AuthenticationRequestParameters.RequestContext.Logger.Verbose("Refreshing access token...");
            await AuthorityEndpoints.UpdateAuthorityEndpointsAsync(AuthenticationRequestParameters)
                .ConfigureAwait(false);

            var msalTokenResponse = await _silentRequest.SendTokenRequestAsync(GetBodyParameters(msalRefreshTokenItem.Secret), cancellationToken)
                                    .ConfigureAwait(false);

            if (msalTokenResponse.RefreshToken == null)
            {
                msalTokenResponse.RefreshToken = msalRefreshTokenItem.Secret;
                AuthenticationRequestParameters.RequestContext.Logger.Info(
                    "Refresh token was missing from the token refresh response, so the refresh token in the request is returned instead");
            }

            return msalTokenResponse;
        }

        private async Task<MsalRefreshTokenCacheItem> FindRefreshTokenOrFailAsync()
        {
            var msalRefreshTokenItem = await CacheManager.FindRefreshTokenAsync().ConfigureAwait(false);
            if (msalRefreshTokenItem == null)
            {
                AuthenticationRequestParameters.RequestContext.Logger.Verbose("No Refresh Token was found in the cache");

                throw new MsalUiRequiredException(
                    MsalError.NoTokensFoundError,
                    MsalErrorMessage.NoTokensFoundError,
                    null,
                    UiRequiredExceptionClassification.AcquireTokenSilentFailed);
            }

            return msalRefreshTokenItem;
        }

        private Dictionary<string, string> GetBodyParameters(string refreshTokenSecret)
        {
            var dict = new Dictionary<string, string>
            {
                [OAuth2Parameter.GrantType] = OAuth2GrantType.RefreshToken,
                [OAuth2Parameter.RefreshToken] = refreshTokenSecret
            };

            return dict;
        }

        private async Task<IAccount> GetSingleAccountForLoginHintAsync(string loginHint)
        {
            var accounts = await CacheManager.GetAccountsAsync()
                .ConfigureAwait(false);

            accounts = accounts
                .Where(a => !string.IsNullOrWhiteSpace(a.Username) &&
                       a.Username.Equals(loginHint, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!accounts.Any())
            {
                throw new MsalUiRequiredException(
                    MsalError.NoAccountForLoginHint,
                    MsalErrorMessage.NoAccountForLoginHint,
                    null,
                    UiRequiredExceptionClassification.AcquireTokenSilentFailed);
            }

            if (accounts.Count() > 1)
            {
                throw new MsalUiRequiredException(
                    MsalError.MultipleAccountsForLoginHint,
                    MsalErrorMessage.MultipleAccountsForLoginHint,
                    null,
                    UiRequiredExceptionClassification.AcquireTokenSilentFailed);

            }

            return accounts.First();
        }

        private async Task<IAccount> GetAccountFromParamsOrLoginHintAsync(AcquireTokenSilentParameters silentParameters)
        {
            if (silentParameters.Account != null)
            {
                return silentParameters.Account;
            }

            return await GetSingleAccountForLoginHintAsync(silentParameters.LoginHint).ConfigureAwait(false);
        }
    }
}
