﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Utils;
using Microsoft.Win32;

namespace Microsoft.Identity.Client.PlatformsCommon.Shared
{
    internal class DeviceAuthHelper
    {
        private static Lazy<bool> s_canOSPerformPKeyAuth;

        public static IDictionary<string, string> ParseChallengeData(HttpResponseHeaders responseHeaders)
        {
            IDictionary<string, string> data = new Dictionary<string, string>();
            string wwwAuthenticate = responseHeaders.GetValues(PKeyAuthConstants.WwwAuthenticateHeader).SingleOrDefault();
            wwwAuthenticate = wwwAuthenticate.Substring(PKeyAuthConstants.PKeyAuthName.Length + 1);
            if (string.IsNullOrEmpty(wwwAuthenticate))
            {
                return data;
            }

            List<string> headerPairs = CoreHelpers.SplitWithQuotes(wwwAuthenticate, ',');
            foreach (string pair in headerPairs)
            {
                List<string> keyValue = CoreHelpers.SplitWithQuotes(pair, '=');
                if (keyValue.Count == 2)
                {
                    data.Add(keyValue[0].Trim(), keyValue[1].Trim().Replace("\"", ""));
                }
            }

            return data;
        }

        public static bool IsDeviceAuthChallenge(HttpResponseHeaders responseHeaders)
        {
            //For PKeyAuth, challenge headers returned from the STS will be case sensitive so a case sensitive check is used to determine
            //if the response is a PKeyAuth challenge.
            return responseHeaders != null
                   && responseHeaders != null
                   && responseHeaders.Contains(PKeyAuthConstants.WwwAuthenticateHeader)
                   && responseHeaders.GetValues(PKeyAuthConstants.WwwAuthenticateHeader).First()
                       .StartsWith(PKeyAuthConstants.PKeyAuthName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Constructs a bypass response to the PKeyAuth challenge on platforms where the challenge cannot be completed.
        /// </summary>
        public static string GetBypassChallengeResponse(HttpResponseHeaders responseHeaders)
        {
            var challengeData = ParseChallengeData(responseHeaders);
            return string.Format(CultureInfo.InvariantCulture,
                                   PKeyAuthConstants.PKeyAuthBypassReponseFormat,
                                   challengeData[PKeyAuthConstants.ChallengeResponseContext],
                                   challengeData[PKeyAuthConstants.ChallengeResponseVersion]);
        }

        /// <summary>
        /// Constructs a bypass response to the PKeyAuth challenge on platforms where the challenge cannot be completed.
        /// </summary>
        public static string GetBypassChallengeResponse(Dictionary<string, string> response)
        {
            return string.Format(CultureInfo.InvariantCulture,
                                   PKeyAuthConstants.PKeyAuthBypassReponseFormat,
                                   response[PKeyAuthConstants.ChallengeResponseContext],
                                   response[PKeyAuthConstants.ChallengeResponseVersion]);
        }

        public static bool CanOSPerformPKeyAuth()
        {
            if (s_canOSPerformPKeyAuth != null)
            {
                return s_canOSPerformPKeyAuth.Value;
            }

            //PKeyAuth can only be performed on operating systems with a major OS version of 6.
            //This corresponds to windows 7, 8, 8.1 and their server equivalents.
            //Environment.OSVersion as it will return incorrect information on some operating systems
            //For more information on how to acquire the current OS version from the registry
            //See (https://stackoverflow.com/a/61914068)
#if NET_CORE || NET5_WIN
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && !RuntimeInformation.OSDescription.Contains("Windows 10", StringComparison.InvariantCultureIgnoreCase)
                && !RuntimeInformation.OSDescription.Contains("Windows Server 2016", StringComparison.InvariantCultureIgnoreCase)
                && !RuntimeInformation.OSDescription.Contains("Windows Server 2019", StringComparison.InvariantCultureIgnoreCase))
            {
                s_canOSPerformPKeyAuth = new Lazy<bool>(() => true);
                return true;
            }
#elif DESKTOP
            var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

            string OSInfo = (string)reg.GetValue("ProductName");

            if (OSInfo.IndexOf("Windows", StringComparison.InvariantCultureIgnoreCase) >= 0
                && OSInfo.IndexOf("Windows 10", StringComparison.InvariantCultureIgnoreCase) < 0
                && OSInfo.IndexOf("Windows Server 2016", StringComparison.InvariantCultureIgnoreCase) < 0
                && OSInfo.IndexOf("Windows Server 2019", StringComparison.InvariantCultureIgnoreCase) < 0)
            {
                s_canOSPerformPKeyAuth = new Lazy<bool>(() => true);
                return true;
            }
#endif
            s_canOSPerformPKeyAuth = new Lazy<bool>(() => false);
            return false;
        }
    }
}
