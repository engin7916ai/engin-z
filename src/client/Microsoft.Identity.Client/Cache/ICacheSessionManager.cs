﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Cache.Keys;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.OAuth2;

namespace Microsoft.Identity.Client.Cache
{
    internal interface ICacheSessionManager
    {
        RequestContext RequestContext { get; }
        ITokenCacheInternal TokenCacheInternal { get; }
        Task<MsalAccessTokenCacheItem> FindAccessTokenAsync();
        Task<Tuple<MsalAccessTokenCacheItem, MsalIdTokenCacheItem, Account>> SaveTokenResponseAsync(MsalTokenResponse tokenResponse);
        Task<MsalIdTokenCacheItem> GetIdTokenCacheItemAsync(MsalIdTokenCacheKey idTokenCacheKey);
        Task<MsalRefreshTokenCacheItem> FindRefreshTokenAsync();
        Task<MsalRefreshTokenCacheItem> FindFamilyRefreshTokenAsync(string familyId);
        Task<bool?> IsAppFociMemberAsync(string familyId);

        Task<IEnumerable<IAccount>> GetAccountsAsync();
    }
}
