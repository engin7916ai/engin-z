﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing.Text;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Test.Unit.AppConfigTests
{
    [TestClass]
    [TestCategory("BuilderTests")]
    public class ConfidentialClientApplicationBuilderTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            TestCommon.ResetInternalStaticCaches();
        }

        [TestMethod]
        public void TestConstructor()
        {
            var cca = ConfidentialClientApplicationBuilder.Create(TestConstants.ClientId).Build();
            Assert.AreEqual(TestConstants.ClientId, cca.AppConfig.ClientId);
            Assert.IsNotNull(cca.UserTokenCache);

            // Validate Defaults
            Assert.AreEqual(LogLevel.Info, cca.AppConfig.LogLevel);
            Assert.AreEqual(TestConstants.ClientId, cca.AppConfig.ClientId);
            Assert.IsNotNull(cca.AppConfig.ClientName);
            Assert.IsNotNull(cca.AppConfig.ClientVersion);
            Assert.AreEqual(false, cca.AppConfig.EnablePiiLogging);
            Assert.IsNull(cca.AppConfig.HttpClientFactory);
            Assert.AreEqual(false, cca.AppConfig.IsDefaultPlatformLoggingEnabled);
            Assert.IsNull(cca.AppConfig.LoggingCallback);
            Assert.AreEqual(Constants.DefaultConfidentialClientRedirectUri, cca.AppConfig.RedirectUri);
            Assert.AreEqual(null, cca.AppConfig.TenantId);
        }

        private ConfidentialClientApplicationOptions CreateConfidentialClientApplicationOptions()
        {
            return new ConfidentialClientApplicationOptions
            {
                ClientId = TestConstants.ClientId,
                ClientSecret = "the_client_secret",
                TenantId = "the_tenant_id",
            };
        }

        private void TestBuildConfidentialClientFromOptions(ConfidentialClientApplicationOptions options)
        {
            var app = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(options).Build();
            var authorityInfo = ((ConfidentialClientApplication)app).ServiceBundle.Config.AuthorityInfo;
            Assert.AreEqual("https://login.microsoftonline.com/the_tenant_id/", authorityInfo.CanonicalAuthority);
        }

        [TestMethod]
        public void TestBuildWithNoClientSecretButUsingCert()
        {
            var options = new ConfidentialClientApplicationOptions()
            {
                ClientId = TestConstants.ClientId,
                TenantId = "the_tenant_id",
                Instance = "https://login.microsoftonline.com"
            };

            var app = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(options).Build();
            var authorityInfo = ((ConfidentialClientApplication)app).ServiceBundle.Config.AuthorityInfo;
            Assert.AreEqual("https://login.microsoftonline.com/the_tenant_id/", authorityInfo.CanonicalAuthority);
        }

        [TestMethod]
        public void TestBuildWithInstanceWithTrailingSlash()
        {
            var options = CreateConfidentialClientApplicationOptions();
            options.Instance = "https://login.microsoftonline.com/";
            TestBuildConfidentialClientFromOptions(options);
        }

        [TestMethod]
        public void TestBuildWithInstanceWithoutTrailingSlash()
        {
            var options = CreateConfidentialClientApplicationOptions();
            options.Instance = "https://login.microsoftonline.com";
            TestBuildConfidentialClientFromOptions(options);
        }

        [TestMethod]
        public void TestBuildWithNullInstance()
        {
            var options = CreateConfidentialClientApplicationOptions();
            options.Instance = null;
            TestBuildConfidentialClientFromOptions(options);
        }

        [TestMethod]
        public void TestBuildWithEmptyInstance()
        {
            var options = CreateConfidentialClientApplicationOptions();
            options.Instance = string.Empty;
            TestBuildConfidentialClientFromOptions(options);
        }

        [TestMethod]
        public void TestWithDifferentClientId()
        {
            const string ClientId = "9340c42a-f5de-4a80-aea0-874adc2ca325";
            var cca = ConfidentialClientApplicationBuilder.Create(ClientId).Build();
            Assert.AreEqual(ClientId, cca.AppConfig.ClientId);
        }

        [TestMethod]
        public void TestConstructor_ClientIdOverride()
        {
            const string ClientId = "73cc145e-798f-430c-8d6d-618f1a5802e9";
            var cca = ConfidentialClientApplicationBuilder.Create(TestConstants.ClientId).WithClientId(ClientId).Build();
            Assert.AreEqual(ClientId, cca.AppConfig.ClientId);
        }

        [TestMethod]
        public void TestConstructor_WithClientNameAndVersion()
        {
            const string ClientName = "my client name";
            const string ClientVersion = "1.2.3.4-prerelease";
            var cca =
                ConfidentialClientApplicationBuilder.Create(TestConstants.ClientId).WithClientName(ClientName).WithClientVersion(ClientVersion).Build();
            Assert.AreEqual(ClientName, cca.AppConfig.ClientName);
            Assert.AreEqual(ClientVersion, cca.AppConfig.ClientVersion);
        }

        [TestMethod]
        public void TestConstructor_WithDebugLoggingCallback()
        {
            var cca = ConfidentialClientApplicationBuilder.Create(TestConstants.ClientId).WithDebugLoggingCallback().Build();
            Assert.IsNotNull(cca.AppConfig.LoggingCallback);
        }

        [TestMethod]
        public void TestConstructor_WithHttpClientFactory()
        {
            var httpClientFactory = NSubstitute.Substitute.For<IMsalHttpClientFactory>();
            var cca = ConfidentialClientApplicationBuilder.Create(TestConstants.ClientId).WithHttpClientFactory(httpClientFactory).Build();
            Assert.AreEqual(httpClientFactory, cca.AppConfig.HttpClientFactory);
        }

        [TestMethod]
        public void TestConstructor_WithLogging()
        {
            var cca = ConfidentialClientApplicationBuilder
                      .Create(TestConstants.ClientId).WithLogging((level, message, pii) => { }).Build();

            Assert.IsNotNull(cca.AppConfig.LoggingCallback);
        }

        [TestMethod]
        public void TestConstructor_WithRedirectUri()
        {
            const string RedirectUri = "http://some_redirect_uri/";
            var cca = ConfidentialClientApplicationBuilder
                      .Create(TestConstants.ClientId).WithRedirectUri(RedirectUri).Build();

            Assert.AreEqual(RedirectUri, cca.AppConfig.RedirectUri);
        }

        [TestMethod]
        public void TestConstructor_WithNullRedirectUri()
        {
            var cca = ConfidentialClientApplicationBuilder
                      .Create(TestConstants.ClientId).WithRedirectUri(null).Build();

            Assert.AreEqual(Constants.DefaultConfidentialClientRedirectUri, cca.AppConfig.RedirectUri);
        }

        [TestMethod]
        public void TestConstructor_WithEmptyRedirectUri()
        {
            var cca = ConfidentialClientApplicationBuilder
                      .Create(TestConstants.ClientId).WithRedirectUri(string.Empty).Build();

            Assert.AreEqual(Constants.DefaultConfidentialClientRedirectUri, cca.AppConfig.RedirectUri);
        }

        [TestMethod]
        public void TestConstructor_WithWhitespaceRedirectUri()
        {
            var cca = ConfidentialClientApplicationBuilder
                      .Create(TestConstants.ClientId).WithRedirectUri("      ").Build();

            Assert.AreEqual(Constants.DefaultConfidentialClientRedirectUri, cca.AppConfig.RedirectUri);
        }

        [TestMethod]
        public void TestConstructor_WithInvalidRedirectUri()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                ConfidentialClientApplicationBuilder.Create(TestConstants.ClientId)
                                                    .WithRedirectUri("this is not a valid uri")
                                                    .Build());
        }

        [TestMethod]
        public void TestConstructor_WithTenantId()
        {
            const string TenantId = "a_tenant id";
            var cca = ConfidentialClientApplicationBuilder
                      .Create(TestConstants.ClientId).WithTenantId(TenantId).Build();

            Assert.AreEqual(TenantId, cca.AppConfig.TenantId);
        }

        [TestMethod]
        public void TestConstructor_WithClientSecret()
        {
            const string ClientSecret = "secret value here";
            var cca = ConfidentialClientApplicationBuilder
                      .Create(TestConstants.ClientId).WithClientSecret(ClientSecret).Build();

            Assert.IsNotNull(cca.AppConfig.ClientSecret);
            Assert.AreEqual(ClientSecret, cca.AppConfig.ClientSecret);
        }

        [TestMethod]
        [DeploymentItem(@"Resources\testCert.crtfile")]
        public void TestConstructor_WithCertificate_X509Certificate2()
        {
            var cert = new X509Certificate2(
                ResourceHelper.GetTestResourceRelativePath("testCert.crtfile"), "passw0rd!");

            var cca = ConfidentialClientApplicationBuilder
                      .Create(TestConstants.ClientId).WithCertificate(cert).Build();

            Assert.IsNotNull(cca.AppConfig.ClientCredentialCertificate);
        }

        [TestMethod]
        [DeploymentItem(@"Resources\CustomInstanceMetadata.json")]
        public void TestConstructor_WithValidInstanceDicoveryMetadata()
        {
            string instanceMetadataJson = File.ReadAllText(ResourceHelper.GetTestResourceRelativePath("CustomInstanceMetadata.json"));
            var cca = ConfidentialClientApplicationBuilder.Create(TestConstants.ClientId)
                                                   .WithInstanceDicoveryMetadata(instanceMetadataJson)
                                                   .Build();

            var instanceDiscoveryMetadata = (cca.AppConfig as ApplicationConfiguration).CustomInstanceDiscoveryMetadata;
            Assert.AreEqual(2, instanceDiscoveryMetadata.Metadata.Length);
        }

        [TestMethod]
        [DeploymentItem(@"Resources\CustomInstanceMetadata.json")]
        public void TestConstructor_InstanceMetadata_ValidateAuthority_MutuallyExclusive()
        {
            string instanceMetadataJson = File.ReadAllText(ResourceHelper.GetTestResourceRelativePath("CustomInstanceMetadata.json"));
            var ex = AssertException.Throws<MsalClientException>(() => ConfidentialClientApplicationBuilder.Create(TestConstants.ClientId)
                                                  .WithInstanceDicoveryMetadata(instanceMetadataJson)
                                                  .WithAuthority("https://some.authority/bogus/", true)
                                                  .Build());
            Assert.AreEqual(ex.ErrorCode, MsalError.ValidateAuthorityOrCustomMetadata);
        }

        [TestMethod]
        public void TestConstructor_BadInstanceMetadata()
        {
            var ex = AssertException.Throws<MsalClientException>(() => ConfidentialClientApplicationBuilder.Create(TestConstants.ClientId)
                                                  .WithInstanceDicoveryMetadata("{bad_json_metadata")
                                                  .Build());

            Assert.AreEqual(ex.ErrorCode, MsalError.InvalidUserInstanceMetadata);
        }
    }
}
