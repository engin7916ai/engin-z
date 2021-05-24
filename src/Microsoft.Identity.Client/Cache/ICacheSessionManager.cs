﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Cache.Keys;
using Microsoft.Identity.Client.OAuth2;

namespace Microsoft.Identity.Client.Cache
{
    internal interface ICacheSessionManager
    {
        ITokenCacheInternal TokenCacheInternal { get; }
        bool HasCache { get; }
        Task<MsalAccessTokenCacheItem> FindAccessTokenAsync();
        Tuple<MsalAccessTokenCacheItem, MsalIdTokenCacheItem> SaveTokenResponse(MsalTokenResponse tokenResponse);
        MsalIdTokenCacheItem GetIdTokenCacheItem(MsalIdTokenCacheKey idTokenCacheKey);
        Task<MsalRefreshTokenCacheItem> FindRefreshTokenAsync();
        Task<MsalRefreshTokenCacheItem> FindFamilyRefreshTokenAsync(string familyId);
        Task<bool?> IsAppFociMemberAsync(string familyId);
    }
}
