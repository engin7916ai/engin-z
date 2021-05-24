﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.;

using System;

namespace Microsoft.Identity.Client.Region
{
    /// <summary>
    /// Indicates where the region information came from. 
    /// </summary>
    internal enum RegionSource
    {
        /// <summary>
        /// Indicates that the API .WithAzureRegion() was not used
        /// </summary>
        None = 0,

        /// <summary>
        /// Auto-detected from Env Variable
        /// </summary>
        EnvVariable = 1,

        /// <summary>
        /// Auto-detected from IMDS
        /// </summary>
        Imds = 2,

        /// <summary>
        /// Auto-detected from MSAL's static cache
        /// </summary>
        Cache = 3,

        /// <summary>
        /// Region was configured by the user
        /// </summary>
        UserProvided = 4,

        /// <summary>
        /// Auto-detection failed, fallback to global
        /// </summary>
        FailedAutoDiscovery = 5
    }
}
