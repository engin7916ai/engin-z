﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Threading;
using System.Collections.ObjectModel;
using Windows.Security.Authentication.Web;
using System.Diagnostics;
using System.Globalization;
using System.Text;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWP_standalone
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private static readonly string s_clientID = "1d18b3b0-251b-4714-a02a-9956cec86c2d";
        private static readonly string s_authority = "https://login.microsoftonline.com/common/";
        private static readonly IEnumerable<string> s_scopes = new[] { "user.read" };
        private const string CacheFileName = "msal_user_cache.json";


        public MainPage()
        {
            InitializeComponent();

            // returns smth like s-1-15-2-2601115387-131721061-1180486061-1362788748-631273777-3164314714-2766189824
            string sid = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host;

            // use uppercase S
            sid = sid.Replace('s', 'S');

            // the redirect uri
            string redirectUri = $"ms-appx-web://microsoft.aad.brokerplugin/{sid}";
        }



        private IPublicClientApplication CreatePublicClient()
        {
            return PublicClientApplicationBuilder.Create(s_clientID)
                .WithAuthority(s_authority)
                .WithBroker(chkUseBroker.IsChecked.Value)
                .WithLogging((x, y, z) => Debug.WriteLine($"{x} {y}"), LogLevel.Verbose, true)
                .Build();
        }

        private async void AcquireTokenIWA_ClickAsync(object sender, RoutedEventArgs e)
        {
            var pca = CreatePublicClient();
            AuthenticationResult result = null;
            try
            {
                result = await pca.AcquireTokenByIntegratedWindowsAuth(s_scopes).ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await DisplayErrorAsync(ex).ConfigureAwait(false);
                return;
            }

            await DisplayResultAsync(result).ConfigureAwait(false);
        }


        private async void GetAccountsAsync(object sender, RoutedEventArgs e)
        {
            var pca = CreatePublicClient();
            IEnumerable<IAccount> accounts = await pca.GetAccountsAsync().ConfigureAwait(false);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Getting accounts ...");
            foreach (IAccount account in accounts)
            {
                sb.AppendLine($"{account.Username} .... from {account.Environment}");
            }

            sb.AppendLine("Done getting accounts.");

            await DisplayMessageAsync(sb.ToString()).ConfigureAwait(false);
        }

        private async void ExpireAtsAsync(object sender, RoutedEventArgs e)
        {
            var pca = CreatePublicClient();
            var tokenCacheInternal = pca.UserTokenCache as ITokenCacheInternal;


            TokenCacheNotificationArgs args =
                 new TokenCacheNotificationArgs(
                 pca.UserTokenCache as ITokenCacheInternal,
                 s_clientID,
                 null,
                 true,
                 false,
                 true);

            await tokenCacheInternal.OnBeforeAccessAsync(args).ConfigureAwait(false);

            var ats = tokenCacheInternal.Accessor.GetAllAccessTokens();


            // set access tokens as expired
            foreach (var accessItem in ats)
            {
                accessItem.ExpiresOnUnixTimestamp =
                    ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)
                    .ToString(CultureInfo.InvariantCulture);

                tokenCacheInternal.Accessor.SaveAccessToken(accessItem);
            }

            await tokenCacheInternal.OnAfterAccessAsync(args).ConfigureAwait(false);
        }

        private async void ClearCacheAsync(object sender, RoutedEventArgs e)
        {            
            var pca = CreatePublicClient();            

            IEnumerable<IAccount> accounts = await pca.GetAccountsAsync().ConfigureAwait(false);
            foreach (IAccount account in accounts)
            {
                await pca.RemoveAsync(account).ConfigureAwait(false);
            }
        }


        private async void ATS_ClickAsync(object sender, RoutedEventArgs e)
        {
            var pca = CreatePublicClient();
            var upnPrefix = tbxUpn.Text;

            IEnumerable<IAccount> accounts = await pca.GetAccountsAsync().ConfigureAwait(false);
            var acc = accounts.SingleOrDefault(a => a.Username.StartsWith(upnPrefix));

            AuthenticationResult result = null;
            try
            {
                result = await pca
                    .AcquireTokenSilent(s_scopes, acc)
                    .ExecuteAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await DisplayErrorAsync(ex).ConfigureAwait(false);
                return;
            }

            await DisplayResultAsync(result).ConfigureAwait(false);

        }

        private async void ATI_ClickAsync(object sender, RoutedEventArgs e)
        {
            var pca = CreatePublicClient();
            var upnPrefix = tbxUpn.Text;

            IEnumerable<IAccount> accounts = await pca.GetAccountsAsync().ConfigureAwait(true); // stay on UI thread
            var acc = accounts.SingleOrDefault(a => a.Username.StartsWith(upnPrefix));

            try
            {
                var result = await pca.AcquireTokenInteractive(s_scopes)
                    .WithAccount(acc)
                    .ExecuteAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                await DisplayResultAsync(result).ConfigureAwait(false);


            }
            catch (Exception ex)
            {
                await DisplayErrorAsync(ex).ConfigureAwait(false);
                return;
            }

        }

        private async Task DisplayErrorAsync(Exception ex)
        {
            await DisplayMessageAsync(ex.ToString()).ConfigureAwait(false);
        }

        private async Task DisplayResultAsync(AuthenticationResult result)
        {
            await DisplayMessageAsync("Signed in User - " + result.Account.Username + "\nAccessToken: \n" + result.AccessToken).ConfigureAwait(false);
        }


        private async Task DisplayMessageAsync(string message)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                   () =>
                   {
                       Log.Text = message;
                   });
        }
    }
}
