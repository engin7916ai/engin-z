﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Identity.Client.Internal;

namespace Microsoft.Identity.Client
{
    /// <summary>
    /// </summary>
#if !SUPPORTS_CONFIDENTIAL_CLIENT
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]  // hide confidential client on mobile
#endif
    public class ConfidentialClientApplicationBuilder : AbstractApplicationBuilder<ConfidentialClientApplicationBuilder>
    {
        /// <inheritdoc />
        internal ConfidentialClientApplicationBuilder(ApplicationConfiguration configuration)
            : base(configuration)
        {
            ConfidentialClientApplication.GuardMobileFrameworks();
        }

        /// <summary>
        /// Constructor of a ConfidentialClientApplicationBuilder from application configuration options.
        /// See https://aka.ms/msal-net-application-configuration
        /// </summary>
        /// <param name="options">Confidential client applications configuration options</param>
        /// <returns>A <see cref="ConfidentialClientApplicationBuilder"/> from which to set more
        /// parameters, and to create a confidential client application instance</returns>
#if !SUPPORTS_CONFIDENTIAL_CLIENT
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]  // hide confidential client on mobile
#endif
        public static ConfidentialClientApplicationBuilder CreateWithApplicationOptions(
            ConfidentialClientApplicationOptions options)
        {
            ConfidentialClientApplication.GuardMobileFrameworks();

            var config = new ApplicationConfiguration();
            var builder = new ConfidentialClientApplicationBuilder(config).WithOptions(options);

            if (!string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                builder = builder.WithClientSecret(options.ClientSecret);
            }

            if (!string.IsNullOrWhiteSpace(options.AzureRegion))
            {
                builder = builder.WithAzureRegion(options.AzureRegion);
            }

            return builder;
        }

        /// <summary>
        /// Creates a ConfidentialClientApplicationBuilder from a clientID.
        /// See https://aka.ms/msal-net-application-configuration
        /// </summary>
        /// <param name="clientId">Client ID (also known as App ID) of the application as registered in the
        /// application registration portal (https://aka.ms/msal-net-register-app)/.</param>
        /// <returns>A <see cref="ConfidentialClientApplicationBuilder"/> from which to set more
        /// parameters, and to create a confidential client application instance</returns>
#if !SUPPORTS_CONFIDENTIAL_CLIENT
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]  // hide confidential client on mobile
#endif
        public static ConfidentialClientApplicationBuilder Create(string clientId)
        {
            ConfidentialClientApplication.GuardMobileFrameworks();

            var config = new ApplicationConfiguration();
            return new ConfidentialClientApplicationBuilder(config).WithClientId(clientId);
        }

        /// <summary>
        /// Sets the certificate associated with the application
        /// </summary>
        /// <param name="certificate">The X509 certificate used as credentials to prove the identity of the application to Azure AD.</param>
        /// <remarks>You should use certificates with a private key size of at least 2048 bytes. Future versions of this library might reject certificates with smaller keys. </remarks>
        public ConfidentialClientApplicationBuilder WithCertificate(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (!certificate.HasPrivateKey)
            {
                throw new MsalClientException(MsalError.CertWithoutPrivateKey, MsalErrorMessage.CertMustHavePrivateKey(nameof(certificate)));
            }

            Config.ClientCredentialCertificate = certificate;
            Config.ConfidentialClientCredentialCount++;
            return this;
        }

        /// <summary>
        /// Sets the certificate associated with the application along with the specific claims to sign.
        /// By default, this will merge the <paramref name="claimsToSign"/> with the default required set of claims needed for authentication.
        /// If <paramref name="mergeWithDefaultClaims"/> is set to false, you will need to provide the required default claims. See https://aka.ms/msal-net-client-assertion
        /// </summary>
        /// <param name="certificate">The X509 certificate used as credentials to prove the identity of the application to Azure AD.</param>
        /// <param name="claimsToSign">The claims to be signed by the provided certificate.</param>
        /// <param name="mergeWithDefaultClaims">Determines whether or not to merge <paramref name="claimsToSign"/> with the default claims required for authentication.</param>
        /// <remarks>You should use certificates with a private key size of at least 2048 bytes. Future versions of this library might reject certificates with smaller keys. </remarks>
        public ConfidentialClientApplicationBuilder WithClientClaims(X509Certificate2 certificate, IDictionary<string, string> claimsToSign, bool mergeWithDefaultClaims = true)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (claimsToSign == null || !claimsToSign.Any())
            {
                throw new ArgumentNullException(nameof(claimsToSign));
            }

            Config.ClientCredentialCertificate = certificate;
            Config.ClaimsToSign = claimsToSign;
            Config.MergeWithDefaultClaims = mergeWithDefaultClaims;
            Config.ConfidentialClientCredentialCount++;
            return this;
        }

        /// <summary>
        /// Sets the application secret
        /// </summary>
        /// <param name="clientSecret">Secret string previously shared with AAD at application registration to prove the identity
        /// of the application (the client) requesting the tokens</param>
        /// <returns></returns>
        public ConfidentialClientApplicationBuilder WithClientSecret(string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentNullException(nameof(clientSecret));
            }

            Config.ClientSecret = clientSecret;
            Config.ConfidentialClientCredentialCount++;
            return this;
        }

        /// <summary>
        /// Sets the application client assertion. See https://aka.ms/msal-net-client-assertion.
        /// This will create an assertion that will be held within the client application's memory for the duration of the client.
        /// You can use <see cref="WithClientAssertion(Func{string})"/> to set a delegate that will be executed for each authentication request. 
        /// This will allow you to update the client asserion used by the client application once the assertion expires.
        /// </summary>
        /// <param name="signedClientAssertion">The client assertion used to prove the identity of the application to Azure AD. This is a Base-64 encoded JWT.</param>
        /// <returns></returns>
        public ConfidentialClientApplicationBuilder WithClientAssertion(string signedClientAssertion)
        {
            if (string.IsNullOrWhiteSpace(signedClientAssertion))
            {
                throw new ArgumentNullException(nameof(signedClientAssertion));
            }

            Config.SignedClientAssertion = signedClientAssertion;
            Config.ConfidentialClientCredentialCount++;
            return this;
        }

        /// <summary>
        /// Configures a delegate that creates a client assertion. See https://aka.ms/msal-net-client-assertion
        /// </summary>
        /// <param name="clientAssertionDelegate">delegate computing the client assertion used to prove the identity of the application to Azure AD.
        /// This is a delegate that computes a Base-64 encoded JWT for each authentication call.</param>
        /// <returns>The ConfidentialClientApplicationBuilder to chain more .With methods</returns>
        /// <remarks> Callers can use this mechanism to cache their assertions </remarks>
        public ConfidentialClientApplicationBuilder WithClientAssertion(Func<string> clientAssertionDelegate)
        {
            if (clientAssertionDelegate == null)
            {
                throw new ArgumentNullException(nameof(clientAssertionDelegate));
            }

            Config.SignedClientAssertionDelegate = clientAssertionDelegate;
            Config.ConfidentialClientCredentialCount++;
            return this;
        }


        /// <summary>
        /// Instructs MSAL.NET to use an Azure regional token service.
        /// </summary>
        /// <param name="azureRegion">Either the string with the region (preferred) or        
        /// use <see cref="ConfidentialClientApplication.AttemptRegionDiscovery"/> and MSAL.NET will attempt to auto-detect the region.                
        /// </param>
        /// <remarks>
        /// Region names as per https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.management.resourcemanager.fluent.core.region?view=azure-dotnet.
        /// Not all auth flows can use the regional token service. 
        /// Service To Service (client credential flow) tokens can be obtained from the regional service.
        /// Requires configuration at the tenant level.
        /// Auto-detection works on a limited number of Azure artifacts (VMs, Azure functions). 
        /// If auto-detection fails, the non-regional endpoint will be used.
        /// If an invalid region name is provided, the non-regional endpoint MIGHT be used or the token request MIGHT fail.
        /// See https://aka.ms/msal-net-region-discovery for more details.        
        /// </remarks>
        /// <returns>The builder to chain the .With methods</returns>
        public ConfidentialClientApplicationBuilder WithAzureRegion(string azureRegion = ConfidentialClientApplication.AttemptRegionDiscovery)
        {
            if (string.IsNullOrEmpty(azureRegion))
            {
                throw new ArgumentNullException(nameof(azureRegion));
            }

            Config.AzureRegion = azureRegion;

            return this;
        }

        internal ConfidentialClientApplicationBuilder WithAppTokenCacheInternalForTest(ITokenCacheInternal tokenCacheInternal)
        {
            Config.AppTokenCacheInternalForTest = tokenCacheInternal;
            return this;
        }

        /// <inheritdoc />
        internal override void Validate()
        {
            base.Validate();

            Config.ClientCredential = new ClientCredentialWrapper(Config);

            if (string.IsNullOrWhiteSpace(Config.RedirectUri))
            {
                Config.RedirectUri = Constants.DefaultConfidentialClientRedirectUri;
            }

            if (!Uri.TryCreate(Config.RedirectUri, UriKind.Absolute, out Uri uriResult))
            {
                throw new InvalidOperationException(MsalErrorMessage.InvalidRedirectUriReceived(Config.RedirectUri));
            }           
        }

        /// <summary>
        /// Builds the ConfidentialClientApplication from the parameters set
        /// in the builder
        /// </summary>
        /// <returns></returns>
        public IConfidentialClientApplication Build()
        {
            return BuildConcrete();
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        internal ConfidentialClientApplication BuildConcrete()
        {
            return new ConfidentialClientApplication(BuildConfiguration());
        }
    }
}
