﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.TelemetryCore;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Identity.Client.WsTrust
{
    internal class WsTrustWebRequestManager : IWsTrustWebRequestManager
    {
        private readonly IHttpManager _httpManager;

        public WsTrustWebRequestManager(IHttpManager httpManager)
        {
            _httpManager = httpManager;
        }

        /// <inheritdoc/>
        public async Task<MexDocument> GetMexDocumentAsync(string federationMetadataUrl, RequestContext requestContext)
        {
            IDictionary<string, string> msalIdParams = MsalIdHelper.GetMsalIdParameters(requestContext.Logger);

            var uri = new UriBuilder(federationMetadataUrl);
            HttpResponse httpResponse = await _httpManager.SendGetAsync(uri.Uri, msalIdParams, requestContext.Logger).ConfigureAwait(false);
            if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                string message = string.Format(CultureInfo.CurrentCulture,
                        MsalErrorMessage.HttpRequestUnsuccessful + "See https://aka.ms/msal-net-ropc for more information. ",
                        (int)httpResponse.StatusCode, httpResponse.StatusCode);

                throw MsalServiceExceptionFactory.FromHttpResponse(
                    MsalError.AccessingWsMetadataExchangeFailed,
                    message,
                    httpResponse);
            }

            var mexDoc = new MexDocument(httpResponse.Body);

            requestContext.Logger.InfoPii(
                $"MEX document fetched and parsed from '{federationMetadataUrl}'",
                "Fetched and parsed MEX");

            return mexDoc;
        }

        /// <inheritdoc/>
        public async Task<WsTrustResponse> GetWsTrustResponseAsync(
            WsTrustEndpoint wsTrustEndpoint,
            string wsTrustRequest,
            RequestContext requestContext)
        {
            var headers = new Dictionary<string, string>
            {
                { "SOAPAction", (wsTrustEndpoint.Version == WsTrustVersion.WsTrust2005) ? XmlNamespace.Issue2005.ToString() : XmlNamespace.Issue.ToString() }
            };

            var body = new StringContent(
                wsTrustRequest,
                Encoding.UTF8, "application/soap+xml");

            HttpResponse resp = await _httpManager.SendPostForceResponseAsync(wsTrustEndpoint.Uri, headers, body, requestContext.Logger).ConfigureAwait(false);

            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                string errorMessage = null;
                try
                {
                    errorMessage = WsTrustResponse.ReadErrorResponse(XDocument.Parse(resp.Body, LoadOptions.None), requestContext);
                }
                catch (System.Xml.XmlException)
                {
                    errorMessage = resp.Body;
                }

                string message = string.Format(
                        CultureInfo.CurrentCulture,
                        MsalErrorMessage.FederatedServiceReturnedErrorTemplate,
                        wsTrustEndpoint.Uri,
                        errorMessage);

                throw MsalServiceExceptionFactory.FromHttpResponse(
                    MsalError.FederatedServiceReturnedError,
                    message,
                    resp);
            }

            try
            {
                return WsTrustResponse.CreateFromResponse(resp.Body, wsTrustEndpoint.Version);
            }
            catch (System.Xml.XmlException ex)
            {
                string message = string.Format(
                        CultureInfo.CurrentCulture,
                        MsalErrorMessage.ParsingWsTrustResponseFailedErrorTemplate,
                        wsTrustEndpoint.Uri,
                        resp.Body);

                throw new MsalClientException(
                    MsalError.ParsingWsTrustResponseFailed, message, ex);
            }
        }

        public async Task<UserRealmDiscoveryResponse> GetUserRealmAsync(
            string userRealmUriPrefix,
            string userName,
            RequestContext requestContext)
        {
            requestContext.Logger.Info("Sending request to userrealm endpoint. ");

            IDictionary<string, string> msalIdParams = MsalIdHelper.GetMsalIdParameters(requestContext.Logger);

            var uri = new UriBuilder(userRealmUriPrefix + userName + "?api-version=1.0").Uri;
            
            var httpResponse = await _httpManager.SendGetAsync(
                uri,
                msalIdParams,
                requestContext.Logger).ConfigureAwait(false);

            return httpResponse.StatusCode == System.Net.HttpStatusCode.OK
                ? JsonHelper.DeserializeFromJson<UserRealmDiscoveryResponse>(httpResponse.Body)
                : null;
        }
    }
}
