﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.Internal.Broker;
using Microsoft.Identity.Client.Platforms.Features.WamBroker;
using Microsoft.Identity.Client.PlatformsCommon.Interfaces;
using Microsoft.Identity.Client.PlatformsCommon.Shared;
using Microsoft.Identity.Client.TelemetryCore.Internal;
using Microsoft.Identity.Client.UI;
using Windows.ApplicationModel;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Security.Authentication.Web;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;
using Windows.System;

namespace Microsoft.Identity.Client.Platforms.uap
{
    /// <summary>
    /// Platform / OS specific logic. No library (ADAL / MSAL) specific code should go in here.
    /// </summary>
    internal class UapPlatformProxy : AbstractPlatformProxy
    {
        public UapPlatformProxy(ICoreLogger logger)
            : base(logger)
        {
        }

        /// <summary>
        /// Get the user logged in to Windows or throws
        /// </summary>
        /// <remarks>
        /// Win10 allows several identities to be logged in at once;
        /// select the first principal name that can be used
        /// </remarks>
        /// <returns>The username or throws</returns>
        public override async Task<string> GetUserPrincipalNameAsync()
        {
            IReadOnlyList<User> users = await User.FindAllAsync();
            if (users == null || !users.Any())
            {
                throw new MsalClientException(
                    MsalError.CannotAccessUserInformationOrUserNotDomainJoined,
                    MsalErrorMessage.UapCannotFindDomainUser);
            }

            var getUserDetailTasks = users.Select(async u =>
            {
                object domainObj = await u.GetPropertyAsync(KnownUserProperties.DomainName);
                string domainString = domainObj?.ToString();

                object principalObject = await u.GetPropertyAsync(KnownUserProperties.PrincipalName);
                string principalNameString = principalObject?.ToString();

                return new { Domain = domainString, PrincipalName = principalNameString };
            }).ToList();

            var userDetails = await Task.WhenAll(getUserDetailTasks).ConfigureAwait(false);

            // try to get a user that has both domain name and upn
            var userDetailWithDomainAndPn = userDetails.FirstOrDefault(
                d => !string.IsNullOrWhiteSpace(d.Domain) &&
                !string.IsNullOrWhiteSpace(d.PrincipalName));

            if (userDetailWithDomainAndPn != null)
            {
                return userDetailWithDomainAndPn.PrincipalName;
            }

            // try to get a user that at least has upn
            var userDetailWithPn = userDetails.FirstOrDefault(
              d => !string.IsNullOrWhiteSpace(d.PrincipalName));

            if (userDetailWithPn != null)
            {
                return userDetailWithPn.PrincipalName;
            }

            // user has domain name, but no upn -> missing Enterprise Auth capability
            if (userDetails.Any(d => !string.IsNullOrWhiteSpace(d.Domain)))
            {
                throw new MsalClientException(
                   MsalError.CannotAccessUserInformationOrUserNotDomainJoined,
                   MsalErrorMessage.UapCannotFindUpn);
            }

            // no domain, no upn -> missing User Info capability
            throw new MsalClientException(
                MsalError.CannotAccessUserInformationOrUserNotDomainJoined,
                MsalErrorMessage.UapCannotFindDomainUser);
        }

        public override async Task<bool> IsUserLocalAsync(RequestContext requestContext)
        {
            IReadOnlyList<User> users = await User.FindAllAsync();
            return users.Any(u => u.Type == UserType.LocalUser || u.Type == UserType.LocalGuest);
        }

        public override bool IsDomainJoined()
        {
            return NetworkInformation.GetHostNames().Any(entry => entry.Type == HostNameType.DomainName);
        }

        public override string GetEnvironmentVariable(string variable)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            return localSettings.Values.ContainsKey(variable) ? localSettings.Values[variable].ToString() : null;
        }

        protected override string InternalGetProcessorArchitecture()
        {
            return WindowsNativeMethods.GetProcessorArchitecture();
        }

        protected override string InternalGetOperatingSystem()
        {
            // In WinRT, there is no way to reliably get OS version. All can be done reliably is to check
            // for existence of specific features which does not help in this case, so we do not emit OS in WinRT.
            return null;
        }

        protected override string InternalGetDeviceModel()
        {
            var deviceInformation = new Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation();
            return deviceInformation.SystemProductName;
        }
        public override bool BrokerSupportsWamAccounts => true;

        public override bool CanBrokerSupportSilentAuth()
        {
            return true;
        }

        public override IBroker CreateBroker(CoreUIParent uiParent)
        {
            return new WamBroker(uiParent, Logger);
        }

        /// <inheritdoc />
        public override string GetDefaultRedirectUri(string clientId, bool useRecommendedRedirectUri = false)
        {
            if (useRecommendedRedirectUri)
            {
                return WebAuthenticationBroker.GetCurrentApplicationCallbackUri().ToString();
            }
            return Constants.DefaultRedirectUri;
        }

        protected override string InternalGetProductName()
        {
            return "MSAL.UAP";
        }

        /// <summary>
        /// Considered PII, ensure that it is hashed.
        /// </summary>
        /// <returns>Name of the calling application</returns>
        protected override string InternalGetCallingApplicationName()
        {
            return Package.Current?.DisplayName?.ToString();
        }

        /// <summary>
        /// Considered PII, ensure that it is hashed.
        /// </summary>
        /// <returns>Version of the calling application</returns>
        protected override string InternalGetCallingApplicationVersion()
        {
            return Package.Current?.Id?.Version.ToString();
        }

        /// <summary>
        /// Considered PII. Please ensure that it is hashed.
        /// </summary>
        /// <returns>Device identifier</returns>
        protected override string InternalGetDeviceId()
        {
            return new EasClientDeviceInformation()?.Id.ToString();
        }

        public override ILegacyCachePersistence CreateLegacyCachePersistence() => new UapLegacyCachePersistence(Logger, CryptographyManager);

        public override ITokenCacheAccessor CreateTokenCacheAccessor() => new InMemoryTokenCacheAccessor(Logger);

        public override ITokenCacheBlobStorage CreateTokenCacheBlobStorage() => new UapTokenCacheBlobStorage(CryptographyManager, Logger);

        protected override IWebUIFactory CreateWebUiFactory() => new WebUIFactory();
        protected override ICryptographyManager InternalGetCryptographyManager() => new UapCryptographyManager();
        protected override IPlatformLogger InternalGetPlatformLogger() => new EventSourcePlatformLogger();

        public override string GetDeviceNetworkState()
        {
            // TODO(mats):
            return string.Empty;
        }

        public override string GetDevicePlatformTelemetryId()
        {
            // TODO(mats):
            return string.Empty;
        }

        public override string GetMatsOsPlatform()
        {
            return MatsConverter.AsString(OsPlatform.Win32);
        }

        public override int GetMatsOsPlatformCode()
        {
            return MatsConverter.AsInt(OsPlatform.Win32);
        }
        protected override IFeatureFlags CreateFeatureFlags() => new UapFeatureFlags();

        public override bool IsSystemWebViewAvailable => false;

        public override IDeviceAuthManager CreateDeviceAuthManager()
        {
            return new UapDeviceAuthManager();
        }
    }
}
