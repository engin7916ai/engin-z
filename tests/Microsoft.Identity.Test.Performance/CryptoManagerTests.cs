﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Instance.Discovery;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.Identity.Test.Unit;

namespace Microsoft.Identity.Test.Performance
{
    /// <summary>
    /// Specifically used to test <c>SignWithCertificate</c> method in <see cref="Microsoft.Identity.Client.Platforms.net45.NetDesktopCryptographyManager"/>.
    /// </summary>
    public class CryptoManagerTests
    {
        private const int AppsCount = 100;
        private MockHttpManager _httpManager;
        private readonly AcquireTokenForClientParameterBuilder[] _requests;
        private int _requestIdx;

        /// <summary>
        /// Generate a certificate. Create a Confidential Client Application with that certificate and
        /// an AcquireTokenForClient call to benchmark.
        /// </summary>
        public CryptoManagerTests()
        {
            _httpManager = new MockHttpManager();
            _requests = new AcquireTokenForClientParameterBuilder[AppsCount];
            for (int i = 0; i < AppsCount; i++)
            {
                X509Certificate2 certificate = CreateCertificate("CN=rsa2048", RSA.Create(2048), HashAlgorithmName.SHA256, null);
                var cca = ConfidentialClientApplicationBuilder
                        .Create(TestConstants.ClientId)
                        .WithAuthority(new Uri(TestConstants.AuthorityTestTenant))
                        .WithRedirectUri(TestConstants.RedirectUri)
                        .WithCertificate(certificate)
                        .WithHttpManager(_httpManager)
                        .BuildConcrete();
                AddHostToInstanceCache(cca.ServiceBundle, TestConstants.ProductionPrefNetworkEnvironment);
                _requests[_requestIdx] = cca.AcquireTokenForClient(TestConstants.s_scope)
                    .WithForceRefresh(true);
            }
        }


        /// <summary>
        /// Adds mocked HTTP response to the HTTP manager before each call.
        /// Sets the index of the next app request to use.
        /// </summary>
        [IterationSetup]
        public void IterationSetup()
        {
            _requestIdx = _requestIdx++ % AppsCount;
            _httpManager.AddMockHandlerSuccessfulClientCredentialTokenResponseMessage();
        }

        [Benchmark]
        public async Task<AuthenticationResult> BenchmarkAsync()
        {
            var result = await _requests[_requestIdx].ExecuteAsync(System.Threading.CancellationToken.None).ConfigureAwait(true);
            return result;
        }

        private X509Certificate2 CreateCertificate(string x509DistinguishedName, object key, HashAlgorithmName hashAlgorithmName, X509Certificate2 issuer)
        {
            CertificateRequest certificateRequest = null;
            if (key is RSA)
                certificateRequest = new CertificateRequest(x509DistinguishedName, key as RSA, hashAlgorithmName, RSASignaturePadding.Pkcs1);

            if (issuer == null)
            {
                certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
                return certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(20));
            }
            else
            {
                var certificate = certificateRequest.Create(issuer, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10), Guid.NewGuid().ToByteArray());

                if (key is RSA)
                    return certificate.CopyWithPrivateKey(key as RSA);

                return certificate;
            }
        }

        private void AddHostToInstanceCache(IServiceBundle serviceBundle, string host)
        {
            (serviceBundle.InstanceDiscoveryManager as InstanceDiscoveryManager)
                .AddTestValueToStaticProvider(
                    host,
                    new InstanceDiscoveryMetadataEntry
                    {
                        PreferredNetwork = host,
                        PreferredCache = host,
                        Aliases = new string[]
                        {
                            host
                        }
                    });
        }
    }
}
