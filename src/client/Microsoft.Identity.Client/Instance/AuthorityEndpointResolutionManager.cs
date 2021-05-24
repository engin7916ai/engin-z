// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.TelemetryCore.Internal;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Identity.Client.Instance
{
    internal class AuthorityEndpointResolutionManager : IAuthorityEndpointResolutionManager
    {
        private static readonly ConcurrentDictionary<string, AuthorityEndpointCacheEntry> s_endpointCacheEntries =
            new ConcurrentDictionary<string, AuthorityEndpointCacheEntry>();

        private readonly IServiceBundle _serviceBundle;

        public AuthorityEndpointResolutionManager(IServiceBundle serviceBundle, bool shouldClearCache = true)
        {
            _serviceBundle = serviceBundle;
            if (shouldClearCache)
            {
                s_endpointCacheEntries.Clear();
            }
        }

        public async Task<AuthorityEndpoints> ResolveEndpointsAsync(
            AuthorityInfo authorityInfo,
            string userPrincipalName,
            RequestContext requestContext)
        {
            if (TryGetCacheValue(authorityInfo, userPrincipalName, out var endpoints))
            {
                requestContext.Logger.Info(LogMessages.ResolvingAuthorityEndpointsTrue);
                return endpoints;
            }

            requestContext.Logger.Info(LogMessages.ResolvingAuthorityEndpointsFalse);

            var endpointManager = OpenIdConfigurationEndpointManagerFactory.Create(authorityInfo, _serviceBundle);

            string openIdConfigurationEndpoint = await endpointManager.ValidateAuthorityAndGetOpenIdDiscoveryEndpointAsync(
                                                     authorityInfo,
                                                     userPrincipalName,
                                                     requestContext).ConfigureAwait(false);

            // Discover endpoints via openid-configuration
            var edr = await DiscoverEndpointsAsync(openIdConfigurationEndpoint, requestContext).ConfigureAwait(false);

            if (string.IsNullOrEmpty(edr.AuthorizationEndpoint))
            {
                throw new MsalClientException(
                    MsalError.TenantDiscoveryFailedError,
                    MsalErrorMessage.AuthorizeEndpointWasNotFoundInTheOpenIdConfiguration);
            }

            if (string.IsNullOrEmpty(edr.TokenEndpoint))
            {
                throw new MsalClientException(
                    MsalError.TenantDiscoveryFailedError,
                    MsalErrorMessage.TokenEndpointWasNotFoundInTheOpenIdConfiguration);
            }

            if (string.IsNullOrEmpty(edr.Issuer))
            {
                throw new MsalClientException(
                    MsalError.TenantDiscoveryFailedError,
                    MsalErrorMessage.IssuerWasNotFoundInTheOpenIdConfiguration);
            }

            var authority = Authority.CreateAuthority(authorityInfo);
            var tenantId = authority.GetTenantId();

            string authorizationEndpoint = ReplaceTenantToken(edr.AuthorizationEndpoint, tenantId);
            string tokenEndpoint = ReplaceTenantToken(edr.TokenEndpoint, tenantId);

            endpoints = new AuthorityEndpoints(
                authorizationEndpoint,
                tokenEndpoint,
                GetSelfSignedJwtAudience(edr.Issuer, tokenEndpoint, tenantId, authorityInfo.AuthorityType));

            Add(authorityInfo, userPrincipalName, endpoints);
            return endpoints;
        }

        // Used in WithCertificate to create an audience claim in JWT sent by MSAL to EVO
        // ADAL uses the token endpoint as audience (tenanted or not)
        // MSAL had been using the issuer, which does not work when tenantnless. But continue to use issuer for ADFS and B2C.
        private static string GetSelfSignedJwtAudience(string issuer, string tokenEndpoint, string tenantId, AuthorityType authorityType)
        {
            if (authorityType == AuthorityType.Aad)
            {
                return tokenEndpoint;
            }

            return ReplaceTenantToken(issuer, tenantId);
        }

        private static string ReplaceTenantToken(string template, string tenantId)
        {
            // some templates use {tenant}, some {tenantid}
            template = template.Replace(Constants.Tenant, tenantId, StringComparison.OrdinalIgnoreCase);
            template = template.Replace(Constants.TenantId, tenantId, StringComparison.OrdinalIgnoreCase);
            return template;
        }

        private bool TryGetCacheValue(AuthorityInfo authorityInfo, string userPrincipalName, out AuthorityEndpoints endpoints)
        {
            endpoints = null;

            if (!s_endpointCacheEntries.TryGetValue(authorityInfo.CanonicalAuthority, out var cacheEntry))
            {
                return false;
            }

            if (authorityInfo.AuthorityType != AuthorityType.Adfs)
            {
                endpoints = cacheEntry.Endpoints;
                return true;
            }

            if (!string.IsNullOrEmpty(userPrincipalName))
            {
                if (!cacheEntry.ValidForDomainsList.Contains(AdfsUpnHelper.GetDomainFromUpn(userPrincipalName)))
                {
                    return false;
                }
            }

            endpoints = cacheEntry.Endpoints;
            return true;
        }

        private void Add(AuthorityInfo authorityInfo, string userPrincipalName, AuthorityEndpoints endpoints)
        {
            var updatedCacheEntry = new AuthorityEndpointCacheEntry(endpoints);

            if (authorityInfo.AuthorityType == AuthorityType.Adfs)
            {
                // Since we're here, we've made a call to the backend.  We want to ensure we're caching
                // the latest values from the server.
                if (s_endpointCacheEntries.TryGetValue(authorityInfo.CanonicalAuthority, out var cacheEntry))
                {
                    foreach (string s in cacheEntry.ValidForDomainsList)
                    {
                        updatedCacheEntry.ValidForDomainsList.Add(s);
                    }
                }

                if (!string.IsNullOrEmpty(userPrincipalName))
                {
                    updatedCacheEntry.ValidForDomainsList.Add(AdfsUpnHelper.GetDomainFromUpn(userPrincipalName));
                }
            }

            s_endpointCacheEntries.TryAdd(authorityInfo.CanonicalAuthority, updatedCacheEntry);
        }

        private async Task<TenantDiscoveryResponse> DiscoverEndpointsAsync(
             string openIdConfigurationEndpoint,
             RequestContext requestContext)
        {
            var client = new OAuth2Client(requestContext.Logger, _serviceBundle.HttpManager, _serviceBundle.TelemetryManager);
            return await client.ExecuteRequestAsync<TenantDiscoveryResponse>(
                       new Uri(openIdConfigurationEndpoint),
                       HttpMethod.Get,
                       requestContext).ConfigureAwait(false);
        }

        private class AuthorityEndpointCacheEntry
        {
            public AuthorityEndpointCacheEntry(AuthorityEndpoints endpoints)
            {
                Endpoints = endpoints;
            }

            public AuthorityEndpoints Endpoints { get; }
            public HashSet<string> ValidForDomainsList { get; } = new HashSet<string>();
        }
    }
}
