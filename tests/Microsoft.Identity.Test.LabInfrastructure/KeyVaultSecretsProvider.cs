﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Identity.Client;

namespace Microsoft.Identity.Test.LabInfrastructure
{
    public class KeyVaultSecretsProvider : IDisposable
    {
        /// <summary>
        /// Token cache used by the test infrastructure when authenticating against KeyVault
        /// </summary>
        /// <remarks>We aren't using the default cache to make sure the tokens used by this
        /// test infrastructure can't end up in the cache being used by the tests (the UI-less
        /// Desktop test app runs in the same AppDomain as the infrastructure and uses the
        /// default cache).</remarks>
        private readonly static TokenCache s_keyVaultTokenCache = new TokenCache();

        private const string KeyVaultConfidentialClientId = "16dab2ba-145d-4b1b-8569-bf4b9aed4dc8";
        private const string KeyVaultPublicClientId = "3c1e0e0d-b742-45ba-a35e-01c664e14b16";
        private const string KeyVaultThumbPrint = "79FBCBEB5CD28994E50DAFF8035BACF764B14306";
        private const string DataFileName = "data.txt";

        private KeyVaultClient _keyVaultClient;
        private readonly KeyVaultConfiguration _config;
        private AuthenticationResult _authResult;

        /// <summary>Initialize the secrets provider with the "keyVault" configuration section.</summary>
        /// <remarks>
        /// <para>
        /// Authentication using <see cref="KeyVaultAuthenticationType.ClientCertificate"/>
        ///     1. Register Azure AD application of "Web app / API" type.
        ///        To set up certificate based access to the application PowerShell should be used.
        ///     2. Add an access policy entry to target Key Vault instance for this application.
        ///
        ///     The "keyVault" configuration section should define:
        ///         "authType": "ClientCertificate"
        ///         "clientId": [client ID]
        ///         "certThumbprint": [certificate thumbprint]
        /// </para>
        /// <para>
        /// Authentication using <see cref="KeyVaultAuthenticationType.UserCredential"/>
        ///     1. Register Azure AD application of "Native" type.
        ///     2. Add to 'Required permissions' access to 'Azure Key Vault (AzureKeyVault)' API.
        ///     3. When you run your native client application, it will automatically prompt user to enter Azure AD credentials.
        ///     4. To successfully access keys/secrets in the Key Vault, the user must have specific permissions to perform those operations.
        ///        This could be achieved by directly adding an access policy entry to target Key Vault instance for this user
        ///        or an access policy entry for an Azure AD security group of which this user is a member of.
        ///
        ///     The "keyVault" configuration section should define:
        ///         "authType": "UserCredential"
        ///         "clientId": [client ID]
        /// </para>
        /// </remarks>
        public KeyVaultSecretsProvider()
        {
            _config = new KeyVaultConfiguration
            {
                AuthType = KeyVaultAuthenticationType.ClientCertificate
            };

            //The data.txt is a place holder for the keyvault secret. It will only be written to during build time when testing appcenter.
            //After the tests are finished in appcenter, the file will be deleted from the appcenter servers.
            //The file will then be deleted locally Via VSTS task.
            if (File.Exists(DataFileName))
            {
                var data = File.ReadAllText(DataFileName);

                if (!string.IsNullOrWhiteSpace(data))
                {
                    _config.AuthType = KeyVaultAuthenticationType.ClientSecret;
                    _config.KeyVaultSecret = data;
                }
            }

            _config.CertThumbprint = KeyVaultThumbPrint;
            _keyVaultClient = new KeyVaultClient(AuthenticationCallbackAsync);
        }

        ~KeyVaultSecretsProvider()
        {
            Dispose();
        }

        public SecretBundle GetSecret(string secretUrl)
        {
            return _keyVaultClient.GetSecretAsync(secretUrl).GetAwaiter().GetResult();
        }

        private async Task<string> AuthenticationCallbackAsync(string authority, string resource, string scope)
        {
            if (_authResult != null)
            {
                return _authResult.AccessToken;
            }

            var scopes = new[] { resource + "/.default" };

            AuthenticationResult authResult;
            IConfidentialClientApplication confidentialApp;
            X509Certificate2 cert;

            switch (_config.AuthType)
            {
            case KeyVaultAuthenticationType.ClientCertificate:
                cert = CertificateHelper.FindCertificateByThumbprint(_config.CertThumbprint);
                if (cert == null)
                {
                    throw new InvalidOperationException(
                        "Test setup error - cannot find a certificate in the My store for KeyVault. This is available for Microsoft employees only.");
                }

                confidentialApp = ConfidentialClientApplicationBuilder
                    .Create(KeyVaultConfidentialClientId)
                    .WithAuthority(new Uri(authority), true)
                    .WithCertificate(cert)
                    .Build();

                authResult = await confidentialApp
                    .AcquireTokenForClient(scopes)
                    .ExecuteAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                break;
            case KeyVaultAuthenticationType.ClientSecret:
                confidentialApp = ConfidentialClientApplicationBuilder
                    .Create(KeyVaultConfidentialClientId)
                    .WithAuthority(new Uri(authority), true)
                    .WithClientSecret(_config.KeyVaultSecret)
                    .Build();

                authResult = await confidentialApp
                    .AcquireTokenForClient(scopes)
                    .ExecuteAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                break;
            case KeyVaultAuthenticationType.UserCredential:
                var publicApp = PublicClientApplicationBuilder
                    .Create(KeyVaultPublicClientId)
                    .WithAuthority(new Uri(authority), true)
                    .Build();

                authResult = await publicApp
                    .AcquireTokenByIntegratedWindowsAuth(scopes)
                    .ExecuteAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                break;
            default:
                throw new ArgumentOutOfRangeException();
            }

            _authResult = authResult;
            return authResult?.AccessToken;
        }

        public void Dispose()
        {
            if (_keyVaultClient != null)
            {
                _keyVaultClient.Dispose();
                _keyVaultClient = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
