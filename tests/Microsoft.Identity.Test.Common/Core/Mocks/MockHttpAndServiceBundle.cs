﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Test.Unit;

namespace Microsoft.Identity.Test.Common.Core.Mocks
{
    internal class MockHttpAndServiceBundle : IDisposable
    {
        public MockHttpAndServiceBundle(
            TelemetryCallback telemetryCallback = null,
            LogCallback logCallback = null,
            bool isExtendedTokenLifetimeEnabled = false)
        {
            HttpManager = new MockHttpManager();
            ServiceBundle = TestCommon.CreateServiceBundleWithCustomHttpManager(
                HttpManager,
                telemetryCallback: telemetryCallback,
                logCallback: logCallback,
                isExtendedTokenLifetimeEnabled: isExtendedTokenLifetimeEnabled);
        }

        public IServiceBundle ServiceBundle { get; }
        public MockHttpManager HttpManager { get; }

        public void Dispose()
        {
            HttpManager.Dispose();
        }

        public AuthenticationRequestParameters CreateAuthenticationRequestParameters(
            string authority,
            SortedSet<string> scopes,
            ITokenCacheInternal tokenCache = null,
            IAccount account = null,
            IDictionary<string, string> extraQueryParameters = null,
            string claims = null)
        {
            var commonParameters = new AcquireTokenCommonParameters
            {
                Scopes = scopes ?? MsalTestConstants.Scope,
                ExtraQueryParameters = extraQueryParameters ?? new Dictionary<string, string>(),
                Claims = claims
            };

            return new AuthenticationRequestParameters(
                ServiceBundle,
                tokenCache,
                commonParameters,
                RequestContext.CreateForTest(ServiceBundle))
            {
                Account = account,
                Authority = Authority.CreateAuthority(ServiceBundle, authority)
            };
        }
    }
}
