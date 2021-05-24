﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Identity.Client.Core;

namespace Microsoft.Identity.Client.Utils
{
    internal static class EnumerableExtensions
    {
        internal static bool IsNullOrEmpty<T>(this IEnumerable<T> input)
        {
            return input == null || !input.Any();
        }

        internal static string AsSingleString(this IEnumerable<string> input)
        {
            if (input.IsNullOrEmpty())
            {
                return string.Empty;
            }

            return string.Join(" ", input);
        }

        internal static bool ContainsOrdinalIgnoreCase(this IEnumerable<string> set, string toLookFor)
        {
            return set.Any(el => el.Equals(toLookFor, System.StringComparison.OrdinalIgnoreCase));
        }

        internal static IEnumerable<T> FilterWithLogging<T>(
            this IEnumerable<T> list,
            Func<T, bool> predicate,
            ICoreLogger logger,
            string logPrefix)
        {
            if (logger.IsLoggingEnabled(LogLevel.Verbose))
            {
                logger.Verbose($"{logPrefix} - item count before: {list.Count()} ");
            }

            list = list.Where(predicate);

            if (logger.IsLoggingEnabled(LogLevel.Verbose))
            {
                logger.Verbose($"{logPrefix} - item count after: {list.Count()} ");
            }

            return list;
        }

        internal static void MergeDifferentEntries<TKey, TValue>(
            this IDictionary<TKey, TValue> source,
            IDictionary<TKey, TValue> other)
        {
            other?.ToList().ForEach(x =>
            {
                if (!source.ContainsKey(x.Key))
                {
                    source[x.Key] = x.Value;
                }
            });
        }
    }
}
