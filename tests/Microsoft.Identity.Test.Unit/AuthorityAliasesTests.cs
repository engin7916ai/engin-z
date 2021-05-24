﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.UI;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.Identity.Test.Common.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Microsoft.Identity.Test.Unit
{
    [TestClass]
    public class AuthorityAliasesTests : TestBase
    {
      

        [TestMethod]
        [Description("Test authority migration")]
        public async Task AuthorityMigrationTestAsync()
        {
            // make sure that for all network calls "preferred_cache" environment is used
            // (it is taken from metadata in instance discovery response),
            // except very first network call - instance discovery

            using (var harness = base.CreateTestHarness())
            {
                var httpManager = harness.HttpManager;
                var authorityUri = new Uri(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "https://{0}/common",
                        TestConstants.ProductionNotPrefEnvironmentAlias));

                httpManager.AddInstanceDiscoveryMockHandler(authorityUri.AbsoluteUri);

                PublicClientApplication app = PublicClientApplicationBuilder
                    .Create(TestConstants.ClientId)
                          .WithAuthority(authorityUri, true)
                          .WithHttpManager(httpManager)
                          .WithUserTokenLegacyCachePersistenceForTest(new TestLegacyCachePersistance())
                          .WithTelemetry(new TraceTelemetryConfig())
                          .WithDebugLoggingCallback()
                          .BuildConcrete();

                // mock webUi authorization
                MsalMockHelpers.ConfigureMockWebUI(
                    app.ServiceBundle.PlatformProxy,
                    AuthorizationResult.FromUri(app.AppConfig.RedirectUri + "?code=some-code"), null, TestConstants.ProductionPrefNetworkEnvironment);

                // mock token request
                httpManager.AddMockHandler(new MockHttpMessageHandler
                {
                    ExpectedUrl = string.Format(CultureInfo.InvariantCulture, "https://{0}/common/oauth2/v2.0/token",
                        TestConstants.ProductionPrefNetworkEnvironment),
                    ExpectedMethod = HttpMethod.Post,
                    ResponseMessage = MockHelpers.CreateSuccessTokenResponseMessage()
                });

                AuthenticationResult result = app.AcquireTokenInteractive(TestConstants.s_scope).ExecuteAsync(CancellationToken.None).Result;

                // make sure that all cache entities are stored with "preferred_cache" environment
                // (it is taken from metadata in instance discovery response)
                await ValidateCacheEntitiesEnvironmentAsync(app.UserTokenCacheInternal, TestConstants.ProductionPrefCacheEnvironment).ConfigureAwait(false);

                // silent request targeting at, should return at from cache for any environment alias
                foreach (var envAlias in TestConstants.s_prodEnvAliases)
                {
                    result = await app
                        .AcquireTokenSilent(
                            TestConstants.s_scope,
                            app.GetAccountsAsync().Result.First())
                        .WithAuthority(string.Format(CultureInfo.InvariantCulture, "https://{0}/{1}/", envAlias, TestConstants.Utid))
                        .WithForceRefresh(false)
                        .ExecuteAsync(CancellationToken.None)
                        .ConfigureAwait(false);

                    Assert.IsNotNull(result);
                }

                // silent request targeting rt should find rt in cache for authority with any environment alias
                foreach (var envAlias in TestConstants.s_prodEnvAliases)
                {
                    result = null;

                    httpManager.AddMockHandler(new MockHttpMessageHandler()
                    {
                        ExpectedUrl = string.Format(CultureInfo.InvariantCulture, "https://{0}/{1}/oauth2/v2.0/token",
                            TestConstants.ProductionPrefNetworkEnvironment, TestConstants.Utid),
                        ExpectedMethod = HttpMethod.Post,
                        ExpectedPostData = new Dictionary<string, string>()
                    {
                        {"grant_type", "refresh_token"}
                    },
                        // return not retriable status code
                        ResponseMessage = MockHelpers.CreateInvalidGrantTokenResponseMessage()
                    });

                    try
                    {
                        result = await app
                            .AcquireTokenSilent(
                                TestConstants.s_scopeForAnotherResource,
                                (await app.GetAccountsAsync().ConfigureAwait(false)).First())
                            .WithAuthority(string.Format(CultureInfo.InvariantCulture, "https://{0}/{1}/", envAlias, TestConstants.Utid))
                            .WithForceRefresh(false)
                            .ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (MsalUiRequiredException)
                    {
                    }
                  

                    Assert.IsNull(result);
                }
            }
        }

        private async Task ValidateCacheEntitiesEnvironmentAsync(ITokenCacheInternal cache, string expectedEnvironment)
        {
            ICoreLogger logger = Substitute.For<ICoreLogger>();
            IEnumerable<Client.Cache.Items.MsalAccessTokenCacheItem> accessTokens = await cache.GetAllAccessTokensAsync(true).ConfigureAwait(false);
            foreach (Client.Cache.Items.MsalAccessTokenCacheItem at in accessTokens)
            {
                Assert.AreEqual(expectedEnvironment, at.Environment);
            }

            IEnumerable<Client.Cache.Items.MsalRefreshTokenCacheItem> refreshTokens = await cache.GetAllRefreshTokensAsync(true).ConfigureAwait(false);
            foreach (Client.Cache.Items.MsalRefreshTokenCacheItem rt in refreshTokens)
            {
                Assert.AreEqual(expectedEnvironment, rt.Environment);
            }

            IEnumerable<Client.Cache.Items.MsalIdTokenCacheItem> idTokens = await cache.GetAllIdTokensAsync(true).ConfigureAwait(false);
            foreach (Client.Cache.Items.MsalIdTokenCacheItem id in idTokens)
            {
                Assert.AreEqual(expectedEnvironment, id.Environment);
            }

            IEnumerable<Client.Cache.Items.MsalAccountCacheItem> accounts = await cache.GetAllAccountsAsync().ConfigureAwait(false);
            foreach (Client.Cache.Items.MsalAccountCacheItem account in accounts)
            {
                Assert.AreEqual(expectedEnvironment, account.Environment);
            }

            IDictionary<AdalTokenCacheKey, AdalResultWrapper> adalCache =
                AdalCacheOperations.Deserialize(logger, cache.LegacyPersistence.LoadCache());

            foreach (KeyValuePair<AdalTokenCacheKey, AdalResultWrapper> kvp in adalCache)
            {
                Assert.AreEqual(expectedEnvironment, new Uri(kvp.Key.Authority).Host);
            }
        }
    }
}
