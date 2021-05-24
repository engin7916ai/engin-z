// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Instance;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;
using Windows.UI.ApplicationSettings;

#if NET5_WIN
using Microsoft.Identity.Client.Platforms.net5win;
#elif DESKTOP
using Microsoft.Identity.Client.Platforms.netdesktop;
#endif

namespace Microsoft.Identity.Client.Platforms.Features.WamBroker
{
    internal class AccountPicker : IAccountPicker
    {
        private readonly IntPtr _parentHandle;
        private readonly ICoreLogger _logger;
        private readonly SynchronizationContext _synchronizationContext;
        private readonly Authority _authority;
        private readonly bool _isMsaPassthrough;
        private volatile WebAccountProvider _provider;


        public AccountPicker(
            IntPtr parentHandle,
            ICoreLogger logger,
            SynchronizationContext synchronizationContext,
            Authority authority,
            bool isMsaPassthrough)
        {
            _parentHandle = parentHandle;
            _logger = logger;
            _synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
            _authority = authority;
            _isMsaPassthrough = isMsaPassthrough;
        }

        public async Task<WebAccountProvider> DetermineAccountInteractivelyAsync()
        {
            WebAccountProvider result = null;

            // go back to the ui thread
            await _synchronizationContext;

            result = await ShowPickerAsync().ConfigureAwait(true);

            return result;
        }

        private async Task<WebAccountProvider> ShowPickerAsync()
        {
            AccountsSettingsPane retaccountPane = null;
            try
            {
#if WINDOWS_APP
                retaccountPane = AccountsSettingsPane.GetForCurrentView();
                retaccountPane.AccountCommandsRequested += Authenticator_AccountCommandsRequested;
                await AccountsSettingsPane.ShowAddAccountAsync();
#else
                retaccountPane = AccountsSettingsPaneInterop.GetForWindow(_parentHandle);
                retaccountPane.AccountCommandsRequested += Authenticator_AccountCommandsRequested;
                await AccountsSettingsPaneInterop.ShowAddAccountForWindowAsync(_parentHandle);
#endif
                return _provider;
            }
            catch (Exception e)
            {
                _logger.ErrorPii(e);
                throw;
            }
            finally
            {
                if (retaccountPane != null)
                {
                    retaccountPane.AccountCommandsRequested -= Authenticator_AccountCommandsRequested;
                }
            }
        }

        private async void Authenticator_AccountCommandsRequested(
            AccountsSettingsPane sender,
            AccountsSettingsPaneCommandsRequestedEventArgs args)
        {
            AccountsSettingsPaneEventDeferral deferral = null;
            try
            {
                deferral = args.GetDeferral();

                if (string.Equals("common", _authority.TenantId))
                {
                    _logger.Verbose("Displaying selector for common");
                    await AddSelectorsAsync(
                        args, 
                        addOrgAccounts: true, 
                        addMsaAccounts: true).ConfigureAwait(false);
                }
                else if (string.Equals("organizations", _authority.TenantId))
                {
                    _logger.Verbose("Displaying selector for organizations");
                    await AddSelectorsAsync(
                        args, 
                        addOrgAccounts: true, 
                        addMsaAccounts: _isMsaPassthrough).ConfigureAwait(false);
                }
                else if (string.Equals("consumers", _authority.TenantId))
                {
                    _logger.Verbose("Displaying selector for consumers");
                    await AddSelectorsAsync(
                        args, 
                        addOrgAccounts: false, 
                        addMsaAccounts: true).ConfigureAwait(false);
                }
                else
                {
                    _logger.Verbose("Displaying selector for tenanted authority");
                    await AddSelectorsAsync(
                        args, 
                        addOrgAccounts: true, 
                        addMsaAccounts: _isMsaPassthrough, 
                        tenantId: _authority.AuthorityInfo.CanonicalAuthority).ConfigureAwait(false);
                }
            }
            finally
            {
                deferral?.Complete();
            }
        }

        private async Task AddSelectorsAsync(AccountsSettingsPaneCommandsRequestedEventArgs args, bool addOrgAccounts, bool addMsaAccounts, string tenantId = null)
        {
            if (addOrgAccounts)
                args.WebAccountProviderCommands.Add(
                    new WebAccountProviderCommand(
                        await WebAuthenticationCoreManager.FindAccountProviderAsync("https://login.microsoft.com", tenantId ?? "organizations"),
                        WebAccountProviderCommandInvoked));

            if (addMsaAccounts)
                args.WebAccountProviderCommands.Add(
                    new WebAccountProviderCommand(
                        await WebAuthenticationCoreManager.FindAccountProviderAsync("https://login.microsoft.com", "consumers"),
                        WebAccountProviderCommandInvoked));
        }

        private void WebAccountProviderCommandInvoked(WebAccountProviderCommand command)
        {
            _provider = command.WebAccountProvider;

        }
    }
}
