﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Identity.Client.Cache.Items;

namespace Microsoft.Identity.Client.AuthScheme.Bearer
{
    internal class BearerAuthenticationScheme : IAuthenticationScheme
    {
        internal const string BearerTokenType = "bearer";

        public string AuthorizationHeaderPrefix => "Bearer";

        public string AccessTokenType => BearerTokenType;

        public string KeyId => null;

        public string FormatAccessToken(MsalAccessTokenCacheItem msalAccessTokenCacheItem)
        {
            return msalAccessTokenCacheItem.Secret;
        }

        public IDictionary<string, string> GetTokenRequestParams()
        {
            // ESTS issues Bearer tokens by default, no need for any extra params
            return new Dictionary<string, string>();
        }
    }
}
