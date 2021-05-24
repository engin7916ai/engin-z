﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Identity.Client.Cache.Keys;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Json.Linq;

namespace Microsoft.Identity.Client.Cache.Items
{
    internal class MsalAccessTokenCacheItem : MsalCredentialCacheItemBase
    {
        internal MsalAccessTokenCacheItem()
        {
            CredentialType = StorageJsonValues.CredentialTypeAccessToken;
        }

        internal MsalAccessTokenCacheItem(
            string environment,
            string clientId,
            MsalTokenResponse response,
            string tenantId)
            : this(
                environment,
                clientId,
                response.Scope,
                tenantId,
                response.AccessToken,
                response.AccessTokenExpiresOn,
                response.AccessTokenExtendedExpiresOn,
                response.ClientInfo)
        {
        }

        internal MsalAccessTokenCacheItem(
            string environment,
            string clientId,
            string scopes,
            string tenantId,
            string secret,
            DateTimeOffset accessTokenExpiresOn,
            DateTimeOffset accessTokenExtendedExpiresOn,
            string rawClientInfo)
            : this()
        {
            Environment = environment;
            ClientId = clientId;
            NormalizedScopes = scopes;
            TenantId = tenantId;
            Secret = secret;
            ExpiresOnUnixTimestamp = CoreHelpers.DateTimeToUnixTimestamp(accessTokenExpiresOn);
            ExtendedExpiresOnUnixTimestamp = CoreHelpers.DateTimeToUnixTimestamp(accessTokenExtendedExpiresOn);
            CachedAt = CoreHelpers.CurrDateTimeInUnixTimestamp();
            RawClientInfo = rawClientInfo;

            InitUserIdentifier();
        }

        internal string TenantId { get; set; }

        /// <summary>
        /// String comprised of scopes that have been lowercased and ordered.
        /// </summary>
        /// <remarks>Normalization is important when creating unique keys.</remarks>
        internal string NormalizedScopes { get; set; }
        internal string CachedAt { get; set; }
        internal string ExpiresOnUnixTimestamp { get; set; }
        internal string ExtendedExpiresOnUnixTimestamp { get; set; }
        public string UserAssertionHash { get; set; }

        internal string Authority =>
            string.Format(CultureInfo.InvariantCulture, "https://{0}/{1}/", Environment, TenantId ?? "common");

        internal SortedSet<string> ScopeSet => ScopeHelper.ConvertStringToLowercaseSortedSet(NormalizedScopes);

        internal DateTimeOffset ExpiresOn => CoreHelpers.UnixTimestampStringToDateTime(ExpiresOnUnixTimestamp);
        internal DateTimeOffset ExtendedExpiresOn => CoreHelpers.UnixTimestampStringToDateTime(ExtendedExpiresOnUnixTimestamp);
        public bool IsExtendedLifeTimeToken { get; set; }

        internal static MsalAccessTokenCacheItem FromJsonString(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return FromJObject(JObject.Parse(json));
        }

        internal static MsalAccessTokenCacheItem FromJObject(JObject j)
        {
            long cachedAt = JsonUtils.ExtractParsedIntOrZero(j, StorageJsonKeys.CachedAt);
            long expiresOn = JsonUtils.ExtractParsedIntOrZero(j, StorageJsonKeys.ExpiresOn);

            // This handles a bug with the name in previous MSAL.  It used "ext_expires_on" instead of
            // "extended_expires_on" per spec, so this works around that.
            long ext_expires_on = JsonUtils.ExtractParsedIntOrZero(j, StorageJsonKeys.ExtendedExpiresOn_MsalCompat);
            long extendedExpiresOn = JsonUtils.ExtractParsedIntOrZero(j, StorageJsonKeys.ExtendedExpiresOn);
            if (extendedExpiresOn == 0 && ext_expires_on > 0)
            {
                extendedExpiresOn = ext_expires_on;
            }

            var item = new MsalAccessTokenCacheItem
            {
                TenantId = JsonUtils.ExtractExistingOrEmptyString(j, StorageJsonKeys.Realm),
                NormalizedScopes = JsonUtils.ExtractExistingOrEmptyString(j, StorageJsonKeys.Target),
                CachedAt = cachedAt.ToString(CultureInfo.InvariantCulture),
                ExpiresOnUnixTimestamp = expiresOn.ToString(CultureInfo.InvariantCulture),
                ExtendedExpiresOnUnixTimestamp = extendedExpiresOn.ToString(CultureInfo.InvariantCulture),
                UserAssertionHash = JsonUtils.ExtractExistingOrEmptyString(j, StorageJsonKeys.UserAssertionHash),
            };

            item.PopulateFieldsFromJObject(j);

            return item;
        }

        internal override JObject ToJObject()
        {
            var json = base.ToJObject();

            json[StorageJsonKeys.Realm] = TenantId;
            json[StorageJsonKeys.Target] = NormalizedScopes;
            json[StorageJsonKeys.UserAssertionHash] = UserAssertionHash;
            json[StorageJsonKeys.CachedAt] = CachedAt;
            json[StorageJsonKeys.ExpiresOn] = ExpiresOnUnixTimestamp;
            json[StorageJsonKeys.ExtendedExpiresOn] = ExtendedExpiresOnUnixTimestamp;

            // previous versions of msal used "ext_expires_on" instead of the correct "extended_expires_on".
            // this is here for back compat
            json[StorageJsonKeys.ExtendedExpiresOn_MsalCompat] = ExtendedExpiresOnUnixTimestamp;

            return json;
        }

        internal string ToJsonString()
        {
            return ToJObject().ToString();
        }

        internal MsalAccessTokenCacheKey GetKey()
        {
            return new MsalAccessTokenCacheKey(
                Environment,
                TenantId,
                HomeAccountId,
                ClientId,
                NormalizedScopes);
        }

        internal MsalIdTokenCacheKey GetIdTokenItemKey()
        {
            return new MsalIdTokenCacheKey(Environment, TenantId, HomeAccountId, ClientId);
        }
    }
}
