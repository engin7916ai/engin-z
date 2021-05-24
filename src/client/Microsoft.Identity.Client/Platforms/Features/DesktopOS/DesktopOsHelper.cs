﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Platforms.Features.DesktopOS;

namespace Microsoft.Identity.Client.Platforms.Features.DesktopOs
{
    internal static class DesktopOsHelper
    {
        public static bool IsWindows()
        {
#if DESKTOP
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        }

        public static bool IsLinux()
        {
#if DESKTOP
            return Environment.OSVersion.Platform == PlatformID.Unix;
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#endif
        }

        public static bool IsMac()
        {
#if DESKTOP
            return Environment.OSVersion.Platform == PlatformID.MacOSX;
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
#endif
        }

        private static unsafe bool IsWindowsDesktopSession()
        {
            // Environment.UserInteractive is hard-coded to return true for POSIX and Windows platforms on .NET Core 2.x and 3.x.
            // In .NET 5 the implementation on Windows has been 'fixed', but still POSIX versions always return true.
            //
            // This code is lifted from the .NET 5 targeting dotnet/runtime implementation for Windows:
            // https://github.com/dotnet/runtime/blob/cf654f08fb0078a96a4e414a0d2eab5e6c069387/src/libraries/System.Private.CoreLib/src/System/Environment.Windows.cs#L125-L145

            // Per documentation of GetProcessWindowStation, this handle should not be closed
            IntPtr handle = User32.GetProcessWindowStation();
            if (handle != IntPtr.Zero)
            {
                USEROBJECTFLAGS flags = default;
                uint dummy = 0;
                if (User32.GetUserObjectInformation(handle, User32.UOI_FLAGS, &flags,
                    (uint)sizeof(USEROBJECTFLAGS), ref dummy))
                {
                    return (flags.dwFlags & User32.WSF_VISIBLE) != 0;
                }
            }

            // If we can't determine, return true optimistically
            // This will include cases like Windows Nano which do not expose WindowStations
            return true;

        }

        private static bool IsMacDesktopSession()
        {
            // Get information about the current session
            int error = SecurityFramework.SessionGetInfo(SecurityFramework.CallerSecuritySession, out int id, out var sessionFlags);

            // Check if the session supports Quartz
            if (error == 0 && (sessionFlags & SessionAttributeBits.SessionHasGraphicAccess) != 0)
            {
                return true;
            }

            // Fall-through and check if X11 is available on macOS
            return IsLinuxDesktopSession();
        }

        private static bool IsLinuxDesktopSession()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"));
        }

        public static bool IsDesktopSession()
        {
            if (IsWindows())
            {
                return IsWindowsDesktopSession();
            }
            if (IsMac())
            {
                return IsMacDesktopSession();
            }
            if (IsLinux())
            {
                return IsLinuxDesktopSession();
            }

            throw new PlatformNotSupportedException(Environment.OSVersion.Platform.ToString());
        }
    }
}
