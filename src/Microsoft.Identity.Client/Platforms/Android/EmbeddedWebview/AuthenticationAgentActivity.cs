//------------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Webkit;
using Android.Widget;
using Microsoft.Identity.Client.Exceptions;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Identity.Client.Platforms.Android.EmbeddedWebview
{
    [Activity(Label = "Sign in")]
    internal class AuthenticationAgentActivity : Activity
    {
        private const string AboutBlankUri = "about:blank";
        private CoreWebViewClient _client;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            // Create your application here

            WebView webView = new WebView(ApplicationContext);
            var relativeLayout = new RelativeLayout(ApplicationContext);
            webView.LayoutParameters = new RelativeLayout.LayoutParams(RelativeLayout.LayoutParams.MatchParent, RelativeLayout.LayoutParams.MatchParent);

            relativeLayout.AddView(webView);
            SetContentView(relativeLayout);

            string url = Intent.GetStringExtra("Url");
            WebSettings webSettings = webView.Settings;
            string userAgent = webSettings.UserAgentString;
            webSettings.UserAgentString = userAgent + BrokerConstants.ClientTlsNotSupported;
            MsalLogger.Default.Verbose("UserAgent:" + webSettings.UserAgentString);

            webSettings.JavaScriptEnabled = true;

            webSettings.LoadWithOverviewMode = true;
            webSettings.DomStorageEnabled = true;
            webSettings.UseWideViewPort = true;
            webSettings.BuiltInZoomControls = true;

            _client = new CoreWebViewClient(Intent.GetStringExtra("Callback"), this);
            webView.SetWebViewClient(_client);
            webView.LoadUrl(url);
        }

        public override void Finish()
        {
            if (_client.ReturnIntent != null)
            {
                SetResult(Result.Ok, _client.ReturnIntent);
            }
            else
            {
                SetResult(Result.Canceled, new Intent("ReturnFromEmbeddedWebview"));
            }
            base.Finish();
        }

        private sealed class CoreWebViewClient : WebViewClient
        {
            private readonly string _callback;
            private Activity Activity { get; set; }

            public CoreWebViewClient(string callback, Activity activity)
            {
                _callback = callback;
                Activity = activity;
            }

            public Intent ReturnIntent { get; private set; }

            public override void OnLoadResource(WebView view, string url)
            {
                base.OnLoadResource(view, url);

                if (url.StartsWith(_callback, StringComparison.OrdinalIgnoreCase))
                {
                    base.OnLoadResource(view, url);
                    Finish(Activity, url);
                }

            }

            [Obsolete]
            public override bool ShouldOverrideUrlLoading(WebView view, string url)
            {
                Uri uri = new Uri(url);
                if (url.StartsWith(BrokerConstants.BrowserExtPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    MsalLogger.Default.Verbose("It is browser launch request");
                    OpenLinkInBrowser(url, Activity);
                    view.StopLoading();
                    Activity.Finish();
                    return true;
                }

                if (url.StartsWith(BrokerConstants.BrowserExtInstallPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    MsalLogger.Default.Verbose("It is an azure authenticator install request");
                    view.StopLoading();
                    Finish(Activity, url);
                    return true;
                }

                if (url.StartsWith(BrokerConstants.ClientTlsRedirect, StringComparison.OrdinalIgnoreCase))
                {
                    string query = uri.Query;
                    if (query.StartsWith("?", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Substring(1);
                    }

                    Dictionary<string, string> keyPair = CoreHelpers.ParseKeyValueList(query, '&', true, false, null);
                    string responseHeader = DeviceAuthHelper.CreateDeviceAuthChallengeResponseAsync(keyPair).Result;
                    Dictionary<string, string> pkeyAuthEmptyResponse = new Dictionary<string, string>
                    {
                        [BrokerConstants.ChallangeResponseHeader] = responseHeader
                    };
                    view.LoadUrl(keyPair["SubmitUrl"], pkeyAuthEmptyResponse);
                    return true;
                }

                if (url.StartsWith(_callback, StringComparison.OrdinalIgnoreCase))
                {
                    Finish(Activity, url);
                    return true;
                }


                if (!url.Equals(AboutBlankUri, StringComparison.OrdinalIgnoreCase) && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    UriBuilder errorUri = new UriBuilder(_callback)
                    {
                        Query = string.Format(
                            CultureInfo.InvariantCulture,
                            "error={0}&error_description={1}",
                            CoreErrorCodes.NonHttpsRedirectNotSupported,
                            CoreErrorMessages.NonHttpsRedirectNotSupported)
                    };
                    Finish(Activity, errorUri.ToString());
                    return true;
                }

                return false;
            }

            private void OpenLinkInBrowser(string url, Activity activity)
            {
                // Construct URL to launch external browser (use HTTPS)
                var externalBrowserUrlBuilder = new UriBuilder(url)
                {
                    Scheme = Uri.UriSchemeHttps
                };

                string link = externalBrowserUrlBuilder.Uri.AbsoluteUri;
                Intent intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(link));
                activity.StartActivity(intent);
            }

            public override void OnPageFinished(WebView view, string url)
            {
                if (url.StartsWith(_callback, StringComparison.OrdinalIgnoreCase))
                {
                    base.OnPageFinished(view, url);
                    Finish(Activity, url);
                }

                base.OnPageFinished(view, url);
            }

            public override void OnPageStarted(WebView view, string url, global::Android.Graphics.Bitmap favicon)
            {
                if (url.StartsWith(_callback, StringComparison.OrdinalIgnoreCase))
                {
                    base.OnPageStarted(view, url, favicon);
                }

                base.OnPageStarted(view, url, favicon);
            }

            private void Finish(Activity activity, string url)
            {
                ReturnIntent = new Intent("ReturnFromEmbeddedWebview");
                ReturnIntent.PutExtra("ReturnedUrl", url);
                activity.Finish();
            }
        }
    }
}
