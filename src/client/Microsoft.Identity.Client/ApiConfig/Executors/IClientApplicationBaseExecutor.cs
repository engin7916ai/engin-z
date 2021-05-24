﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Internal;

namespace Microsoft.Identity.Client.ApiConfig.Executors
{
    internal interface IClientApplicationBaseExecutor
    {
        IServiceBundle ServiceBundle { get; }

        Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenSilentParameters silentParameters,
            CancellationToken cancellationToken);

        Task<AuthenticationResult> ExecuteAsync(
            AcquireTokenCommonParameters commonParameters,
            AcquireTokenByRefreshTokenParameters byRefreshTokenParameters,
            CancellationToken cancellationToken);
    }
}
