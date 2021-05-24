﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.UI;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.Identity.Test.Common.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Test.Unit.PublicApiTests
{
    [TestClass]
    public class PublicClientApplicationTestsWithB2C : TestBase
    {
        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();
        }

        [TestMethod]
        [TestCategory("B2C")]
        public void B2CLoginAcquireTokenTest()
        {
            using (var httpManager = new MockHttpManager())
            {
                PublicClientApplication app = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                                            .WithAuthority(new Uri(TestConstants.B2CLoginAuthority), true)
                                                                            .WithHttpManager(httpManager)
                                                                            .WithTelemetry(new TraceTelemetryConfig())
                                                                            .BuildConcrete();

                MsalMockHelpers.ConfigureMockWebUI(
                    app.ServiceBundle.PlatformProxy,
                                        AuthorizationResult.FromUri(app.AppConfig.RedirectUri + "?code=some-code"));

                httpManager.AddSuccessTokenResponseMockHandlerForPost(TestConstants.B2CLoginAuthority);

                AuthenticationResult result = app
                    .AcquireTokenInteractive(TestConstants.s_scope)
                    .ExecuteAsync(CancellationToken.None)
                    .Result;

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.Account);
            }
        }

        [TestMethod]
        [TestCategory("B2C")]
        public void B2CAcquireTokenTest()
        {
            using (var httpManager = new MockHttpManager())
            {
                PublicClientApplication app = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                                            .WithAuthority(new Uri(TestConstants.B2CAuthority), true)
                                                                            .WithHttpManager(httpManager)
                                                                            .WithTelemetry(new TraceTelemetryConfig())
                                                                            .BuildConcrete();

                MsalMockHelpers.ConfigureMockWebUI(
                    app.ServiceBundle.PlatformProxy,
                                        AuthorizationResult.FromUri(app.AppConfig.RedirectUri + "?code=some-code"));

                httpManager.AddSuccessTokenResponseMockHandlerForPost(TestConstants.B2CAuthority);

                AuthenticationResult result = app
                    .AcquireTokenInteractive(TestConstants.s_scope)
                    .ExecuteAsync(CancellationToken.None)
                    .Result;

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.Account);
            }
        }

        [TestMethod]
        [TestCategory("B2C")]
        public void B2CAcquireTokenWithValidateAuthorityTrueTest()
        {
            using (var httpManager = new MockHttpManager())
            {
                PublicClientApplication app = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                                            .WithAuthority(new Uri(TestConstants.B2CLoginAuthority), true)
                                                                            .WithHttpManager(httpManager)
                                                                            .WithTelemetry(new TraceTelemetryConfig())
                                                                            .BuildConcrete();

                MsalMockHelpers.ConfigureMockWebUI(
                    app.ServiceBundle.PlatformProxy,
                                        AuthorizationResult.FromUri(app.AppConfig.RedirectUri + "?code=some-code"));

                httpManager.AddSuccessTokenResponseMockHandlerForPost(TestConstants.B2CLoginAuthority);

                AuthenticationResult result = app
                    .AcquireTokenInteractive(TestConstants.s_scope)
                    .ExecuteAsync(CancellationToken.None)
                    .Result;

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.Account);
            }
        }

        [TestMethod]
        [TestCategory("B2C")]
        public void B2CAcquireTokenWithValidateAuthorityTrueAndRandomAuthorityTest()
        {
            using (var httpManager = new MockHttpManager())
            {
                PublicClientApplication app = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                                            .WithAuthority(new Uri(TestConstants.B2CCustomDomain), true)
                                                                            .WithHttpManager(httpManager)
                                                                            .WithTelemetry(new TraceTelemetryConfig())
                                                                            .BuildConcrete();

                MsalMockHelpers.ConfigureMockWebUI(
                    app.ServiceBundle.PlatformProxy,
                                        AuthorizationResult.FromUri(app.AppConfig.RedirectUri + "?code=some-code"));

                httpManager.AddSuccessTokenResponseMockHandlerForPost(TestConstants.B2CCustomDomain);

                AuthenticationResult result = app
                    .AcquireTokenInteractive(TestConstants.s_scope)
                    .ExecuteAsync(CancellationToken.None)
                    .Result;

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.Account);
            }
        }

        [TestMethod]
        [TestCategory("B2C")]
        public void B2CAcquireTokenAuthorityHostMisMatchErrorTest()
        {
            using (var httpManager = new MockHttpManager())
            {
                PublicClientApplication app = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                                            .WithAuthority(new Uri(TestConstants.B2CLoginAuthority), true)
                                                                            .WithHttpManager(httpManager)
                                                                            .WithTelemetry(new TraceTelemetryConfig())
                                                                            .BuildConcrete();
                try
                {
                    AuthenticationResult result = app
                        .AcquireTokenInteractive(TestConstants.s_scope)
                        .WithB2CAuthority(TestConstants.B2CLoginAuthorityWrongHost)
                        .ExecuteAsync(CancellationToken.None)
                        .Result;
                }
                catch (Exception exc)
                {
                    Assert.IsNotNull(exc);
                    Assert.AreEqual(MsalErrorMessage.B2CAuthorityHostMisMatch, exc.InnerException.Message);
                    return;
                }
            }

            Assert.Fail("Should not reach here. Exception was not thrown.");
        }

        [TestMethod]
        [Description("Test for AcquireToken with user resetting password")]
        public async Task B2CAcquireTokenWithResetPasswordTestAsync()
        {
            using (var httpManager = new MockHttpManager())
            {
                PublicClientApplication app = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                                            .WithB2CAuthority(TestConstants.B2CLoginAuthority)
                                                                            .WithHttpManager(httpManager)
                                                                            .WithDebugLoggingCallback(logLevel: LogLevel.Verbose)
                                                                            .BuildConcrete();

                // Interactive call and user wants to reset password
                var ui = new MockWebUI()
                {
                    MockResult = AuthorizationResult.FromUri(TestConstants.B2CLoginAuthority +
                    "?error=access_denied&error_description=AADB2C90091%3a+The+user+has+cancelled+entering+self-asserted+information.")
                };

                MsalMockHelpers.ConfigureMockWebUI(app.ServiceBundle.PlatformProxy, ui);

                try
                {
                    AuthenticationResult result = await app
                        .AcquireTokenInteractive(TestConstants.s_scope)
                        .ExecuteAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (MsalServiceException exc)
                {
                    Assert.IsNotNull(exc);
                    Assert.AreEqual("access_denied", exc.ErrorCode);
                    Assert.AreEqual("AADB2C90091: The user has cancelled entering self-asserted information.", exc.Message);
                    return;
                }
            }

            Assert.Fail("Should not reach here. Exception was not thrown.");
        }

        [TestMethod]
        [TestCategory("B2C")]
        public void B2CAcquireTokenWithB2CLoginAuthorityTest()
        {
            using (var harness = CreateTestHarness())
            {
                ValidateB2CLoginAuthority(harness, TestConstants.B2CAuthority);
                ValidateB2CLoginAuthority(harness, TestConstants.B2CLoginAuthority);
                ValidateB2CLoginAuthority(harness, TestConstants.B2CLoginAuthorityBlackforest);
                ValidateB2CLoginAuthority(harness, TestConstants.B2CLoginAuthorityMoonCake);
                ValidateB2CLoginAuthority(harness, TestConstants.B2CLoginAuthorityUsGov);
                ValidateB2CLoginAuthority(harness, TestConstants.B2CCustomDomain);
            }
        }

        /// <summary>
        /// If no scopes are passed in, B2C does not return a AT. MSAL must be able to 
        /// persist the data to the cache and return an AuthenticationResult.
        /// This behavior has been seen on B2C, as AAD will return an access token for the implicit scopes.
        /// </summary>
        [TestMethod]
        [TestCategory("B2C")]
        public async Task B2C_NoScopes_NoAccessToken_Async()
        {
            
            using (var httpManager = new MockHttpManager())
            {
                PublicClientApplication app = PublicClientApplicationBuilder.Create(TestConstants.ClientId)
                                                                            .WithAuthority(new Uri(TestConstants.B2CLoginAuthority), true)
                                                                            .WithHttpManager(httpManager)
                                                                            .BuildConcrete();

                MsalMockHelpers.ConfigureMockWebUI(
                    app.ServiceBundle.PlatformProxy,
                                        AuthorizationResult.FromUri(app.AppConfig.RedirectUri + "?code=some-code"));

                // Arrange 1 - interactive call with 0 scopes
                httpManager.AddSuccessTokenResponseMockHandlerForPost(
                    TestConstants.B2CLoginAuthority,
                    responseMessage: MockHelpers.CreateSuccessResponseMessage(MockHelpers.B2CTokenResponseWithoutAT));

                // Act 
                AuthenticationResult result = await app
                    .AcquireTokenInteractive(null) // no scopes -> no Access Token!
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                // Assert
                AssertNoAccessToken(result);
                Assert.AreEqual(0, httpManager.QueueSize);

                var ex = await AssertException.TaskThrowsAsync<MsalUiRequiredException>(() =>
                  app.AcquireTokenSilent(null, result.Account).ExecuteAsync()
              ).ConfigureAwait(false);

                Assert.AreEqual(MsalError.ScopesRequired, ex.ErrorCode);
                Assert.AreEqual(UiRequiredExceptionClassification.AcquireTokenSilentFailed, ex.Classification);
            }
        }

        private static void AssertNoAccessToken(AuthenticationResult result)
        {
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Account);
            Assert.IsNotNull(result.IdToken);
            Assert.IsNull(result.AccessToken);
            Assert.IsNull(result.Scopes);
            Assert.IsTrue(result.ExpiresOn == default);
            Assert.IsTrue(result.ExtendedExpiresOn == default);
        }

        private static void ValidateB2CLoginAuthority(MockHttpAndServiceBundle harness, string authority)
        {
            var app = PublicClientApplicationBuilder
                .Create(TestConstants.ClientId)
                .WithB2CAuthority(authority)
                .WithHttpManager(harness.HttpManager)
                .BuildConcrete();

            var ui = new MockWebUI()
            {
                MockResult = AuthorizationResult.FromUri(authority + "?code=some-code")
            };

            MsalMockHelpers.ConfigureMockWebUI(app.ServiceBundle.PlatformProxy, ui);
            harness.HttpManager.AddSuccessTokenResponseMockHandlerForPost(authority);

            var result = app
                .AcquireTokenInteractive(TestConstants.s_scope)
                .ExecuteAsync(CancellationToken.None)
                .Result;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Account);
        }
    }
}
