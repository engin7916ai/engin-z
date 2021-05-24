﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Identity.Client.Cache.Keys;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Json.Linq;

namespace Microsoft.Identity.Client.Cache.Items
{
    internal class MsalIdTokenCacheItem : MsalCredentialCacheItemBase
    {
        internal MsalIdTokenCacheItem()
        {
            CredentialType = StorageJsonValues.CredentialTypeIdToken;
        }

        internal MsalIdTokenCacheItem(
            string environment,
            string clientId,
            MsalTokenResponse response,
            string tenantId)
            : this(
                environment,
                clientId,
                response.IdToken,
                response.ClientInfo,
                tenantId)
        {
        }

        internal MsalIdTokenCacheItem(
            string environment,
            string clientId,
            string secret,
            string rawClientInfo,
            string tenantId)
            : this()
        {
            Environment = environment;
            TenantId = tenantId;
            ClientId = clientId;
            Secret = secret;
            RawClientInfo = rawClientInfo;

            InitUserIdentifier();
        }

        internal string TenantId { get; set; }

        internal string Authority =>
            string.Format(CultureInfo.InvariantCulture, "https://{0}/{1}/", Environment, TenantId ?? "common");

        internal IdToken IdToken => IdToken.Parse(Secret);

        internal MsalIdTokenCacheKey GetKey()
        {
            return new MsalIdTokenCacheKey(Environment, TenantId, HomeAccountId, ClientId);
        }

        internal static MsalIdTokenCacheItem FromJsonString(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return FromJObject(JObject.Parse(json));
        }

        internal static MsalIdTokenCacheItem FromJObject(JObject j)
        {
            var item = new MsalIdTokenCacheItem
            {
                TenantId = JsonUtils.ExtractExistingOrEmptyString(j, StorageJsonKeys.Realm),
            };

            item.PopulateFieldsFromJObject(j);

            return item;
        }

        internal override JObject ToJObject()
        {
            var json = base.ToJObject();
            json[StorageJsonKeys.Realm] = TenantId;
            return json;
        }

        internal string ToJsonString()
        {
            return ToJObject()
                .ToString();
        }
    }
}
