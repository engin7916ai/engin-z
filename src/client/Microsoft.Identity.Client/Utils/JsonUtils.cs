﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Identity.Json;
using Microsoft.Identity.Json.Linq;

namespace Microsoft.Identity.Client.Utils
{
    internal static class JsonUtils
    {
        public static string GetExistingOrEmptyString(JObject json, string key)
        {
            if (json.TryGetValue(key, out var val))
            {
                return val.ToObject<string>();
            }

            return string.Empty;
        }

        public static string ExtractExistingOrEmptyString(JObject json, string key)
        {
            if (json.TryGetValue(key, out var val))
            {
                string strVal = val.ToObject<string>();
                json.Remove(key);
                return strVal;
            }

            return string.Empty;
        }

        public static IDictionary<string, string> ExtractInnerJsonAsDictionary(JObject json, string key)
        {
            if (json.TryGetValue(key, out JToken val))
            {
                IDictionary<string, JToken> valueAsDict = (JObject)val;
                Dictionary<string, string> dictionary =
                    valueAsDict.ToDictionary(pair => pair.Key, pair => (string)pair.Value);

                json.Remove(key);
                return dictionary;
            }

            return null;
        }

        public static T ExtractExistingOrDefault<T>(JObject json, string key)
        {
            if (json.TryGetValue(key, out var val))
            {
                T obj = val.ToObject<T>();
                json.Remove(key);
                return obj;
            }

            return default(T);
        }

        public static long ExtractParsedIntOrZero(JObject json, string key)
        {
            string strVal = ExtractExistingOrEmptyString(json, key);
            if (!string.IsNullOrWhiteSpace(strVal) && long.TryParse(strVal, out long result))
            {
                return result;
            }

            return 0;
        }
    }
}
