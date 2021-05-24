﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Integration.Infrastructure;
using Microsoft.Identity.Test.Integration.net45.Infrastructure;
using Microsoft.Identity.Test.LabInfrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Identity.Test.Integration.HeadlessTests
{
    [TestClass]
    public class SilentAuthTests
    {
        private static readonly string[] s_scopes = { "User.Read" };

        /// <summary>
        /// Initialized by MSTest (do not make private or readonly)
        /// </summary>
        public TestContext TestContext { get; set; }

        private IPublicClientApplication pca = null;

        [TestMethod]
        public async Task SilentAuth_ForceRefresh_Async()
        {
            var labResponse = await LabUserHelper.GetDefaultUserAsync().ConfigureAwait(false);

            pca = PublicClientApplicationBuilder
                .Create(labResponse.App.AppId)
                .WithAuthority("https://login.microsoftonline.com/organizations")
                .WithTestLogging()
                .Build();

            Trace.WriteLine("Part 1 - Acquire a token with U/P");
            AuthenticationResult authResult = await pca
                .AcquireTokenByUsernamePassword(s_scopes, labResponse.User.Upn, new NetworkCredential("", labResponse.User.GetOrFetchPassword()).SecurePassword)
                .ExecuteAsync(new CancellationTokenSource().Token)
                .ConfigureAwait(false);

            await ValidateAuthResultAsync(authResult, labResponse).ConfigureAwait(false);
        }

        [TestMethod]
        [TestCategory(TestCategories.MSA)]
        [Ignore] // Failing sporadically https://identitydivision.visualstudio.com/Engineering/_workitems/edit/1045664 
        public async Task SilentAuth_MsaUser_ForceRefresh_Async()
        {
            var labResponse = await LabUserHelper.GetMsaUserAsync().ConfigureAwait(false);

            pca = PublicClientApplicationBuilder
                .Create(LabApiConstants.MSAOutlookAccountClientID)
                .WithTestLogging()
                .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount)
                .Build();

            Trace.WriteLine("Part 1 - Acquire a token with device code with msa user");
            AuthenticationResult authResult = await pca.AcquireTokenWithDeviceCode(s_scopes, deviceCodeResult =>
            {
                SeleniumExtensions.PerformDeviceCodeLogin(
                    deviceCodeResult,
                    labResponse.User,
                    TestContext,
                    false);

                return Task.FromResult(0);
            }).ExecuteAsync()
              .ConfigureAwait(false);

            await ValidateAuthResultAsync(authResult, labResponse).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task SilentAuth_TokenCacheRemainsPersistent_Async()
        {
            var labResponse = await LabUserHelper.GetDefaultUserAsync().ConfigureAwait(false);
            var user = labResponse.User;
            string cacheFilePath = null;

            try
            {
                cacheFilePath = Path.GetTempFileName();

                var pca1 = PublicClientApplicationBuilder
                   .Create(labResponse.App.AppId)
                   .WithTestLogging()
                   .WithAuthority("https://login.microsoftonline.com/organizations")
                   .Build();

                SetCacheSerializationToFile(pca1, cacheFilePath);

                AuthenticationResult authResult = await pca1
                    .AcquireTokenByUsernamePassword(s_scopes, user.Upn, new NetworkCredential("", user.GetOrFetchPassword()).SecurePassword)
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                MsalAssert.AssertAuthResult(authResult, user);

                // simulate a restart by creating a new client
                var pca2 = PublicClientApplicationBuilder
                 .Create(labResponse.App.AppId)
                 .WithTestLogging()
                 .Build();

                SetCacheSerializationToFile(pca2, cacheFilePath);

                authResult = await pca2.AcquireTokenSilent(s_scopes, user.Upn)
                  .WithAuthority("https://login.microsoftonline.com/organizations")
                  .ExecuteAsync()
                  .ConfigureAwait(false);

                MsalAssert.AssertAuthResult(authResult, user);
            }
            finally
            {
                if (cacheFilePath != null && File.Exists(cacheFilePath))
                {
                    File.Delete(cacheFilePath);
                }
            }
        }

        private async Task ValidateAuthResultAsync(
            AuthenticationResult authResult, 
            LabResponse labResponse)
        {
            MsalAssert.AssertAuthResult(authResult, labResponse.User);
            var at1 = authResult.AccessToken;
            // If test fails with "user needs to consent to the application, do an interactive request" error - see UsernamePassword tests

            Trace.WriteLine("Part 2 - Acquire a token silently, with forceRefresh = true");
            IAccount account = await MsalAssert.AssertSingleAccountAsync(labResponse, pca, authResult).ConfigureAwait(false);

            authResult = await pca.AcquireTokenSilent(s_scopes, account)
                .WithForceRefresh(true)
                .ExecuteAsync()
                .ConfigureAwait(false);
            MsalAssert.AssertAuthResult(authResult, labResponse.User);
            var at2 = authResult.AccessToken;

            Trace.WriteLine("Part 3 - Acquire a token silently with a login hint, with forceRefresh = true");
            authResult = await pca.AcquireTokenSilent(s_scopes, labResponse.User.Upn)
               .WithForceRefresh(true)
               .ExecuteAsync()
               .ConfigureAwait(false);
            MsalAssert.AssertAuthResult(authResult, labResponse.User);
            var at3 = authResult.AccessToken;

            Assert.IsFalse(at1.Equals(at2, System.StringComparison.InvariantCultureIgnoreCase));
            Assert.IsFalse(at1.Equals(at3, System.StringComparison.InvariantCultureIgnoreCase));
            Assert.IsFalse(at2.Equals(at3, System.StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual(TokenSource.IdentityProvider, authResult.AuthenticationResultMetadata.TokenSource);
        }

        private static void SetCacheSerializationToFile(IPublicClientApplication pca, string filePath)
        {
            pca.UserTokenCache.SetBeforeAccess(notificationArgs =>
            {
                notificationArgs.TokenCache.DeserializeMsalV3(File.Exists(filePath)
                    ? File.ReadAllBytes(filePath)
                    : null);
            });

            pca.UserTokenCache.SetAfterAccess(notificationArgs =>
            {
                // if the access operation resulted in a cache update
                if (notificationArgs.HasStateChanged)
                {
                    // reflect changes in the persistent store
                    File.WriteAllBytes(filePath, notificationArgs.TokenCache.SerializeMsalV3());
                }
            });
        }
    }
}
