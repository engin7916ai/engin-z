﻿// ------------------------------------------------------------------------------
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
// ------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Threading.Tasks;
using Foundation;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.PlatformsCommon.Interfaces;
using Microsoft.Identity.Client.PlatformsCommon.Shared;
using Microsoft.Identity.Client.UI;
using UIKit;

namespace Microsoft.Identity.Client.Platforms.iOS
{
    /// <summary>
    ///     Platform / OS specific logic.  No library (ADAL / MSAL) specific code should go in here.
    /// </summary>
    internal class iOSPlatformProxy : IPlatformProxy
    {
        internal const string IosDefaultRedirectUriTemplate = "msal{0}://auth";
        private readonly Lazy<IPlatformLogger> _platformLogger = 
            new Lazy<IPlatformLogger>(() => new ConsolePlatformLogger());
        private IWebUIFactory _overloadWebUiFactory;

        /// <summary>
        /// Get the user logged
        /// </summary>
        public Task<string> GetUserPrincipalNameAsync()
        {
            return Task.FromResult(string.Empty);
        }

        public Task<bool> IsUserLocalAsync(RequestContext requestContext)
        {
            return Task.FromResult(false);
        }

        public bool IsDomainJoined()
        {
            return false;
        }

        public string GetEnvironmentVariable(string variable)
        {
            return null;
        }

        public string GetProcessorArchitecture()
        {
            return null;
        }

        public string GetOperatingSystem()
        {
            return UIDevice.CurrentDevice.SystemVersion;
        }

        public string GetDeviceModel()
        {
            return UIDevice.CurrentDevice.Model;
        }

       
        /// <inheritdoc />
        public string GetBrokerOrRedirectUri(Uri redirectUri)
        {
            return redirectUri.OriginalString;
        }

        /// <inheritdoc />
        public string GetDefaultRedirectUri(string clientId)
        {
            return string.Format(CultureInfo.InvariantCulture, IosDefaultRedirectUriTemplate, clientId);
        }

        public string GetProductName()
        {
            return "MSAL.Xamarin.iOS";
        }

        /// <summary>
        /// Considered PII, ensure that it is hashed. 
        /// </summary>
        /// <returns>Name of the calling application</returns>
        public string GetCallingApplicationName()
        {
            return (NSString)NSBundle.MainBundle?.InfoDictionary?["CFBundleName"];
        }

        /// <summary>
        /// Considered PII, ensure that it is hashed. 
        /// </summary>
        /// <returns>Version of the calling application</returns>
        public string GetCallingApplicationVersion()
        {
            return (NSString)NSBundle.MainBundle?.InfoDictionary?["CFBundleVersion"];
        }

        /// <summary>
        /// Considered PII. Please ensure that it is hashed. 
        /// </summary>
        /// <returns>Device identifier</returns>
        public string GetDeviceId()
        {
            return UIDevice.CurrentDevice?.IdentifierForVendor?.AsString();
        }

        public ILegacyCachePersistence CreateLegacyCachePersistence()
        {
            return new iOSLegacyCachePersistence();
        }

        public ITokenCacheAccessor CreateTokenCacheAccessor()
        {
            return new iOSTokenCacheAccessor();
        }

        /// <inheritdoc />
        public ICryptographyManager CryptographyManager { get; } = new iOSCryptographyManager();

        /// <inheritdoc />
        public IPlatformLogger PlatformLogger => _platformLogger.Value;

        /// <inheritdoc />
        public IWebUIFactory GetWebUiFactory()
        {
            return _overloadWebUiFactory ?? new IosWebUIFactory();
        }

        /// <inheritdoc />
        public void SetWebUiFactory(IWebUIFactory webUiFactory)
        {
            _overloadWebUiFactory = webUiFactory;
        }
    }
}