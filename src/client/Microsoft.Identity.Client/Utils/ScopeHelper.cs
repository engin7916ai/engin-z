// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.OAuth2;

namespace Microsoft.Identity.Client.Utils
{
    internal static class ScopeHelper
    {
        public static bool ScopeContains(ISet<string> outerSet, IEnumerable<string> possibleContainedSet)
        {
            foreach (string key in possibleContainedSet)
            {
                if (!outerSet.Contains(key) && !string.IsNullOrEmpty(key))
                {
                    return false;
                }
            }

            return true;
        }

        public static HashSet<string> ConvertStringToScopeSet(string singleString)
        {
            if (string.IsNullOrEmpty(singleString))
            {
                return new HashSet<string>();
            }

            return new HashSet<string>(
                singleString.Split(' '), 
                StringComparer.OrdinalIgnoreCase);
        }

        public static HashSet<string> CreateScopeSet(IEnumerable<string> input)
        {
            if (input == null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(input, StringComparer.OrdinalIgnoreCase);
        }

        internal static IEnumerable<string> GetScopesForUserRequest(AuthenticationRequestParameters request)
        {
            if (request.AppConfig.DefaultScopeOverride != null) // even if it's empty
            {
                return request.Scope.Union(request.AppConfig.DefaultScopeOverride, StringComparer.OrdinalIgnoreCase);
            }

            return request.Scope.Union(OAuth2Value.ReservedScopes, StringComparer.OrdinalIgnoreCase);
        }
    }
}
