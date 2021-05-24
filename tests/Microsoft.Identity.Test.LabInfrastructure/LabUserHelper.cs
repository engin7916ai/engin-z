﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Identity.Test.LabInfrastructure
{
    public static class LabUserHelper
    {
        private static readonly LabServiceApi s_labService;
        private static readonly KeyVaultSecretsProvider s_keyVaultSecretsProvider;
        private static readonly IDictionary<UserQuery, LabResponse> s_userCache =
            new Dictionary<UserQuery, LabResponse>();


        static LabUserHelper()
        {
            s_keyVaultSecretsProvider = new KeyVaultSecretsProvider();
            s_labService = new LabServiceApi();
        }

        public static LabResponse GetLabUserData(UserQuery query)
        {
            if (s_userCache.ContainsKey(query))
            {
                Debug.WriteLine("User cache hit");
                return s_userCache[query];
            }

            var user = s_labService.GetLabResponse(query);
            if (user == null)
            {
                throw new LabUserNotFoundException(query, "Found no users for the given query.");
            }

            Debug.WriteLine("User cache miss");
            s_userCache.Add(query, user);

            return user;
        }

        public static LabResponse GetDefaultUser()
        {
            return GetLabUserData(UserQuery.DefaultUserQuery);
        }

        public static LabResponse GetB2CLocalAccount()
        {
            return GetLabUserData(UserQuery.B2CLocalAccountUserQuery);
        }

        public static LabResponse GetB2CFacebookAccount()
        {
            return GetLabUserData(UserQuery.B2CFacebookUserQuery);
        }

        public static LabResponse GetB2CGoogleAccount()
        {
            return GetLabUserData(UserQuery.B2CGoogleUserQuery);
        }

        public static LabResponse GetAdfsUser(FederationProvider federationProvider, bool federated = true)
        {
            var query = UserQuery.DefaultUserQuery;
            query.FederationProvider = federationProvider;
            query.IsFederatedUser = true;
            query.IsFederatedUser = federated;
            return GetLabUserData(query);
        }

        public static string FetchUserPassword(string passwordUri)
        {
            if (string.IsNullOrWhiteSpace(passwordUri))
            {
                throw new InvalidOperationException("Error: CredentialUrl is not set on user. Password retrieval failed.");
            }

            if (s_keyVaultSecretsProvider == null)
            {
                throw new InvalidOperationException("Error: Keyvault secrets provider is not set");
            }

            try
            {
                var secret = s_keyVaultSecretsProvider.GetSecret(passwordUri);
                return secret.Value;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Test setup: cannot get the user password. See inner exception.", e);
            }
        }
    }
}
