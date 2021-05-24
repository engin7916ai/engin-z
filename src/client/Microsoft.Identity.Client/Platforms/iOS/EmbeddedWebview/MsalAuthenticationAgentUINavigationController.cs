// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CoreGraphics;
using UIKit;

namespace Microsoft.Identity.Client.Platforms.iOS.EmbeddedWebview
{
    [Foundation.Register("MsalUniversalView")]
    internal class MsalUniversalView : UIView
    {
        public MsalUniversalView()
        {
            Initialize();
        }

        public MsalUniversalView(CGRect bounds)
            : base(bounds)
        {
            Initialize();
        }

        private void Initialize()
        {
            BackgroundColor = UIColor.Red;
        }
    }

    [Foundation.Register("MsalAuthenticationAgentUINavigationController")]
    internal class MsalAuthenticationAgentUINavigationController : UINavigationController
    {
        private readonly string _url;
        private readonly string _callback;

        private readonly MsalAuthenticationAgentUIViewController.ReturnCodeCallback _callbackMethod;

        private readonly UIStatusBarStyle _preferredStatusBarStyle;

        public MsalAuthenticationAgentUINavigationController(string url, string callback, MsalAuthenticationAgentUIViewController.ReturnCodeCallback callbackMethod, UIStatusBarStyle preferredStatusBarStyle)
        {
            _url = url;
            _callback = callback;
            _callbackMethod = callbackMethod;
            _preferredStatusBarStyle = preferredStatusBarStyle;
        }

        public override void DidReceiveMemoryWarning()
        {
            // Releases the view if it doesn't have a superview.
            base.DidReceiveMemoryWarning();

            // Release any cached data, images, etc that aren't in use.
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Perform any additional setup after loading the view
            PushViewController(new MsalAuthenticationAgentUIViewController(_url, _callback,_callbackMethod), true);
        }

        public override UIStatusBarStyle PreferredStatusBarStyle()
        {
            return _preferredStatusBarStyle;
        }
    }
}
