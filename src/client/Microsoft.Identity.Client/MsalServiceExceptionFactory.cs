﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Identity.Client
{
    internal class MsalServiceExceptionFactory
    {
        static ISet<string> s_nonUiSubErrors = new HashSet<string>(
            new[] { MsalError.ClientMismatch, MsalError.ProtectionPolicyRequired },
            StringComparer.OrdinalIgnoreCase);

        internal static MsalServiceException FromHttpResponse(
          string errorCode,
          string errorMessage,
          HttpResponse httpResponse,
          Exception innerException = null)
        {
            MsalServiceException ex = null;
            var oAuth2Response = JsonHelper.TryToDeserializeFromJson<OAuth2ResponseBase>(httpResponse?.Body);

            if (IsInvalidGrant(oAuth2Response?.Error, oAuth2Response?.SubError) || IsInteractionRequired(oAuth2Response?.Error))
            {
                ex = new MsalUiRequiredException(errorCode, errorMessage, innerException);
            }

            if (string.Equals(oAuth2Response?.Error, MsalError.InvalidClient, StringComparison.OrdinalIgnoreCase))
            {
                ex = new MsalServiceException(
                    MsalError.InvalidClient,
                    MsalErrorMessage.InvalidClient + " Original exception: " + oAuth2Response?.ErrorDescription,
                    innerException);
            }

            if (ex == null)
            {
                ex = new MsalServiceException(errorCode, errorMessage, innerException);
            }


            ex.ResponseBody = httpResponse?.Body;
            ex.StatusCode = httpResponse != null ? (int)httpResponse.StatusCode : 0;
            ex.Headers = httpResponse?.Headers;

            ex.Claims = oAuth2Response?.Claims;
            ex.CorrelationId = oAuth2Response?.CorrelationId;
            ex.SubError = oAuth2Response?.SubError;

            return ex;
        }

        internal static MsalServiceException FromBrokerResponse(
          string errorCode,
          string errorMessage,
          string subErrorCode,
          string correlationId,
          HttpResponse brokerHttpResponse)
        {
            MsalServiceException ex = null;

            if (IsInvalidGrant(errorCode, subErrorCode) || IsInteractionRequired(errorCode))
            {
                ex = new MsalUiRequiredException(errorCode, errorMessage);
            }

            if (string.Equals(errorCode, MsalError.InvalidClient, StringComparison.OrdinalIgnoreCase))
            {
                ex = new MsalServiceException(
                    MsalError.InvalidClient,
                    MsalErrorMessage.InvalidClient + " Original exception: " + errorMessage);
            }

            if (ex == null)
            {
                ex = new MsalServiceException(errorCode, errorMessage);
            }

            if (brokerHttpResponse != null)
            {
                ex.ResponseBody = brokerHttpResponse.Body;
                ex.StatusCode = (int)brokerHttpResponse.StatusCode;
                ex.Headers = brokerHttpResponse.Headers;
            }

            ex.CorrelationId = correlationId;
            ex.SubError = subErrorCode;

            return ex;
        }

        internal static MsalServiceException FromImdsResponse(
          string errorCode,
          string errorMessage,
          HttpResponse httpResponse,
          Exception innerException = null)
        {
            MsalServiceException ex = new MsalServiceException(errorCode, errorMessage, innerException);

            ex.ResponseBody = httpResponse?.Body;
            ex.StatusCode = httpResponse != null ? (int)httpResponse.StatusCode : 0;
            ex.Headers = httpResponse?.Headers;

            return ex;
        }

        private static bool IsInteractionRequired(string errorCode)
        {
            return string.Equals(errorCode, MsalError.InteractionRequired, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInvalidGrant(string errorCode, string subErrorCode)
        {
            return string.Equals(errorCode, MsalError.InvalidGrantError, StringComparison.OrdinalIgnoreCase)
                             && IsInvalidGrantSubError(subErrorCode);
        }

        private static bool IsInvalidGrantSubError(string subError)
        {
            if (string.IsNullOrEmpty(subError))
            {
                return true;
            }

            return !s_nonUiSubErrors.Contains(subError);
        }
    }
}
