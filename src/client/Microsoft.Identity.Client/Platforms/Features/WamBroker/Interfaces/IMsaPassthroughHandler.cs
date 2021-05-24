// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Identity.Client.Internal.Requests;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;

namespace Microsoft.Identity.Client.Platforms.Features.WamBroker
{
    internal interface IMsaPassthroughHandler
    {
        void AddTransferTokenToRequest(WebTokenRequest webTokenRequest, string transferToken);
        Task<string> TryFetchTransferTokenAsync(AuthenticationRequestParameters authenticationRequestParameters, WebAccountProvider accountProvider);
    }
}
