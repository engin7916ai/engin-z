﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Identity.Client
{
    /// <summary>
    /// Configuration properties used to build a public or confidential client application
    /// </summary>
    public interface IAppConfig 
    {
        /// <summary>
        /// Client ID (also known as App ID) of the application as registered in the
        /// application registration portal (https://aka.ms/msal-net-register-app)
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// Flag telling if logging of Personally Identifiable Information (PII) is enabled/disabled for
        /// the application. See https://aka.ms/msal-net-logging
        /// </summary>
        /// <seealso cref="IsDefaultPlatformLoggingEnabled"/>
        bool EnablePiiLogging { get; }

        /// <summary>
        /// <see cref="IMsalHttpClientFactory"/> used to get HttpClient instances to communicate
        /// with the identity provider.
        /// </summary>
        IMsalHttpClientFactory HttpClientFactory { get; }

        /// <summary>
        /// Level of logging requested for the app.
        /// See https://aka.ms/msal-net-logging
        /// </summary>
        LogLevel LogLevel { get; }

        /// <summary>
        /// Flag telling if logging to platform defaults is enabled/disabled for the app.
        /// In Desktop/UWP, Event Tracing is used. In iOS, NSLog is used.
        /// In Android, logcat is used. See https://aka.ms/msal-net-logging
        /// </summary>
        bool IsDefaultPlatformLoggingEnabled { get; }

        /// <summary>
        /// Redirect URI for the application. See <see cref="ApplicationOptions.RedirectUri"/>
        /// </summary>
        string RedirectUri { get; }

        /// <summary>
        /// Audience for the application. See <see cref="ApplicationOptions.TenantId"/>
        /// </summary>
        string TenantId { get; }

        /// <summary>
        /// Callback used for logging. It was set with <see cref="AbstractApplicationBuilder{T}.WithLogging(LogCallback, LogLevel?, bool?, bool?)"/>
        /// See https://aka.ms/msal-net-logging
        /// </summary>
        LogCallback LoggingCallback { get; }

        /// <summary>
        /// Extra query parameters that will be applied to every acquire token operation.
        /// See <see cref="AbstractApplicationBuilder{T}.WithExtraQueryParameters(IDictionary{string, string})"/>
        /// </summary>
        IDictionary<string, string> ExtraQueryParameters { get; }

        /// <summary>
        /// Indicates whether or not the current application object is configured to use brokered authentication.
        /// </summary>
        bool IsBrokerEnabled { get; }

        /// <summary>
        /// The name of the calling application for telemetry purposes.
        /// </summary>
        string ClientName { get; }

        /// <summary>
        /// The version of the calling application for telemetry purposes.
        /// </summary>
        string ClientVersion { get; }

        /// <summary>
        /// </summary>
        ITelemetryConfig TelemetryConfig { get; }

        /// <summary>
        /// Allows usage of features that are experimental and would otherwise throw a specific exception. 
        /// Use of experimental features in production is not recommended and are subject to be removed between builds. 
        /// For details see https://aka.ms/msal-net-experimental-features
        /// </summary>
        bool ExperimentalFeaturesEnabled { get; }

        /// <summary>
        /// Microsoft Identity specific OIDC extension that allows resource challenges to be resolved without interaction. 
        /// Allows configuration of one or more client capabilities, e.g. "llt"
        /// </summary>
        /// <remarks>
        /// MSAL will transform these into a "access_token" claims request. See https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter for
        /// details on claim requests.
        /// For more details see https://aka.ms/msal-net-claims-request
        /// </remarks>
        IEnumerable<string> ClientCapabilities { get; }


        /// <summary>
        /// A secret that is configured both in your app and in the app registration. A secret or a certificate is required for Confidential Client apps.
        /// Not recommended for production - please use <see cref="ClientCredentialCertificate"/>
        /// </summary>
        string ClientSecret { get; }

        /// <summary>
        /// For Confidential Client applications only, 
        /// </summary>
        X509Certificate2 ClientCredentialCertificate { get; }

        /// <summary>
        /// </summary>
        Func<object> ParentActivityOrWindowFunc { get; }


        /// <summary>
        /// This setting is only relevant on Windows Universal Platform. 
        /// Allows the WAB component which handles user interaction when the broker is not used, to use the corporate network.         
        /// Also affects Integrated Windows Authentication. 
        /// For more details see: https://docs.microsoft.com/en-us/azure/active-directory/develop/msal-net-uwp-consideration
        /// </summary>
        bool UseCorporateNetwork { get; }

        /// <summary>
        /// This setting is only relevant for Xamarin.iOS applications. 
        /// Configures the keychain security group used to store tokens in the keychain. 
        /// For details see: https://docs.microsoft.com/en-us/azure/active-directory/develop/msal-net-xamarin-ios-considerations
        /// </summary>
        string IosKeychainSecurityGroup { get; }
    }
}
