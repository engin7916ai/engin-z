﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.Identity.Test.Integration.Infrastructure;
using Microsoft.Identity.Test.Integration.net45.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;

namespace Microsoft.Identity.Test.Integration.SeleniumTests
{
    [TestClass]
    public class SeleniumInfrastructureTests
    {
        private static readonly string[] s_scopes = new[] { "user.read" };

        #region MSTest Hooks
        /// <summary>
        /// Initialized by MSTest (do not make private or readonly)
        /// </summary>
        public TestContext TestContext { get; set; }

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

        #endregion

        [TestMethod]
        public async Task FailingTest_SeleniumFailureAsync()
        {
            var pca = PublicClientApplicationBuilder
                    .Create("1d18b3b0-251b-4714-a02a-9956cec86c2d")
                    .WithRedirectUri(SeleniumWebUI.FindFreeLocalhostRedirectUri())
                    .WithTestLogging()
                    .Build();

            // This should fail after a few seconds
            var seleniumLogic = new SeleniumWebUI((driver) =>
            {
                Trace.WriteLine("Looking for an element that does not exist");
                driver.FindElement(By.Id("i_hope_this_element_does_not_exist"));
            }, TestContext);

            // The exception propagated to the test should be Selenium exception,
            // the test should not wait for the TCP listener to time out
            await AssertException.TaskThrowsAsync<NoSuchElementException>(() => pca
                 .AcquireTokenInteractive(s_scopes)
                 .WithCustomWebUi(seleniumLogic)
                 .ExecuteAsync(CancellationToken.None))
                 .ConfigureAwait(false);
        }

        [TestMethod]
        public async Task FailingTest_ListenerTimesOut_Async()
        {
            var pca = PublicClientApplicationBuilder
                    .Create("1d18b3b0-251b-4714-a02a-9956cec86c2d")
                    .WithRedirectUri(SeleniumWebUI.FindFreeLocalhostRedirectUri())
                    .WithTestLogging()
                    .Build();

            // The timeout is greater than the timeout of the TCP listener
            var seleniumLogic = new SeleniumWebUI((driver) =>
            {
                Trace.WriteLine("Doing nothing for while, until the TCP listener times out");
                Task.Delay(TimeSpan.FromSeconds(3));  

            }, TestContext);

            // The exception propagated to the test should be Selenium exception,
            // the test should not wait for the TCP listener to time out
            var ex = await AssertException.TaskThrowsAsync<MsalClientException>(() => pca
                 .AcquireTokenInteractive(s_scopes)
                 .WithCustomWebUi(seleniumLogic)
                 .ExecuteAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token))
                 .ConfigureAwait(false);

            // TODO: CustomWebUI uses an MSAL exception for cancellation, however this
            // breaks the CancellationToken cancellation semantics, which state that
            // the listener (i.e. CustomWebUI) should throw OperationCancelledException
            Assert.AreEqual(MsalError.AuthenticationCanceledError, ex.ErrorCode);
        }


    }
}
