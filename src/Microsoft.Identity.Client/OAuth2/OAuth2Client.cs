﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Mats.Internal.Events;
using Microsoft.Identity.Client.TelemetryCore;
using Microsoft.Identity.Client.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client.OAuth2
{
    internal class OAuth2Client
    {
        private readonly Dictionary<string, string> _bodyParameters = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _headers;
        private readonly Dictionary<string, string> _queryParameters = new Dictionary<string, string>();
        private readonly IHttpManager _httpManager;
        private readonly ITelemetryManager _telemetryManager;

        public OAuth2Client(ICoreLogger logger, IHttpManager httpManager, ITelemetryManager telemetryManager)
        {
            _headers = new Dictionary<string, string>(MsalIdHelper.GetMsalIdParameters(logger));
            _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
            _telemetryManager = telemetryManager ?? throw new ArgumentNullException(nameof(telemetryManager));
        }

        public void AddQueryParameter(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                _queryParameters[key] = value;
            }
        }

        public void AddBodyParameter(string key, string value)
        {
            _bodyParameters[key] = value;
        }

        public async Task<InstanceDiscoveryResponse> DiscoverAadInstanceAsync(Uri endPoint, RequestContext requestContext)
        {
            return await ExecuteRequestAsync<InstanceDiscoveryResponse>(endPoint, HttpMethod.Get, requestContext)
                       .ConfigureAwait(false);
        }

        public async Task<MsalTokenResponse> GetTokenAsync(Uri endPoint, RequestContext requestContext)
        {
            return await ExecuteRequestAsync<MsalTokenResponse>(endPoint, HttpMethod.Post, requestContext).ConfigureAwait(false);
        }

        internal async Task<T> ExecuteRequestAsync<T>(Uri endPoint, HttpMethod method, RequestContext requestContext)
        {
            bool addCorrelationId = requestContext != null && !string.IsNullOrEmpty(requestContext.Logger.CorrelationId.ToString());
            if (addCorrelationId)
            {
                _headers.Add(OAuth2Header.CorrelationId, requestContext.Logger.CorrelationId.ToString());
                _headers.Add(OAuth2Header.RequestCorrelationIdInResponse, "true");
            }

            if (!string.IsNullOrWhiteSpace(requestContext.Logger.ClientName))
            {
                _headers.Add(OAuth2Header.AppName, requestContext.Logger.ClientName);
            }

            if (!string.IsNullOrWhiteSpace(requestContext.Logger.ClientVersion))
            {
                _headers.Add(OAuth2Header.AppVer, requestContext.Logger.ClientVersion);
            }

            HttpResponse response = null;
            Uri endpointUri = CreateFullEndpointUri(endPoint);
            var httpEvent = new HttpEvent(requestContext.TelemetryCorrelationId)
            {
                HttpPath = endpointUri,
                QueryParams = endpointUri.Query
            };

            using (_telemetryManager.CreateTelemetryHelper(httpEvent))
            {
                if (method == HttpMethod.Post)
                {
                    response = await _httpManager.SendPostAsync(endpointUri, _headers, _bodyParameters, requestContext)
                                                .ConfigureAwait(false);
                }
                else
                {
                    response = await _httpManager.SendGetAsync(endpointUri, _headers, requestContext).ConfigureAwait(false);
                }

                httpEvent.HttpResponseStatus = (int)response.StatusCode;
                httpEvent.UserAgent = response.UserAgent;
                httpEvent.HttpMethod = method.Method;

                IDictionary<string, string> headersAsDictionary = response.HeadersAsDictionary;
                if (headersAsDictionary.ContainsKey("x-ms-request-id") &&
                    headersAsDictionary["x-ms-request-id"] != null)
                {
                    httpEvent.RequestIdHeader = headersAsDictionary["x-ms-request-id"];
                }

                if (headersAsDictionary.ContainsKey("x-ms-clitelem") &&
                    headersAsDictionary["x-ms-clitelem"] != null)
                {
                    XmsCliTelemInfo xmsCliTeleminfo = new XmsCliTelemInfoParser().ParseXMsTelemHeader(headersAsDictionary["x-ms-clitelem"], requestContext);
                    if (xmsCliTeleminfo != null)
                    {
                        httpEvent.TokenAge = xmsCliTeleminfo.TokenAge;
                        httpEvent.SpeInfo = xmsCliTeleminfo.SpeInfo;
                        httpEvent.ServerErrorCode = xmsCliTeleminfo.ServerErrorCode;
                        httpEvent.ServerSubErrorCode = xmsCliTeleminfo.ServerSubErrorCode;
                    }
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    try
                    {
                        httpEvent.OauthErrorCode = MsalError.UnknownError;
                        // In cases where the end-point is not found (404) response.body will be empty.
                        // CreateResponse handles throwing errors - in the case of HttpStatusCode <> and ErrorResponse will be created.
                        if (!string.IsNullOrWhiteSpace(response.Body))
                        {
                            var msalTokenResponse = JsonHelper.DeserializeFromJson<MsalTokenResponse>(response.Body);
                            if (msalTokenResponse != null)
                            {
                                httpEvent.OauthErrorCode = msalTokenResponse.Error;
                            }
                        }
                    }
                    catch (SerializationException) // in the rare case we get an error response we cannot deserialize
                    {
                        // CreateErrorResponse does the same validation. Will be logging the error there.
                    }
                }
            }

            return CreateResponse<T>(response, requestContext, addCorrelationId);
        }

        public static T CreateResponse<T>(HttpResponse response, RequestContext requestContext, bool addCorrelationId)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                CreateErrorResponse(response, requestContext);
            }

            if (addCorrelationId)
            {
                VerifyCorrelationIdHeaderInResponse(response.HeadersAsDictionary, requestContext);
            }

            return JsonHelper.DeserializeFromJson<T>(response.Body);
        }

        public static void CreateErrorResponse(HttpResponse response, RequestContext requestContext)
        {
            bool shouldLogAsError = true;

            var httpErrorCodeMessage = string.Format(CultureInfo.InvariantCulture, "HttpStatusCode: {0}: {1}", (int)response.StatusCode, response.StatusCode.ToString());
            requestContext.Logger.Info(httpErrorCodeMessage);

            Exception exceptionToThrow = null;

            try
            {
                exceptionToThrow = ExtractErrorsFromTheResponse(response, ref shouldLogAsError);
            }
            catch (SerializationException) // in the rare case we get an error response we cannot deserialize
            {
                exceptionToThrow = new MsalServiceException(
                    MsalError.NonParsableOAuthError,
                    MsalErrorMessage.NonParsableOAuthError)
                {
                    HttpResponse = response
                };
            }
            catch (Exception ex)
            {
                exceptionToThrow = new MsalServiceException(MsalError.UnknownError, response.Body, ex)
                {
                    HttpResponse = response
                };
            }

            if (exceptionToThrow == null)
            {
                exceptionToThrow = new MsalServiceException(
                    response.StatusCode == HttpStatusCode.NotFound
                        ? MsalError.HttpStatusNotFound
                        : MsalError.HttpStatusCodeNotOk, httpErrorCodeMessage)
                {
                    HttpResponse = response
                };
            }

            if (shouldLogAsError)
            {
                requestContext.Logger.ErrorPii(exceptionToThrow);
            }
            else
            {
                requestContext.Logger.InfoPii(exceptionToThrow);
            }

            throw exceptionToThrow;
        }

        private static Exception ExtractErrorsFromTheResponse(HttpResponse response, ref bool shouldLogAsError)
        {
            Exception exceptionToThrow = null;

            // In cases where the end-point is not found (404) response.body will be empty.
            if (string.IsNullOrWhiteSpace(response.Body))
            {
                return null;
            }

            var msalTokenResponse = JsonHelper.DeserializeFromJson<MsalTokenResponse>(response.Body);

            if (msalTokenResponse?.Error == null)
            {
                return null;
            }

            if (MsalError.InvalidGrantError.Equals(msalTokenResponse.Error, StringComparison.OrdinalIgnoreCase))
            {
                exceptionToThrow = new MsalUiRequiredException(
                    MsalError.InvalidGrantError,
                    msalTokenResponse.ErrorDescription)
                {
                    HttpResponse = response
                };
            }
            else
            {
                exceptionToThrow = new MsalServiceException(
                    msalTokenResponse.Error,
                    msalTokenResponse.ErrorDescription)
                {
                    HttpResponse = response
                };
            }

            // For device code flow, AuthorizationPending can occur a lot while waiting
            // for the user to auth via browser and this causes a lot of error noise in the logs.
            // So suppress this particular case to an Info so we still see the data but don't
            // log it as an error since it's expected behavior while waiting for the user.
            if (string.Compare(msalTokenResponse.Error, OAuth2Error.AuthorizationPending,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                shouldLogAsError = false;
            }

            return exceptionToThrow;
        }

        private Uri CreateFullEndpointUri(Uri endPoint)
        {
            var endpointUri = new UriBuilder(endPoint);
            string extraQp = _queryParameters.ToQueryParameter();
            endpointUri.AppendQueryParameters(extraQp);

            return endpointUri.Uri;
        }

        private static void VerifyCorrelationIdHeaderInResponse(
            IDictionary<string, string> headers,
            RequestContext requestContext)
        {
            foreach (string responseHeaderKey in headers.Keys)
            {
                string trimmedKey = responseHeaderKey.Trim();
                if (string.Compare(trimmedKey, OAuth2Header.CorrelationId, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    string correlationIdHeader = headers[trimmedKey].Trim();
                    if (string.Compare(
                            correlationIdHeader,
                            requestContext.Logger.CorrelationId.ToString(),
                            StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        requestContext.Logger.WarningPii(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Returned correlation id '{0}' does not match the sent correlation id '{1}'",
                                correlationIdHeader,
                                requestContext.Logger.CorrelationId),
                            "Returned correlation id does not match the sent correlation id");
                    }

                    break;
                }
            }
        }
    }
}
