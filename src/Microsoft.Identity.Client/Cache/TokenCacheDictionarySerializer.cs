﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Json.Linq;

namespace Microsoft.Identity.Client.Cache
{
    /// <remarks>
    /// The dictionary serializer does not handle Unknown Nodes
    /// </remarks>
    internal class TokenCacheDictionarySerializer : ITokenCacheSerializer
    {
        private const string AccessTokenKey = "access_tokens";
        private const string RefreshTokenKey = "refresh_tokens";
        private const string IdTokenKey = "id_tokens";
        private const string AccountKey = "accounts";
        private readonly ITokenCacheAccessor _accessor;

        public TokenCacheDictionarySerializer(ITokenCacheAccessor accessor)
        {
            _accessor = accessor;
        }

        public byte[] Serialize(IDictionary<string, JToken> unkownNodes)
        {
            var accessTokensAsString = new List<string>();
            var refreshTokensAsString = new List<string>();
            var idTokensAsString = new List<string>();
            var accountsAsString = new List<string>();

            foreach (var accessToken in _accessor.GetAllAccessTokens())
            {
                accessTokensAsString.Add(accessToken.ToJsonString());
            }

            foreach (var refreshToken in _accessor.GetAllRefreshTokens())
            {
                refreshTokensAsString.Add(refreshToken.ToJsonString());
            }

            foreach (var idToken in _accessor.GetAllIdTokens())
            {
                idTokensAsString.Add(idToken.ToJsonString());
            }

            foreach (var account in _accessor.GetAllAccounts())
            {
                accountsAsString.Add(account.ToJsonString());
            }

            // reads the underlying in-memory dictionary and dumps out the content as a JSON
            var cacheDict = new Dictionary<string, IEnumerable<string>>
            {
                [AccessTokenKey] = accessTokensAsString,
                [RefreshTokenKey] = refreshTokensAsString,
                [IdTokenKey] = idTokensAsString,
                [AccountKey] = accountsAsString
            };

            return JsonHelper.SerializeToJson(cacheDict)
                             .ToByteArray();
        }

        public IDictionary<string, JToken> Deserialize(byte[] bytes, bool clearExistingCacheData)
        {
            Dictionary<string, IEnumerable<string>> cacheDict;

            try
            {
                cacheDict = JsonHelper.DeserializeFromJson<Dictionary<string, IEnumerable<string>>>(bytes);
            }
            catch (Exception ex)
            {
                throw new MsalClientException(MsalError.JsonParseError, MsalErrorMessage.TokenCacheDictionarySerializerFailedParse, ex);
            }

            if (clearExistingCacheData)
            {
                _accessor.Clear();
            }

            if (cacheDict == null || cacheDict.Count == 0)
            {
                return null;
            }

            if (cacheDict.ContainsKey(AccessTokenKey))
            {
                foreach (var atItem in cacheDict[AccessTokenKey])
                {
                    _accessor.SaveAccessToken(MsalAccessTokenCacheItem.FromJsonString(atItem));
                }
            }

            if (cacheDict.ContainsKey(RefreshTokenKey))
            {
                foreach (var rtItem in cacheDict[RefreshTokenKey])
                {
                    _accessor.SaveRefreshToken(MsalRefreshTokenCacheItem.FromJsonString(rtItem));
                }
            }

            if (cacheDict.ContainsKey(IdTokenKey))
            {
                foreach (var idItem in cacheDict[IdTokenKey])
                {
                    _accessor.SaveIdToken(MsalIdTokenCacheItem.FromJsonString(idItem));
                }
            }

            if (cacheDict.ContainsKey(AccountKey))
            {
                foreach (var account in cacheDict[AccountKey])
                {
                    _accessor.SaveAccount(MsalAccountCacheItem.FromJsonString(account));
                }
            }

            return null;
        }
    }
}
