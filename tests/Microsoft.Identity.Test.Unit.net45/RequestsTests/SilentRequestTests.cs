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
using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Identity.Client.OAuth2;

namespace Microsoft.Identity.Test.Unit.RequestsTests
{
    [TestClass]
    public class SilentRequestTests
    {
        private TokenCacheHelper _tokenCacheHelper;

        [TestInitialize]
        public void TestInitialize()
        {
            TestCommon.ResetInternalStaticCaches();
            _tokenCacheHelper = new TokenCacheHelper();
        }

        [TestMethod]
        public void ConstructorTests()
        {
            using (var harness = new MockHttpTestHarness(MsalTestConstants.AuthorityHomeTenant))
            {
                var parameters = harness.CreateRequestParams(harness.Cache, null);
                var silentParameters = new AcquireTokenSilentParameters();
                var request = new SilentRequest(harness.ServiceBundle, parameters, silentParameters);
                Assert.IsNotNull(request);

                parameters.Account = new Account(MsalTestConstants.UserIdentifier, MsalTestConstants.DisplayableId, null);
                request = new SilentRequest(harness.ServiceBundle, parameters, silentParameters);
                Assert.IsNotNull(request);

                request = new SilentRequest(harness.ServiceBundle, parameters, silentParameters);
                Assert.IsNotNull(request);
            }
        }

        [TestMethod]
        public void ExpiredTokenRefreshFlowTest()
        {
            IDictionary<string, string> extraQueryParamsAndClaims =
               MsalTestConstants.ExtraQueryParams.ToDictionary(e => e.Key, e => e.Value);
            extraQueryParamsAndClaims.Add(OAuth2Parameter.Claims, MsalTestConstants.Claims);

            using (var harness = new MockHttpTestHarness(MsalTestConstants.AuthorityHomeTenant))
            {
                _tokenCacheHelper.PopulateCache(harness.Cache.Accessor);
                var parameters = harness.CreateRequestParams(
                    harness.Cache,
                    null,
                    MsalTestConstants.ExtraQueryParams,
                    MsalTestConstants.Claims,
                    authorityOverride: AuthorityInfo.FromAuthorityUri(MsalTestConstants.AuthorityHomeTenant, false));

                var silentParameters = new AcquireTokenSilentParameters()
                {
                    Account = new Account(MsalTestConstants.HomeAccountId, MsalTestConstants.DisplayableId, MsalTestConstants.ProductionPrefCacheEnvironment),
                };

                // set access tokens as expired
                foreach (var accessItem in harness.Cache.GetAllAccessTokens(true))
                {
                    accessItem.ExpiresOnUnixTimestamp =
                        ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);

                    harness.Cache.AddAccessTokenCacheItem(accessItem);
                }

                TestCommon.MockInstanceDiscoveryAndOpenIdRequest(harness.HttpManager);

                harness.HttpManager.AddMockHandler(
                    new MockHttpMessageHandler()
                    {
                        ExpectedMethod = HttpMethod.Post,
                        ResponseMessage = MockHelpers.CreateSuccessTokenResponseMessage(),
                        ExpectedQueryParams = extraQueryParamsAndClaims
                    });

                var request = new SilentRequest(harness.ServiceBundle, parameters, silentParameters);

                Task<AuthenticationResult> task = request.RunAsync(CancellationToken.None);
                var result = task.Result;
                Assert.IsNotNull(result);
                Assert.AreEqual("some-access-token", result.AccessToken);
                Assert.AreEqual(MsalTestConstants.Scope.AsSingleString(), result.Scopes.AsSingleString());
            }
        }

        [TestMethod]
        public void SilentRefreshFailedNullCacheTest()
        {
            using (var harness = new MockHttpTestHarness(MsalTestConstants.AuthorityHomeTenant))
            {
                var parameters = harness.CreateRequestParams(
                    null,
                    ScopeHelper.CreateSortedSetFromEnumerable(
                        new[]
                        {
                            "some-scope1",
                            "some-scope2"
                        }),
                    authorityOverride: AuthorityInfo.FromAuthorityUri(MsalTestConstants.AuthorityHomeTenant, false));

                var silentParameters = new AcquireTokenSilentParameters()
                {
                    Account = new Account(MsalTestConstants.HomeAccountId, MsalTestConstants.DisplayableId, MsalTestConstants.ProductionPrefCacheEnvironment),
                };

                try
                {
                    var request = new SilentRequest(harness.ServiceBundle, parameters, silentParameters);
                    Task<AuthenticationResult> task = request.RunAsync(CancellationToken.None);
                    var authenticationResult = task.Result;
                    Assert.Fail("MsalUiRequiredException should be thrown here");
                }
                catch (AggregateException ae)
                {
                    var exc = ae.InnerException as MsalUiRequiredException;
                    Assert.IsNotNull(exc);
                    Assert.AreEqual(MsalError.TokenCacheNullError, exc.ErrorCode);
                }
            }
        }

        [TestMethod]
        public void SilentRefreshFailedNoCacheItemFoundTest()
        {
            using (var harness = new MockHttpTestHarness(MsalTestConstants.AuthorityHomeTenant))
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();

                var parameters = harness.CreateRequestParams(
                    harness.Cache,
                    ScopeHelper.CreateSortedSetFromEnumerable(
                        new[]
                        {
                            "some-scope1",
                            "some-scope2"
                        }),
                    authorityOverride: AuthorityInfo.FromAuthorityUri(MsalTestConstants.AuthorityHomeTenant, false));

                var silentParameters = new AcquireTokenSilentParameters()
                {
                    Account = new Account(MsalTestConstants.HomeAccountId, MsalTestConstants.DisplayableId, MsalTestConstants.ProductionPrefCacheEnvironment),
                };

                try
                {
                    var request = new SilentRequest(harness.ServiceBundle, parameters, silentParameters);
                    Task<AuthenticationResult> task = request.RunAsync(CancellationToken.None);
                    var authenticationResult = task.Result;
                    Assert.Fail("MsalUiRequiredException should be thrown here");
                }
                catch (AggregateException ae)
                {
                    var exc = ae.InnerException as MsalUiRequiredException;
                    Assert.IsNotNull(exc, "Actual exception type is " + ae.InnerException.GetType());
                    Assert.AreEqual(MsalError.NoTokensFoundError, exc.ErrorCode);
                }
            }
        }

        private class MockHttpTestHarness : IDisposable
        {
            private readonly MockHttpAndServiceBundle _mockHttpAndServiceBundle;

            public MockHttpTestHarness(string authorityUri)
            {
                _mockHttpAndServiceBundle = new MockHttpAndServiceBundle();
                Authority = Authority.CreateAuthority(ServiceBundle, authorityUri);
                Cache = new TokenCache(ServiceBundle);
            }

            public IServiceBundle ServiceBundle => _mockHttpAndServiceBundle.ServiceBundle;
            public MockHttpManager HttpManager => _mockHttpAndServiceBundle.HttpManager;
            public Authority Authority { get; }
            public ITokenCacheInternal Cache { get; }

            /// <inheritdoc />
            public void Dispose()
            {
                _mockHttpAndServiceBundle.Dispose();
            }

            public AuthenticationRequestParameters CreateRequestParams(
                ITokenCacheInternal cache,
                SortedSet<string> scopes,
                IDictionary<string, string> extraQueryParams = null,
                string claims = null,
                AuthorityInfo authorityOverride = null)
            {
                var commonParameters = new AcquireTokenCommonParameters
                {
                    Scopes = scopes ?? MsalTestConstants.Scope,
                    ExtraQueryParameters = extraQueryParams,
                    Claims = claims,
                    AuthorityOverride = authorityOverride
                };

                var parameters = new AuthenticationRequestParameters(
                    ServiceBundle,
                    cache,
                    commonParameters,
                    RequestContext.CreateForTest(ServiceBundle))
                {
                    Account = new Account(MsalTestConstants.UserIdentifier, MsalTestConstants.DisplayableId, null),
                };
                return parameters;
            }
        }
    }
}
