﻿using AppKit;

namespace MacCocoaApp
{
    public static class MainClass
    {
        // Tutorial for Xamarin.Mac, including XCode interface builder: 
        // https://docs.microsoft.com/en-gb/xamarin/mac/get-started/hello-mac
        public static void Main(string[] args)
        {
            NSApplication.Init();
            NSApplication.Main(args);

            // All the auth code is in ViewController.cs
        }
    }
}
