﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Mats.Internal.Events;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Identity.Client.Internal.Requests
{
    internal class AuthenticationRequestParameters
    {
        private readonly AcquireTokenCommonParameters _commonParameters;

        public AuthenticationRequestParameters(
            IServiceBundle serviceBundle,
            ITokenCacheInternal tokenCache,
            AcquireTokenCommonParameters commonParameters,
            RequestContext requestContext)
        {
            _commonParameters = commonParameters;

            Authority = commonParameters.AuthorityOverride == null
                ? Authority.CreateAuthority(serviceBundle)
                : Authority.CreateAuthorityWithOverride(serviceBundle, commonParameters.AuthorityOverride);

            ClientId = serviceBundle.Config.ClientId;
            CacheSessionManager = new CacheSessionManager(tokenCache, this);
            Scope = ScopeHelper.CreateSortedSetFromEnumerable(commonParameters.Scopes);
            RedirectUri = new Uri(serviceBundle.Config.RedirectUri);
            RequestContext = requestContext;
            IsBrokerEnabled = serviceBundle.Config.IsBrokerEnabled;

            // Set application wide query parameters.
            ExtraQueryParameters = serviceBundle.Config.ExtraQueryParameters ?? new Dictionary<string, string>();

            // Copy in call-specific query parameters.
            if (commonParameters.ExtraQueryParameters != null)
            {
                foreach (var kvp in commonParameters.ExtraQueryParameters)
                {
                    ExtraQueryParameters[kvp.Key] = kvp.Value;
                }
            }
        }

        public ApiTelemetryId ApiTelemId => _commonParameters.ApiTelemId;

        public IEnumerable<KeyValuePair<string, string>> GetApiTelemetryFeatures()
        {
            return _commonParameters.GetApiTelemetryFeatures();
        }

        public ApiEvent.ApiIds ApiId => _commonParameters.ApiId;

        public RequestContext RequestContext { get; }
        public Authority Authority { get; set; }
        public AuthorityInfo AuthorityInfo => Authority.AuthorityInfo;
        public AuthorityEndpoints Endpoints { get; set; }
        public string TenantUpdatedCanonicalAuthority { get; set; }
        public ICacheSessionManager CacheSessionManager { get; }
        public SortedSet<string> Scope { get; }
        public string ClientId { get; }
        public Uri RedirectUri { get; set; }
        public IDictionary<string, string> ExtraQueryParameters { get; }
        public string Claims => _commonParameters.Claims;

        public AuthorityInfo AuthorityOverride => _commonParameters.AuthorityOverride;

        internal bool IsBrokerEnabled { get; set; }

#region TODO REMOVE FROM HERE AND USE FROM SPECIFIC REQUEST PARAMETERS
        // TODO: ideally, these can come from the particular request instance and not be in RequestBase since it's not valid for all requests.

#if !ANDROID_BUILDTIME && !iOS_BUILDTIME && !WINDOWS_APP_BUILDTIME && !MAC_BUILDTIME // Hide confidential client on mobile platforms
        public ClientCredentialWrapper ClientCredential { get; set; }
#endif

        // TODO: ideally, this can come from the particular request instance and not be in RequestBase since it's not valid for all requests.
        public bool SendX5C { get; set; }
        public string LoginHint { get; set; }
        public IAccount Account { get; set; }

        public bool IsClientCredentialRequest { get; set; }
        public bool IsRefreshTokenRequest { get; set; }
        public UserAssertion UserAssertion { get; set; }

#endregion

        public void LogParameters(ICoreLogger logger)
        {
            // Create Pii enabled string builder
            var builder = new StringBuilder(
                Environment.NewLine + "=== Request Data ===" + Environment.NewLine + "Authority Provided? - " +
                (Authority != null) + Environment.NewLine);
            builder.AppendLine("Client Id - " + ClientId);
            builder.AppendLine("Scopes - " + Scope?.AsSingleString());
            builder.AppendLine("Redirect Uri - " + RedirectUri?.OriginalString);
            builder.AppendLine("Extra Query Params Keys (space separated) - " + ExtraQueryParameters.Keys.AsSingleString());

            string messageWithPii = builder.ToString();

            // Create no Pii enabled string builder
            builder = new StringBuilder(
                Environment.NewLine + "=== Request Data ===" + Environment.NewLine + "Authority Provided? - " +
                (Authority != null) + Environment.NewLine);
            builder.AppendLine("Scopes - " + Scope?.AsSingleString());
            builder.AppendLine("Extra Query Params Keys (space separated) - " + ExtraQueryParameters.Keys.AsSingleString());
            logger.InfoPii(messageWithPii, builder.ToString());
        }
    }
}
