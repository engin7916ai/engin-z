﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Test.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Test.Unit.CacheTests
{
    [TestClass]
    public class ManualCacheLoadTest
    {
        [TestInitialize]
        public void TestInitialize()
        {
            TestCommon.ResetInternalStaticCaches();
        }

        // This is a manual run test to be able to load a cache file from python manually until we get automated tests across the other languages/platforms.
        [TestMethod]
        [Ignore]
        public async Task TestLoadCacheAsync()
        {
            // string authority = "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/";
            string authority = "https://login.microsoftonline.com/organizations/";
            string scope = "https://graph.microsoft.com/.default";
            string clientId = "b945c513-3946-4ecd-b179-6499803a2167";
            string accountId = "13dd2c19-84cd-416a-ae7d-49573e425619.26039cce-489d-4002-8293-5b0c5134eacb";

            string filePathCacheBin = @"C:\Users\mark\Downloads\python_msal_cache.bin";

            var pca = PublicClientApplicationBuilder.Create(clientId).WithAuthority(authority).Build();
            pca.UserTokenCache.DeserializeMsalV3(File.ReadAllBytes(filePathCacheBin));

            var account = await pca.GetAccountAsync(accountId).ConfigureAwait(false);
            var result = await pca.AcquireTokenSilent(new List<string> { scope }, account).ExecuteAsync().ConfigureAwait(false);

            Console.WriteLine();
        }
    }
}
