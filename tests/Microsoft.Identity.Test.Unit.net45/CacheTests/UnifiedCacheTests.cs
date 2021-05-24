﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.UI;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.Identity.Test.Common.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Common.Core.Helpers;
using System.Threading;
using Microsoft.Identity.Client.Instance;

namespace Microsoft.Identity.Test.Unit.CacheTests
{
    [TestClass]
    public class UnifiedCacheTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            TestCommon.ResetInternalStaticCaches();
        }

#if !NET_CORE
        [TestMethod]
        [Description("Test unified token cache")]
        public void UnifiedCache_MsalStoresToAndReadRtFromAdalCache()
        {
            using (var httpManager = new MockHttpManager())
            {
                httpManager.AddInstanceDiscoveryMockHandler();

                var app = PublicClientApplicationBuilder.Create(MsalTestConstants.ClientId)
                                                        .WithAuthority(new Uri(ClientApplicationBase.DefaultAuthority), true)
                                                        .WithHttpManager(httpManager)
                                                        .WithUserTokenLegacyCachePersistenceForTest(
                                                            new TestLegacyCachePersistance())
                                                        .BuildConcrete();

                MsalMockHelpers.ConfigureMockWebUI(
                    app.ServiceBundle.PlatformProxy,
                    new AuthorizationResult(AuthorizationStatus.Success,
                    app.AppConfig.RedirectUri + "?code=some-code"));
                httpManager.AddMockHandlerForTenantEndpointDiscovery(MsalTestConstants.AuthorityCommonTenant);
                httpManager.AddSuccessTokenResponseMockHandlerForPost(ClientApplicationBase.DefaultAuthority);

                AuthenticationResult result = app.AcquireTokenInteractive(MsalTestConstants.Scope).ExecuteAsync(CancellationToken.None).Result;
                Assert.IsNotNull(result);

                // make sure Msal stored RT in Adal cache
                IDictionary<AdalTokenCacheKey, AdalResultWrapper> adalCacheDictionary =
                    AdalCacheOperations.Deserialize(app.ServiceBundle.DefaultLogger, app.UserTokenCacheInternal.LegacyPersistence.LoadCache());

                Assert.IsTrue(adalCacheDictionary.Count == 1);

                var requestContext = RequestContext.CreateForTest(app.ServiceBundle);
                var accounts = app.UserTokenCacheInternal.GetAccountsAsync(
                    MsalTestConstants.AuthorityCommonTenant, requestContext).Result;
                foreach (IAccount account in accounts)
                {
                    app.UserTokenCacheInternal.RemoveMsalAccount(account, requestContext);
                }

                httpManager.AddMockHandler(
                    new MockHttpMessageHandler()
                    {
                        ExpectedMethod = HttpMethod.Post,
                        ExpectedPostData = new Dictionary<string, string>()
                        {
                            {"grant_type", "refresh_token"}
                        },
                        ResponseMessage = MockHelpers.CreateSuccessTokenResponseMessage(
                            MsalTestConstants.UniqueId,
                            MsalTestConstants.DisplayableId,
                            MsalTestConstants.Scope.ToArray())
                    });

                // Using RT from Adal cache for silent call
                AuthenticationResult result1 = app
                    .AcquireTokenSilent(MsalTestConstants.Scope, result.Account)
                    .WithAuthority(MsalTestConstants.AuthorityCommonTenant)
                    .WithForceRefresh(false)
                    .ExecuteAsync(CancellationToken.None)
                    .Result;

                Assert.IsNotNull(result1);
            }
        }

        [TestMethod]
        [Description("Test that RemoveAccount api is application specific")]
        public void UnifiedCache_RemoveAccountIsApplicationSpecific()
        {
            byte[] data = null;

            using (var httpManager = new MockHttpManager())
            {
                // login to app
                var app = PublicClientApplicationBuilder.Create(MsalTestConstants.ClientId)
                                                        .WithAuthority(new Uri(ClientApplicationBase.DefaultAuthority), true)
                                                        .WithHttpManager(httpManager)
                                                        .BuildConcrete();

                app.UserTokenCache.SetBeforeAccess((TokenCacheNotificationArgs args) =>
                {
                    args.TokenCache.DeserializeMsalV3(data);
                });
                app.UserTokenCache.SetAfterAccess((TokenCacheNotificationArgs args) =>
                {
                    data = args.TokenCache.SerializeMsalV3();
                });

                httpManager.AddInstanceDiscoveryMockHandler();

                MsalMockHelpers.ConfigureMockWebUI(
                    app.ServiceBundle.PlatformProxy,
                    new AuthorizationResult(AuthorizationStatus.Success,
                    app.AppConfig.RedirectUri + "?code=some-code"));

                httpManager.AddMockHandlerForTenantEndpointDiscovery(MsalTestConstants.AuthorityCommonTenant);
                httpManager.AddSuccessTokenResponseMockHandlerForPost(ClientApplicationBase.DefaultAuthority);

                AuthenticationResult result = app
                    .AcquireTokenInteractive(MsalTestConstants.Scope)
                    .ExecuteAsync(CancellationToken.None)
                    .Result;

                Assert.IsNotNull(result);

                Assert.AreEqual(1, app.UserTokenCacheInternal.Accessor.GetAllAccounts().Count());
                Assert.AreEqual(1, app.GetAccountsAsync().Result.Count());

                // login to app1 with same credentials

                var app1 = PublicClientApplicationBuilder.Create(MsalTestConstants.ClientId2)
                                                         .WithHttpManager(httpManager)
                                                         .WithAuthority(
                                                             new Uri(ClientApplicationBase.DefaultAuthority),
                                                             true).BuildConcrete();

                MsalMockHelpers.ConfigureMockWebUI(
                    app1.ServiceBundle.PlatformProxy,
                    new AuthorizationResult(AuthorizationStatus.Success,
                    app1.AppConfig.RedirectUri + "?code=some-code"));

                app1.UserTokenCache.SetBeforeAccess((TokenCacheNotificationArgs args) =>
                {
                    args.TokenCache.DeserializeMsalV3(data);
                });
                app1.UserTokenCache.SetAfterAccess((TokenCacheNotificationArgs args) =>
                {
                    data = args.TokenCache.SerializeMsalV3();
                });

                httpManager.AddSuccessTokenResponseMockHandlerForPost(ClientApplicationBase.DefaultAuthority);

                result = app1
                    .AcquireTokenInteractive(MsalTestConstants.Scope)
                    .ExecuteAsync(CancellationToken.None)
                    .Result;

                Assert.IsNotNull(result);

                // make sure that only one account cache entity was created
                Assert.AreEqual(1, app1.GetAccountsAsync().Result.Count());

                Assert.AreEqual(2, app1.UserTokenCacheInternal.Accessor.GetAllAccessTokens().Count());
                Assert.AreEqual(2, app1.UserTokenCacheInternal.Accessor.GetAllRefreshTokens().Count());
                Assert.AreEqual(2, app1.UserTokenCacheInternal.Accessor.GetAllIdTokens().Count());
                Assert.AreEqual(1, app1.UserTokenCacheInternal.Accessor.GetAllAccounts().Count());

                // remove account from app
                app.RemoveAsync(app.GetAccountsAsync().Result.First()).Wait();

                // make sure account removed from app
                Assert.AreEqual(0, app.GetAccountsAsync().Result.Count());

                // make sure account Not removed from app1
                Assert.AreEqual(1, app1.GetAccountsAsync().Result.Count());
            }
        }
#endif

        [TestMethod]
        [Description("Test for duplicate key in ADAL cache")]
        public void UnifiedCache_ProcessAdalDictionaryForDuplicateKeyTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                var app = PublicClientApplicationBuilder
                          .Create(MsalTestConstants.ClientId).WithAuthority(new Uri(ClientApplicationBase.DefaultAuthority), true)
                          .WithUserTokenLegacyCachePersistenceForTest(new TestLegacyCachePersistance()).BuildConcrete();

                CreateAdalCache(harness.ServiceBundle.DefaultLogger, app.UserTokenCacheInternal.LegacyPersistence, MsalTestConstants.Scope.ToString());

                var adalUsers =
                    CacheFallbackOperations.GetAllAdalUsersForMsal(
                        harness.ServiceBundle.DefaultLogger,
                        app.UserTokenCacheInternal.LegacyPersistence,
                        MsalTestConstants.ClientId);

                CreateAdalCache(harness.ServiceBundle.DefaultLogger, app.UserTokenCacheInternal.LegacyPersistence, "user.read");

                var adalUsers2 =
                    CacheFallbackOperations.GetAllAdalUsersForMsal(
                        harness.ServiceBundle.DefaultLogger,
                        app.UserTokenCacheInternal.LegacyPersistence,
                        MsalTestConstants.ClientId);

                Assert.AreEqual(
                    adalUsers.GetUsersWithClientInfo(null).Single().Key,
                    adalUsers2.GetUsersWithClientInfo(null).Single().Key);

                app.UserTokenCacheInternal.Accessor.ClearAccessTokens();
                app.UserTokenCacheInternal.Accessor.ClearRefreshTokens();
            }
        }

        private void CreateAdalCache(ICoreLogger logger, ILegacyCachePersistence legacyCachePersistence, string scopes)
        {
            var key = new AdalTokenCacheKey(
                MsalTestConstants.AuthorityHomeTenant,
                scopes,
                MsalTestConstants.ClientId,
                MsalTestConstants.TokenSubjectTypeUser,
                MsalTestConstants.UniqueId,
                MsalTestConstants.User.Username);

            var wrapper = new AdalResultWrapper()
            {
                Result = new AdalResult(null, null, DateTimeOffset.MinValue)
                {
                    UserInfo = new AdalUserInfo()
                    {
                        UniqueId = MsalTestConstants.UniqueId,
                        DisplayableId = MsalTestConstants.User.Username
                    }
                },
                RefreshToken = MsalTestConstants.ClientSecret,
                RawClientInfo = MsalTestConstants.RawClientId,
                ResourceInResponse = scopes
            };

            IDictionary<AdalTokenCacheKey, AdalResultWrapper> dictionary =
                AdalCacheOperations.Deserialize(logger, legacyCachePersistence.LoadCache());
            dictionary[key] = wrapper;
            legacyCachePersistence.WriteCache(AdalCacheOperations.Serialize(logger, dictionary));
        }
    }
}
