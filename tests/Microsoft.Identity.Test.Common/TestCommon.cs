﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.PlatformsCommon.Factories;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.Identity.Test.Unit;

namespace Microsoft.Identity.Test.Common
{
    internal static class TestCommon
    {
        public static void ResetInternalStaticCaches()
        {
            // This initializes the classes so that the statics inside them are fully initialized, and clears any cached content in them.
            new AadInstanceDiscovery(null, null, null, true);
            new AuthorityEndpointResolutionManager(null, true);
        }

        public static IServiceBundle CreateServiceBundleWithCustomHttpManager(
            IHttpManager httpManager,
            TelemetryCallback telemetryCallback = null,
            LogCallback logCallback = null,
            string authority = ClientApplicationBase.DefaultAuthority,
            bool isExtendedTokenLifetimeEnabled = false,
            bool enablePiiLogging = false,
            string clientId = MsalTestConstants.ClientId)
        {
            var appConfig = new ApplicationConfiguration()
            {
                ClientId = clientId,
                HttpManager = httpManager,
                RedirectUri = PlatformProxyFactory.CreatePlatformProxy(null).GetDefaultRedirectUri(clientId),
                TelemetryCallback = telemetryCallback,
                LoggingCallback = logCallback,
                LogLevel = LogLevel.Verbose,
                EnablePiiLogging = enablePiiLogging,
                IsExtendedTokenLifetimeEnabled = isExtendedTokenLifetimeEnabled,
                AuthorityInfo = AuthorityInfo.FromAuthorityUri(authority, false)
            };
            return ServiceBundle.Create(appConfig);
        }

        public static IServiceBundle CreateDefaultServiceBundle()
        {
            return CreateServiceBundleWithCustomHttpManager(null);
        }

        internal static void MockInstanceDiscoveryAndOpenIdRequest(MockHttpManager mockHttpManager)
        {
            mockHttpManager.AddInstanceDiscoveryMockHandler();
            mockHttpManager.AddMockHandlerForTenantEndpointDiscovery(MsalTestConstants.AuthorityHomeTenant);
        }
    }
}
