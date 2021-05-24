﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Test.LabInfrastructure;
using System;

namespace Microsoft.Identity.Test.UIAutomation.Infrastructure
{
    public class UserInformationFieldIds
    {
        private readonly LabUser _user;
        private string _passwordInputId;
        private string _passwordSignInButtonId;

        public UserInformationFieldIds(LabUser user)
        {
            _user = user;
        }

        public string PasswordInputId
        {
            get
            {
                if (String.IsNullOrWhiteSpace(_passwordInputId))
                {
                    DetermineFieldIds();
                }
                return _passwordInputId;
            }
        }

        public string PasswordSignInButtonId
        {
            get
            {
                if (String.IsNullOrWhiteSpace(_passwordSignInButtonId))
                {
                    DetermineFieldIds();
                }
                return _passwordSignInButtonId;
            }
        }

        /// <summary>
        /// When starting auth, the firt screen, which collects the username, is from AAD.
        /// Subsequent screens can be from ADFS.
        /// </summary>
        public string AADSignInButtonId
        {
            get
            {
                return CoreUiTestConstants.WebSubmitId;
            }
        }

        /// <summary>
        /// When starting auth, the firt screen, which collects the username, is from AAD.
        /// Subsequent screens can be from ADFS.
        /// </summary>
        public string AADUsernameInputId
        {
            get
            {
                return CoreUiTestConstants.WebUPNInputId;
            }
        }

        private void DetermineFieldIds()
        {
            if (_user.IsFederated)
            {
                if (_user.FederationProvider == FederationProvider.AdfsV2)
                {
                    _passwordInputId = CoreUiTestConstants.AdfsV2WebPasswordInputId;
                    _passwordSignInButtonId = CoreUiTestConstants.AdfsV2WebSubmitButtonId;
                    return;
                }

                // We use the same IDs for ADFSv3 and ADFSv4
                _passwordInputId = CoreUiTestConstants.AdfsV4WebPasswordId;
                _passwordSignInButtonId = CoreUiTestConstants.AdfsV4WebSubmitId;
                return;
            }

            if (_user.UserType == UserType.B2C)
            {
                DetermineB2CFieldIds();
                return;
            }

            _passwordInputId = CoreUiTestConstants.WebPasswordId;
            _passwordSignInButtonId = CoreUiTestConstants.WebSubmitId;
        }

        private void DetermineB2CFieldIds()
        {
            switch (_user.B2CIdentityProvider)
            {
                case B2CIdentityProvider.Local:
                    _passwordInputId = CoreUiTestConstants.B2CWebPasswordId;
                    _passwordSignInButtonId = CoreUiTestConstants.B2CWebSubmitId;
                    break;
                case B2CIdentityProvider.Facebook:
                    _passwordInputId = CoreUiTestConstants.B2CWebPasswordFacebookId;
                    _passwordSignInButtonId = CoreUiTestConstants.B2CFacebookSubmitId;
                    break;
                case B2CIdentityProvider.Google:
                    _passwordInputId = CoreUiTestConstants.B2CWebPasswordGoogleId;
                    _passwordSignInButtonId = CoreUiTestConstants.B2CGoogleSignInId;
                    break;
            }
        }
    }
}
