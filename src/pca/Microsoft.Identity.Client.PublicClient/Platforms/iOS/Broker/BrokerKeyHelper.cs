// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
#if iOS

using Foundation;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Utils;
using Security;
using System.IO;
using System.Security.Cryptography;

namespace Microsoft.Identity.Client.Platforms.iOS
{
    /// <summary>
    /// iOS broker communication encrypts the tokens using a symmetric algorithm. 
    /// MSAL first sends the key to the broker and the broker returns an encrypted response.
    /// It is recommended to use the same key irrespective of application - the main reasons is: 
    /// fewer calls from broker to AAD, because broker tokens are scoped to the key (i.e. new key -> broker cache is not hit)
    /// </summary>
    internal static class BrokerKeyHelper
    {
        internal static byte[] GetOrCreateBrokerKey(ICoreLogger logger)
        {
            if (TryGetBrokerKey(out byte[] brokerKey))
            {
                logger.Info("GetOrCreateBrokerKey - found an existing key");
                return brokerKey;
            }

            brokerKey = CreateAndStoreBrokerKey(logger);

            return brokerKey;
        }

        private static byte[] CreateAndStoreBrokerKey(ICoreLogger logger)
        {
            logger.Info("CreateAndStoreBrokerKey - creating a new key");

            byte[] brokerKey;
            byte[] rawBytes;
            using (AesManaged algo = CreateSymmetricAlgorith(null))
            {
                algo.GenerateKey();
                rawBytes = algo.Key;
            }

            NSData byteData = NSData.FromArray(rawBytes);

            var recordToAdd = new SecRecord(SecKind.GenericPassword)
            {
                Generic = NSData.FromString(iOSBrokerConstants.LocalSettingsContainerName),
                Service = iOSBrokerConstants.BrokerKeyService,
                Account = iOSBrokerConstants.BrokerKeyAccount,
                Label = iOSBrokerConstants.BrokerKeyLabel,
                Comment = iOSBrokerConstants.BrokerKeyComment,
                Description = iOSBrokerConstants.BrokerKeyStorageDescription,
                ValueData = byteData
            };

            var result = SecKeyChain.Add(recordToAdd);
            if (result == SecStatusCode.DuplicateItem)
            {
                logger.Info("Could not add the broker key, a key already exists. Trying to delete it first.");
                var recordToRemove = new SecRecord(SecKind.GenericPassword)
                {
                    Service = iOSBrokerConstants.BrokerKeyService,
                    Account = iOSBrokerConstants.BrokerKeyAccount,
                };

                var removeResult = SecKeyChain.Remove(recordToRemove);
                logger.Info("Broker key removal result: " + removeResult);

                result = SecKeyChain.Add(recordToAdd);
                logger.Info("Broker key re-adding result: " + result);
            }

            if (result != SecStatusCode.Success)
            {
                logger.Error("Failed to save the broker key to keychain. Result " + result);
                throw new MsalClientException(
                    MsalError.BrokerKeySaveFailed,
                    MsalErrorMessage.iOSBrokerKeySaveFailed(result.ToString()));
            }

            brokerKey = byteData.ToArray();
            return brokerKey;
        }

        private static bool TryGetBrokerKey(out byte[] brokerKey)
        {
            SecRecord record = new SecRecord(SecKind.GenericPassword)
            {
                Generic = NSData.FromString(iOSBrokerConstants.LocalSettingsContainerName),
                Account = iOSBrokerConstants.BrokerKeyAccount,
                Service = iOSBrokerConstants.BrokerKeyService
            };

            NSData key = SecKeyChain.QueryAsData(record);
            if (key != null)
            {
                brokerKey = key.ToArray();
                return true;
            }

            brokerKey = null;
            return false;
        }

        internal static string DecryptBrokerResponse(string encryptedBrokerResponse, ICoreLogger logger)
        {
            byte[] outputBytes = Base64UrlHelpers.DecodeToBytes(encryptedBrokerResponse);
            string plaintext = string.Empty;

            if (TryGetBrokerKey(out byte[] key))
            {
                AesManaged algo = null;
                CryptoStream cryptoStream = null;
                MemoryStream memoryStream = null;
                try
                {
                    memoryStream = new MemoryStream(outputBytes);
                    algo = CreateSymmetricAlgorith(key);
                    cryptoStream = new CryptoStream(
                        memoryStream,
                        algo.CreateDecryptor(),
                        CryptoStreamMode.Read);
                    using (StreamReader srDecrypt = new StreamReader(cryptoStream))
                    {
                        plaintext = srDecrypt.ReadToEnd();
                        return plaintext;
                    }
                }
                finally
                {
                    memoryStream?.Dispose();
                    cryptoStream?.Dispose();
                    algo?.Dispose();
                }
            }

            throw new MsalClientException(
                MsalError.BrokerKeyFetchFailed,
                MsalErrorMessage.iOSBrokerKeyFetchFailed);
        }

       private static AesManaged CreateSymmetricAlgorith(byte[] key)
        {
            AesManaged algorithm = new AesManaged
            {
                //set the mode, padding and block size
                Padding = PaddingMode.PKCS7,
                Mode = CipherMode.CBC,
                KeySize = 256,
                BlockSize = 128
            };

            if (key != null)
            {
                algorithm.Key = key;
            }

            algorithm.IV = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            return algorithm;
        }
    }
}
#endif
