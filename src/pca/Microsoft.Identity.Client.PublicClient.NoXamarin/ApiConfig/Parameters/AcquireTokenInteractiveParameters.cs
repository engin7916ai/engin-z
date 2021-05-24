﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Identity.Client.Core;

using Microsoft.Identity.Client.Extensibility;
using Microsoft.Identity.Client.Internal.Broker;
using Microsoft.Identity.Client.UI;

namespace Microsoft.Identity.Client.ApiConfig.Parameters
{
    /// <summary>
    /// Shared on Confidential Client as well, for 
    /// </summary>
    internal partial class AcquireTokenInteractiveParameters : IAcquireTokenParameters
    {
        public Prompt Prompt { get; set; } = Prompt.NotSpecified;
        public IEnumerable<string> ExtraScopesToConsent { get; set; } = new List<string>();
        public string LoginHint { get; set; }
        public IAccount Account { get; set; }

        public WebViewPreference UseEmbeddedWebView { get; set; } = WebViewPreference.NotSpecified;

        public CoreUIParent UiParent { get; } = new CoreUIParent();
        public ICustomWebUi CustomWebUi { get; set; }

        public void LogParameters(ICoreLogger logger)
        {
            var builder = new StringBuilder();
            builder.AppendLine("=== InteractiveParameters Data ===");
            builder.AppendLine("LoginHint provided: " + !string.IsNullOrEmpty(LoginHint));
            builder.AppendLine("User provided: " + (Account != null));
            builder.AppendLine("ExtraScopesToConsent: " + string.Join(";", ExtraScopesToConsent ?? new List<string>()));
            builder.AppendLine("Prompt: " + Prompt.PromptValue);

            builder.AppendLine("UseEmbeddedWebView: " + UseEmbeddedWebView);
            builder.AppendLine("HasCustomWebUi: " + (CustomWebUi != null));
            UiParent.SystemWebViewOptions?.LogParameters(logger);

            logger.Info(builder.ToString());
        }

        public BrokerAcquireTokenInteractiveParameters ToBrokerInteractiveParams()
        {
            return new BrokerAcquireTokenInteractiveParameters()
            {
                Prompt = Prompt,
                LoginHint = LoginHint
            };
        }

        public GetAuthorizationRequestUrlParameters ToAuthorizationRequestParams()
        {
            return new GetAuthorizationRequestUrlParameters()
            {
                Prompt = Prompt.PromptValue,
                LoginHint = LoginHint,
                ExtraScopesToConsent = ExtraScopesToConsent,
                Account = Account
            };
        }
    }
}

