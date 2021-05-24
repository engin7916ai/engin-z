// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Microsoft.Identity.Client;
using Xamarin.Forms.Platform.Android;
using XamarinDev;
using Xamarin.Forms;
using XamarinDev.Droid;
using Android;
using Android.Support.V4.App;

[assembly: ExportRenderer(typeof(AcquirePage), typeof(AcquirePageRenderer))]

namespace XamarinDev.Droid
{
    internal class AcquirePageRenderer : PageRenderer
    {
        AcquirePage _page;

		public AcquirePageRenderer(Context context) : base(context)
		{

        }

		protected override void OnElementChanged(ElementChangedEventArgs<Page> e)
        {
            base.OnElementChanged(e);
            _page = (AcquirePage)e.NewElement;
            var activity = this.Context as Activity;
        }
    }
}
