﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Instance.Discovery;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.PlatformsCommon.Factories;
using Microsoft.Identity.Client.PlatformsCommon.Interfaces;
using Microsoft.Identity.Client.TelemetryCore;
using Microsoft.Identity.Client.WsTrust;

namespace Microsoft.Identity.Client.Core
{
    internal class ServiceBundle : IServiceBundle
    {
        internal ServiceBundle(
            ApplicationConfiguration config,
            bool shouldClearCaches = false)
        {
            Config = config;

            DefaultLogger = new MsalLogger(
                Guid.Empty,
                config.ClientName,
                config.ClientVersion,
                config.LogLevel,
                config.EnablePiiLogging,
                config.IsDefaultPlatformLoggingEnabled,
                config.LoggingCallback);

            PlatformProxy = config.PlatformProxy ?? PlatformProxyFactory.CreatePlatformProxy(DefaultLogger);
            HttpManager = config.HttpManager ?? new HttpManager(config.HttpClientFactory);

            if (config.TelemetryConfig != null)
            {
                // This can return null if the device isn't sampled in.  There's no need for processing MATS events if we're not going to send them.
                Mats = TelemetryClient.CreateMats(config, PlatformProxy, config.TelemetryConfig);
                TelemetryManager = Mats?.TelemetryManager ?? new TelemetryManager(config, PlatformProxy, config.TelemetryCallback);
            }
            else
            {
                TelemetryManager = new TelemetryManager(config, PlatformProxy, config.TelemetryCallback);
            }

            InstanceDiscoveryManager = new InstanceDiscoveryManager(HttpManager, TelemetryManager, shouldClearCaches, config.CustomInstanceDiscoveryMetadata);
            WsTrustWebRequestManager = new WsTrustWebRequestManager(HttpManager);
            AuthorityEndpointResolutionManager = new AuthorityEndpointResolutionManager(this, shouldClearCaches);
        }

        public ICoreLogger DefaultLogger { get; }

        /// <inheritdoc />
        public IHttpManager HttpManager { get; }

        /// <inheritdoc />
        public ITelemetryManager TelemetryManager { get; }

        public IInstanceDiscoveryManager InstanceDiscoveryManager { get; }

        /// <inheritdoc />
        public IWsTrustWebRequestManager WsTrustWebRequestManager { get; }

        /// <inheritdoc />
        public IAuthorityEndpointResolutionManager AuthorityEndpointResolutionManager { get; }

        /// <inheritdoc />
        public IPlatformProxy PlatformProxy { get; }

        /// <inheritdoc />
        public IApplicationConfiguration Config { get; }

        /// <inheritdoc />
        public ITelemetryClient Mats { get; }

        public static ServiceBundle Create(ApplicationConfiguration config)
        {
            return new ServiceBundle(config);
        }
    }
}
