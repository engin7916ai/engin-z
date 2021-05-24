﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
namespace Microsoft.Identity.Client.Internal.Broker
{
    internal class BrokerParameter
    {
        public const string Authority = "authority";
        public const string ClientId = "client_id";
        public const string Scope = "scope";
        public const string ExtraOidcScopes = "extra_oidc_scopes";
        public const string OidcScopesValue = "openid offline_access profile";
        public const string RedirectUri = "redirect_uri";
        public const string BrokerKey = "broker_key";
        public const string ClientVersion = "client_version";
        public const string MsgProtocolVersion = "msg_protocol_ver";
        public const string MsgProtocolVersion3 = "3";
        public const string SkipCache = "YES";
        public const string AppLink = "app_link";
        public const string IsSilentBrokerRequest = "is_silent_broker_request";

        // not required
        public const string CorrelationId = "correlation_id";
        public const string ExtraQp = "extra_query_param";
        public const string HomeAccountId = "home_account_id";
        public const string LocalAccountId = "local_account_id";
        public const string Username = "username";
        public const string IntuneEnrollmentIds = "intune_enrollment_ids";
        public const string IntuneMamResource = "intune_mam_resource";
        public const string ClientCapabilities = "client_capabilities";
        public const string ClientAppName = "client_app_name";
        public const string ClientAppVersion = "client_app_version";
        public const string Claims = "claims";
        public const string ExtraConsentScopes = "extra_consent_scopes";
        public const string Prompt = "prompt";
        public const string BrokerAccountName = "broker_account_name";
        public const string ForceRefresh = "force_refresh";

        public const string Force = "force";

        public const string BrokerInstallUrl = "broker_install_url";
        public const string UriSchemeBrokerV2 = "msauthv2://";
        public const string UriSchemeBrokerV3 = "msauthv3://";
        public const string AuthCodePrefixForEmbeddedWebviewBrokerInstallRequired = "msauth://";
    }
}
