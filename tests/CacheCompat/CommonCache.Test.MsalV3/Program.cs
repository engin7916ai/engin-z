﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonCache.Test.Common;
using Microsoft.Identity.Client;

namespace CommonCache.Test.MsalV2
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            new MsalV3CacheExecutor().Execute(args);
        }

        private class MsalV3CacheExecutor : AbstractCacheExecutor
        {
            /// <inheritdoc />
            protected override async Task<IEnumerable<CacheExecutorAccountResult>> InternalExecuteAsync(TestInputData testInputData)
            {
                Debugger.Launch();
                Debugger.Break();

                string resource = TestInputData.MsGraph;
                string[] scopes = new[]
                {
                    resource + "/user.read"
                };

                var results = new List<CacheExecutorAccountResult>();

                foreach (var labUserData in testInputData.LabUserDatas)
                {
                    var app = PublicClientApplicationBuilder
                       .Create(labUserData.ClientId)
                       .WithAuthority(new Uri(labUserData.Authority), true)
                       .WithLogging((LogLevel level, string message, bool containsPii) =>
                       {
                           Console.WriteLine("{0}: {1}", level, message);
                       })
                       .Build();

                    FileBasedTokenCacheHelper.ConfigureUserCache(
                        testInputData.StorageType,
                        app.UserTokenCache,
                        CommonCacheTestUtils.AdalV3CacheFilePath,
                        CommonCacheTestUtils.MsalV2CacheFilePath,
                        CommonCacheTestUtils.MsalV3CacheFilePath);

                    IEnumerable<IAccount> accounts = await app.GetAccountsAsync().ConfigureAwait(false);

                    IAccount accountToReference = accounts.FirstOrDefault(x => x.Username.Equals(labUserData.Upn, StringComparison.OrdinalIgnoreCase));

                    try
                    {
                        var result = await app
                            .AcquireTokenSilent(scopes, accountToReference)
                            .WithAuthority(app.Authority)
                            .WithForceRefresh(false)
                            .ExecuteAsync(CancellationToken.None)
                            .ConfigureAwait(false);

                        Console.WriteLine($"got token for '{result.Account.Username}' from the cache");

                        results.Add(new CacheExecutorAccountResult(labUserData.Upn, result.Account.Username, true));
                    }
                    catch (MsalUiRequiredException)
                    {
                        var result = await app
                            .AcquireTokenByUsernamePassword(scopes, labUserData.Upn, labUserData.Password.ToSecureString())
                            .ExecuteAsync(CancellationToken.None)
                            .ConfigureAwait(false);

                        if (string.IsNullOrWhiteSpace(result.AccessToken))
                        {
                            results.Add(new CacheExecutorAccountResult(labUserData.Upn, string.Empty, false));
                        }
                        else
                        {
                            Console.WriteLine($"got token for '{result.Account.Username}' without the cache");
                            results.Add(new CacheExecutorAccountResult(labUserData.Upn, result.Account.Username, false));
                        }
                    }
                }

                return results;
            }
        }
    }
}
