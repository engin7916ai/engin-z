﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Json;
using Microsoft.Identity.Json.Linq;

using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace Microsoft.Identity.Client.Kerberos
{
    /// <summary>
    /// Helper class to manage Kerberos Ticket Claims.
    /// </summary>
    public static class KerberosSupplementalTicketManager
    {
        private const int _defaultLogonId = 0;
        private const string _kerberosClaimType = "xms_as_rep";
        private const string _idTokenAsRepTemplate = @"{{""id_token"": {{ ""xms_as_rep"":{{""essential"":""false"",""value"":""{0}""}} }} }}";
        private const string _accessTokenAsRepTemplate = @"{{""access_token"": {{ ""xms_as_rep"":{{""essential"":""false"",""value"":""{0}""}} }} }}";

        /// <summary>
        /// Creates a <see cref="KerberosSupplementalTicket"/> object from given ID token string..
        /// </summary>
        /// <param name="idToken">ID token string.</param>
        /// <returns>A <see cref="KerberosSupplementalTicket"/> object if a Kerberos Ticket Claim exists in the given
        /// idToken parameter and is parsed correctly. Null, otherwise.</returns>
        public static KerberosSupplementalTicket FromIdToken(string idToken)
        {
            if (string.IsNullOrEmpty(idToken) || idToken.Length < 128)
            {
                // Token is empty or too short - ignore parsing.
                return null;
            }

            string[] sections = idToken.Split('.');
            if (sections.Length != 3)
            {
                // JWT should be consists of 3 parts separated with '.'
                return null;
            }

            // decodes the second section containing the Kerberos Ticket claim if exists.
            byte[] payloadBytes = Base64UrlHelpers.DecodeToBytes(sections[1]);
            string payload = Encoding.UTF8.GetString(payloadBytes);
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            // parse the JSON data and find the included Kerberos Ticket claim.
            JObject payloadJson = JObject.Parse(payload);
            JToken claimValue;
            if (!payloadJson.TryGetValue(_kerberosClaimType, out claimValue))
            {
                return null;
            }

            // Kerberos Ticket claim found.
            // Parse the json and construct the KerberosSupplementalTicket object.
            string kerberosAsRep = claimValue.Value<string>();
            return (KerberosSupplementalTicket)JsonConvert.DeserializeObject(kerberosAsRep, typeof(KerberosSupplementalTicket));
        }

        /// <summary>
        /// Save current Kerberos Ticket to current user's Ticket Cache.
        /// </summary>
        /// <param name="ticket">Kerberos ticket object to save.</param>
        /// <remarks>Can throws <see cref="ArgumentException"/> when given ticket parameter is not a valid Kerberos Supplemental Ticket.
        /// Can throws <see cref="Win32Exception"/> if error occurs while saving ticket information into Ticket Cache.
        /// </remarks>
        public static void SaveToWindowsTicketCache(KerberosSupplementalTicket ticket)
        {
            SaveToWindowsTicketCache(ticket, _defaultLogonId);
        }

        /// <summary>
         /// Save current Kerberos Ticket to current user's Ticket Cache.
         /// </summary>
         /// <param name="ticket">Kerberos ticket object to save.</param>
         /// <param name="logonId">The Logon Id of the user owning the ticket cache.
         /// The default of 0 represents the currently logged on user.</param>
         /// <remarks>Can throws <see cref="ArgumentException"/> when given ticket parameter is not a valid Kerberos Supplemental Ticket.
         /// Can throws <see cref="Win32Exception"/> if error occurs while saving ticket information into Ticket Cache.
         /// </remarks>
        public static void SaveToWindowsTicketCache(KerberosSupplementalTicket ticket, long logonId)
        {
#if (iOS || MAC || ANDROID)
            throw new NotSupportedException("Ticket Cache interface is not supported for this OS platform.");
#else
            if (ticket == null || string.IsNullOrEmpty(ticket.KerberosMessageBuffer))
            {
                throw new ArgumentException("Kerberos Ticket information is not valid");
            }

            using (var cache = Win32.TicketCacheWriter.Connect())
            {
                byte[] krbCred = Convert.FromBase64String(ticket.KerberosMessageBuffer);
                cache.ImportCredential(krbCred, logonId);
            }
#endif
        }

        /// <summary>
        /// Reads a Kerberos Service Ticket associated with given service principal name from
        /// current user's Ticket Cache.
        /// </summary>
        /// <param name="servicePrincipalName">Service principal name to find associated Kerberos Ticket.</param>
        /// <returns>Byte stream of searched Kerberos Ticket information if exists. Null, otherwise.</returns>
        /// <remarks>
        /// Can throws <see cref="Win32Exception"/> if error occurs while searching ticket information from Ticket Cache.
        /// </remarks>
        public static byte[] GetKerberosTicketFromWindowsTicketCache(string servicePrincipalName)
        {
            return GetKerberosTicketFromWindowsTicketCache(servicePrincipalName, _defaultLogonId);
        }

        /// <summary>
        /// Reads a Kerberos Service Ticket associated with given service principal name from
        /// current user's Ticket Cache.
        /// </summary>
        /// <param name="servicePrincipalName">Service principal name to find associated Kerberos Ticket.</param>
        /// <param name="logonId">The Logon Id of the user owning the ticket cache.
        /// The default of 0 represents the currently logged on user.</param>
        /// <returns>Byte stream of searched Kerberos Ticket information if exists. Null, otherwise.</returns>
        /// <remarks>
        /// Can throws <see cref="Win32Exception"/> if error occurs while searching ticket information from Ticket Cache.
        /// </remarks>
        public static byte[] GetKerberosTicketFromWindowsTicketCache(string servicePrincipalName, long logonId)
        {
#if (iOS || MAC || ANDROID)
            throw new NotSupportedException("Ticket Cache interface is not supported for this OS platform.");
#else
            using (var reader = new Win32.TicketCacheReader(servicePrincipalName, logonId))
            {
                return reader.RequestToken();
            }
#endif
        }

        /// <summary>
        /// Gets the KRB-CRED Kerberos Ticket information as byte stream.
        /// </summary>
        /// <param name="ticket">Kerberos ticket object to save.</param>
        /// <returns>Byte stream representaion of KRB-CRED Kerberos Ticket if it contains valid ticket information.
        /// Null, otherwise.</returns>
        public static byte[] GetKrbCred(KerberosSupplementalTicket ticket)
        {
            if (!string.IsNullOrEmpty(ticket.KerberosMessageBuffer))
            {
                return Convert.FromBase64String(ticket.KerberosMessageBuffer);
            }

            return null;
        }

        /// <summary>
        /// Add Claims to body parameter for POST request.
        /// </summary>
        /// <param name="oAuth2Client"><see cref="OAuth2Client"/> object for Token request.</param>
        /// <param name="requestParams"><see cref="AuthenticationRequestParameters"/> containing request parameters.</param>
        internal static void AddKerberosTicketClaim(
            OAuth2Client oAuth2Client,
            AuthenticationRequestParameters requestParams)
        {
            string kerberosClaim = GetKerberosTicketClaim(
                requestParams.RequestContext.ServiceBundle.Config.KerberosServicePrincipalName,
                requestParams.RequestContext.ServiceBundle.Config.TicketContainer);

            if (string.IsNullOrEmpty(kerberosClaim))
            {
                oAuth2Client.AddBodyParameter(OAuth2Parameter.Claims, requestParams.ClaimsAndClientCapabilities);
            }
            else if (string.IsNullOrEmpty(requestParams.ClaimsAndClientCapabilities))
            {
                oAuth2Client.AddBodyParameter(OAuth2Parameter.Claims, kerberosClaim);
            }
            else
            {
                JObject existingClaims = JObject.Parse(requestParams.ClaimsAndClientCapabilities);
                JObject mergedClaims
                    = ClaimsHelper.MergeClaimsIntoCapabilityJson(kerberosClaim, existingClaims);

                oAuth2Client.AddBodyParameter(OAuth2Parameter.Claims, mergedClaims.ToString(Formatting.None));
            }
        }

        /// <summary>
        /// Generate a Kerberos Ticket Claim string.
        /// </summary>
        /// <param name="servicePrincipalName">Service principal name to use.</param>
        /// <param name="ticketContainer">Ticket container to use.</param>
        /// <returns>A Kerberos Ticket Claim string if valid service principal name was given. Empty string, otherwise.</returns>
        internal static string GetKerberosTicketClaim(string servicePrincipalName, KerberosTicketContainer ticketContainer)
        {
            if (string.IsNullOrEmpty(servicePrincipalName))
            {
                return string.Empty;
            }

            if (ticketContainer == KerberosTicketContainer.IdToken)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    _idTokenAsRepTemplate,
                    servicePrincipalName);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                _accessTokenAsRepTemplate,
                servicePrincipalName);
        }
    }
}
