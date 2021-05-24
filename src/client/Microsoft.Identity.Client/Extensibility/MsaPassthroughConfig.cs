﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client.MsaPassthrough
{
    /// <summary>
    /// Extensions for MSA-Passthrough apps (Microsoft internal only).
    /// </summary>
    public static class MsaPassthroughConfig
    {

        /// <summary>
        /// Declares that the app is MSA-Passthrough enabled (Microsoft internal only). This is a legacy 
        /// feature. Required for the functionality of some brokers, like WAM.
        /// </summary>        
        public static PublicClientApplicationBuilder WithMsaPassthrough(
            this PublicClientApplicationBuilder builder,
            bool enabled = true)
        {
            if (!builder.Config.ExperimentalFeaturesEnabled)
            {
                throw new MsalClientException(
                    MsalError.ExperimentalFeature,
                    MsalErrorMessage.ExperimentalFeature(nameof(WithMsaPassthrough)));
            }

            builder.Config.IsMsaPassthrough = enabled;
            return builder;
        }
    }
}
