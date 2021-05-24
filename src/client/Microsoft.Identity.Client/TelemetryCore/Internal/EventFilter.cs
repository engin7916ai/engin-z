﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Identity.Client.TelemetryCore.Internal.Constants;

namespace Microsoft.Identity.Client.TelemetryCore.Internal
{
    internal class EventFilter : IEventFilter
    {
        private readonly bool _enableAggregation;

        public EventFilter(bool enableAggregation)
        {
            _enableAggregation = enableAggregation;
        }

        // todo: remove this method, use value from constructor...
        public void SetShouldAggregate(bool shouldAggregate) => throw new NotImplementedException();

        public bool ShouldAggregateAction(PropertyBagContents contents)
        {
            if (_enableAggregation)
            {
                return IsSuccessfulAction(contents) && IsSilentAction(contents);
            }
            return false;
        }

        private bool IsSuccessfulAction(PropertyBagContents contents)
        {
            if (IsOfActionType(ActionType.Msal, contents.StringProperties))
            {
                return IsMsalActionSuccessful(contents);
            }

            return HasActionOutcome(AuthOutcome.Succeeded, contents.StringProperties);
        }

        private bool IsMsalActionSuccessful(PropertyBagContents contents)
        {
            if (contents.StringProperties.TryGetValue(MsalTelemetryBlobEventNames.IsSuccessfulConstStrKey, out string isSuccessful))
            {
                return isSuccessful.Equals(MsalTelemetryBlobEventValues.IsSuccessfulConstStrValue) && HasActionOutcome(AuthOutcome.Succeeded, contents.StringProperties);
            }

            // MatsPrivate::ReportError("Could not retrieve MSAL is_successful property.", ErrorType::OTHER, ErrorSeverity::LIBRARY_ERROR);
            return false;
        }

        public bool IsSilentAction(PropertyBagContents contents)
        {
            if (IsOfActionType(ActionType.Msal, contents.StringProperties))
            {
                return IsMsalActionSilent(contents);
            }

            if (contents.BoolProperties.TryGetValue(ActionPropertyNames.IsSilentConstStrKey, out bool isSilent))
            {
                return isSilent;
            }

            // MatsPrivate::ReportError("Could not retrieve IsSilent property.", ErrorType::OTHER, ErrorSeverity::LIBRARY_ERROR);
            return false;
        }

        private bool IsOfActionType(ActionType actionType, ConcurrentDictionary<string, string> properties)
        {
            if (properties.TryGetValue(ActionPropertyNames.ActionTypeConstStrKey, out string actionTypeStr))
            {
                return actionTypeStr.Equals(MatsConverter.AsString(actionType));
            }
            return false;
        }

        private bool HasActionOutcome(AuthOutcome outcome, ConcurrentDictionary<string, string> properties)
        {
            if (properties.TryGetValue(ActionPropertyNames.OutcomeConstStrKey, out string outcomeStr))
            {
                return outcomeStr.Equals(MatsConverter.AsString(outcome));
            }
            return false;
        }

        private bool IsMsalActionSilent(PropertyBagContents contents)
        {
            string isSilentUi = string.Empty;
            contents.StringProperties.TryGetValue(MsalTelemetryBlobEventNames.IsSilentTelemetryBatchKey, out isSilentUi);
            string uiEventCount = string.Empty;
            contents.StringProperties.TryGetValue(MsalTelemetryBlobEventNames.UiEventCountTelemetryBatchKey, out uiEventCount);
            bool isBlocking = false;
            contents.BoolProperties.TryGetValue(ActionPropertyNames.BlockingPromptConstStrKey, out isBlocking);
            bool askedForCredentials = false;
            contents.BoolProperties.TryGetValue(ActionPropertyNames.AskedForCredsConstStrKey, out askedForCredentials);

            // todo: if we can't find one of these properties, do this below...
            //if (!error.empty())
            //{
            //    MatsPrivate::ReportError(error, ErrorType::OTHER, ErrorSeverity::LIBRARY_ERROR);
            //    return true;
            //}

            if (isSilentUi == "false" ||
                (string.IsNullOrEmpty(isSilentUi) && uiEventCount != "0" && !string.IsNullOrEmpty(uiEventCount)) ||
                isBlocking == true ||
                askedForCredentials == true)
            {
                return false;
            }

            return true;
        }
    }
}
