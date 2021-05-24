﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Internal;

namespace Microsoft.Identity.Client
{
    internal sealed class ApplicationConfiguration : IApplicationConfiguration
    {
        // For telemetry, the ClientName of the application.
        public string ClientName { get; internal set; }

        // For telemetry, the ClientVersion of the application.
        public string ClientVersion { get; internal set; }

        public bool UseCorporateNetwork { get; internal set; }
        public string IosKeychainSecurityGroup { get; internal set; }

        public bool IsBrokerEnabled { get; internal set; }

        public IMatsConfig MatsConfig { get; internal set; }

        public IHttpManager HttpManager { get; internal set; }
        public AuthorityInfo AuthorityInfo { get; internal set; }
        public string ClientId { get; internal set; }
        public string TenantId { get; internal set; }
        public string RedirectUri { get; internal set; }
        public bool EnablePiiLogging { get; internal set; }
        public LogLevel LogLevel { get; internal set; } = LogLevel.Info;
        public bool IsDefaultPlatformLoggingEnabled { get; internal set; }
        public IMsalHttpClientFactory HttpClientFactory { get; internal set; }
        public bool IsExtendedTokenLifetimeEnabled { get; set; }
        public TelemetryCallback TelemetryCallback { get; internal set; }
        public LogCallback LoggingCallback { get; internal set; }
        public string Component { get; internal set; }
        public IDictionary<string, string> ExtraQueryParameters { get; internal set; } = new Dictionary<string, string>();

        internal ILegacyCachePersistence UserTokenLegacyCachePersistenceForTest { get; set; }
        internal ILegacyCachePersistence AppTokenLegacyCachePersistenceForTest { get; set; }

#if !ANDROID_BUILDTIME && !iOS_BUILDTIME && !WINDOWS_APP_BUILDTIME && !MAC_BUILDTIME // Hide confidential client on mobile platforms

        public ClientCredentialWrapper ClientCredential { get; internal set; }
        public string ClientSecret { get; internal set; }
        public X509Certificate2 ClientCredentialCertificate { get; internal set; }
#endif
        /// <summary>
        /// Should _not_ go in the interface, only for builder usage while determining authorities with ApplicationOptions
        /// </summary>
        internal AadAuthorityAudience AadAuthorityAudience { get; set; }

        /// <summary>
        /// Should _not_ go in the interface, only for builder usage while determining authorities with ApplicationOptions
        /// </summary>
        internal AzureCloudInstance AzureCloudInstance { get; set; }

        /// <summary>
        /// Should _not_ go in the interface, only for builder usage while determining authorities with ApplicationOptions
        /// </summary>
        internal string Instance { get; set; }
    }
}
