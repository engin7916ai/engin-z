﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.Identity.Test.Common.Mocks;
using Microsoft.Identity.Client.UI;
using System.Threading;
using System.Web;
using Microsoft.Identity.Client.Internal;

namespace Microsoft.Identity.Test.Unit.CoreTests.InstanceTests
{
    [TestClass]
    [DeploymentItem("Resources\\OpenidConfiguration.json")]
    [DeploymentItem("Resources\\OpenidConfiguration-MissingFields.json")]
    [DeploymentItem("Resources\\OpenidConfigurationCommon.json")]
    public class AadAuthorityTests : TestBase
    {

#if NET_CORE
        [TestMethod]
        public void ImmutableTest()
        {
            CoreAssert.IsImmutable<AadAuthority>();
            CoreAssert.IsImmutable<AdfsAuthority>();
            CoreAssert.IsImmutable<B2CAuthority>();
        }
#endif


        [TestMethod]
        public void CreateEndpointsWithCommonTenantTest()
        {
            using (var harness = CreateTestHarness())
            {
                Authority instance = Authority.CreateAuthority("https://login.microsoftonline.com/common");
                Assert.IsNotNull(instance);
                Assert.AreEqual(instance.AuthorityInfo.AuthorityType, AuthorityType.Aad);

                var resolver = new AuthorityEndpointResolutionManager(harness.ServiceBundle);
                var endpoints = resolver.ResolveEndpointsAsync(
                    instance.AuthorityInfo,
                    null,
                    new RequestContext(harness.ServiceBundle, Guid.NewGuid()))
                    .ConfigureAwait(false).GetAwaiter().GetResult();

                Assert.AreEqual("https://login.microsoftonline.com/common/oauth2/v2.0/authorize", endpoints.AuthorizationEndpoint);
                Assert.AreEqual("https://login.microsoftonline.com/common/oauth2/v2.0/token", endpoints.TokenEndpoint);
                Assert.AreEqual("https://login.microsoftonline.com/common/oauth2/v2.0/token", endpoints.SelfSignedJwtAudience);
            }
        }

        [TestMethod]
        public void FailedValidationTest()
        {
            using (var harness = CreateTestHarness())
            {
                // add mock response for instance validation
                harness.HttpManager.AddMockHandler(
                    new MockHttpMessageHandler
                    {
                        ExpectedMethod = HttpMethod.Get,
                        ExpectedUrl = "https://login.microsoftonline.com/common/discovery/instance",
                        ExpectedQueryParams = new Dictionary<string, string>
                        {
                            {"api-version", "1.1"},
                            {
                                "authorization_endpoint",
                                "https%3A%2F%2Flogin.microsoft0nline.com%2Fmytenant.com%2Foauth2%2Fv2.0%2Fauthorize"
                            },
                        },
                        ResponseMessage = MockHelpers.CreateFailureMessage(
                            HttpStatusCode.BadRequest,
                            "{\"error\":\"invalid_instance\"," + "\"error_description\":\"AADSTS50049: " +
                            "Unknown or invalid instance. Trace " + "ID: b9d0894d-a9a4-4dba-b38e-8fb6a009bc00 " +
                            "Correlation ID: 34f7b4cf-4fa2-4f35-a59b" + "-54b6f91a9c94 Timestamp: 2016-08-23 " +
                            "20:45:49Z\",\"error_codes\":[50049]," + "\"timestamp\":\"2016-08-23 20:45:49Z\"," +
                            "\"trace_id\":\"b9d0894d-a9a4-4dba-b38e-8f" + "b6a009bc00\",\"correlation_id\":\"34f7b4cf-" +
                            "4fa2-4f35-a59b-54b6f91a9c94\"}")
                    });

                Authority instance = Authority.CreateAuthority("https://login.microsoft0nline.com/mytenant.com", true);
                Assert.IsNotNull(instance);
                Assert.AreEqual(instance.AuthorityInfo.AuthorityType, AuthorityType.Aad);

                TestCommon.CreateServiceBundleWithCustomHttpManager(harness.HttpManager, authority: instance.AuthorityInfo.CanonicalAuthority, validateAuthority: true);
                try
                {
                    var resolver = new AuthorityEndpointResolutionManager(harness.ServiceBundle);
                    var endpoints = resolver.ResolveEndpointsAsync(
                        instance.AuthorityInfo,
                        null,
                        new RequestContext(harness.ServiceBundle, Guid.NewGuid()))
                        .ConfigureAwait(false).GetAwaiter().GetResult();

                    Assert.Fail("validation should have failed here");
                }
                catch (Exception exc)
                {
                    Assert.IsTrue(exc is MsalServiceException);
                    Assert.AreEqual(((MsalServiceException)exc).ErrorCode, "invalid_instance");
                }
            }
        }
       

        [TestMethod]
        public void CanonicalAuthorityInitTest()
        {
            var serviceBundle = TestCommon.CreateDefaultServiceBundle();

            const string UriNoPort = "https://login.microsoftonline.in/mytenant.com";
            const string UriNoPortTailSlash = "https://login.microsoftonline.in/mytenant.com/";

            const string UriDefaultPort = "https://login.microsoftonline.in:443/mytenant.com";

            const string UriCustomPort = "https://login.microsoftonline.in:444/mytenant.com";
            const string UriCustomPortTailSlash = "https://login.microsoftonline.in:444/mytenant.com/";

            var authority = Authority.CreateAuthority(UriNoPort);
            Assert.AreEqual(UriNoPortTailSlash, authority.AuthorityInfo.CanonicalAuthority);

            authority = Authority.CreateAuthority(UriDefaultPort);
            Assert.AreEqual(UriNoPortTailSlash, authority.AuthorityInfo.CanonicalAuthority);

            authority = Authority.CreateAuthority(UriCustomPort);
            Assert.AreEqual(UriCustomPortTailSlash, authority.AuthorityInfo.CanonicalAuthority);
        }

        [TestMethod]
        public void TenantSpecificAuthorityInitTest()
        {
            var host = String.Concat("https://", TestConstants.ProductionPrefNetworkEnvironment);
            var expectedAuthority = String.Concat(host, "/", TestConstants.TenantId, "/");

            var publicClient = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                             .WithAuthority(host, TestConstants.TenantId)
                                                             .BuildConcrete();

            Assert.AreEqual(publicClient.Authority, expectedAuthority);

            publicClient = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                         .WithAuthority(host, new Guid(TestConstants.TenantId))
                                                         .BuildConcrete();

            Assert.AreEqual(publicClient.Authority, expectedAuthority);

            publicClient = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                         .WithAuthority(new Uri(expectedAuthority))
                                                         .BuildConcrete();

            Assert.AreEqual(publicClient.Authority, expectedAuthority);
        }

        [TestMethod]
        public void MalformedAuthorityInitTest()
        {
            PublicClientApplication publicClient = null;
            var expectedAuthority = String.Concat("https://", TestConstants.ProductionPrefNetworkEnvironment, "/", TestConstants.TenantId, "/");

            //Check bad URI format
            var host = String.Concat("test", TestConstants.ProductionPrefNetworkEnvironment, "/");
            var fullAuthority = String.Concat(host, TestConstants.TenantId);

            AssertException.Throws<UriFormatException>(() =>
            {
                publicClient = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                             .WithAuthority(fullAuthority)
                                                             .BuildConcrete();
            });

            //Check empty path segments
            host = String.Concat("https://", TestConstants.ProductionPrefNetworkEnvironment, "/");
            fullAuthority = String.Concat(host, TestConstants.TenantId, "//");

            publicClient = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                         .WithAuthority(host, new Guid(TestConstants.TenantId))
                                                         .BuildConcrete();

            Assert.AreEqual(publicClient.Authority, expectedAuthority);

            //Check additional path segments
            fullAuthority = String.Concat(host, TestConstants.TenantId, "/ABCD!@#$TEST//");

            publicClient = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                         .WithAuthority(new Uri(fullAuthority))
                                                         .BuildConcrete();

            Assert.AreEqual(publicClient.Authority, expectedAuthority);
        }

        [TestMethod]
        public void TenantAuthorityDoesNotChange()
        {
            // no change because initial authority is tenanted
            AuthorityTestHelper.AuthorityDoesNotUpdateTenant(
                TestConstants.AuthorityUtidTenant, TestConstants.Utid);
        }

        [TestMethod]
        public void TenantlessAuthorityChanges()
        {
            Authority authority = AuthorityTestHelper.CreateAuthorityFromUrl(
                TestConstants.AuthorityCommonTenant);

            Assert.AreEqual("common", authority.TenantId);

            string updatedAuthority = authority.GetTenantedAuthority(TestConstants.Utid);
            Assert.AreEqual(TestConstants.AuthorityUtidTenant, updatedAuthority);
            Assert.AreEqual(updatedAuthority, TestConstants.AuthorityUtidTenant);

            authority = Authority.CreateAuthorityWithTenant(
              authority.AuthorityInfo,
              TestConstants.Utid);

            Assert.AreEqual(authority.AuthorityInfo.CanonicalAuthority, TestConstants.AuthorityUtidTenant);
        }

        [TestMethod]
        //Test for bug #1292 (https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/1292)
        public void AuthorityCustomPortTest()
        {
            var customPortAuthority = "https://localhost:5215/common/";

            using (var harness = CreateTestHarness())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler(customPortAuthority);

                PublicClientApplication app = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                                            .WithAuthority(new Uri(customPortAuthority), false)
                                                                            .WithHttpManager(harness.HttpManager)
                                                                            .BuildConcrete();

                //Ensure that the PublicClientApplication init does not remove the port from the authority
                Assert.AreEqual(customPortAuthority, app.Authority);

                MsalMockHelpers.ConfigureMockWebUI(
                    app.ServiceBundle.PlatformProxy,
                    AuthorizationResult.FromUri(app.AppConfig.RedirectUri + "?code=some-code"));

                harness.HttpManager.AddSuccessTokenResponseMockHandlerForPost(customPortAuthority);
                harness.HttpManager.AddInstanceDiscoveryMockHandler(customPortAuthority);

                AuthenticationResult result = app
                    .AcquireTokenInteractive(TestConstants.s_scope)
                    .ExecuteAsync(CancellationToken.None)
                    .Result;

                //Ensure that acquiring a token does not remove the port from the authority
                Assert.AreEqual(customPortAuthority, app.Authority);
            }
        }
    }
}
