﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Cache.Keys;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.TelemetryCore.Internal;
using Microsoft.Identity.Client.TelemetryCore.Internal.Events;

namespace Microsoft.Identity.Client.Cache
{
    /// <summary>
    /// MSAL should only interact with the cache though this object. It is responsible for firing cache notifications.
    /// Flows should only perform (at most) 2 cache accesses: one to read data and one to write tokens. Reading data multiple times 
    /// (e.g. read all ATs, read all RTs) should not refresh the cache from disk because of performance impact.
    /// Write operations are still the responsibility of TokenCache.
    /// </summary>
    internal class CacheSessionManager : ICacheSessionManager
    {
        private readonly AuthenticationRequestParameters _requestParams;
        private bool _cacheRefreshedForRead = false;

        public CacheSessionManager(
            ITokenCacheInternal tokenCacheInternal,
            AuthenticationRequestParameters requestParams)
        {
            TokenCacheInternal = tokenCacheInternal ?? throw new ArgumentNullException(nameof(tokenCacheInternal));
            _requestParams = requestParams ?? throw new ArgumentNullException(nameof(requestParams));
        }

        #region ICacheSessionManager implementation
        public ITokenCacheInternal TokenCacheInternal { get; }

        public async Task<MsalAccessTokenCacheItem> FindAccessTokenAsync()
        {
            await RefreshCacheForReadOperationsAsync(CacheEvent.TokenTypes.AT).ConfigureAwait(false);
            return await TokenCacheInternal.FindAccessTokenAsync(_requestParams).ConfigureAwait(false);
        }

        public async Task<Tuple<MsalAccessTokenCacheItem, MsalIdTokenCacheItem>> SaveTokenResponseAsync(MsalTokenResponse tokenResponse)
        {
            return await TokenCacheInternal.SaveTokenResponseAsync(_requestParams, tokenResponse).ConfigureAwait(false);
        }

        public async Task<MsalIdTokenCacheItem> GetIdTokenCacheItemAsync(MsalIdTokenCacheKey idTokenCacheKey)
        {
            await RefreshCacheForReadOperationsAsync(CacheEvent.TokenTypes.ID).ConfigureAwait(false);
            return TokenCacheInternal.GetIdTokenCacheItem(idTokenCacheKey);
        }

        public async Task<MsalRefreshTokenCacheItem> FindFamilyRefreshTokenAsync(string familyId)
        {
            await RefreshCacheForReadOperationsAsync(CacheEvent.TokenTypes.RT).ConfigureAwait(false);

            if (string.IsNullOrEmpty(familyId))
            {
                throw new ArgumentNullException(nameof(familyId));
            }

            return await TokenCacheInternal.FindRefreshTokenAsync(_requestParams, familyId).ConfigureAwait(false);
        }

        public async Task<MsalRefreshTokenCacheItem> FindRefreshTokenAsync()
        {
            await RefreshCacheForReadOperationsAsync(CacheEvent.TokenTypes.RT).ConfigureAwait(false);
            return await TokenCacheInternal.FindRefreshTokenAsync(_requestParams).ConfigureAwait(false);
        }

        public async Task<bool?> IsAppFociMemberAsync(string familyId)
        {
            await RefreshCacheForReadOperationsAsync(CacheEvent.TokenTypes.AppMetadata).ConfigureAwait(false);
            return await TokenCacheInternal.IsFociMemberAsync(_requestParams, familyId).ConfigureAwait(false);
        }

        public async Task<IEnumerable<IAccount>> GetAccountsAsync()
        {
            await RefreshCacheForReadOperationsAsync(CacheEvent.TokenTypes.Account).ConfigureAwait(false);
            return await TokenCacheInternal.GetAccountsAsync(_requestParams).ConfigureAwait(false);
        }

        #endregion

        private async Task RefreshCacheForReadOperationsAsync(CacheEvent.TokenTypes cacheEventType)
        {
            if (!_cacheRefreshedForRead)
            {
                string telemetryId = _requestParams.RequestContext.CorrelationId.AsMatsCorrelationId();
                var cacheEvent = new CacheEvent(CacheEvent.TokenCacheLookup, telemetryId)
                {
                    TokenType = cacheEventType
                };

                await TokenCacheInternal.Semaphore.WaitAsync().ConfigureAwait(false);

                try
                {
                    if (!_cacheRefreshedForRead) // double check locking
                    {
                        using (_requestParams.RequestContext.CreateTelemetryHelper(cacheEvent))
                        {
                            string key = SuggestedWebCacheKeyFactory.GetKeyFromRequest(_requestParams);
                            try
                            {
                                var args = new TokenCacheNotificationArgs(
                                   TokenCacheInternal,
                                   _requestParams.ClientId,
                                   _requestParams.Account,
                                   hasStateChanged: false,
                                   TokenCacheInternal.IsApplicationCache,
                                   hasTokens: TokenCacheInternal.HasTokensNoLocks(),
                                   suggestedCacheKey: key);
                                await TokenCacheInternal.OnBeforeAccessAsync(args).ConfigureAwait(false);
                            }
                            finally
                            {
                                var args = new TokenCacheNotificationArgs(
                                   TokenCacheInternal,
                                   _requestParams.ClientId,
                                   _requestParams.Account,
                                   hasStateChanged: false,
                                   TokenCacheInternal.IsApplicationCache,
                                   hasTokens: TokenCacheInternal.HasTokensNoLocks(),
                                   suggestedCacheKey: key);
                                await TokenCacheInternal.OnAfterAccessAsync(args).ConfigureAwait(false);
                            }

                            _cacheRefreshedForRead = true;
                        }
                    }
                }
                finally
                {
                    TokenCacheInternal.Semaphore.Release();
                }
            }
        }
    }
}
