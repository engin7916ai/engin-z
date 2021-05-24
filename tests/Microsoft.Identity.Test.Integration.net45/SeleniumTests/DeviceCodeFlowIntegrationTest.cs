﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Integration.Infrastructure;
using Microsoft.Identity.Test.LabInfrastructure;
using Microsoft.Identity.Test.UIAutomation.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;

namespace Microsoft.Identity.Test.Integration.SeleniumTests
{
    // Important: do not install a NuGet package with the Chrome driver as it is a security risk.
    // Instead, install the Chrome driver on the test machine

    // Note: these tests require permission to a KeyVault Microsoft account;
    // Please ignore them if you are not a Microsoft FTE, they will run as part of the CI build
    [TestClass]
    [TestCategory(TestCategories.Selenium)]
    [TestCategory(TestCategories.LabAccess)]
    public class DeviceCodeFlow
    {
        private static readonly string[] s_scopes = { "User.Read" };
        private IWebDriver _seleniumDriver;

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
            //TODO: hook up the logger?
            _seleniumDriver = SeleniumExtensions.CreateDefaultWebDriver();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _seleniumDriver?.Dispose();
        }
        #endregion

        [TestMethod]
        [Timeout(1 * 60 * 1000)] // 1 min timeout
        public async Task DeviceCodeFlowTestAsync()
        {
            LabResponse labResponse = LabUserHelper.GetDefaultUser();

            Trace.WriteLine("Calling AcquireTokenWithDeviceCodeAsync");
            var pca = PublicClientApplicationBuilder.Create(labResponse.AppId).Build();
            var result = await pca.AcquireTokenWithDeviceCode(s_scopes, deviceCodeResult =>
            {
                RunAutomatedDeviceCodeFlow(deviceCodeResult, labResponse.User);

                return Task.FromResult(0);
            }).ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

            Trace.WriteLine("Running asserts");

            Assert.IsNotNull(result);
            Assert.IsTrue(!string.IsNullOrEmpty(result.AccessToken));
        }

        private void RunAutomatedDeviceCodeFlow(DeviceCodeResult deviceCodeResult, LabUser user)
        {
            try
            {
                var fields = new UserInformationFieldIds(user);

                Trace.WriteLine("Browser is open. Navigating to the Device Code url and entering the code");

                _seleniumDriver.Navigate().GoToUrl(deviceCodeResult.VerificationUrl);
                _seleniumDriver
                    // Device Code Flow web ui is undergoing A/B testing and is sometimes different - use 2 IDs
                    .FindElement(SeleniumExtensions.ByIds("otc", "code"))
                    .SendKeys(deviceCodeResult.UserCode);

                IWebElement continueBtn = _seleniumDriver.WaitForElementToBeVisibleAndEnabled(
                    SeleniumExtensions.ByIds(fields.AADSignInButtonId, "continueBtn"));
                continueBtn?.Click();

                _seleniumDriver.PerformLogin(user);

                Trace.WriteLine("Authentication complete");

            }
            catch (Exception ex)
            {
                Trace.WriteLine("Browser automation failed " + ex);
                _seleniumDriver.SaveScreenshot(TestContext);
                throw;
            }
        }

    }
}
