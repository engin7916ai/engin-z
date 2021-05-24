﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Test.Unit;

namespace Microsoft.Identity.Test.Common.Core.Mocks
{
    internal class TokenCacheHelper
    {
        public static long ValidExpiresIn = 28800;
        public static long ValidExtendedExpiresIn = 57600;

        internal void PopulateCacheForClientCredential(ITokenCacheAccessor accessor)
        {
            MsalAccessTokenCacheItem atItem = new MsalAccessTokenCacheItem(
               MsalTestConstants.ProductionPrefCacheEnvironment,
               MsalTestConstants.ClientId,
               MsalTestConstants.Scope.AsSingleString(),
               MsalTestConstants.Utid,
               "",
               new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExpiresIn)),
               new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExtendedExpiresIn)),
               MockHelpers.CreateClientInfo());

            accessor.SaveAccessToken(atItem);
        }

        internal void PopulateCache(
            ITokenCacheAccessor accessor,
            string uid = MsalTestConstants.Uid,
            string utid = MsalTestConstants.Utid,
            string clientId = MsalTestConstants.ClientId)
        {
            MsalAccessTokenCacheItem atItem = new MsalAccessTokenCacheItem(
                MsalTestConstants.ProductionPrefCacheEnvironment,
                clientId,
                MsalTestConstants.Scope.AsSingleString(),
                utid,
                "",
                new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExpiresIn)),
                new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExtendedExpiresIn)),
                MockHelpers.CreateClientInfo(uid, utid));

            // add access token
            accessor.SaveAccessToken(atItem);

            var idTokenCacheItem = new MsalIdTokenCacheItem(
                MsalTestConstants.ProductionPrefCacheEnvironment,
                clientId,
                MockHelpers.CreateIdToken(MsalTestConstants.UniqueId + "more", MsalTestConstants.DisplayableId),
                MockHelpers.CreateClientInfo(uid, utid),
                utid);

            accessor.SaveIdToken(idTokenCacheItem);

            var accountCacheItem = new MsalAccountCacheItem(
                MsalTestConstants.ProductionPrefCacheEnvironment,
                null,
                MockHelpers.CreateClientInfo(uid, utid),
                null,
                MsalTestConstants.DisplayableId,
                utid,
                null,
                null);

            accessor.SaveAccount(accountCacheItem);

            atItem = new MsalAccessTokenCacheItem(
                MsalTestConstants.ProductionPrefCacheEnvironment,
                clientId,
                MsalTestConstants.ScopeForAnotherResource.AsSingleString(),
                utid,
                "",
                new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExpiresIn)),
                new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExtendedExpiresIn)),
                MockHelpers.CreateClientInfo(uid, utid));

            // add another access token
            accessor.SaveAccessToken(atItem);

            AddRefreshTokenToCache(accessor,uid, utid, clientId);

            var appMetadataItem = new MsalAppMetadataCacheItem(
                clientId,
                MsalTestConstants.ProductionPrefCacheEnvironment,
                null);

            accessor.SaveAppMetadata(appMetadataItem);
        }

        internal void PopulateCacheWithOneAccessToken(ITokenCacheAccessor accessor)
        {
            MsalAccessTokenCacheItem atItem  = new MsalAccessTokenCacheItem(
               MsalTestConstants.ProductionPrefCacheEnvironment,
               MsalTestConstants.ClientId,
               MsalTestConstants.Scope.AsSingleString(),
               MsalTestConstants.Utid,
               "",
               new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExpiresIn)),
               new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExtendedExpiresIn)),
               MockHelpers.CreateClientInfo());

            // add access token
            accessor.SaveAccessToken(atItem);

            MsalIdTokenCacheItem idTokenCacheItem = new MsalIdTokenCacheItem(
                MsalTestConstants.ProductionPrefCacheEnvironment, MsalTestConstants.ClientId,
                MockHelpers.CreateIdToken(MsalTestConstants.UniqueId + "more", MsalTestConstants.DisplayableId),
                MockHelpers.CreateClientInfo(), MsalTestConstants.Utid);

            accessor.SaveIdToken(idTokenCacheItem);

            MsalAccountCacheItem accountCacheItem = new MsalAccountCacheItem
                (MsalTestConstants.ProductionPrefNetworkEnvironment, null, MockHelpers.CreateClientInfo(), null, null, MsalTestConstants.Utid,
                null, null);

            accessor.SaveAccount(accountCacheItem);

            atItem = new MsalAccessTokenCacheItem(
                MsalTestConstants.ProductionPrefCacheEnvironment,
                MsalTestConstants.ClientId,
                MsalTestConstants.ScopeForAnotherResource.AsSingleString(),
                MsalTestConstants.Utid,
                "",
                new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExpiresIn)),
                new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExtendedExpiresIn)),
                MockHelpers.CreateClientInfo());

            AddRefreshTokenToCache(accessor, MsalTestConstants.Uid, MsalTestConstants.Utid);
        }

        public static void AddRefreshTokenToCache(
            ITokenCacheAccessor accessor,
            string uid,
            string utid,
            string clientId = MsalTestConstants.ClientId)
        {
            var rtItem = new MsalRefreshTokenCacheItem
                (MsalTestConstants.ProductionPrefCacheEnvironment, clientId, "someRT", MockHelpers.CreateClientInfo(uid, utid));

            accessor.SaveRefreshToken(rtItem);
        }

        public static void AddIdTokenToCache(ITokenCacheAccessor accessor, string uid, string utid)
        {
            MsalIdTokenCacheItem idTokenCacheItem = new MsalIdTokenCacheItem(
                MsalTestConstants.ProductionPrefCacheEnvironment,
                MsalTestConstants.ClientId,
                MockHelpers.CreateIdToken(MsalTestConstants.UniqueId, MsalTestConstants.DisplayableId),
                MockHelpers.CreateClientInfo(),
                MsalTestConstants.Utid);

            accessor.SaveIdToken(idTokenCacheItem);
        }

        public static void AddAccountToCache(ITokenCacheAccessor accessor, string uid, string utid)
        {
            MsalAccountCacheItem accountCacheItem = new MsalAccountCacheItem
                (MsalTestConstants.ProductionPrefCacheEnvironment, null, MockHelpers.CreateClientInfo(uid, utid), null, null, utid, null, null);

            accessor.SaveAccount(accountCacheItem);
        }
    }
}
