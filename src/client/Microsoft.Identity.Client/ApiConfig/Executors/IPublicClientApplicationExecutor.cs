// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Internal;

namespace Microsoft.Identity.Client.ApiConfig.Executors
{
    internal interface IPublicClientApplicationExecutor
    {
        IServiceBundle ServiceBundle { get; }

        Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenInteractiveParameters interactiveParameters,
            CancellationToken cancellationToken);

        Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenWithDeviceCodeParameters withDeviceCodeParameters,
            CancellationToken cancellationToken);

        Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenByIntegratedWindowsAuthParameters integratedWindowsAuthParameters,
            CancellationToken cancellationToken);

        Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenByUsernamePasswordParameters usernamePasswordParameters,
            CancellationToken cancellationToken);
    }
}
