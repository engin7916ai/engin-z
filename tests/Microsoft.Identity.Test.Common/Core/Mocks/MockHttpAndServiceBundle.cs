﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.TelemetryCore.Internal;
using Microsoft.Identity.Client.TelemetryCore.Internal.Events;
using Microsoft.Identity.Test.Unit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Test.Common.Core.Mocks
{
    internal class MockHttpAndServiceBundle : IDisposable
    {
        public MockHttpAndServiceBundle(
            TelemetryCallback telemetryCallback = null,
            LogCallback logCallback = null,
            bool isExtendedTokenLifetimeEnabled = false,
            string authority = ClientApplicationBase.DefaultAuthority,
            TestContext testContext = null)
        {
            HttpManager = new MockHttpManager(testContext);
            ServiceBundle = TestCommon.CreateServiceBundleWithCustomHttpManager(
                HttpManager,
                telemetryCallback: telemetryCallback,
                logCallback: logCallback,
                isExtendedTokenLifetimeEnabled: isExtendedTokenLifetimeEnabled,
                authority: authority);
        }

        public IServiceBundle ServiceBundle { get; }
        public MockHttpManager HttpManager { get; }

        public void Dispose()
        {
            HttpManager.Dispose();
        }

        public AuthenticationRequestParameters CreateAuthenticationRequestParameters(
            string authority,            
            IEnumerable<string> scopes = null,
            ITokenCacheInternal tokenCache = null,
            IAccount account = null,
            IDictionary<string, string> extraQueryParameters = null,
            string claims = null,
            ApiEvent.ApiIds apiId = ApiEvent.ApiIds.None, 
            bool validateAuthority = false)
        {            
            scopes = scopes ?? TestConstants.s_scope;
            tokenCache = tokenCache ?? new TokenCache(ServiceBundle, false);

            var commonParameters = new AcquireTokenCommonParameters
            {
                Scopes = scopes ?? TestConstants.s_scope,
                ExtraQueryParameters = extraQueryParameters ?? new Dictionary<string, string>(),
                Claims = claims,
                ApiId = apiId
            };

            AuthenticationRequestParameters authenticationRequestParameters = new AuthenticationRequestParameters(
                ServiceBundle,
                tokenCache,
                commonParameters,
                new RequestContext(ServiceBundle, Guid.NewGuid()))
            {
                Account = account,
                Authority = Authority.CreateAuthority(authority, validateAuthority)
            };

            authenticationRequestParameters.RequestContext.ApiEvent = new ApiEvent(
                authenticationRequestParameters.RequestContext.Logger,
                ServiceBundle.PlatformProxy.CryptographyManager,
                Guid.NewGuid().AsMatsCorrelationId());

            return authenticationRequestParameters;
        }
    }
}
