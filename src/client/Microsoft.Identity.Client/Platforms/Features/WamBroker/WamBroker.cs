﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Identity.Client.ApiConfig.Parameters;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.Internal.Broker;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Client.OAuth2;
using Microsoft.Identity.Client.UI;
using Windows.Foundation.Metadata;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;

#if DESKTOP || NET5_WIN
using Microsoft.Identity.Client.Platforms.Features.Windows;
using System.Runtime.InteropServices;
using System.Windows.Forms;
#endif


namespace Microsoft.Identity.Client.Platforms.Features.WamBroker
{
    internal class WamBroker : IBroker
    {
        private readonly IWamPlugin _aadPlugin;
        private readonly IWamPlugin _msaPlugin;
        private readonly IWamProxy _wamProxy;
        private readonly IWebAccountProviderFactory _webAccountProviderFactory;
        private readonly IAccountPickerFactory _accountPickerFactory;
        private readonly ICoreLogger _logger;
        private readonly IntPtr _parentHandle;
        private readonly SynchronizationContext _synchronizationContext;
        private const string WamErrorPrefix = "WAM Error ";
        private const string ErrorMessageSuffix = " For more details see https://aka.ms/msal-net-wam";


        public WamBroker(
            CoreUIParent uiParent,
            ICoreLogger logger,
            IWamPlugin testAadPlugin = null,
            IWamPlugin testmsaPlugin = null,
            IWamProxy wamProxy = null,
            IWebAccountProviderFactory webAccountProviderFactory = null,
            IAccountPickerFactory accountPickerFactory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _wamProxy = wamProxy ?? new WamProxy(_logger);
            _parentHandle = GetParentWindow(uiParent);
            _synchronizationContext = uiParent?.SynchronizationContext;

            _webAccountProviderFactory = webAccountProviderFactory ?? new WebAccountProviderFactory();
            _accountPickerFactory = accountPickerFactory ?? new AccountPickerFactory();
            _aadPlugin = testAadPlugin ?? new AadPlugin(_wamProxy, _webAccountProviderFactory, _logger);
            _msaPlugin = testmsaPlugin ?? new MsaPlugin(_wamProxy, _webAccountProviderFactory, _logger);
        }

        /// <summary>
        /// In WAM, AcquireTokenInteractive is always associated to an account. WAM also allows for an "account picker" to be displayed, 
        /// which is similar to the EVO browser experience, allowing the user to add an account or use an existing one.
        /// 
        /// MSAL does not have a concept of account picker so MSAL.AccquireTokenInteractive will: 
        /// 
        /// 1. Call WAM.AccountPicker if an IAccount (or possibly login_hint) is not configured
        /// 2. Figure out the WAM.AccountID associated to the MSAL.Account
        /// 3. Call WAM.AcquireTokenInteractive with the WAM.AccountID
        /// 
        /// To make matters more complicated, WAM has 2 plugins - AAD and MSA. With AAD plugin, 
        /// it is possible to list all WAM accounts and find the one associated to the MSAL account. 
        /// However, MSA plugin does NOT allow listing of accounts, and the only way to figure out the 
        /// WAM account ID is to use the account picker. This makes AcquireTokenSilent impossible for MSA, 
        /// because we would not be able to map an Msal.Account to a WAM.Account. To overcome this, 
        /// we save the WAM.AccountID in MSAL's cache. 
        /// </summary>
        public async Task<MsalTokenResponse> AcquireTokenInteractiveAsync(
            AuthenticationRequestParameters authenticationRequestParameters,
            AcquireTokenInteractiveParameters acquireTokenInteractiveParameters)
        {
            if (_synchronizationContext == null)
            {
                throw new MsalClientException(
                    MsalError.WamUiThread,
                    "AcquireTokenInteractive with broker must be called from the UI thread when using WAM. " +
                    "Note that console applications are not currently supported in conjuction with WAM." + ErrorMessageSuffix);
            }

            if (authenticationRequestParameters.Account != null ||
                !string.IsNullOrEmpty(authenticationRequestParameters.LoginHint))
            {
                bool isMsa = await IsMsaRequestAsync(
                    authenticationRequestParameters.Authority,
                    authenticationRequestParameters?.Account?.HomeAccountId?.TenantId, // TODO: we could furher optimize here by searching for an account based on UPN
                    IsMsaPassthrough(authenticationRequestParameters)).ConfigureAwait(false);

                IWamPlugin wamPlugin = isMsa ? _msaPlugin : _aadPlugin;
                WebAccountProvider provider;
                provider = await GetProviderAsync(authenticationRequestParameters.Authority.AuthorityInfo.CanonicalAuthority, isMsa)
                    .ConfigureAwait(false);

                if (PublicClientApplication.IsOperatingSystemAccount(authenticationRequestParameters.Account))
                {
                    var wamResult = await AcquireInteractiveWithoutPickerAsync(
                        authenticationRequestParameters,
                        acquireTokenInteractiveParameters.Prompt,
                        wamPlugin,
                        provider,
                        null)
                        .ConfigureAwait(false);
                    return CreateMsalTokenResponse(wamResult, wamPlugin, isInteractive: true);
                }

                var wamAccount = await FindWamAccountForMsalAccountAsync(
                    provider,
                    wamPlugin,
                    authenticationRequestParameters.Account,
                    authenticationRequestParameters.LoginHint,
                    authenticationRequestParameters.ClientId).ConfigureAwait(false);

                if (wamAccount != null)
                {
                    var wamResult = await AcquireInteractiveWithoutPickerAsync(
                        authenticationRequestParameters,
                        acquireTokenInteractiveParameters.Prompt,
                        wamPlugin,
                        provider,
                        wamAccount)
                        .ConfigureAwait(false);
                    return CreateMsalTokenResponse(wamResult, wamPlugin, isInteractive: true);
                }
            }

            return await AcquireInteractiveWithPickerAsync(
                authenticationRequestParameters)
                .ConfigureAwait(false);
        }

        internal /* internal for test only */ static bool IsForceLoginPrompt(Prompt prompt)
        {
            if (prompt == Prompt.ForceLogin || prompt == Prompt.SelectAccount || prompt == Prompt.Consent)
            {
                return true;
            }

            return false;
        }

        private async Task<IWebTokenRequestResultWrapper> AcquireInteractiveWithoutPickerAsync(
            AuthenticationRequestParameters authenticationRequestParameters,
            Prompt prompt,
            IWamPlugin wamPlugin,
            WebAccountProvider provider,
            WebAccount wamAccount)
        {
            bool isForceLoginPrompt = IsForceLoginPrompt(prompt);

            WebTokenRequest webTokenRequest = await wamPlugin.CreateWebTokenRequestAsync(
                provider,
                authenticationRequestParameters,
                isForceLoginPrompt: isForceLoginPrompt,
                isInteractive: true,
                isAccountInWam: true)
           .ConfigureAwait(false);

            if (isForceLoginPrompt &&
                ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 6))
            {
                // this feature works correctly since windows RS4, aka 1803 with the AAD plugin only!
                webTokenRequest.Properties["prompt"] = prompt.PromptValue;
            }

            AddCommonParamsToRequest(authenticationRequestParameters, webTokenRequest);

            try
            {
#if WINDOWS_APP
                // UWP requires being on the UI thread
                await _synchronizationContext;
#endif
                IWebTokenRequestResultWrapper wamResult;
                if (wamAccount != null)
                {
                    wamResult = await _wamProxy.RequestTokenForWindowAsync(
                        _parentHandle,
                        webTokenRequest,
                        wamAccount).ConfigureAwait(false);
                }
                else
                {
                    // default user
                    wamResult = await _wamProxy.RequestTokenForWindowAsync(
                          _parentHandle,
                          webTokenRequest).ConfigureAwait(false);
                }
                return wamResult;

            }
            catch (Exception ex)
            {
                _logger.ErrorPii(ex);
                throw new MsalServiceException(
                    MsalError.WamInteractiveError,
                    "AcquireTokenInteractive without picker failed. See inner exception for details. ", ex);
            }
        }

        private void AddCommonParamsToRequest(AuthenticationRequestParameters authenticationRequestParameters, WebTokenRequest webTokenRequest)
        {
            AddExtraParamsToRequest(webTokenRequest, authenticationRequestParameters.ExtraQueryParameters);
            AddAuthorityParamToRequest(authenticationRequestParameters, webTokenRequest);
            AddPOPParamsToRequest(webTokenRequest);
        }

        private static void AddAuthorityParamToRequest(AuthenticationRequestParameters authenticationRequestParameters, WebTokenRequest webTokenRequest)
        {
            webTokenRequest.Properties.Add(
                            "authority",
                            authenticationRequestParameters.UserConfiguredAuthority.AuthorityInfo.CanonicalAuthority);
            webTokenRequest.Properties.Add(
                "validateAuthority",
                authenticationRequestParameters.AuthorityInfo.ValidateAuthority ? "yes" : "no");
        }

        private async Task<MsalTokenResponse> AcquireInteractiveWithPickerAsync(
            AuthenticationRequestParameters authenticationRequestParameters)
        {
            bool isMsaPassthrough = IsMsaPassthrough(authenticationRequestParameters);
            var accountPicker = _accountPickerFactory.Create(
                _parentHandle,
                _logger,
                _synchronizationContext,
                authenticationRequestParameters.Authority,
                isMsaPassthrough);

            IWamPlugin wamPlugin;
            WebTokenRequest webTokenRequest;
            try
            {
                WebAccountProvider accountProvider = await
                    accountPicker.DetermineAccountInteractivelyAsync().ConfigureAwait(false);

                if (accountProvider == null)
                {
                    throw new MsalClientException(MsalError.AuthenticationCanceledError, "WAM Account Picker did not return an account.");
                }

                // WAM returns the tenant here, not the full authority
                bool isConsumerTenant = string.Equals(accountProvider.Authority, "consumers", StringComparison.OrdinalIgnoreCase);

                string transferToken = null;

                // for MSA-PT, the AAD plugin needs to handle the this but it needs a special transfer token first
                if (isConsumerTenant && isMsaPassthrough)
                {
                    var webTokenRequestMsa = await _msaPlugin.CreateWebTokenRequestAsync(
                      accountProvider,
                      authenticationRequestParameters,
                      isForceLoginPrompt: false,
                      isInteractive: true,
                      isAccountInWam: false)
                     .ConfigureAwait(false);


#if WINDOWS_APP
                await _synchronizationContext;
#endif
                    var webTokenResponseMsa = await _wamProxy.RequestTokenForWindowAsync(_parentHandle, webTokenRequestMsa)
                        .ConfigureAwait(true);

                    if (webTokenResponseMsa.ResponseStatus != WebTokenRequestStatus.Success)
                    {
                        var errorResp = CreateMsalTokenResponse(webTokenResponseMsa, _msaPlugin, true);
                        throw new MsalServiceException(
                            errorResp.Error,
                            "Error fetching the MSA-PT initial token - " + errorResp.ErrorDescription);
                    }

                    transferToken = await FetchMsaPassthroughTransferTokenAsync(                        
                        accountProvider,
                        authenticationRequestParameters.ClientId).ConfigureAwait(true);

                    var aadRequest = await _aadPlugin.CreateWebTokenRequestAsync(
                        accountProvider,
                        authenticationRequestParameters,
                        isForceLoginPrompt: false,
                        isInteractive: true,
                        isAccountInWam: true)
                       .ConfigureAwait(true);
                    AddCommonParamsToRequest(authenticationRequestParameters, webTokenRequestMsa);

                    var wamResult2 = await 
                        _wamProxy.RequestTokenForWindowAsync(
                            _parentHandle, 
                            aadRequest, 
                            webTokenResponseMsa.ResponseData[0].WebAccount)
                        .ConfigureAwait(false);

                    
                    return CreateMsalTokenResponse(wamResult2, _aadPlugin, isInteractive: true);

                }

#if WINDOWS_APP
                await _synchronizationContext;
#endif
                wamPlugin = (isConsumerTenant && !isMsaPassthrough) ? _msaPlugin : _aadPlugin;
                webTokenRequest = await wamPlugin.CreateWebTokenRequestAsync(
                     accountProvider,
                     authenticationRequestParameters,
                     isForceLoginPrompt: false,
                     isInteractive: true,
                     isAccountInWam: false)
                    .ConfigureAwait(true);

                if (!string.IsNullOrEmpty(transferToken))
                {
                    webTokenRequest.Properties.Add("SamlAssertion", transferToken);
                    webTokenRequest.Properties.Add("SamlAssertionType", "SAMLV1");
                }

                AddCommonParamsToRequest(authenticationRequestParameters, webTokenRequest);

            }
            catch (Exception ex) when (!(ex is MsalException))
            {
                _logger.ErrorPii(ex);
                throw new MsalServiceException(
                    MsalError.WamPickerError,
                    "Could not get the account provider - account picker. See inner exception for details", ex);
            }

            IWebTokenRequestResultWrapper wamResult;
            try
            {
                wamResult = await _wamProxy.RequestTokenForWindowAsync(_parentHandle, webTokenRequest).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorPii(ex);
                throw new MsalServiceException(
                    MsalError.WamPickerError,
                    "Could not get the result - account picker. See inner exception for details", ex);
            }

            return CreateMsalTokenResponse(wamResult, wamPlugin, isInteractive: true);
        }

        private async Task<string> FetchMsaPassthroughTransferTokenAsync(
            WebAccountProvider accountProvider, 
            string clientId)
        {
#if WINDOWS_APP
                    await _synchronizationContext;
#endif
            var transferTokenRequest = await _msaPlugin.CreateWebTokenRequestAsync(
                      accountProvider,
                      clientId,
                      "service::http://Passport.NET/purpose::PURPOSE_AAD_WAM_TRANSFER")
                      .ConfigureAwait(true);

            //transferTokenRequest.Properties.Add(
            //              "authority",
            //              "https://login.microsoftonline.com/consumers");            

            var transferResponse = await _wamProxy.RequestTokenForWindowAsync(_parentHandle, transferTokenRequest)
                .ConfigureAwait(false);
            if (transferResponse.ResponseStatus != WebTokenRequestStatus.Success)
            {
                var errorResp = CreateMsalTokenResponse(transferResponse, _msaPlugin, true);
                throw new MsalServiceException(
                    errorResp.Error, 
                    "Error fetching the MSA-PT transfer token - " + errorResp.ErrorDescription);
            }

            // var result = wamPlugin.ParseNonTokenResponse(transferResponse.ResponseData[0]);

            // return result["code"];
            return transferResponse.ResponseData[0].Token;
        }

        private IntPtr GetParentWindow(CoreUIParent uiParent)
        {
#if WINDOWS_APP
            // On UWP there is no need for a window handle
            return IntPtr.Zero;
#else

            if (uiParent?.OwnerWindow is IntPtr ptr)
            {
                _logger.Info("Owner window specified as IntPtr.");
                return ptr;
            }

            if (uiParent?.OwnerWindow is IWin32Window window)
            {
                _logger.Info("Owner window specified as IWin32Window.");
                return window.Handle;
            }

            return WindowsNativeMethods.GetForegroundWindow();
#endif

        }

        private void AddPOPParamsToRequest(WebTokenRequest webTokenRequest)
        {
            // TODO: add POP support by adding "token_type" = "pop" and "req_cnf" = req_cnf
        }

        private bool IsMsaPassthrough(AuthenticationRequestParameters authenticationRequestParameters)
        {
            // TODO: not currently working
            //return 
            //    authenticationRequestParameters.ExtraQueryParameters.TryGetValue("msal_msa_pt", out string val) &&
            //    string.Equals("1", val);
            return true;
        }

        public async Task<MsalTokenResponse> AcquireTokenSilentAsync(
            AuthenticationRequestParameters authenticationRequestParameters,
            AcquireTokenSilentParameters acquireTokenSilentParameters)
        {
            using (_logger.LogMethodDuration())
            {
                // Important: MSAL will have already resolved the authority by now, 
                // so we are not expecting "common" or "organizations" but a tenanted authority
                bool isMsa = await IsMsaRequestAsync(
                    authenticationRequestParameters.Authority,
                    null,
                    IsMsaPassthrough(authenticationRequestParameters)).ConfigureAwait(false);

                IWamPlugin wamPlugin = isMsa ? _msaPlugin : _aadPlugin;
                WebAccountProvider provider = await GetProviderAsync(
                    authenticationRequestParameters.Authority.AuthorityInfo.CanonicalAuthority,
                    isMsa).ConfigureAwait(false);

                WebAccount webAccount = await FindWamAccountForMsalAccountAsync(
                    provider,
                    wamPlugin,
                    authenticationRequestParameters.Account,
                    null, // ATS requires an account object, login_hint is not supported on its own
                    authenticationRequestParameters.ClientId).ConfigureAwait(false);

                if (webAccount == null)
                {
                    throw new MsalUiRequiredException(
                        MsalError.InteractionRequired,
                        "Could not find a WAM account for the silent request.");
                }

                WebTokenRequest webTokenRequest = await wamPlugin.CreateWebTokenRequestAsync(
                    provider,
                    authenticationRequestParameters,
                    isForceLoginPrompt: false,
                    isAccountInWam: true,
                    isInteractive: false)
                    .ConfigureAwait(false);

                AddCommonParamsToRequest(authenticationRequestParameters, webTokenRequest);

                var wamResult =
                    await _wamProxy.GetTokenSilentlyAsync(webAccount, webTokenRequest).ConfigureAwait(false);

                return CreateMsalTokenResponse(wamResult, wamPlugin, isInteractive: false);
            }
        }

        private async Task<WebAccountProvider> GetProviderAsync(
            string authority,
            bool isMsa)
        {
            WebAccountProvider provider;
            string tenantOrAuthority = isMsa ? "consumers" : authority;
            provider = await _webAccountProviderFactory.GetAccountProviderAsync(tenantOrAuthority)
                    .ConfigureAwait(false);
            return provider;
        }



        public async Task<MsalTokenResponse> AcquireTokenSilentDefaultUserAsync(
            AuthenticationRequestParameters authenticationRequestParameters,
            AcquireTokenSilentParameters acquireTokenSilentParameters)
        {
            using (_logger.LogMethodDuration())
            {
                bool isMsa = await IsMsaRequestAsync(
                    authenticationRequestParameters.Authority,
                    null,
                    IsMsaPassthrough(authenticationRequestParameters)).ConfigureAwait(false);

                IWamPlugin wamPlugin = isMsa ? _msaPlugin : _aadPlugin;
                WebAccountProvider provider = await GetProviderAsync(
                    authenticationRequestParameters.Authority.AuthorityInfo.CanonicalAuthority,
                    isMsa).ConfigureAwait(false);

                WebTokenRequest webTokenRequest = await wamPlugin.CreateWebTokenRequestAsync(
                    provider,
                    authenticationRequestParameters,
                    isForceLoginPrompt: false,
                    isAccountInWam: false,
                    isInteractive: false)
                    .ConfigureAwait(false);

                AddCommonParamsToRequest(authenticationRequestParameters, webTokenRequest);

                var wamResult =
                    await _wamProxy.GetTokenSilentlyForDefaultAccountAsync(webTokenRequest).ConfigureAwait(false);

                return CreateMsalTokenResponse(wamResult, wamPlugin, isInteractive: false);
            }
        }

        private async Task<WebAccount> FindWamAccountForMsalAccountAsync(
           WebAccountProvider provider,
           IWamPlugin wamPlugin,
           IAccount msalAccount,
           string loginHint,
           string clientId)
        {
            if (msalAccount == null && string.IsNullOrEmpty(loginHint))
            {
                return null;
            }

            IAccountInternal accountInternal = (msalAccount as IAccountInternal);
            if (accountInternal?.WamAccountIds != null &&
                accountInternal.WamAccountIds.TryGetValue(clientId, out string wamAccountId))
            {
                _logger.Info("WAM will try to find an account based on the wam account id from the cache");
                WebAccount result = await _wamProxy.FindAccountAsync(provider, wamAccountId).ConfigureAwait(false);
                if (result != null)
                {
                    return result;
                }

                _logger.Warning("WAM account was not found for given wam account id.");
            }

            var wamAccounts = await _wamProxy.FindAllWebAccountsAsync(provider, clientId).ConfigureAwait(false);
            return MatchWamAccountToMsalAccount(
                wamPlugin,
                msalAccount,
                loginHint,
                wamAccounts);
        }

        private static WebAccount MatchWamAccountToMsalAccount(
            IWamPlugin wamPlugin,
            IAccount account,
            string loginHint,
            IEnumerable<WebAccount> wamAccounts)
        {
            WebAccount matchedAccountByLoginHint = null;
            foreach (var wamAccount in wamAccounts)
            {
                string homeAccountId = wamPlugin.GetHomeAccountIdOrNull(wamAccount);
                if (string.Equals(homeAccountId, account?.HomeAccountId?.Identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return wamAccount;
                }

                if (string.Equals(loginHint, wamAccount.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    matchedAccountByLoginHint = wamAccount;
                }
            }

            return matchedAccountByLoginHint;
        }

        public async Task<IEnumerable<IAccount>> GetAccountsAsync(string clientID, string redirectUri)
        {
            using (_logger.LogMethodDuration())
            {
                if (!ApiInformation.IsMethodPresent(
                    "Windows.Security.Authentication.Web.Core.WebAuthenticationCoreManager",
                    "FindAllAccountsAsync"))
                {
                    _logger.Info("WAM::FindAllAccountsAsync method does not exist. Returning 0 broker accounts. ");
                    return Enumerable.Empty<IAccount>();
                }

                var aadAccounts = await _aadPlugin.GetAccountsAsync(clientID).ConfigureAwait(false);
                var msaAccounts = await _msaPlugin.GetAccountsAsync(clientID).ConfigureAwait(false);

                return aadAccounts.Concat(msaAccounts);
            }
        }

        public void HandleInstallUrl(string appLink)
        {
            throw new NotImplementedException();
        }

        public bool IsBrokerInstalledAndInvokable()
        {
#if NET_CORE
            if (!netcore.NetCorePlatformProxy.IsWindowsPlatform())
            {
                return false;
            }
#endif
            // WAM is present on Win 10 only
            return ApiInformation.IsMethodPresent(
                   "Windows.Security.Authentication.Web.Core.WebAuthenticationCoreManager",
                   "GetTokenSilentlyAsync");
        }

        public async Task RemoveAccountAsync(IApplicationConfiguration appConfig, IAccount account)
        {
            string homeTenantId = account?.HomeAccountId?.TenantId;
            if (!string.IsNullOrEmpty(homeTenantId))
            {
                bool isMsaAccount = IsConsumerTenantId(homeTenantId);
                IWamPlugin wamPlugin = isMsaAccount ? _msaPlugin : _aadPlugin;
                WebAccountProvider provider;
                if (isMsaAccount)
                {
                    provider = await _webAccountProviderFactory.GetAccountProviderAsync("consumers").ConfigureAwait(false);
                }
                else
                {
                    provider = await _webAccountProviderFactory.GetAccountProviderAsync("organizations")
                        .ConfigureAwait(false);
                }

                var webAccount = await FindWamAccountForMsalAccountAsync(provider, wamPlugin, account, null, appConfig.ClientId)
                    .ConfigureAwait(false);
                _logger.Info("Found a webAccount? " + (webAccount != null));

                if (webAccount != null)
                {
                    await webAccount.SignOutAsync();
                }
            }
        }

        private async Task<bool> IsGivenOrDefaultAccountMsaAsync(string homeTenantId)
        {
            if (!string.IsNullOrEmpty(homeTenantId))
            {
                bool result = IsConsumerTenantId(homeTenantId);
                _logger.Info("[WAM Broker] Deciding plugin based on home tenant Id ... Msa? " + result);
                return result;
            }

            _logger.Warning("[WAM Broker] Cannot decide which plugin (AAD or MSA) to use. Using AAD. ");
            var isMsa = await _webAccountProviderFactory.IsDefaultAccountMsaAsync().ConfigureAwait(false);
            return isMsa;
        }

        private static bool IsConsumerTenantId(string tenantId)
        {
            return
                string.Equals(Constants.ConsumerTenant, tenantId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Constants.MsaTenantId, tenantId, StringComparison.OrdinalIgnoreCase);
        }

        private MsalTokenResponse CreateMsalTokenResponse(
            IWebTokenRequestResultWrapper wamResponse,
            IWamPlugin wamPlugin,
            bool isInteractive)
        {
            string internalErrorCode = null;
            string errorMessage;
            string errorCode;

            switch (wamResponse.ResponseStatus)
            {
                case WebTokenRequestStatus.Success:
                    _logger.Info("WAM response status success");
                    return wamPlugin.ParseSuccesfullWamResponse(wamResponse.ResponseData[0]);

                // Account Switch occurs when a login hint is passed to WAM but the user chooses a different account for login.
                // MSAL treats this as a success scenario
                case WebTokenRequestStatus.AccountSwitch:
                    _logger.Info("WAM response status account switch. Treating as success");
                    return wamPlugin.ParseSuccesfullWamResponse(wamResponse.ResponseData[0]);

                case WebTokenRequestStatus.UserInteractionRequired:
                    errorCode =
                        wamPlugin.MapTokenRequestError(wamResponse.ResponseStatus, wamResponse.ResponseError?.ErrorCode ?? 0, isInteractive);
                    internalErrorCode = (wamResponse.ResponseError?.ErrorCode ?? 0).ToString(CultureInfo.InvariantCulture);
                    errorMessage = WamErrorPrefix +
                        $"Wam plugin {wamPlugin.GetType()}" +
                        $" error code: {internalErrorCode}" +
                        $" error: " + wamResponse.ResponseError?.ErrorMessage;
                    break;
                case WebTokenRequestStatus.UserCancel:
                    errorCode = MsalError.AuthenticationCanceledError;
                    errorMessage = MsalErrorMessage.AuthenticationCanceled;
                    break;
                case WebTokenRequestStatus.ProviderError:
                    errorCode =
                        wamPlugin.MapTokenRequestError(wamResponse.ResponseStatus, wamResponse.ResponseError?.ErrorCode ?? 0, isInteractive);
                    errorMessage =
                        WamErrorPrefix +
                        " " +
                        wamPlugin.GetType() +
                        $" Error Code: {errorCode}." +
                        $" Possible causes: no Internet connection or invalid redirect uri - please see https://aka.ms/msal-net-wam" +
                        $" Details: " + wamResponse.ResponseError?.ErrorMessage;
                    internalErrorCode = (wamResponse.ResponseError?.ErrorCode ?? 0).ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    errorCode = MsalError.UnknownBrokerError;
                    internalErrorCode = wamResponse.ResponseError.ErrorCode.ToString(CultureInfo.InvariantCulture);
                    errorMessage = $"Unknown WebTokenRequestStatus {wamResponse.ResponseStatus} (internal error code {internalErrorCode})";
                    break;
            }

            return new MsalTokenResponse()
            {
                Error = errorCode,
                ErrorCodes = internalErrorCode != null ? new[] { internalErrorCode } : null,
                ErrorDescription = errorMessage
            };
        }

        private void AddExtraParamsToRequest(WebTokenRequest webTokenRequest, IDictionary<string, string> extraQueryParameters)
        {
            if (extraQueryParameters != null)
            {
                // MSAL uses instance_aware=true, but WAM calls it discover=home, so we rename the parameter before passing
                // it to WAM.
                foreach (var kvp in extraQueryParameters)
                {
                    string key = kvp.Key;
                    string value = kvp.Value;

                    if (string.Equals("instance_aware", key) && string.Equals("true", value))
                    {
                        key = "discover";
                        value = "home";
                    }

                    webTokenRequest.Properties.Add(key, value);
                }
            }
        }

        internal /* for test only */ async Task<bool> IsMsaRequestAsync(
            Authority authority,
            string homeTenantId,
            bool msaPassthrough)
        {
            if (authority.AuthorityInfo.AuthorityType == AuthorityType.B2C)
            {
                throw new MsalClientException(
                    MsalError.WamNoB2C,
                    "The Windows broker (WAM) is only supported in conjunction with work and school and with Microsoft accounts.");
            }

            if (authority.AuthorityInfo.AuthorityType == AuthorityType.Adfs)
            {
                _logger.Info("[WAM Broker] ADFS authority - using only AAD plugin");
                return false;
            }

            string authorityTenant = authority.TenantId;

            // common 
            if (string.Equals(Constants.CommonTenant, authorityTenant, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"[WAM Broker] Tenant is common.");
                return await IsGivenOrDefaultAccountMsaAsync(homeTenantId).ConfigureAwait(false);
            }

            // org
            if (string.Equals(Constants.OrganizationsTenant, authorityTenant, StringComparison.OrdinalIgnoreCase))
            {
                if (msaPassthrough)
                {
                    _logger.Info($"[WAM Broker] Tenant is organizations, but with MSA-PT (similar to common).");
                    return await IsGivenOrDefaultAccountMsaAsync(homeTenantId).ConfigureAwait(false);
                }

                _logger.Info($"[WAM Broker] Tenant is organizations, using WAM-AAD.");
                return false;
            }

            // consumers
            if (IsConsumerTenantId(authorityTenant))
            {
                _logger.Info("[WAM Broker] Authority tenant is consumers. ATS will try WAM-MSA ");
                return true;
            }

            _logger.Info("[WAM Broker] Tenant is not consumers and ATS will try WAM-AAD");
            return false;
        }
    }
}
