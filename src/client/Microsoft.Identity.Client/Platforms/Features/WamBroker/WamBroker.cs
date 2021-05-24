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
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Instance.Discovery;
#if !UAP10_0
using Microsoft.Identity.Client.Platforms.Features.DesktopOs;
#endif

#if DESKTOP || NET5_WIN
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Runtime.Versioning;
#endif


namespace Microsoft.Identity.Client.Platforms.Features.WamBroker
{
    /// <summary>
    /// Important: all the WAM code has Win10 specific types and MUST be guarded against 
    /// usage on older Windows, Mac and Linux, otherwise TypeLoadExceptions occur
    /// </summary>
#if NET5_WIN
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763.0")]
#endif
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
        private readonly IMsaPassthroughHandler _msaPassthroughHandler;
        private const string WamErrorPrefix = "WAM Error ";
        internal const string ErrorMessageSuffix = " For more details see https://aka.ms/msal-net-wam";
        private readonly WindowsBrokerOptions _wamOptions;

        /// <summary>
        /// Ctor. Only call if on Win10, otherwise a TypeLoadException occurs. See DesktopOsHelper.IsWin10
        /// </summary>
        public WamBroker(
            CoreUIParent uiParent,
            ApplicationConfiguration appConfig,
            ICoreLogger logger,
            IWamPlugin testAadPlugin = null,
            IWamPlugin testmsaPlugin = null,
            IWamProxy wamProxy = null,
            IWebAccountProviderFactory webAccountProviderFactory = null,
            IAccountPickerFactory accountPickerFactory = null,
            IMsaPassthroughHandler msaPassthroughHandler = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _synchronizationContext = uiParent?.SynchronizationContext;

            _wamProxy = wamProxy ?? new WamProxy(_logger, _synchronizationContext);
            _parentHandle = GetParentWindow(uiParent);

            _webAccountProviderFactory = webAccountProviderFactory ?? new WebAccountProviderFactory();
            _accountPickerFactory = accountPickerFactory ?? new AccountPickerFactory();
            _aadPlugin = testAadPlugin ?? new AadPlugin(_wamProxy, _webAccountProviderFactory, _logger);
            _msaPlugin = testmsaPlugin ?? new MsaPlugin(_wamProxy, _webAccountProviderFactory, _logger);

            _msaPassthroughHandler = msaPassthroughHandler ??
                new MsaPassthroughHandler(_logger, _msaPlugin, _wamProxy, _parentHandle);

            _wamOptions = appConfig.WindowsBrokerOptions ??
                WindowsBrokerOptions.CreateDefault();

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
#if WINDOWS_APP
            if (_synchronizationContext == null)
            {
                throw new MsalClientException(
                    MsalError.WamUiThread,
                    "AcquireTokenInteractive with broker must be called from the UI thread when using WAM." +
                     ErrorMessageSuffix);
            }
#endif

            if (authenticationRequestParameters.Account != null ||
                !string.IsNullOrEmpty(authenticationRequestParameters.LoginHint))
            {
                bool isMsaPassthrough = _wamOptions.MsaPassthrough;
                bool isMsa = await IsMsaRequestAsync(
                    authenticationRequestParameters.Authority,
                    authenticationRequestParameters?.Account?.HomeAccountId?.TenantId, // TODO: we could further optimize here by searching for an account based on UPN
                    isMsaPassthrough).ConfigureAwait(false);

                IWamPlugin wamPlugin = isMsa ? _msaPlugin : _aadPlugin;
                WebAccountProvider provider = await GetProviderAsync(
                    authenticationRequestParameters.Authority.TenantId, isMsa)
                    .ConfigureAwait(false);

                if (PublicClientApplication.IsOperatingSystemAccount(authenticationRequestParameters.Account))
                {
                    var wamResult = await AcquireInteractiveWithWamAccountAsync(
                        authenticationRequestParameters,
                        acquireTokenInteractiveParameters.Prompt,
                        wamPlugin,
                        provider,
                        null)
                        .ConfigureAwait(false);
                    return WamAdapters.CreateMsalResponseFromWamResponse(
                        wamResult,
                        wamPlugin,
                        authenticationRequestParameters.AppConfig.ClientId,
                        _logger,
                        isInteractive: true);
                }

                var wamAccount = await FindWamAccountForMsalAccountAsync(
                    provider,
                    wamPlugin,
                    authenticationRequestParameters.Account,
                    authenticationRequestParameters.LoginHint,
                    authenticationRequestParameters.AppConfig.ClientId).ConfigureAwait(false);

                if (wamAccount != null)
                {
                    var wamResult = await AcquireInteractiveWithWamAccountAsync(
                        authenticationRequestParameters,
                        acquireTokenInteractiveParameters.Prompt,
                        wamPlugin,
                        provider,
                        wamAccount)
                        .ConfigureAwait(false);
                    return WamAdapters.CreateMsalResponseFromWamResponse(
                        wamResult,
                        wamPlugin,
                        authenticationRequestParameters.AppConfig.ClientId,
                        _logger,
                        isInteractive: true);
                }
                else
                {
                    if (IsAadOnlyAuthority(authenticationRequestParameters.Authority))
                    {
                        return await AcquireInteractiveWithAadBrowserAsync(
                            authenticationRequestParameters,
                            acquireTokenInteractiveParameters.Prompt).ConfigureAwait(false);
                    }
                }
            }

            return await AcquireInteractiveWithPickerAsync(
                authenticationRequestParameters,
                acquireTokenInteractiveParameters.Prompt)
                .ConfigureAwait(false);
        }

        // only works for AAD plugin. MSA plugin does not allow for privacy reasons
        private async Task<MsalTokenResponse> AcquireInteractiveWithAadBrowserAsync(
            AuthenticationRequestParameters authenticationRequestParameters,
            Prompt msalPrompt)
        {
            var provider = await _webAccountProviderFactory.GetAccountProviderAsync(
                            authenticationRequestParameters.Authority.TenantId).ConfigureAwait(true);

            WebTokenRequest webTokenRequest = await _aadPlugin.CreateWebTokenRequestAsync(
               provider,
               authenticationRequestParameters,
               isForceLoginPrompt: true,
               isInteractive: true,
               isAccountInWam: false)
                .ConfigureAwait(false);

            WamAdapters.AddMsalParamsToRequest(authenticationRequestParameters, webTokenRequest);
            AddPromptToRequest(msalPrompt, true, webTokenRequest);

            var wamResult = await _wamProxy.RequestTokenForWindowAsync(
                  _parentHandle,
                  webTokenRequest).ConfigureAwait(false);

            return WamAdapters.CreateMsalResponseFromWamResponse(
                wamResult,
                _aadPlugin,
                authenticationRequestParameters.AppConfig.ClientId,
                _logger,
                isInteractive: true);
        }

        private bool IsAadOnlyAuthority(Authority authority)
        {
            if (authority is AdfsAuthority)
            {
                return true;
            }

            if (authority is AadAuthority a && a.IsWorkAndSchoolOnly())
            {
                return true;
            }

            return false;
        }

        internal /* internal for test only */ static bool IsForceLoginPrompt(Prompt prompt)
        {
            if (prompt == Prompt.ForceLogin || prompt == Prompt.SelectAccount || prompt == Prompt.Consent)
            {
                return true;
            }

            return false;
        }

        private async Task<IWebTokenRequestResultWrapper> AcquireInteractiveWithWamAccountAsync(
            AuthenticationRequestParameters authenticationRequestParameters,
            Prompt msalPrompt,
            IWamPlugin wamPlugin,
            WebAccountProvider provider,
            WebAccount wamAccount)
        {
            WebTokenRequest webTokenRequest = await wamPlugin.CreateWebTokenRequestAsync(
                provider,
                authenticationRequestParameters,
                isForceLoginPrompt: false,
                isInteractive: true,
                isAccountInWam: true)
           .ConfigureAwait(false);

            // because of https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/2476
            string differentAuthority = null;
            if (string.Equals(wamAccount?.WebAccountProvider?.Authority, Constants.OrganizationsTenant) &&
                string.Equals(authenticationRequestParameters.Authority.TenantId, Constants.OrganizationsTenant))
            {
                differentAuthority = authenticationRequestParameters.Authority.GetTenantedAuthority("common");
            }

            WamAdapters.AddMsalParamsToRequest(authenticationRequestParameters, webTokenRequest, differentAuthority);

            try
            {
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

        private static void AddPromptToRequest(Prompt prompt, bool isForceLoginPrompt, WebTokenRequest webTokenRequest)
        {
            if (isForceLoginPrompt &&
                prompt != Prompt.NotSpecified &&
                prompt != Prompt.NoPrompt &&
                ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 6))
            {
                // this feature works correctly since windows RS4, aka 1803 with the AAD plugin only!
                webTokenRequest.Properties["prompt"] = prompt.PromptValue;
            }
        }

        private async Task<MsalTokenResponse> AcquireInteractiveWithPickerAsync(
            AuthenticationRequestParameters authenticationRequestParameters,
            Prompt msalPrompt)
        {
            bool isMsaPassthrough = _wamOptions.MsaPassthrough;
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

                bool isConsumerTenant = _webAccountProviderFactory.IsConsumerProvider(accountProvider);
                // WAM returns the tenant here, not the full authority
                wamPlugin = (isConsumerTenant && !isMsaPassthrough) ? _msaPlugin : _aadPlugin;

                string transferToken = null;
                bool isForceLoginPrompt = false;
                if (isConsumerTenant && isMsaPassthrough)
                {
                    // Get a transfer token to avoid prompting the user twice
                    transferToken = await _msaPassthroughHandler.TryFetchTransferTokenAsync(
                       authenticationRequestParameters,
                       accountProvider).ConfigureAwait(false);

                    // If a TT cannot be obtained, force the interactive experience again
                    isForceLoginPrompt = string.IsNullOrEmpty(transferToken);

                    // For MSA-PT, the MSA provider will issue v1 token, which cannot be used.
                    // Only the AAD provider can issue a v2 token
                    accountProvider = await _webAccountProviderFactory.GetAccountProviderAsync(
                        authenticationRequestParameters.Authority.TenantId)
                        .ConfigureAwait(false);
                }

                webTokenRequest = await wamPlugin.CreateWebTokenRequestAsync(
                     accountProvider,
                     authenticationRequestParameters,
                     isForceLoginPrompt: isForceLoginPrompt,
                     isInteractive: true,
                     isAccountInWam: false)
                    .ConfigureAwait(true);

                _msaPassthroughHandler.AddTransferTokenToRequest(webTokenRequest, transferToken);

                WamAdapters.AddMsalParamsToRequest(authenticationRequestParameters, webTokenRequest);
                AddPromptToRequest(msalPrompt, isForceLoginPrompt, webTokenRequest);

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

            return WamAdapters.CreateMsalResponseFromWamResponse(
                wamResult,
                wamPlugin,
                authenticationRequestParameters.AppConfig.ClientId,
                _logger,
                isInteractive: true);
        }

        private IntPtr GetParentWindow(CoreUIParent uiParent)
        {
#if WINDOWS_APP
            // On UWP there is no need for a window handle
            return IntPtr.Zero;
#endif

#if DESKTOP || NET5_WIN // net core doesn't reference WinForms

            if (uiParent?.OwnerWindow is IWin32Window window)
            {
                _logger.Info("[WAM Broker] Owner window specified as IWin32Window.");
                return window.Handle;
            }
#endif

#if DESKTOP || NET5_WIN || NET_CORE

            if (uiParent?.OwnerWindow is IntPtr ptr)
            {
                _logger.Info("[WAM Broker] Owner window specified as IntPtr.");
                return ptr;
            }

            // other MSALs prefer to default to GetForegroundWindow() but this causes issues 
            // for example if the user quickly switches windows.
            // GetDesktopWindow will make the default more consistent with the embedded browser
            _logger.Info("[WAM Broker] Using desktop as a parent window.");
            IntPtr desktopWindow = WindowsNativeMethods.GetDesktopWindow();
            return desktopWindow;
#endif
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
                    _wamOptions.MsaPassthrough)
                    .ConfigureAwait(false);

                IWamPlugin wamPlugin = isMsa ? _msaPlugin : _aadPlugin;
                WebAccountProvider provider = await GetProviderAsync(
                    authenticationRequestParameters.Authority.AuthorityInfo.CanonicalAuthority,
                    isMsa).ConfigureAwait(false);

                WebAccount webAccount = await FindWamAccountForMsalAccountAsync(
                    provider,
                    wamPlugin,
                    authenticationRequestParameters.Account,
                    null, // ATS requires an account object, login_hint is not supported on its own
                    authenticationRequestParameters.AppConfig.ClientId).ConfigureAwait(false);

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

                WamAdapters.AddMsalParamsToRequest(authenticationRequestParameters, webTokenRequest);

                var wamResult =
                    await _wamProxy.GetTokenSilentlyAsync(webAccount, webTokenRequest).ConfigureAwait(false);

                return WamAdapters.CreateMsalResponseFromWamResponse(
                    wamResult, 
                    wamPlugin,
                    authenticationRequestParameters.AppConfig.ClientId,
                    _logger, 
                    isInteractive: false);

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
                    _wamOptions.MsaPassthrough).ConfigureAwait(false);

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

                WamAdapters.AddMsalParamsToRequest(authenticationRequestParameters, webTokenRequest);

                var wamResult =
                    await _wamProxy.GetTokenSilentlyForDefaultAccountAsync(webTokenRequest).ConfigureAwait(false);

                return WamAdapters.CreateMsalResponseFromWamResponse(
                    wamResult, 
                    wamPlugin,
                    authenticationRequestParameters.AppConfig.ClientId,
                    _logger, 
                    isInteractive: false);
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

            Account accountInternal = (msalAccount as Account);
            if (accountInternal?.WamAccountIds != null &&
                accountInternal.WamAccountIds.TryGetValue(clientId, out string wamAccountId))
            {
                _logger.Info("WAM will try to find an account based on the WAM account id from the cache");
                WebAccount result = await _wamProxy.FindAccountAsync(provider, wamAccountId).ConfigureAwait(false);
                if (result != null)
                {
                    return result;
                }

                _logger.Warning("WAM account was not found for given WAM account id.");
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
                if (!string.IsNullOrEmpty(homeAccountId) &&
                    string.Equals(homeAccountId, account?.HomeAccountId?.Identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return wamAccount;
                }

                if (!string.IsNullOrEmpty(loginHint) &&
                    string.Equals(loginHint, wamAccount.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    matchedAccountByLoginHint = wamAccount;
                }
            }

            return matchedAccountByLoginHint;
        }

        public async Task<IReadOnlyList<IAccount>> GetAccountsAsync(
            string clientID,
            string redirectUri,
            AuthorityInfo authorityInfo,
            ICacheSessionManager cacheSessionManager,
            IInstanceDiscoveryManager instanceDiscoveryManager)
        {
            using (_logger.LogMethodDuration())
            {
                if (!_wamOptions.ListWindowsWorkAndSchoolAccounts)
                {
                    _logger.Info("WAM::FindAllAccountsAsync returning no accounts due to configuration option");
                    return Array.Empty<IAccount>();
                }

                if (
                    !ApiInformation.IsMethodPresent(
                    "Windows.Security.Authentication.Web.Core.WebAuthenticationCoreManager",
                    "FindAllAccountsAsync"))
                {
                    _logger.Info("WAM::FindAllAccountsAsync method does not exist. Returning 0 broker accounts. ");
                    return Array.Empty<IAccount>();
                }

                var aadAccounts = await _aadPlugin.GetAccountsAsync(clientID, authorityInfo, cacheSessionManager, instanceDiscoveryManager).ConfigureAwait(false);
                var msaAccounts = await _msaPlugin.GetAccountsAsync(clientID, authorityInfo, cacheSessionManager, instanceDiscoveryManager).ConfigureAwait(false);

                return (aadAccounts.Concat(msaAccounts)).ToList();
            }
        }

        public void HandleInstallUrl(string appLink)
        {
            throw new NotImplementedException();
        }

        public bool IsBrokerInstalledAndInvokable()
        {
#if NET_CORE
            if (!DesktopOsHelper.IsWindows())
            {
                return false;
            }
#endif
            // WAM is present on Win 10 only
            return ApiInformation.IsMethodPresent(
                   "Windows.Security.Authentication.Web.Core.WebAuthenticationCoreManager",
                   "GetTokenSilentlyAsync");
        }

        public async Task RemoveAccountAsync(ApplicationConfiguration appConfig, IAccount account)
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
                _logger.Info("[WAM Broker] Deciding plugin based on home tenant Id ... MSA? " + result);
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
                _logger.Info($"[WAM Broker] Authority tenant is consumers. " +
                    $"ATS will try {(msaPassthrough ? "WAM-AAD" : "WAM-MSA")} ");

                return !msaPassthrough; // for silent flow, the authority is MSA-tenant-id 
            }

            _logger.Info("[WAM Broker] Tenant is not consumers and ATS will try WAM-AAD");
            return false;
        }
    }
}
