﻿// ------------------------------------------------------------------------------
// 
// Copyright (c) Microsoft Corporation.
// All rights reserved.
// 
// This code is licensed under the MIT License.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// ------------------------------------------------------------------------------

using System;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.PlatformsCommon.Factories;
using Microsoft.Identity.Client.PlatformsCommon.Interfaces;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Test.Unit
{
    [TestClass]
    public class PlatformProxyPerformanceTests
    {
        private const long AllowedMilliseconds = 10;
        private const long DomainJoinedAllowedMilliseconds = 100;

        [TestMethod]
        public void ValidateGetPlatformProxyPerformance()
        {
            using (new PerformanceValidator(50, "GetPlatformProxy"))
            {
                PlatformProxyFactory.GetPlatformProxy();
            }
        }

        [TestMethod]
        public void ValidateGetDeviceModelPerformance()
        {
            ValidateMethodPerformance(AllowedMilliseconds, "GetDeviceModel", proxy => proxy.GetDeviceModel());
        }

        [TestMethod]
        public void ValidateGetDeviceIdPerformance()
        {
            ValidateMethodPerformance(AllowedMilliseconds, "GetDeviceId", proxy => proxy.GetDeviceId());
        }

        [TestMethod]
        public void ValidateGetOperatingSystemPerformance()
        {
            ValidateMethodPerformance(AllowedMilliseconds, "GetOperatingSystem", proxy => proxy.GetOperatingSystem());
        }

        [TestMethod]
        public void ValidateGetProcessorArchitecturePerformance()
        {
            ValidateMethodPerformance(AllowedMilliseconds, "GetProcessorArchitecture", proxy => proxy.GetProcessorArchitecture());
        }

        [TestMethod]
        public void ValidateIsDomainJoinedPerformance()
        {
            ValidateMethodPerformance(DomainJoinedAllowedMilliseconds, "IsDomainJoined", proxy => proxy.IsDomainJoined());
        }

        [TestMethod]
        public void ValidateGetCallingApplicationNamePerformance()
        {
            ValidateMethodPerformance(AllowedMilliseconds, "GetCallingApplicationName", proxy => proxy.GetCallingApplicationName());
        }

        [TestMethod]
        public void ValidateGetCallingApplicationVersionPerformance()
        {
            ValidateMethodPerformance(AllowedMilliseconds, "GetCallingApplicationVersion", proxy => proxy.GetCallingApplicationVersion());
        }

        [TestMethod]
        public void ValidateGetProductNamePerformance()
        {
            ValidateMethodPerformance(AllowedMilliseconds, "GetProductName", proxy => proxy.GetProductName());
        }


        private void ValidateMethodPerformance(long maxMilliseconds, string name, Action<IPlatformProxy> action)
        {
            var platformProxy = PlatformProxyFactory.GetPlatformProxy();

            // Call it once to pre-load it.  We're not worried about the time it takes to call it
            // the first time, we're worried about subsequent calls.
            action(platformProxy);

            using (new PerformanceValidator(maxMilliseconds, name))
            {
                action(platformProxy);
            }
        }
    }
}
