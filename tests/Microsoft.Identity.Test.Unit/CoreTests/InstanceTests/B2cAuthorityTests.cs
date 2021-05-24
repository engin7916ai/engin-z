﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net.Http;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Test.Unit.CoreTests.InstanceTests
{
    [TestClass]
    [DeploymentItem("Resources\\OpenidConfiguration-B2C.json")]
    [DeploymentItem("Resources\\OpenidConfiguration-B2CLogin.json")]
    public class B2CAuthorityTests : TestBase
    {
        [TestMethod]
        public void NotEnoughPathSegmentsTest()
        {
            try
            {
                var serviceBundle = TestCommon.CreateDefaultServiceBundle();
                var instance = Authority.CreateAuthority("https://login.microsoftonline.in/tfp/");
                Assert.IsNotNull(instance);
                Assert.AreEqual(instance.AuthorityInfo.AuthorityType, AuthorityType.B2C);

                var resolver = new AuthorityEndpointResolutionManager(serviceBundle);
                var endpoints = resolver.ResolveEndpointsAsync(
                    instance.AuthorityInfo,
                    null,
                    new RequestContext(serviceBundle, Guid.NewGuid()))
                    .GetAwaiter().GetResult();
                Assert.Fail("test should have failed");
            }
            catch (Exception exc)
            {
                Assert.IsInstanceOfType(exc, typeof(ArgumentException));
                Assert.AreEqual(MsalErrorMessage.B2cAuthorityUriInvalidPath, exc.Message);
            }
        }

        [TestMethod]
        public void TenantTest()
        {
            AuthorityTestHelper.AuthorityDoesNotUpdateTenant(
                "https://sometenantid.b2clogin.com/tfp/sometenantid/policy/",
                "sometenantid");

            AuthorityTestHelper.AuthorityDoesNotUpdateTenant(
                "https://catsareamazing.com/tfp/catsareamazing/policy/",
                "catsareamazing");

            AuthorityTestHelper.AuthorityDoesNotUpdateTenant(
              "https://sometenantid.b2clogin.de/tfp/tid/policy/",
              "tid");
        }

        [TestMethod]
        public void B2CLoginAuthorityEndpoints()
        {
            using (var httpManager = new MockHttpManager())
            {
                var appConfig = new ApplicationConfiguration()
                {
                    HttpManager = httpManager,
                    AuthorityInfo = AuthorityInfo.FromAuthorityUri(
                        "https://sometenantid.b2clogin.com/tfp/6babcaad-604b-40ac-a9d7-9fd97c0b779f/b2c_1_susi/", true)
                };

                var serviceBundle = ServiceBundle.Create(appConfig);

                Authority instance = Authority.CreateAuthority(
                    "https://sometenantid.b2clogin.com/tfp/6babcaad-604b-40ac-a9d7-9fd97c0b779f/b2c_1_susi/");
                Assert.IsNotNull(instance);
                Assert.AreEqual(instance.AuthorityInfo.AuthorityType, AuthorityType.B2C);
                var resolver = new AuthorityEndpointResolutionManager(serviceBundle);
                var endpoints = resolver.ResolveEndpointsAsync(
                    instance.AuthorityInfo,
                    null,
                    new RequestContext(serviceBundle, Guid.NewGuid()))
                    .GetAwaiter().GetResult();

                Assert.AreEqual(
                    "https://sometenantid.b2clogin.com/tfp/6babcaad-604b-40ac-a9d7-9fd97c0b779f/b2c_1_susi/oauth2/v2.0/authorize",
                    endpoints.AuthorizationEndpoint);
                Assert.AreEqual(
                    "https://sometenantid.b2clogin.com/tfp/6babcaad-604b-40ac-a9d7-9fd97c0b779f/b2c_1_susi/oauth2/v2.0/token",
                    endpoints.TokenEndpoint);
                Assert.AreEqual("https://sometenantid.b2clogin.com/tfp/6babcaad-604b-40ac-a9d7-9fd97c0b779f/b2c_1_susi/oauth2/v2.0/token", 
                    endpoints.SelfSignedJwtAudience);
            }
        }

        [TestMethod]
        [Ignore] // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/1038
        public void B2CMicrosoftOnlineCreateAuthority()
        {
            using (var harness = CreateTestHarness())
            {
                // add mock response for tenant endpoint discovery
                harness.HttpManager.AddMockHandler(
                    new MockHttpMessageHandler
                    {
                        ExpectedMethod = HttpMethod.Get,
                        ExpectedUrl = "https://login.microsoftonline.com/tfp/mytenant.com/my-policy/v2.0/.well-known/openid-configuration",
                        ResponseMessage = MockHelpers.CreateSuccessResponseMessage(
                           File.ReadAllText(ResourceHelper.GetTestResourceRelativePath("OpenidConfiguration-B2C.json")))
                    });

                Authority instance = Authority.CreateAuthority(
                    "https://login.microsoftonline.com/tfp/mytenant.com/my-policy/");
                Assert.IsNotNull(instance);
                Assert.AreEqual(instance.AuthorityInfo.AuthorityType, AuthorityType.B2C);

                var resolver = new AuthorityEndpointResolutionManager(harness.ServiceBundle);
                var endpoints = resolver.ResolveEndpointsAsync(
                    instance.AuthorityInfo,
                    null,
                    new RequestContext(harness.ServiceBundle, Guid.NewGuid()))
                    .GetAwaiter().GetResult();

                Assert.AreEqual(
                    "https://login.microsoftonline.com/6babcaad-604b-40ac-a9d7-9fd97c0b779f/my-policy/oauth2/v2.0/authorize",
                    endpoints.AuthorizationEndpoint);
                Assert.AreEqual(
                    "https://login.microsoftonline.com/6babcaad-604b-40ac-a9d7-9fd97c0b779f/my-policy/oauth2/v2.0/token",
                    endpoints.TokenEndpoint);
                Assert.AreEqual("https://sts.windows.net/6babcaad-604b-40ac-a9d7-9fd97c0b779f/", endpoints.SelfSignedJwtAudience);
            }
        }

        [TestMethod]
        public void CanonicalAuthorityInitTest()
        {
            string UriNoPort = TestConstants.B2CAuthority;
            string UriNoPortTailSlash = TestConstants.B2CAuthority;

            string UriDefaultPort = $"https://login.microsoftonline.in:443/tfp/tenant/{TestConstants.B2CSignUpSignIn}";

            string UriCustomPort = $"https://login.microsoftonline.in:444/tfp/tenant/{TestConstants.B2CSignUpSignIn}";
            string UriCustomPortTailSlash = $"https://login.microsoftonline.in:444/tfp/tenant/{TestConstants.B2CSignUpSignIn}/";
            string UriVanityPort = TestConstants.B2CLoginAuthority;

            var authority = new B2CAuthority(new AuthorityInfo(AuthorityType.B2C, UriNoPort, true));
            Assert.AreEqual(UriNoPortTailSlash, authority.AuthorityInfo.CanonicalAuthority);

            authority = new B2CAuthority(new AuthorityInfo(AuthorityType.B2C, UriDefaultPort, true));
            Assert.AreEqual(UriNoPortTailSlash, authority.AuthorityInfo.CanonicalAuthority);

            authority = new B2CAuthority(new AuthorityInfo(AuthorityType.B2C, UriCustomPort, true));
            Assert.AreEqual(UriCustomPortTailSlash, authority.AuthorityInfo.CanonicalAuthority);

            authority = new B2CAuthority(new AuthorityInfo(AuthorityType.B2C, UriVanityPort, true));
            Assert.AreEqual(UriVanityPort, authority.AuthorityInfo.CanonicalAuthority);
        }
    }
}
