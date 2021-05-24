﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.UI;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.Identity.Test.Common.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Microsoft.Identity.Test.Unit.CacheTests
{
    [TestClass]
    public class TokenCacheNotificationTests : TestBase
    {
        [TestMethod]
        public async Task AfterAccess_Is_Called_When_BeforeAceess_Throws_Async()
        {
            using (var harness = CreateTestHarness())
            {
                var pca = PublicClientApplicationBuilder
                    .Create(TestConstants.ClientId)
                    .WithHttpManager(harness.HttpManager)
                    .BuildConcrete();

                var tokenCacheHelper = new TokenCacheHelper();
                tokenCacheHelper.PopulateCache(pca.UserTokenCacheInternal.Accessor, addSecondAt: false);
                var account = (await pca.GetAccountsAsync().ConfigureAwait(false)).First();

                // All these actions trigger a reloading of the cache
                await RunAfterAccessFailureAsync(pca, () => pca.GetAccountsAsync()).ConfigureAwait(false);
                await RunAfterAccessFailureAsync(pca,
                    () => pca.AcquireTokenSilent(new[] { "User.Read" }, account).ExecuteAsync())
                    .ConfigureAwait(false);
                await RunAfterAccessFailureAsync(pca, () => pca.RemoveAsync(account)).ConfigureAwait(false);

                // AcquireTokenInteractive will save a token to the cache, but needs more setup
                harness.HttpManager.AddInstanceDiscoveryMockHandler();
                harness.HttpManager.AddSuccessTokenResponseMockHandlerForPost(TestConstants.AuthorityCommonTenant);

                pca.ServiceBundle.ConfigureMockWebUI();

                await RunAfterAccessFailureAsync(pca, () =>
                    pca.AcquireTokenInteractive(new[] { "User.Read" }).ExecuteAsync())
                        .ConfigureAwait(false);

            }
        }

        private static async Task RunAfterAccessFailureAsync(
            IPublicClientApplication pca,
            Func<Task> operationThatTouchesCache)
        {
            bool beforeAccessCalled = false;
            bool afterAccessCalled = false;

            pca.UserTokenCache.SetBeforeAccess(args =>
            {
                beforeAccessCalled = true;
                throw new InvalidOperationException();
            });

            pca.UserTokenCache.SetAfterAccess(args => { afterAccessCalled = true; });

            await AssertException.TaskThrowsAsync<InvalidOperationException>(
                operationThatTouchesCache).ConfigureAwait(false);

            Assert.IsTrue(beforeAccessCalled);
            Assert.IsTrue(afterAccessCalled);
        }

        [TestMethod]
        public async Task TestSubscribeNonAsync()
        {
            var pca = PublicClientApplicationBuilder.Create(TestConstants.ClientId).WithTelemetry(new TraceTelemetryConfig()).Build();

            bool beforeAccessCalled = false;
            bool afterAccessCalled = false;
            bool beforeWriteCalled = false;

            pca.UserTokenCache.SetBeforeAccess(args => { beforeAccessCalled = true; });
            pca.UserTokenCache.SetAfterAccess(args => { afterAccessCalled = true; });
            pca.UserTokenCache.SetBeforeWrite(args => { beforeWriteCalled = true; });

            await pca.GetAccountsAsync().ConfigureAwait(false);

            Assert.IsTrue(beforeAccessCalled);
            Assert.IsTrue(afterAccessCalled);
            Assert.IsFalse(beforeWriteCalled);
        }

        [TestMethod]
        public async Task TestSubscribeAsync()
        {
            var pca = PublicClientApplicationBuilder.Create(TestConstants.ClientId).WithTelemetry(new TraceTelemetryConfig()).Build();

            bool beforeAccessCalled = false;
            bool afterAccessCalled = false;
            bool beforeWriteCalled = false;

            pca.UserTokenCache.SetBeforeAccessAsync(async args => { beforeAccessCalled = true; await Task.Delay(10).ConfigureAwait(false); });
            pca.UserTokenCache.SetAfterAccessAsync(async args => { afterAccessCalled = true; await Task.Delay(10).ConfigureAwait(false); });
            pca.UserTokenCache.SetBeforeWriteAsync(async args => { beforeWriteCalled = true; await Task.Delay(10).ConfigureAwait(false); });

            await pca.GetAccountsAsync().ConfigureAwait(false);

            Assert.IsTrue(beforeAccessCalled);
            Assert.IsTrue(afterAccessCalled);
            Assert.IsFalse(beforeWriteCalled);
        }

        [TestMethod]
        public async Task TestSubscribeBothAsync()
        {
            var pca = PublicClientApplicationBuilder.Create(TestConstants.ClientId).WithTelemetry(new TraceTelemetryConfig()).Build();

            bool beforeAccessCalled = false;
            bool afterAccessCalled = false;
            bool beforeWriteCalled = false;

            bool asyncBeforeAccessCalled = false;
            bool asyncAfterAccessCalled = false;
            bool asyncBeforeWriteCalled = false;

            // Sync method should be called _first_ (just by convention).  But let's validate this.

            pca.UserTokenCache.SetBeforeAccess(args => { beforeAccessCalled = true; });
            pca.UserTokenCache.SetAfterAccess(args => { afterAccessCalled = true; });
            pca.UserTokenCache.SetBeforeWrite(args => { beforeWriteCalled = true; });

            pca.UserTokenCache.SetBeforeAccessAsync(async args => { asyncBeforeAccessCalled = beforeAccessCalled; await Task.Delay(10).ConfigureAwait(false); });
            pca.UserTokenCache.SetAfterAccessAsync(async args => { asyncAfterAccessCalled = afterAccessCalled; await Task.Delay(10).ConfigureAwait(false); });
            pca.UserTokenCache.SetBeforeWriteAsync(async args => { asyncBeforeWriteCalled = beforeWriteCalled; await Task.Delay(10).ConfigureAwait(false); });

            await pca.GetAccountsAsync().ConfigureAwait(false);

            Assert.IsTrue(asyncBeforeAccessCalled);
            Assert.IsTrue(asyncAfterAccessCalled);
            Assert.IsFalse(asyncBeforeWriteCalled);

            Assert.IsTrue(beforeAccessCalled);
            Assert.IsTrue(afterAccessCalled);
            Assert.IsFalse(beforeWriteCalled);
        }

        [TestMethod]
        public async Task TestSerializationViaAsync()
        {
            int numBeforeAccessCalls = 0;
            int numAfterAccessCalls = 0;
            int numBeforeWriteCalls = 0;

            byte[] serializedPayload = null;

            var sb = new StringBuilder();

            using (var harness = CreateTestHarness())
            {
                harness.HttpManager.AddInstanceDiscoveryMockHandler();

                PublicClientApplication pca = PublicClientApplicationBuilder
                    .Create(TestConstants.ClientId)
                    .WithAuthority(new Uri(ClientApplicationBase.DefaultAuthority), true)
                    .WithHttpManager(harness.HttpManager)
                    .WithTelemetry(new TraceTelemetryConfig())
                    .BuildConcrete();

                pca.UserTokenCache.SetBeforeAccessAsync(async args =>
                {
                    sb.Append("beforeaccess-");
                    numBeforeAccessCalls++;

                    // Task Delay is so that we have an await within the async callback and also to simulate
                    // some level of time that we did work.
                    await Task.Delay(10).ConfigureAwait(false);
                });
                pca.UserTokenCache.SetAfterAccessAsync(async args =>
                {
                    sb.Append("afteraccess-");
                    numAfterAccessCalls++;
                    serializedPayload = args.TokenCache.SerializeMsalV3();
                    await Task.Delay(10).ConfigureAwait(false);
                });
                pca.UserTokenCache.SetBeforeWriteAsync(async args =>
                {
                    sb.Append("beforewrite-");
                    numBeforeWriteCalls++;
                    await Task.Delay(10).ConfigureAwait(false);
                });


                pca.ServiceBundle.ConfigureMockWebUI(
                    AuthorizationResult.FromUri(pca.AppConfig.RedirectUri + "?code=some-code"));

                harness.HttpManager.AddSuccessTokenResponseMockHandlerForPost(TestConstants.AuthorityCommonTenant);

                AuthenticationResult result = await pca
                    .AcquireTokenInteractive(TestConstants.s_scope)
                    .ExecuteAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }

            Assert.AreEqual("beforeaccess-beforewrite-afteraccess-", sb.ToString());

            Assert.AreEqual(1, numBeforeAccessCalls);
            Assert.AreEqual(1, numAfterAccessCalls);
            Assert.AreEqual(1, numBeforeWriteCalls);

            Assert.IsNotNull(serializedPayload);
        }

        [TestMethod]
        public async Task TestAccountAcrossMultipleClientIdsAsync()
        {
            using (var _harness = CreateTestHarness())
            {
                // Arrange
                PublicClientApplication app = PublicClientApplicationBuilder
                    .Create(TestConstants.ClientId)
                    .WithHttpManager(_harness.HttpManager)
                    .BuildConcrete();

                Trace.WriteLine("Step 1 - call GetAccounts with empty cache - no accounts, no tokens");
                var cacheAccessRecorder1 = app.UserTokenCache.RecordAccess();
                var accounts = await app.GetAccountsAsync().ConfigureAwait(false);

                Assert.IsFalse(cacheAccessRecorder1.LastBeforeAccessNotificationArgs.HasTokens);
                Assert.IsNull(cacheAccessRecorder1.LastBeforeWriteNotificationArgs);
                Assert.IsFalse(cacheAccessRecorder1.LastAfterAccessNotificationArgs.HasTokens);
                Assert.IsFalse(accounts.Any());

                Trace.WriteLine("Step 2 - call AcquireTokenInteractive - it will save new tokens in the cache");
                app.ServiceBundle.ConfigureMockWebUI(
                     AuthorizationResult.FromUri(app.AppConfig.RedirectUri + "?code=some-code"));
                _harness.HttpManager.AddInstanceDiscoveryMockHandler();
                _harness.HttpManager.AddSuccessTokenResponseMockHandlerForPost();
                var cacheAccessRecorder2 = app.UserTokenCache.RecordAccess();

                await app
                    .AcquireTokenInteractive(TestConstants.s_scope)
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                Assert.IsFalse(cacheAccessRecorder2.LastBeforeAccessNotificationArgs.HasTokens);
                Assert.IsFalse(cacheAccessRecorder2.LastBeforeWriteNotificationArgs.HasTokens);
                Assert.IsTrue(cacheAccessRecorder2.LastAfterAccessNotificationArgs.HasTokens);

                Trace.WriteLine("Step 3 - call GetAccounts - now with 1 account");
                var cacheAccessRecorder3 = app.UserTokenCache.RecordAccess();
                accounts = await app.GetAccountsAsync().ConfigureAwait(false);

                Assert.IsTrue(cacheAccessRecorder3.LastBeforeAccessNotificationArgs.HasTokens);
                Assert.IsNull(cacheAccessRecorder3.LastBeforeWriteNotificationArgs);
                Assert.IsTrue(cacheAccessRecorder3.LastAfterAccessNotificationArgs.HasTokens);
                Assert.IsTrue(accounts.Any());

                Trace.WriteLine("Step 4 - call RemoveAccounts - this will delete all the tokens");
                var cacheAccessRecorder4 = app.UserTokenCache.RecordAccess();
                await app.RemoveAsync(accounts.Single()).ConfigureAwait(false);

                Assert.IsTrue(cacheAccessRecorder4.LastBeforeAccessNotificationArgs.HasTokens);
                Assert.IsTrue(cacheAccessRecorder4.LastBeforeWriteNotificationArgs.HasTokens);
                Assert.IsFalse(cacheAccessRecorder4.LastAfterAccessNotificationArgs.HasTokens);
            }
        }

        [TestMethod]
        public async Task GetAccounts_DoesNotFireNotifications_WhenTokenCacheIsNotSerialized_Async()
        {
            // Arrange
            var userTokenCacheInternal = Substitute.For<ITokenCacheInternal>();
            var semaphore = new SemaphoreSlim(1, 1);
            userTokenCacheInternal.Semaphore.Returns(semaphore);

            var cca = ConfidentialClientApplicationBuilder
                .Create(TestConstants.ClientId)
                .WithAuthority(new Uri(TestConstants.AuthorityTestTenant))
                .WithRedirectUri(TestConstants.RedirectUri)
                .WithClientSecret(TestConstants.ClientSecret)
                .WithUserTokenCacheInternalForTest(userTokenCacheInternal)
                .BuildConcrete();

            userTokenCacheInternal.IsTokenCacheSerialized().Returns(false);

            // Act
            await cca.GetAccountsAsync().ConfigureAwait(false);

            // Assert
            await cca.UserTokenCacheInternal.DidNotReceiveWithAnyArgs().OnBeforeAccessAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);
            await cca.UserTokenCacheInternal.DidNotReceiveWithAnyArgs().OnBeforeWriteAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);
            await cca.UserTokenCacheInternal.DidNotReceiveWithAnyArgs().OnAfterAccessAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);


            // Arrange
            userTokenCacheInternal.IsTokenCacheSerialized().Returns(true);

            // Act
            await cca.GetAccountsAsync().ConfigureAwait(true);

            // Assert
            await cca.UserTokenCacheInternal.Received().OnBeforeAccessAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);
            await cca.UserTokenCacheInternal.DidNotReceiveWithAnyArgs().OnBeforeWriteAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);
            await cca.UserTokenCacheInternal.Received().OnAfterAccessAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);


        }

        [TestMethod]
        public async Task AcquireTokenForClient_DoesNotFireNotifications_WhenTokenCacheIsNotSerialized_Async()
        {
            // Arrange
            var appTokenCache = Substitute.For<ITokenCacheInternal>();
            var semaphore = new SemaphoreSlim(1, 1);
            appTokenCache.Semaphore.Returns(semaphore);

            appTokenCache.FindAccessTokenAsync(default).ReturnsForAnyArgs(TokenCacheHelper.CreateAccessTokenItem());

            var cca = ConfidentialClientApplicationBuilder
                .Create(TestConstants.ClientId)
                .WithClientSecret(TestConstants.ClientSecret)
                .WithAppTokenCacheInternalForTest(appTokenCache)
                .BuildConcrete();

            appTokenCache.IsTokenCacheSerialized().Returns(false);

            // Act
            await cca.AcquireTokenForClient(new[] { "https://resource/.default" }).ExecuteAsync().ConfigureAwait(false);

            // Assert
            await cca.AppTokenCacheInternal.DidNotReceiveWithAnyArgs().OnBeforeAccessAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);
            await cca.AppTokenCacheInternal.DidNotReceiveWithAnyArgs().OnBeforeWriteAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);
            await cca.AppTokenCacheInternal.DidNotReceiveWithAnyArgs().OnAfterAccessAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);


            // Arrange
            appTokenCache.IsTokenCacheSerialized().Returns(true);

            // Act
            await cca.AcquireTokenForClient(new[] { "https://resource/.default" }).ExecuteAsync().ConfigureAwait(false);

            // Assert
            await cca.AppTokenCacheInternal.Received().OnBeforeAccessAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);
            await cca.AppTokenCacheInternal.DidNotReceiveWithAnyArgs().OnBeforeWriteAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);
            await cca.AppTokenCacheInternal.Received().OnAfterAccessAsync(Arg.Any<TokenCacheNotificationArgs>()).ConfigureAwait(true);
        }

        [TestMethod]
        public void IsSerializedTest()
        {
            var cca = ConfidentialClientApplicationBuilder
               .Create(TestConstants.ClientId)
               .WithClientSecret(TestConstants.ClientSecret)
               .BuildConcrete();

            Assert.IsFalse((cca.AppTokenCache as ITokenCacheInternal).IsTokenCacheSerialized());
            Assert.IsFalse((cca.UserTokenCache as ITokenCacheInternal).IsTokenCacheSerialized());

            var inMemoryTokenCache = new InMemoryTokenCache();
            inMemoryTokenCache.Bind(cca.AppTokenCache);

            Assert.IsTrue((cca.AppTokenCache as ITokenCacheInternal).IsTokenCacheSerialized());
            Assert.IsFalse((cca.UserTokenCache as ITokenCacheInternal).IsTokenCacheSerialized());

            inMemoryTokenCache.Bind(cca.UserTokenCache);

            Assert.IsTrue((cca.AppTokenCache as ITokenCacheInternal).IsTokenCacheSerialized());
            Assert.IsTrue((cca.UserTokenCache as ITokenCacheInternal).IsTokenCacheSerialized());

        }
    }
}
