// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web.Core;

namespace Microsoft.Identity.Client.Platforms.Features.WamBroker
{
    internal static class WamExtensions
    {
        public static bool IsSuccessResponse(this WebTokenRequestStatus status)
        {
            return status == WebTokenRequestStatus.Success ||
                status == WebTokenRequestStatus.AccountSwitch;
        }
    }
}
