﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
#if !WINDOWS_APP && !ANDROID && !iOS // U/P not available on UWP, Android and iOS
using System.Net;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.LabInfrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Test.Integration.HeadlessTests
{
    // Note: these tests require permission to a KeyVault Microsoft account;
    // Please ignore them if you are not a Microsoft FTE, they will run as part of the CI build
    [TestClass]
    public class UsernamePasswordIntegrationTests
    {
        private const string _authority = "https://login.microsoftonline.com/organizations/";
        private static readonly string[] s_scopes = { "User.Read" };

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        [TestInitialize]
        public void TestInitialize()
        {
            TestCommon.ResetInternalStaticCaches();
        }

        #region Happy Path Tests
        [TestMethod]
        public async Task ROPC_AAD_Async()
        {
            var labResponse = LabUserHelper.GetDefaultUser();
            await RunHappyPathTestAsync(labResponse).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ROPC_ADFSv4Federated_Async()
        {
            var labResponse = LabUserHelper.GetAdfsUser(FederationProvider.AdfsV4, true);
            await RunHappyPathTestAsync(labResponse).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ROPC_ADFSv4Managed_Async()
        {
            var labResponse = LabUserHelper.GetAdfsUser(FederationProvider.AdfsV4, false);
            await RunHappyPathTestAsync(labResponse).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ROPC_ADFSv3Federated_Async()
        {
            var labResponse = LabUserHelper.GetAdfsUser(FederationProvider.AdfsV3, true);
            await RunHappyPathTestAsync(labResponse).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ROPC_ADFSv3Managed_Async()
        {
            var labResponse = LabUserHelper.GetAdfsUser(FederationProvider.AdfsV3, false);
            await RunHappyPathTestAsync(labResponse).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task ROPC_ADFSv2Fderated_Async()
        {
            var labResponse = LabUserHelper.GetAdfsUser(FederationProvider.AdfsV2, true);
            await RunHappyPathTestAsync(labResponse).ConfigureAwait(false);
        }

        #endregion

        [TestMethod]
        public async Task AcquireTokenWithManagedUsernameIncorrectPasswordAsync()
        {
            var labResponse = LabUserHelper.GetDefaultUser();
            var user = labResponse.User;

            SecureString incorrectSecurePassword = new SecureString();
            incorrectSecurePassword.AppendChar('x');
            incorrectSecurePassword.MakeReadOnly();

            var msalPublicClient = PublicClientApplicationBuilder.Create(labResponse.AppId).WithAuthority(_authority).Build();

            try
            {
                var result = await msalPublicClient
                    .AcquireTokenByUsernamePassword(s_scopes, user.Upn, incorrectSecurePassword)
                    .ExecuteAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (MsalServiceException ex)
            {
                Assert.IsTrue(!string.IsNullOrWhiteSpace(ex.CorrelationId));
                Assert.AreEqual(400, ex.StatusCode);
                Assert.AreEqual("invalid_grant", ex.ErrorCode);
                Assert.IsTrue(ex.Message.StartsWith("AADSTS50126: Invalid username or password"));

                return;
            }

            Assert.Fail("Bad exception or no exception thrown");
        }

        [TestMethod]
        public void AcquireTokenWithFederatedUsernameIncorrectPassword()
        {
            UserQuery query = new UserQuery
            {
                FederationProvider = FederationProvider.AdfsV4,
                IsMamUser = false,
                IsMfaUser = false,
                IsFederatedUser = false
            };

            var labResponse = LabUserHelper.GetLabUserData(query);
            var user = labResponse.User;

            SecureString incorrectSecurePassword = new SecureString();
            incorrectSecurePassword.AppendChar('x');
            incorrectSecurePassword.MakeReadOnly();

            var msalPublicClient = PublicClientApplicationBuilder.Create(labResponse.AppId).WithAuthority(_authority).Build();

            var result = Assert.ThrowsExceptionAsync<MsalException>(async () => await msalPublicClient
                .AcquireTokenByUsernamePassword(s_scopes, user.Upn, incorrectSecurePassword)
                .ExecuteAsync(CancellationToken.None)
                .ConfigureAwait(false));
        }

        private async Task RunHappyPathTestAsync(LabResponse labResponse)
        {
            var user = labResponse.User;

            SecureString securePassword = new NetworkCredential("", user.GetOrFetchPassword()).SecurePassword;

            var msalPublicClient = PublicClientApplicationBuilder.Create(labResponse.AppId).WithAuthority(_authority).Build();

            //AuthenticationResult authResult = await msalPublicClient.AcquireTokenByUsernamePasswordAsync(Scopes, user.Upn, securePassword).ConfigureAwait(false);
            AuthenticationResult authResult = await msalPublicClient
                .AcquireTokenByUsernamePassword(s_scopes, user.Upn, securePassword)
                .ExecuteAsync(CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsNotNull(authResult);
            Assert.IsNotNull(authResult.AccessToken);
            Assert.IsNotNull(authResult.IdToken);
            Assert.AreEqual(user.Upn, authResult.Account.Username);
            // If test fails with "user needs to consent to the application, do an interactive request" error,
            // Do the following:
            // 1) Add in code to pull the user's password before creating the SecureString, and put a breakpoint there.
            // string password = ((LabUser)user).GetPassword();
            // 2) Using the MSAL Desktop app, make sure the ClientId matches the one used in integration testing.
            // 3) Do the interactive sign-in with the MSAL Desktop app with the username and password from step 1.
            // 4) After successful log-in, remove the password line you added in with step 1, and run the integration test again.
        }
    }
}
#endif
