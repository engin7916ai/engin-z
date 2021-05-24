﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Json.Linq;

namespace Microsoft.Identity.Client.CacheV2.Schema
{
    /// <summary>
    /// This class contains the methods for encoding/decoding our object representations of cache data.
    /// If you're modifying this class, you're updating the schema persistence behavior so ensure you're
    /// aligned with the other cache schema models.
    /// </summary>
    internal static class StorageJsonUtils
    {
        public static JObject CredentialToJson(Credential credential)
        {
            var json = string.IsNullOrWhiteSpace(credential.AdditionalFieldsJson)
                           ? new JObject()
                           : JObject.Parse(credential.AdditionalFieldsJson);

            json[StorageJsonKeys.HomeAccountId] = credential.HomeAccountId;
            json[StorageJsonKeys.Environment] = credential.Environment;
            json[StorageJsonKeys.Realm] = credential.Realm;
            json[StorageJsonKeys.CredentialType] = CredentialTypeToString(credential.CredentialType);
            json[StorageJsonKeys.ClientId] = credential.ClientId;
            json[StorageJsonKeys.FamilyId] = credential.FamilyId;
            json[StorageJsonKeys.Target] = credential.Target;
            json[StorageJsonKeys.Secret] = credential.Secret;
            json[StorageJsonKeys.CachedAt] = credential.CachedAt.ToString(CultureInfo.InvariantCulture);
            json[StorageJsonKeys.ExpiresOn] = credential.ExpiresOn.ToString(CultureInfo.InvariantCulture);
            json[StorageJsonKeys.ExtendedExpiresOn] = credential.ExtendedExpiresOn.ToString(CultureInfo.InvariantCulture);

            return json;
        }

        public static string CredentialTypeToString(CredentialType credentialType)
        {
            switch (credentialType)
            {
            case CredentialType.OAuth2AccessToken:
                return StorageJsonValues.CredentialTypeAccessToken;
            case CredentialType.OAuth2RefreshToken:
                return StorageJsonValues.CredentialTypeRefreshToken;
            case CredentialType.OidcIdToken:
                return StorageJsonValues.CredentialTypeIdToken;
            default:
                return StorageJsonValues.CredentialTypeOther;
            }
        }

        public static Credential CredentialFromJson(JObject credentialJson)
        {
            var credential = Credential.CreateEmpty();
            credential.HomeAccountId = JsonUtils.ExtractExistingOrEmptyString(credentialJson, StorageJsonKeys.HomeAccountId);
            credential.Environment = JsonUtils.ExtractExistingOrEmptyString(credentialJson, StorageJsonKeys.Environment);
            credential.Realm = JsonUtils.ExtractExistingOrEmptyString(credentialJson, StorageJsonKeys.Realm);
            credential.CredentialType = CredentialTypeToEnum(
                JsonUtils.ExtractExistingOrEmptyString(credentialJson, StorageJsonKeys.CredentialType));
            credential.ClientId = JsonUtils.ExtractExistingOrEmptyString(credentialJson, StorageJsonKeys.ClientId);
            credential.FamilyId = JsonUtils.ExtractExistingOrEmptyString(credentialJson, StorageJsonKeys.FamilyId);
            credential.Target = JsonUtils.ExtractExistingOrEmptyString(credentialJson, StorageJsonKeys.Target);
            credential.Secret = JsonUtils.ExtractExistingOrEmptyString(credentialJson, StorageJsonKeys.Secret);
            credential.CachedAt = JsonUtils.ExtractParsedIntOrZero(credentialJson, StorageJsonKeys.CachedAt);
            credential.ExpiresOn = JsonUtils.ExtractParsedIntOrZero(credentialJson, StorageJsonKeys.ExpiresOn);
            credential.ExtendedExpiresOn = JsonUtils.ExtractParsedIntOrZero(credentialJson, StorageJsonKeys.ExtendedExpiresOn);

            credential.AdditionalFieldsJson = credentialJson.ToString();

            return credential;
        }

        public static CredentialType CredentialTypeToEnum(string credentialTypeString)
        {
            if (string.Compare(
                    credentialTypeString,
                    StorageJsonValues.CredentialTypeAccessToken,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                return CredentialType.OAuth2AccessToken;
            }

            if (string.Compare(
                    credentialTypeString,
                    StorageJsonValues.CredentialTypeRefreshToken,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                return CredentialType.OAuth2RefreshToken;
            }

            if (string.Compare(
                    credentialTypeString,
                    StorageJsonValues.CredentialTypeIdToken,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                return CredentialType.OidcIdToken;
            }

            return CredentialType.Other;
        }

        public static JObject AccountToJson(Account account)
        {
            var json = string.IsNullOrWhiteSpace(account.AdditionalFieldsJson)
                           ? new JObject()
                           : JObject.Parse(account.AdditionalFieldsJson);

            json[StorageJsonKeys.HomeAccountId] = account.HomeAccountId;
            json[StorageJsonKeys.Environment] = account.Environment;
            json[StorageJsonKeys.Realm] = account.Realm;
            json[StorageJsonKeys.LocalAccountId] = account.LocalAccountId;
            json[StorageJsonKeys.AuthorityType] = AuthorityTypeToString(account.AuthorityType);
            json[StorageJsonKeys.Username] = account.Username;
            json[StorageJsonKeys.GivenName] = account.GivenName;
            json[StorageJsonKeys.FamilyName] = account.FamilyName;
            json[StorageJsonKeys.MiddleName] = account.MiddleName;
            json[StorageJsonKeys.Name] = account.Name;
            json[StorageJsonKeys.AlternativeAccountId] = account.AlternativeAccountId;
            json[StorageJsonKeys.ClientInfo] = account.ClientInfo;

            return json;
        }

        private static string AuthorityTypeToString(CacheV2AuthorityType authorityType)
        {
            switch (authorityType)
            {
            case CacheV2AuthorityType.MsSts:
                return StorageJsonValues.AuthorityTypeMsSts;
            case CacheV2AuthorityType.Adfs:
                return StorageJsonValues.AuthorityTypeAdfs;
            case CacheV2AuthorityType.Msa:
                return StorageJsonValues.AuthorityTypeMsa;
            default:
                return StorageJsonValues.AuthorityTypeOther;
            }
        }

        public static Microsoft.Identity.Client.CacheV2.Schema.Account AccountFromJson(JObject accountJson)
        {
            var account = Account.CreateEmpty();

            account.HomeAccountId = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.Environment = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.Realm = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.LocalAccountId = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.AuthorityType =
                AuthorityTypeToEnum(JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId));
            account.Username = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.GivenName = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.FamilyName = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.MiddleName = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.Name = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.AlternativeAccountId = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);
            account.ClientInfo = JsonUtils.ExtractExistingOrEmptyString(accountJson, StorageJsonKeys.HomeAccountId);

            account.AdditionalFieldsJson = accountJson.ToString();

            return account;
        }

        private static CacheV2AuthorityType AuthorityTypeToEnum(string authorityTypeString)
        {
            if (string.Compare(
                    authorityTypeString,
                    StorageJsonValues.AuthorityTypeMsSts,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                return CacheV2AuthorityType.MsSts;
            }

            if (string.Compare(authorityTypeString, StorageJsonValues.AuthorityTypeAdfs, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return CacheV2AuthorityType.Adfs;
            }

            if (string.Compare(authorityTypeString, StorageJsonValues.AuthorityTypeMsa, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return CacheV2AuthorityType.Msa;
            }

            return CacheV2AuthorityType.Other;
        }

        public static JObject AppMetadataToJson(AppMetadata appMetadata)
        {
            var json = new JObject
            {
                [StorageJsonKeys.Environment] = appMetadata.Environment,
                [StorageJsonKeys.ClientId] = appMetadata.ClientId,
                [StorageJsonKeys.FamilyId] = appMetadata.FamilyId
            };

            return json;
        }

        public static AppMetadata AppMetadataFromJson(JObject appMetadataJson)
        {
            string environment = JsonUtils.GetExistingOrEmptyString(appMetadataJson, StorageJsonKeys.Environment);
            string clientId = JsonUtils.GetExistingOrEmptyString(appMetadataJson, StorageJsonKeys.ClientId);
            string familyId = JsonUtils.GetExistingOrEmptyString(appMetadataJson, StorageJsonKeys.FamilyId);

            return new AppMetadata(environment, clientId, familyId);
        }
    }
}
