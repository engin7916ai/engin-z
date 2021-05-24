﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Identity.Client.PlatformsCommon.Factories;
using System.Linq;
using Microsoft.Identity.Client.Http;
using System.Net;
using Microsoft.Identity.Client.PlatformsCommon.Shared;

namespace Microsoft.Identity.Test.Unit.CoreTests
{
    [TestClass]
    public class PlatformProxyFactoryTests
    {
        [TestMethod]
        public void PlatformProxyFactoryDoesNotCacheTheProxy()
        {
            // Act
            var proxy1 = CCAPlatformProxyFactory.CreatePlatformProxy(null);
            var proxy2 = CCAPlatformProxyFactory.CreatePlatformProxy(null);

            // Assert
            Assert.IsFalse(proxy1 == proxy2);
        }

        [TestMethod]
        public void PlatformProxyFactoryReturnsInstances()
        {
            // Arrange
            var proxy = CCAPlatformProxyFactory.CreatePlatformProxy(null);

            // Act and Assert
            Assert.AreNotSame(
                proxy.CreateLegacyCachePersistence(),
                proxy.CreateLegacyCachePersistence());

            Assert.AreNotSame(
                proxy.CreateTokenCacheAccessor(),
                proxy.CreateTokenCacheAccessor());

            Assert.AreSame(
                proxy.CryptographyManager,
                proxy.CryptographyManager);

        }


        [TestMethod]
        public void PlatformProxy_HttpClient()
        {
            // Arrange
            var proxy = CCAPlatformProxyFactory.CreatePlatformProxy(null);
            var factory1 = proxy.CreateDefaultHttpClientFactory();
            var factory2 = proxy.CreateDefaultHttpClientFactory();

            // Act
            var client1 = factory1.GetHttpClient();
            var client2 = factory1.GetHttpClient();
            var client3 = factory2.GetHttpClient();

            // Assert
            Assert.AreNotSame(factory1, factory2, "HttpClient factory does not need to be static");

            Assert.AreSame(
               client1, client2, "On NetDesktop and NetCore, the HttpClient should be static");
            Assert.AreSame(
               client2, client3, "On NetDesktop and NetCore, the HttpClient should be static");
            Assert.AreEqual("application/json",
                client1.DefaultRequestHeaders.Accept.Single().MediaType);
            Assert.AreEqual(HttpClientConfig.MaxResponseContentBufferSizeInBytes,
                client1.MaxResponseContentBufferSize);
        }

#if NET_CORE
        [TestMethod]
        public void PlatformProxy_HttpClient_NetCore()
        {
            // Arrange
            var factory = CCAPlatformProxyFactory.CreatePlatformProxy(null)
                .CreateDefaultHttpClientFactory();

            // Act
            var client1 = factory.GetHttpClient();
            var client2 = factory.GetHttpClient();

            // Assert
            Assert.IsTrue(factory is SimpleHttpClientFactory);
            Assert.AreSame(client1, client2, "On NetDesktop and NetCore, the HttpClient should be static");


        }
#endif
#if DESKTOP
 [TestMethod]
        public void PlatformProxy_HttpClient_NetDesktop()
        {
            // Arrange
            var factory = CCAPlatformProxyFactory.CreatePlatformProxy(null)
                .CreateDefaultHttpClientFactory();          

            // Assert
            Assert.IsTrue(factory is 
                Client.Platforms.net45.Http.NetDesktopHttpClientFactory);
        }
#endif

        [TestMethod]
        public void PlatformProxy_HttpClient_DoesNotSetGlobalProperties()
        {
            // Arrange
            int originalDnsTimeout = ServicePointManager.DnsRefreshTimeout;
            int originalConnLimit = ServicePointManager.DefaultConnectionLimit;

            try
            {
                int newDnsTimeout = 1001;
                int newConnLimit = 42;

                ServicePointManager.DnsRefreshTimeout = newDnsTimeout;
                ServicePointManager.DefaultConnectionLimit = newConnLimit;

                // Act
                var factory = CCAPlatformProxyFactory.CreatePlatformProxy(null)
                    .CreateDefaultHttpClientFactory();
                _ = factory.GetHttpClient();

                // Assert - the factory does not override these global properties
                Assert.AreEqual(newDnsTimeout, ServicePointManager.DnsRefreshTimeout);
                Assert.AreEqual(newConnLimit, ServicePointManager.DefaultConnectionLimit);

            }
            finally
            {
                ServicePointManager.DnsRefreshTimeout = originalDnsTimeout;
                ServicePointManager.DefaultConnectionLimit = originalConnLimit;
            }

        }

        [TestMethod]
        public void GetEnvRetrievesAValue()
        {
            try
            {
                // Arrange
                Environment.SetEnvironmentVariable("proxy_foo", "bar");

                var proxy = CCAPlatformProxyFactory.CreatePlatformProxy(null);

                // Act
                string actualValue = proxy.GetEnvironmentVariable("proxy_foo");
                string actualEmptyValue = proxy.GetEnvironmentVariable("no_such_env_var_exists");


                // Assert
                Assert.AreEqual("bar", actualValue);
                Assert.IsNull(actualEmptyValue);
            }
            finally
            {
                Environment.SetEnvironmentVariable("proxy_foo", "");
            }
        }

        [TestMethod]
        public void GetEnvThrowsArgNullEx()
        {

            AssertException.Throws<ArgumentNullException>(
                () =>
                CCAPlatformProxyFactory.CreatePlatformProxy(null).GetEnvironmentVariable(""));
        }


    }
}
