﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

using AppKit;
using Foundation;
using Microsoft.Identity.Client;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace MacCocoaApp
{
    public partial class ViewController : NSViewController
    {
        private const string ClientId = "0615b6ca-88d4-4884-8729-b178178f7c27";
        private const string Authority = "https://login.microsoftonline.com/common";
        private readonly string[] _scopes = new[] { "User.Read" };
        // Consider having a single object for the entire app.
        private readonly IPublicClientApplication _pca;

        // This is a simple, unencrypted, file based cache
        public static readonly string CacheFilePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "mac_sample_cache.txt";

        public ViewController(IntPtr handle) : base(handle)
        {
            // use a simple file cache (alternatively, use no cache and the app will lose all tokens if restarted)
            _pca = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(new Uri(Authority))
                .WithLogging((level, message, pii) =>
                {
                    Console.WriteLine($"MSAL {level} {pii} {message}");
                    Console.ResetColor();
                },
                LogLevel.Verbose,
                true)
                .Build();

            _pca.UserTokenCache.SetBeforeAccess(args =>
            {
                args.TokenCache.DeserializeMsalV3(
                    File.Exists(CacheFilePath) ? File.ReadAllBytes(CacheFilePath): null);
            });

            _pca.UserTokenCache.SetAfterAccess(args =>
            {
                // if the access operation resulted in a cache update
                if (args.HasStateChanged)
                {
                    // reflect changes in the persistent store
                    File.WriteAllBytes(CacheFilePath, args.TokenCache.SerializeMsalV3());
                }
            });
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            // Do any additional setup after loading the view.
        }

        public override NSObject RepresentedObject
        {
            get => base.RepresentedObject;
            set => base.RepresentedObject = value;
        }


#pragma warning disable AvoidAsyncVoid // Avoid async void
        async partial void GetTokenClickAsync(NSObject sender)
#pragma warning restore AvoidAsyncVoid // Avoid async void
        {
            try
            {
                AuthenticationResult result = null;
                var existingAccounts = await _pca.GetAccountsAsync().ConfigureAwait(false);
                var firstExistingAccount = existingAccounts.FirstOrDefault();

                if (firstExistingAccount != null)
                {
                    try
                    {
                        result = await _pca
                            .AcquireTokenSilent(_scopes, firstExistingAccount)
                            .ExecuteAsync(CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch (MsalUiRequiredException)
                    {
                        Console.WriteLine("No token found the in the cache, need to call AcquireTokenAsync");
                    }
                }

                if (result == null)
                {
                    result = await _pca
                        .AcquireTokenInteractive(_scopes)
                        .ExecuteAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }

                UpdateStatus($"Access token acquired: {result.AccessToken}");

            }
            catch (Exception e)
            {
                UpdateStatus("Unexpected error: " +
                    e.Message + Environment.NewLine + e.StackTrace);
            }
        }

#pragma warning disable AvoidAsyncVoid // Avoid async void
        async partial void ShowCacheStatusAsync(NSObject sender)
#pragma warning restore AvoidAsyncVoid // Avoid async void
        {
            var existingAccounts = await _pca.GetAccountsAsync().ConfigureAwait(false);
            if (!existingAccounts.Any())
            {
                UpdateStatus("There are no tokens in the cache");
            }
            else
            {
                UpdateStatus($"There are {existingAccounts.Count()} accounts in the cache." +
                    $" {Environment.NewLine} {string.Join(Environment.NewLine, existingAccounts)} ");
            }
        }

#pragma warning disable AvoidAsyncVoid // Avoid async void
        async partial void ClearCacheClickAsync(Foundation.NSObject sender)
#pragma warning restore AvoidAsyncVoid // Avoid async void
        {
            var accounts = await _pca.GetAccountsAsync().ConfigureAwait(false);
            foreach (var acc in accounts)
            {
                await _pca.RemoveAsync(acc).ConfigureAwait(false);
            }

            ShowCacheStatusAsync(sender);
        }

#pragma warning disable AvoidAsyncVoid // Avoid async void
        async partial void GetTokenDeviceCodeAsync(NSObject sender)
#pragma warning restore AvoidAsyncVoid // Avoid async void
        {
            try
            {
                // Left out checking the token cache for clarity
                var result = await _pca
                    .AcquireTokenWithDeviceCode(
                        _scopes,
                        deviceCodeResult =>
                        {
                            UpdateStatus(deviceCodeResult.Message);
                            return Task.FromResult(0);
                        })
                    .ExecuteAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                UpdateStatus($"Access token acquired: {result.AccessToken}");
            }
            catch (Exception e)
            {
                UpdateStatus("Unexpected error: " +
                    e.Message + Environment.NewLine + e.StackTrace);
            }
        }

        private void UpdateStatus(string message)
        {
            NSRunLoop.Main.BeginInvokeOnMainThread(() => OutputLabel.StringValue = message);
        }
    }
}
