﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.PlatformsCommon.Interfaces;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Microsoft.Identity.Test.Unit.CacheTests
{
    [TestClass]
    public class TokenCacheTests
    {
        public static long ValidExpiresIn = 3600;
        public static long ValidExtendedExpiresIn = 7200;

        private readonly TokenCacheHelper _tokenCacheHelper = new TokenCacheHelper();

        [TestInitialize]
        public void TestInitialize()
        {
            TestCommon.ResetInternalStaticCaches();
        }

        private void AddHostToInstanceCache(IServiceBundle serviceBundle, string host)
        {
            serviceBundle.AadInstanceDiscovery.TryAddValue(
                host,
                new InstanceDiscoveryMetadataEntry
                {
                    PreferredNetwork = host,
                    PreferredCache = host,
                    Aliases = new string[]
                    {
                        host
                    }
                });
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetExactScopesMatchedAccessTokenTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);
                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    "",
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExpiresIn)),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExtendedExpiresIn)),
                    MockHelpers.CreateClientInfo());

                // create key out of access token cache item and then
                // set it as the value of the access token.
                string atKey = atItem.GetKey().ToString();
                atItem.Secret = atKey;

                cache.Accessor.SaveAccessToken(atItem);
                var item = cache.FindAccessTokenAsync(
                    harness.CreateAuthenticationRequestParameters(MsalTestConstants.AuthorityTestTenant, MsalTestConstants.Scope, account: MsalTestConstants.User)).Result;

                Assert.IsNotNull(item);
                Assert.AreEqual(atKey, item.Secret);
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetSubsetScopesMatchedAccessTokenTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    null,
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(1)),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(2)),
                    MockHelpers.CreateClientInfo());

                // create key out of access token cache item and then
                // set it as the value of the access token.
                string atKey = atItem.GetKey().ToString();
                atItem.Secret = atKey;

                cache.Accessor.SaveAccessToken(atItem);
                var param = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    new SortedSet<string>(),
                    account: MsalTestConstants.User);

                param.Scope.Add("r1/scope1");
                var item = cache.FindAccessTokenAsync(param).Result;

                Assert.IsNotNull(item);
                Assert.AreEqual(atKey, item.Secret);
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetIntersectedScopesMatchedAccessTokenTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    null,
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(1)),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(2)),
                    MockHelpers.CreateClientInfo());

                // create key out of access token cache item and then
                // set it as the value of the access token.
                string atKey = atItem.GetKey().ToString();
                atItem.Secret = atKey;

                cache.Accessor.SaveAccessToken(atItem);

                var param = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityHomeTenant,
                    new SortedSet<string>(),
                    account: new Account(MsalTestConstants.UserIdentifier, MsalTestConstants.DisplayableId, null));

                param.Scope.Add(MsalTestConstants.Scope.First());
                param.Scope.Add("non-existent-scopes");
                var item = cache.FindAccessTokenAsync(param).Result;

                // intersected scopes are not returned.
                Assert.IsNull(item);
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetExpiredAccessTokenTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    null,
                    new DateTimeOffset(DateTime.UtcNow),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(2)),
                    MockHelpers.CreateClientInfo());

                atItem.Secret = atItem.GetKey().ToString();
                cache.Accessor.SaveAccessToken(atItem);

                var param = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    new SortedSet<string>(),
                    account: new Account(MsalTestConstants.UserIdentifier, MsalTestConstants.DisplayableId, null));

                Assert.IsNull(cache.FindAccessTokenAsync(param).Result);
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetExpiredAccessToken_WithExtendedExpireStillValid_Test()
        {
            using (var harness = new MockHttpAndServiceBundle(isExtendedTokenLifetimeEnabled: true))
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    null,
                    new DateTimeOffset(DateTime.UtcNow),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(2)),
                    MockHelpers.CreateClientInfo());

                atItem.Secret = atItem.GetKey().ToString();
                cache.Accessor.SaveAccessToken(atItem);

                var param = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    new SortedSet<string>(),
                    account: new Account(MsalTestConstants.UserIdentifier, MsalTestConstants.DisplayableId, null));

                var cacheItem = cache.FindAccessTokenAsync(param).Result;

                Assert.IsNotNull(cacheItem);
                Assert.AreEqual(atItem.GetKey().ToString(), cacheItem.GetKey().ToString());
                Assert.IsTrue(cacheItem.IsExtendedLifeTimeToken);
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetAccessTokenExpiryInRangeTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    "",
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromMinutes(4)),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(2)),
                    MockHelpers.CreateClientInfo());

                atItem.Secret = atItem.GetKey().ToString();
                cache.Accessor.SaveAccessToken(atItem);

                var param = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    new SortedSet<string>(),
                    account: new Account(MsalTestConstants.UserIdentifier, MsalTestConstants.DisplayableId, null));

                Assert.IsNull(cache.FindAccessTokenAsync(param).Result);
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetRefreshTokenTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var rtItem = new MsalRefreshTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    "someRT",
                    MockHelpers.CreateClientInfo());

                string rtKey = rtItem.GetKey().ToString();
                cache.Accessor.SaveRefreshToken(rtItem);

                var authParams = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    MsalTestConstants.Scope,
                    account: MsalTestConstants.User);

                Assert.IsNotNull(cache.FindRefreshTokenAsync(authParams));

                // RT is stored by environment, client id and userIdentifier as index.
                // any change to authority (within same environment), uniqueid and displyableid will not
                // change the outcome of cache look up.
                Assert.IsNotNull(cache.FindRefreshTokenAsync(
                                     harness.CreateAuthenticationRequestParameters(
                                         MsalTestConstants.AuthorityHomeTenant + "more",
                                         MsalTestConstants.Scope,
                                         account: MsalTestConstants.User)));
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetRefreshTokenDifferentEnvironmentTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);
                var rtItem = new MsalRefreshTokenCacheItem(
                    MsalTestConstants.SovereignNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    "someRT",
                    MockHelpers.CreateClientInfo());

                string rtKey = rtItem.GetKey().ToString();
                cache.Accessor.SaveRefreshToken(rtItem);

                var authParams = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    MsalTestConstants.Scope,
                    account: MsalTestConstants.User);

                var rt = cache.FindRefreshTokenAsync(authParams).Result;
                Assert.IsNull(rt);
            }
        }

#if !WINDOWS_APP && !ANDROID && !iOS // Confidential Client N/A
        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetAppTokenFromCacheTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    null,
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExpiresIn)),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromSeconds(ValidExtendedExpiresIn)),
                    MockHelpers.CreateClientInfo());

                string atKey = atItem.GetKey().ToString();
                atItem.Secret = atKey;

                cache.Accessor.SaveAccessToken(atItem);

                var authParams = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    MsalTestConstants.Scope);
                authParams.ClientCredential = MsalTestConstants.CredentialWithSecret;
                authParams.IsClientCredentialRequest = true;

                var cacheItem = cache.FindAccessTokenAsync(authParams).Result;

                Assert.IsNotNull(cacheItem);
                Assert.AreEqual(atItem.GetKey().ToString(), cacheItem.GetKey().ToString());
            }
        }
#endif

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void DoNotSaveRefreshTokenInAdalCacheForMsalB2CAuthorityTest()
        {
            var appConfig = new ApplicationConfiguration()
            {
                ClientId = MsalTestConstants.ClientId,
                RedirectUri = MsalTestConstants.RedirectUri,
                AuthorityInfo = AuthorityInfo.FromAuthorityUri(MsalTestConstants.B2CAuthority, false)
            };

            var serviceBundle = ServiceBundle.Create(appConfig);
            ITokenCacheInternal cache = new TokenCache(serviceBundle);

            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();

            var requestParams = CreateAuthenticationRequestParameters(serviceBundle, authority: Authority.CreateAuthority(serviceBundle, MsalTestConstants.B2CAuthority));
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityTestTenant;

            AddHostToInstanceCache(serviceBundle, MsalTestConstants.ProductionPrefNetworkEnvironment);

            cache.SaveTokenResponse(requestParams, response);

            Assert.AreEqual(1, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(1, cache.Accessor.GetAllAccessTokens().Count());

            IDictionary<AdalTokenCacheKey, AdalResultWrapper> dictionary =
                AdalCacheOperations.Deserialize(serviceBundle.DefaultLogger, cache.LegacyPersistence.LoadCache());
            cache.LegacyPersistence.WriteCache(AdalCacheOperations.Serialize(serviceBundle.DefaultLogger, dictionary));

            // ADAL cache is empty because B2C scenario is only for MSAL
            Assert.AreEqual(0, dictionary.Count);
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetAccessTokenNoUserAssertionInCacheTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    null,
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(1)),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(2)),
                    MockHelpers.CreateClientInfo());

                // create key out of access token cache item and then
                // set it as the value of the access token.
                string atKey = atItem.GetKey().ToString();
                atItem.Secret = atKey;

                cache.Accessor.SaveAccessToken(atItem);

                var authParams = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    MsalTestConstants.Scope);
                authParams.UserAssertion = new UserAssertion(
                    harness.ServiceBundle.PlatformProxy.CryptographyManager.CreateBase64UrlEncodedSha256Hash(atKey));

                var item = cache.FindAccessTokenAsync(authParams).Result;

                // cache lookup should fail because there was no userassertion hash in the matched
                // token cache item.

                Assert.IsNull(item);
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetAccessTokenUserAssertionMismatchInCacheTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    null,
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(1)),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(2)),
                    MockHelpers.CreateClientInfo());

                // create key out of access token cache item and then
                // set it as the value of the access token.
                string atKey = atItem.GetKey().ToString();
                atItem.Secret = atKey;

                atItem.UserAssertionHash = harness.ServiceBundle.PlatformProxy.CryptographyManager.CreateBase64UrlEncodedSha256Hash(atKey);

                cache.Accessor.SaveAccessToken(atItem);

                var authParams = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    MsalTestConstants.Scope);
                authParams.UserAssertion = new UserAssertion(atItem.UserAssertionHash + "-random");

                var item = cache.FindAccessTokenAsync(authParams).Result;

                // cache lookup should fail because there was userassertion hash did not match the one
                // stored in token cache item.
                Assert.IsNull(item);
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetAccessTokenMatchedUserAssertionInCacheTest()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);

                var atItem = new MsalAccessTokenCacheItem(
                    MsalTestConstants.ProductionPrefNetworkEnvironment,
                    MsalTestConstants.ClientId,
                    MsalTestConstants.Scope.AsSingleString(),
                    MsalTestConstants.Utid,
                    null,
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(1)),
                    new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(2)),
                    MockHelpers.CreateClientInfo());

                // create key out of access token cache item and then
                // set it as the value of the access token.
                string atKey = atItem.GetKey().ToString();
                atItem.Secret = atKey;
                atItem.UserAssertionHash = harness.ServiceBundle.PlatformProxy.CryptographyManager.CreateBase64UrlEncodedSha256Hash(atKey);

                cache.Accessor.SaveAccessToken(atItem);

                var authParams = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    MsalTestConstants.Scope);
                authParams.UserAssertion = new UserAssertion(atKey);

                ((TokenCache)cache).AfterAccess = AfterAccessNoChangeNotification;
                var item = cache.FindAccessTokenAsync(authParams).Result;

                Assert.IsNotNull(item);
                Assert.AreEqual(atKey, item.Secret);
            }
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void SaveAccessAndRefreshTokenWithEmptyCacheTest()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();
            ITokenCacheInternal cache = new TokenCache(serviceBundle);

            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();

            var requestParams = CreateAuthenticationRequestParameters(serviceBundle);
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityTestTenant;

            AddHostToInstanceCache(serviceBundle, MsalTestConstants.ProductionPrefNetworkEnvironment);

            cache.SaveTokenResponse(requestParams, response);

            cache.Accessor.AssertItemCount(
                expectedAtCount: 1,
                expectedRtCount: 1,
                expectedAccountCount: 1,
                expectedIdtCount: 1,
                expectedAppMetadataCount: 1);

            var metadata = cache.Accessor.GetAllAppMetadata().First();
            Assert.AreEqual(MsalTestConstants.ClientId, metadata.ClientId);
            Assert.AreEqual(MsalTestConstants.ProductionPrefNetworkEnvironment, metadata.Environment);
            Assert.IsNull(metadata.FamilyId);
        }

        [TestMethod]
        public void NoAppMetadata_WhenFociIsDisabled()
        {
            using (var harness = new MockHttpAndServiceBundle())
            {
                // Arrange
                var testFlags = Substitute.For<IFeatureFlags>();
                testFlags.IsFociEnabled.Returns(false);

                harness.ServiceBundle.PlatformProxy.SetFeatureFlags(testFlags);

                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);
                MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();
                var requestParams = CreateAuthenticationRequestParameters(harness.ServiceBundle);
                requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityTestTenant;
                AddHostToInstanceCache(harness.ServiceBundle, MsalTestConstants.ProductionPrefNetworkEnvironment);

                // Act
                cache.SaveTokenResponse(requestParams, response);

                // Assert
                cache.Accessor.AssertItemCount(
                    expectedAtCount: 1,
                    expectedRtCount: 1,
                    expectedAccountCount: 1,
                    expectedIdtCount: 1,
                    expectedAppMetadataCount: 0);

                // Don't save RT as an FRT if FOCI is disabled
                Assert.IsTrue(string.IsNullOrEmpty(cache.Accessor.GetAllRefreshTokens().First().FamilyId));
            }
        }

        [TestMethod]
        public void SaveMultipleAppmetadata()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();
            ITokenCacheInternal cache = new TokenCache(serviceBundle);

            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();
            MsalTokenResponse response2 = MsalTestConstants.CreateMsalTokenResponse();
            response2.FamilyId = "1";

            var requestParams = CreateAuthenticationRequestParameters(serviceBundle);
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityTestTenant;

            AddHostToInstanceCache(serviceBundle, MsalTestConstants.ProductionPrefNetworkEnvironment);

            cache.SaveTokenResponse(requestParams, response);
            cache.SaveTokenResponse(requestParams, response2);

            cache.Accessor.AssertItemCount(
                expectedAtCount: 1,
                expectedRtCount: 2, // a normal RT and an FRT
                expectedAccountCount: 1,
                expectedIdtCount: 1,
                expectedAppMetadataCount: 1);

            var metadata = cache.Accessor.GetAllAppMetadata().First();
            Assert.AreEqual(MsalTestConstants.ClientId, metadata.ClientId);
            Assert.AreEqual(MsalTestConstants.ProductionPrefNetworkEnvironment, metadata.Environment);
            Assert.AreEqual(MsalTestConstants.FamilyId, metadata.FamilyId);

            Assert.IsTrue(cache.Accessor.GetAllRefreshTokens().Any(rt => rt.FamilyId == "1"));
            Assert.IsTrue(cache.Accessor.GetAllRefreshTokens().Any(rt => string.IsNullOrEmpty(rt.FamilyId)));
        }


        [TestMethod]
        public void CreateFrtFromTokenResponse()
        {
            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();
            response.FamilyId = "1";

            var frt = new MsalRefreshTokenCacheItem("env", MsalTestConstants.ClientId, response);

            Assert.AreEqual("1", frt.FamilyId);
        }


        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void SaveAccessAndRefreshTokenWithMoreScopesTest()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();
            ITokenCacheInternal cache = new TokenCache(serviceBundle);
            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();

            var requestParams = CreateAuthenticationRequestParameters(serviceBundle);
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityTestTenant;

            AddHostToInstanceCache(serviceBundle, MsalTestConstants.ProductionPrefNetworkEnvironment);

            cache.SaveTokenResponse(requestParams, response);

            Assert.AreEqual(1, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(1, cache.Accessor.GetAllAccessTokens().Count());

            Assert.IsNull(cache.Accessor.GetAllRefreshTokens().First().FamilyId);

            response = MsalTestConstants.CreateMsalTokenResponse();
            response.Scope = MsalTestConstants.Scope.AsSingleString() + " another-scope";
            response.AccessToken = "access-token-2";
            response.RefreshToken = "refresh-token-2";

            cache.SaveTokenResponse(requestParams, response);

            Assert.AreEqual(1, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(1, cache.Accessor.GetAllAccessTokens().Count());

            Assert.AreEqual("refresh-token-2", cache.GetAllRefreshTokens(true).First().Secret);
            Assert.AreEqual("access-token-2", cache.GetAllAccessTokens(true).First().Secret);
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void SaveAccessAndRefreshTokenWithLessScopesTest()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();
            ITokenCacheInternal cache = new TokenCache(serviceBundle);
            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();

            var requestParams = CreateAuthenticationRequestParameters(serviceBundle);
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityTestTenant;

            AddHostToInstanceCache(serviceBundle, MsalTestConstants.ProductionPrefNetworkEnvironment);

            cache.SaveTokenResponse(requestParams, response);

            response = MsalTestConstants.CreateMsalTokenResponse();
            response.Scope = MsalTestConstants.Scope.First();
            response.AccessToken = "access-token-2";
            response.RefreshToken = "refresh-token-2";

            cache.SaveTokenResponse(requestParams, response);

            Assert.AreEqual(1, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(1, cache.Accessor.GetAllAccessTokens().Count());
            Assert.AreEqual("refresh-token-2", cache.GetAllRefreshTokens(true).First().Secret);
            Assert.AreEqual("access-token-2", cache.GetAllAccessTokens(true).First().Secret);
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void SaveAccessAndRefreshTokenWithIntersectingScopesTest()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();
            ITokenCacheInternal cache = new TokenCache(serviceBundle);
            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();

            var requestParams = CreateAuthenticationRequestParameters(serviceBundle);
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityTestTenant;

            AddHostToInstanceCache(serviceBundle, MsalTestConstants.ProductionPrefNetworkEnvironment);

            cache.SaveTokenResponse(requestParams, response);

            response = MsalTestConstants.CreateMsalTokenResponse();
            response.Scope = MsalTestConstants.Scope.AsSingleString() + " random-scope";
            response.AccessToken = "access-token-2";
            response.RefreshToken = "refresh-token-2";

            cache.SaveTokenResponse(requestParams, response);

            Assert.AreEqual(1, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(1, cache.Accessor.GetAllAccessTokens().Count());

            Assert.AreEqual("refresh-token-2", cache.GetAllRefreshTokens(true).First().Secret);
            Assert.AreEqual("access-token-2", cache.GetAllAccessTokens(true).First().Secret);
        }

        private void AfterAccessChangedNotification(TokenCacheNotificationArgs args)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.IsTrue(((TokenCache)args.TokenCache).HasStateChanged);
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.IsTrue(args.HasStateChanged);

        }

        private void AfterAccessNoChangeNotification(TokenCacheNotificationArgs args)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.IsFalse(((TokenCache)args.TokenCache).HasStateChanged);
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.IsFalse(args.HasStateChanged);
        }


#if !WINDOWS_APP && !ANDROID && !iOS // Token Cache Serialization N/A

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void SaveAccessAndRefreshTokenWithDifferentAuthoritySameUserTest()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();
            ITokenCacheInternal cache = new TokenCache(serviceBundle);
            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();

            var requestParams = CreateAuthenticationRequestParameters(serviceBundle);
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityHomeTenant;

            AddHostToInstanceCache(serviceBundle, MsalTestConstants.ProductionPrefNetworkEnvironment);

            cache.SaveTokenResponse(requestParams, response);
            response = MsalTestConstants.CreateMsalTokenResponse();
            response.Scope = MsalTestConstants.Scope.AsSingleString() + " another-scope";
            response.AccessToken = "access-token-2";
            response.RefreshToken = "refresh-token-2";

            requestParams = CreateAuthenticationRequestParameters(serviceBundle);
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityGuestTenant;

            cache.SetAfterAccess(AfterAccessChangedNotification);
            cache.SaveTokenResponse(requestParams, response);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.IsFalse(((TokenCache)cache).HasStateChanged);
#pragma warning restore CS0618 // Type or member is obsolete

            Assert.AreEqual(1, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(2, cache.Accessor.GetAllAccessTokens().Count());

            Assert.AreEqual("refresh-token-2", cache.GetAllRefreshTokens(true).First().Secret);
        }


        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void CanDeserializeTokenCacheInNet462()
        {
            var tokenCache = new TokenCache(TestCommon.CreateDefaultServiceBundle())
            {
                AfterAccess = args => { Assert.IsFalse(args.HasStateChanged); }
            };
            tokenCache.DeserializeMsalV3(null);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.IsFalse(tokenCache.HasStateChanged, "State should not have changed when deserializing nothing.");
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void SerializeDeserializeCacheTest()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();
            ITokenCacheInternal cache = new TokenCache(serviceBundle);

            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();

            var requestContext = RequestContext.CreateForTest(serviceBundle);
            var requestParams = CreateAuthenticationRequestParameters(serviceBundle, requestContext: requestContext);
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityTestTenant;

            AddHostToInstanceCache(serviceBundle, MsalTestConstants.ProductionPrefNetworkEnvironment);

            cache.SaveTokenResponse(requestParams, response);
            byte[] serializedCache = cache.SerializeMsalV3();

            cache.Accessor.ClearAccessTokens();
            cache.Accessor.ClearRefreshTokens();

            Assert.AreEqual(0, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(0, cache.Accessor.GetAllAccessTokens().Count());

            cache.DeserializeMsalV3(serializedCache);

            Assert.AreEqual(1, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(1, cache.Accessor.GetAllAccessTokens().Count());

            serializedCache = cache.SerializeMsalV3();
            cache.DeserializeMsalV3(serializedCache);
            // item count should not change because old cache entries should have
            // been overriden

            Assert.AreEqual(1, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(1, cache.Accessor.GetAllAccessTokens().Count());

            var atItem = cache.GetAllAccessTokens(true).First();
            Assert.AreEqual(response.AccessToken, atItem.Secret);
            Assert.AreEqual(MsalTestConstants.AuthorityTestTenant, atItem.Authority);
            Assert.AreEqual(MsalTestConstants.ClientId, atItem.ClientId);
            Assert.AreEqual(response.Scope, atItem.ScopeSet.AsSingleString());

            // todo add test for idToken serialization
            // Assert.AreEqual(response.IdToken, atItem.RawIdToken);

            var rtItem = cache.GetAllRefreshTokens(true).First();
            Assert.AreEqual(response.RefreshToken, rtItem.Secret);
            Assert.AreEqual(MsalTestConstants.ClientId, rtItem.ClientId);
            Assert.AreEqual(MsalTestConstants.UserIdentifier, rtItem.HomeAccountId);
            Assert.AreEqual(MsalTestConstants.ProductionPrefNetworkEnvironment, rtItem.Environment);
        }
#endif

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void FindAccessToken_ScopeCaseInsensitive()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();
            ITokenCacheInternal cache = new TokenCache(serviceBundle);

            _tokenCacheHelper.PopulateCacheWithOneAccessToken(cache.Accessor);

            var requestParams = CreateAuthenticationRequestParameters(serviceBundle, scopes: new SortedSet<string>());
            requestParams.Account = MsalTestConstants.User;

            string scopeInCache = MsalTestConstants.Scope.FirstOrDefault();

            string upperCaseScope = scopeInCache.ToUpperInvariant();
            requestParams.Scope.Add(upperCaseScope);

            var item = cache.FindAccessTokenAsync(requestParams).Result;

            Assert.IsNotNull(item);
            Assert.IsTrue(item.ScopeSet.Contains(scopeInCache));
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void CacheB2CTokenTest()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();
            ITokenCacheInternal cache = new TokenCache(serviceBundle);

            string tenantID = "someTenantID";
            var authority = Authority.CreateAuthority(
                serviceBundle,
                $"https://login.microsoftonline.com/tfp/{tenantID}/somePolicy/oauth2/v2.0/authorize");

            // creating IDToken with empty tenantID and displayableID/PreferredUserName for B2C scenario
            MsalTokenResponse response = MsalTestConstants.CreateMsalTokenResponse();

            var requestContext = RequestContext.CreateForTest(serviceBundle);
            var requestParams = CreateAuthenticationRequestParameters(serviceBundle, authority, requestContext: requestContext);
            requestParams.TenantUpdatedCanonicalAuthority = MsalTestConstants.AuthorityTestTenant;

            cache.SaveTokenResponse(requestParams, response);

            Assert.AreEqual(1, cache.Accessor.GetAllRefreshTokens().Count());
            Assert.AreEqual(1, cache.Accessor.GetAllAccessTokens().Count());
        }

        private AuthenticationRequestParameters CreateAuthenticationRequestParameters(
            IServiceBundle serviceBundle,
            Authority authority = null,
            SortedSet<string> scopes = null,
            RequestContext requestContext = null)
        {
            var commonParameters = new AcquireTokenCommonParameters
            {
                Scopes = scopes ?? MsalTestConstants.Scope,
            };

            return new AuthenticationRequestParameters(
                serviceBundle,
                null,
                commonParameters,
                requestContext ?? RequestContext.CreateForTest(serviceBundle))
            {
                Authority = authority ?? Authority.CreateAuthority(serviceBundle, MsalTestConstants.AuthorityTestTenant)
            };
        }

        [TestMethod]
        [Ignore]  // todo(migration): need to figure out cache issue
        [TestCategory("TokenCacheTests")]
        public void TestCacheDeserializeWithoutServiceBundle()
        {
            var tokenCache = new TokenCache();
            tokenCache.DeserializeMsalV3(new byte[0]);
        }

        [TestMethod]
        public void TestIsFociMember()
        {
            // Arrange
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);
                AuthenticationRequestParameters requestParams = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    MsalTestConstants.Scope,
                    account: MsalTestConstants.User);

                // Act
                bool? result = cache.IsFociMemberAsync(requestParams, "1").Result;

                // Assert
                Assert.IsNull(result, "No app metadata, should return null which indicates <uknown>");

                ValidateIsFociMember(cache, requestParams,
                    metadataFamilyId: "1",
                    expectedResult: true, // checks for familyId "1"
                    errMessage: "Valid app metadata, should return true because family Id matches");


                ValidateIsFociMember(cache, requestParams,
                    metadataFamilyId: "2",
                    expectedResult: false, // checks for familyId "1"
                    errMessage: "Valid app metadata, should return false because family Id does not match");


                ValidateIsFociMember(cache, requestParams,
                    metadataFamilyId: null,
                    expectedResult: false, // checks for familyId "1"
                    errMessage: "Valid app metadata showing that the app is not member of any family");
            }
        }

        [TestMethod]
        public void TestIsFociMember_EnvAlias()
        {
            // Arrange
            using (var harness = new MockHttpAndServiceBundle())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                ITokenCacheInternal cache = new TokenCache(harness.ServiceBundle);
                AuthenticationRequestParameters requestParams = harness.CreateAuthenticationRequestParameters(
                    MsalTestConstants.AuthorityTestTenant,
                    MsalTestConstants.Scope,
                    account: MsalTestConstants.User);

                cache.Accessor.SaveAppMetadata(
                    new MsalAppMetadataCacheItem(
                        MsalTestConstants.ClientId,
                        MsalTestConstants.ProductionNotPrefEnvironmentAlias,
                        "1"));

                // Act
                bool? result = cache.IsFociMemberAsync(requestParams, "1").Result; //requst params uses ProductionPrefEnvAlias

                // Assert
                Assert.AreEqual(true, result.Value);
            }
        }

        private void ValidateIsFociMember(
            ITokenCacheInternal cache,
            AuthenticationRequestParameters requestParams,
            string metadataFamilyId,
            bool? expectedResult,
            string errMessage)
        {
            // Arrange
            var metadata = new MsalAppMetadataCacheItem(MsalTestConstants.ClientId, MsalTestConstants.ProductionPrefCacheEnvironment, metadataFamilyId);
            cache.Accessor.SaveAppMetadata(metadata);

            // Act
            var result = cache.IsFociMemberAsync(requestParams, "1").Result;

            // Assert
            Assert.AreEqual(expectedResult, result, errMessage);
            Assert.AreEqual(1, cache.Accessor.GetAllAppMetadata().Count());
        }

        /*
        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void DeserializeCacheItemWithNoVersion()
        {
            string noVersionCacheEntry = "{\"client_id\":\"client_id\",\"client_info\":\"eyJ1aWQiOiJteS1VSUQiLCJ1dGlkIjoibXktVVRJRCJ9\",\"access_token\":\"access-token\",\"authority\":\"https:\\\\/\\\\/login.microsoftonline.com\\\\/home\\\\/\",\"expires_on\":1494025355,\"id_token\":\"someheader.eyJhdWQiOiAiZTg1NGE0YTctNmMzNC00NDljLWIyMzctZmM3YTI4MDkzZDg0IiwiaXNzIjogImh0dHBzOi8vbG9naW4ubWljcm9zb2Z0b25saW5lLmNvbS82YzNkNTFkZC1mMGU1LTQ5NTktYjRlYS1hODBjNGUzNmZlNWUvdjIuMC8iLCJpYXQiOiAxNDU1ODMzODI4LCJuYmYiOiAxNDU1ODMzODI4LCJleHAiOiAxNDU1ODM3NzI4LCJpcGFkZHIiOiAiMTMxLjEwNy4xNTkuMTE3IiwibmFtZSI6ICJNYXJycnJyaW8gQm9zc3kiLCJvaWQiOiAidW5pcXVlX2lkIiwicHJlZmVycmVkX3VzZXJuYW1lIjogImRpc3BsYXlhYmxlQGlkLmNvbSIsInN1YiI6ICJLNF9TR0d4S3FXMVN4VUFtaGc2QzFGNlZQaUZ6Y3gtUWQ4MGVoSUVkRnVzIiwidGlkIjogIm15LWlkcCIsInZlciI6ICIyLjAifQ.somesignature\",\"scope\":\"r1\\\\/scope1 r1\\\\/scope2\",\"token_type\":\"Bearer\",\"user_assertion_hash\":null}";

            TokenCache cache = new TokenCache()
            {
                ClientId = TestConstants.ClientId
            };
            cache.AddAccessTokenCacheItem(JsonHelper.DeserializeFromJson<MsalAccessTokenCacheItem>(noVersionCacheEntry));
            ICollection<MsalAccessTokenCacheItem> items = cache.GetAllAccessTokensForClient(new RequestContext(new MsalLogger(Guid.NewGuid(), null)));
            Assert.AreEqual(1, items.Count);
            MsalAccessTokenCacheItem item = items.First();
            Assert.AreEqual(0, item.Version);
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void DeserializeCacheItemWithDifferentVersion()
        {
            string differentVersionEntry = "{\"client_id\":\"client_id\",\"client_info\":\"eyJ1aWQiOiJteS1VSUQiLCJ1dGlkIjoibXktVVRJRCJ9\",\"ver\":5,\"access_token\":\"access-token\",\"authority\":\"https:\\\\/\\\\/login.microsoftonline.com\\\\/home\\\\/\",\"expires_on\":1494025355,\"id_token\":\"someheader.eyJhdWQiOiAiZTg1NGE0YTctNmMzNC00NDljLWIyMzctZmM3YTI4MDkzZDg0IiwiaXNzIjogImh0dHBzOi8vbG9naW4ubWljcm9zb2Z0b25saW5lLmNvbS82YzNkNTFkZC1mMGU1LTQ5NTktYjRlYS1hODBjNGUzNmZlNWUvdjIuMC8iLCJpYXQiOiAxNDU1ODMzODI4LCJuYmYiOiAxNDU1ODMzODI4LCJleHAiOiAxNDU1ODM3NzI4LCJpcGFkZHIiOiAiMTMxLjEwNy4xNTkuMTE3IiwibmFtZSI6ICJNYXJycnJyaW8gQm9zc3kiLCJvaWQiOiAidW5pcXVlX2lkIiwicHJlZmVycmVkX3VzZXJuYW1lIjogImRpc3BsYXlhYmxlQGlkLmNvbSIsInN1YiI6ICJLNF9TR0d4S3FXMVN4VUFtaGc2QzFGNlZQaUZ6Y3gtUWQ4MGVoSUVkRnVzIiwidGlkIjogIm15LWlkcCIsInZlciI6ICIyLjAifQ.somesignature\",\"scope\":\"r1\\\\/scope1 r1\\\\/scope2\",\"token_type\":\"Bearer\",\"user_assertion_hash\":null}";

            TokenCache cache = new TokenCache()
            {
                ClientId = TestConstants.ClientId
            };
            cache.AddAccessTokenCacheItem(JsonHelper.DeserializeFromJson<MsalAccessTokenCacheItem>(differentVersionEntry));
            ICollection<MsalAccessTokenCacheItem> items = cache.GetAllAccessTokensForClient(new RequestContext(new MsalLogger(Guid.NewGuid(), null)));
            Assert.AreEqual(1, items.Count);
            MsalAccessTokenCacheItem item = items.First();
            Assert.AreEqual(5, item.Version);
        }
        */
    }
}
