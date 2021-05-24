﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Identity.Client
{
    /// <summary>
    /// Contains metadata of the authentication result.
    /// </summary>
    public class AuthenticationResultMetadata
    {

        /// <summary>
        /// Constructor for the class AuthenticationResultMetadata
        /// <param name="tokenSource">The token source.</param>
        /// </summary>
        public AuthenticationResultMetadata(TokenSource tokenSource)
        {
            TokenSource = tokenSource;
        }

        /// <summary>
        /// The source of the token in the result.
        /// </summary>
        public TokenSource TokenSource { get; }

        /// <summary>
        /// Total time (in ms) spent to service this request, in ms. Includes time spent making Http Requests <see cref="DurationInHttpInMs"/>, time spent
        /// in token cache callbacks <see cref="DurationInCacheInMs"/>, time spent in MSAL and context switching.
        /// </summary>
        public long DurationTotalInMs { get; set; }

        /// <summary>
        /// Time (in ms) MSAL spent in reading and writing to the token cache, i.e. in the OnBeforeAccess, OnAfterAccess etc. callbacks. 
        /// Does not include internal MSAL logic for searching through the cache once loaded.
        /// </summary>
        public long DurationInCacheInMs { get; set; }

        /// <summary>
        /// Time (in ms) MSAL spent for HTTP communication.
        /// </summary>
        public long DurationInHttpInMs { get; set; }
    }
}
