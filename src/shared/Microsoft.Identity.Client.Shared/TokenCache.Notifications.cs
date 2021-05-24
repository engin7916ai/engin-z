﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client
{
    public sealed partial class TokenCache : ITokenCacheInternal
    {
        /// <summary>
        /// Notification method called before any library method accesses the cache.
        /// </summary>
        internal TokenCacheCallback BeforeAccess { get; set; }

        /// <summary>
        /// Notification method called before any library method writes to the cache. This notification can be used to reload
        /// the cache state from a row in database and lock that row. That database row can then be unlocked in the
        /// <see cref="AfterAccess"/>notification.
        /// </summary>
        internal TokenCacheCallback BeforeWrite { get; set; }

        /// <summary>
        /// Notification method called after any library method accesses the cache.
        /// </summary>
        internal TokenCacheCallback AfterAccess { get; set; }

        internal Func<TokenCacheNotificationArgs, Task> AsyncBeforeAccess { get; set; }
        internal Func<TokenCacheNotificationArgs, Task> AsyncAfterAccess { get; set; }
        internal Func<TokenCacheNotificationArgs, Task> AsyncBeforeWrite { get; set; }

        async Task ITokenCacheInternal.OnAfterAccessAsync(TokenCacheNotificationArgs args)
        {
            AfterAccess?.Invoke(args);

            if (AsyncAfterAccess != null)
            {
                await AsyncAfterAccess.Invoke(args).ConfigureAwait(false);
            }
        }

        async Task ITokenCacheInternal.OnBeforeAccessAsync(TokenCacheNotificationArgs args)
        {
            BeforeAccess?.Invoke(args);
            if (AsyncBeforeAccess != null)
            {
                await AsyncBeforeAccess
                    .Invoke(args)
                    .ConfigureAwait(false);
            }
        }

        async Task ITokenCacheInternal.OnBeforeWriteAsync(TokenCacheNotificationArgs args)
        {
            args.HasStateChanged = true;
            BeforeWrite?.Invoke(args);

            if (AsyncBeforeWrite != null)
            {
                await AsyncBeforeWrite.Invoke(args).ConfigureAwait(false);
            }
        }



        /// <summary>
        /// Sets a delegate to be notified before any library method accesses the cache. This gives an option to the
        /// delegate to deserialize a cache entry for the application and accounts specified in the <see cref="TokenCacheNotificationArgs"/>.
        /// See https://aka.ms/msal-net-token-cache-serialization
        /// </summary>
        /// <param name="beforeAccess">Delegate set in order to handle the cache deserialiation</param>      
#if MOBILE_PLATFORM // no custom cache on mobile
        [EditorBrowsable(EditorBrowsableState.Never)]
#endif
        public void SetBeforeAccess(TokenCacheCallback beforeAccess)
        {
            GuardOnMobilePlatforms();
            BeforeAccess = beforeAccess;
        }

        /// <summary>
        /// Sets a delegate to be notified after any library method accesses the cache. This gives an option to the
        /// delegate to serialize a cache entry for the application and accounts specified in the <see cref="TokenCacheNotificationArgs"/>.
        /// See https://aka.ms/msal-net-token-cache-serialization
        /// </summary>
        /// <param name="afterAccess">Delegate set in order to handle the cache serialization </param>
#if MOBILE_PLATFORM // no custom cache on mobile
        [EditorBrowsable(EditorBrowsableState.Never)]
#endif
        public void SetAfterAccess(TokenCacheCallback afterAccess)
        {
            GuardOnMobilePlatforms();
            AfterAccess = afterAccess;
        }

        /// <summary>
        /// Sets a delegate called before any library method writes to the cache. This gives an option to the delegate
        /// to reload the cache state from a row in database and lock that row. That database row can then be unlocked in the delegate
        /// registered with <see cref="SetAfterAccess(TokenCacheCallback)"/>
        /// </summary>
        /// <param name="beforeWrite">Delegate set in order to prepare the cache serialization</param>
        /// 
#if MOBILE_PLATFORM // no custom cache on mobile
        [EditorBrowsable(EditorBrowsableState.Never)]
#endif
        public void SetBeforeWrite(TokenCacheCallback beforeWrite)
        {
            GuardOnMobilePlatforms();
            BeforeWrite = beforeWrite;
        }

        /// <summary>
        /// Sets an async delegate to be notified before any library method accesses the cache. This gives an option to the
        /// delegate to deserialize a cache entry for the application and accounts specified in the <see cref="TokenCacheNotificationArgs"/>.
        /// See https://aka.ms/msal-net-token-cache-serialization
        /// </summary>
        /// <param name="beforeAccess">Delegate set in order to handle the cache deserialiation</param>
#if MOBILE_PLATFORM // no custom cache on mobile
        [EditorBrowsable(EditorBrowsableState.Never)]
#endif
        public void SetBeforeAccessAsync(Func<TokenCacheNotificationArgs, Task> beforeAccess)
        {
            GuardOnMobilePlatforms();
            AsyncBeforeAccess = beforeAccess;
        }

        /// <summary>
        /// Sets an async delegate to be notified after any library method accesses the cache. This gives an option to the
        /// delegate to serialize a cache entry for the application and accounts specified in the <see cref="TokenCacheNotificationArgs"/>.
        /// See https://aka.ms/msal-net-token-cache-serialization
        /// </summary>
        /// <param name="afterAccess">Delegate set in order to handle the cache serialization </param>
#if MOBILE_PLATFORM // no custom cache on mobile
        [EditorBrowsable(EditorBrowsableState.Never)]
#endif        
        public void SetAfterAccessAsync(Func<TokenCacheNotificationArgs, Task> afterAccess)
        {
            GuardOnMobilePlatforms();
            AsyncAfterAccess = afterAccess;
        }

        /// <summary>
        /// Sets an async delegate called before any library method writes to the cache. This gives an option to the delegate
        /// to reload the cache state from a row in database and lock that row. That database row can then be unlocked in the delegate
        /// registered with <see cref="SetAfterAccess(TokenCacheCallback)"/>
        /// </summary>
        /// <param name="beforeWrite">Delegate set in order to prepare the cache serialization</param>
        /// 
#if MOBILE_PLATFORM // no custom cache on mobile
        [EditorBrowsable(EditorBrowsableState.Never)]
#endif
        public void SetBeforeWriteAsync(Func<TokenCacheNotificationArgs, Task> beforeWrite)
        {
            GuardOnMobilePlatforms();
            AsyncBeforeWrite = beforeWrite;
        }

        private static void GuardOnMobilePlatforms()
        {
#if MOBILE_PLATFORM
        throw new PlatformNotSupportedException("You should not use these TokenCache methods on mobile platforms. " +
            "They are meant to allow applications to define their own storage strategy on .net desktop and non-mobile platforms such as .net core. " +
            "On mobile platforms, MSAL.NET implements a secure and performant storage mechanism. " +
            "For more details about custom token cache serialization, visit https://aka.ms/msal-net-serialization");
#endif
        }
    }
}
