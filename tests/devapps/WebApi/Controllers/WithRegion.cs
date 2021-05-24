using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;
using Microsoft.Identity.Client.AuthScheme.PoP;
using Microsoft.Identity.Client.Cache.CacheImpl;
using Microsoft.Identity.Client.Internal.Logger;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StaticDictionaryWithRegionController : ControllerBase
    {
        // TODO: replace with Redis implementation
        static DistributedCacheInMemory s_inMemoryPartitionedCacheSerializer = new DistributedCacheInMemory();


        const string TenantId = "d3adb33f-c0de-ed0c-c0de-deadb33fc0d3";
        const string ClientId = "a3adb33f-c0de-ed0c-c0de-deadb33fc0d3";

        X509Certificate2 x509Certificate2 = null; // find and use a cert

        [HttpGet]
#pragma warning disable UseAsyncSuffix // Use Async suffix
        public async Task<ActionResult<long>> GetBearer()
#pragma warning restore UseAsyncSuffix // Use Async suffix
        {
            // it's faster to create a CCA per request than having a singleton CCA objecrt
            var cca = ConfidentialClientApplicationBuilder
                .Create(ClientId)            
                .WithAzureRegion("westus1")  // use regional ESTS for greater availability
                .WithAuthority($"https://login.microsoftonline.com/{TenantId}")
                .WithCertificate(x509Certificate2)
                .Build();

            s_inMemoryPartitionedCacheSerializer.Initialize(cca.AppTokenCache);

            var res = await cca.AcquireTokenForClient(new[] { "scope" })            
                 .ExecuteAsync().ConfigureAwait(false);

            return res.AuthenticationResultMetadata.DurationTotalInMs;
        }

        [HttpGet]
#pragma warning disable UseAsyncSuffix // Use Async suffix
        public async Task<ActionResult<long>> GetPOP()
#pragma warning restore UseAsyncSuffix // Use Async suffix
        {
            // it's faster to create a CCA per request than having a singleton CCA objecrt
            IConfidentialClientApplication cca = ConfidentialClientApplicationBuilder
                .Create(ClientId)
                .WithExperimentalFeatures()
                .WithAzureRegion("westus1") // use regional ESTS for greater availability
                .WithAuthority($"https://login.microsoftonline.com/{TenantId}")
                .WithCertificate(x509Certificate2)
                .Build();

            s_inMemoryPartitionedCacheSerializer.Initialize(cca.AppTokenCache);

            var protectedUri = new Uri("https://graph.microsoft.com/v1.0/users");
            PoPAuthenticationConfiguration popAuthenticationConfiguration = new PoPAuthenticationConfiguration(protectedUri);
            popAuthenticationConfiguration.HttpMethod = HttpMethod.Get;
            popAuthenticationConfiguration.PopCryptoProvider = new KeyVaultPoPCryptoProvider();

            var result = await cca.AcquireTokenForClient(new[] { "scope" })
                 .WithProofOfPossession(popAuthenticationConfiguration)
                 .ExecuteAsync()
                 .ConfigureAwait(false);


            // result.AccessToken; - this is the POP token


            return result.AuthenticationResultMetadata.DurationTotalInMs;
        }
    }

    internal class KeyVaultPoPCryptoProvider : IPoPCryptoProvider
    {

        public KeyVaultPoPCryptoProvider()
        {
            // connect to the correct KV
        }

        public string CannonicalPublicKeyJwk
        {
            // JWK representation of the public key, e.g. https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/master/tests/Microsoft.Identity.Test.Integration.netfx/HeadlessTests/PoPTests.cs#L429
            get { throw new NotImplementedException(); }
        }

        public string CryptographicAlgorithm { get => "RS256"; }


        public byte[] Sign(byte[] data)
        {
            // TODO: KV signs this data 
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// This cache is in-memory but can easily be replaced with Redis. 
    /// 
    /// Each node will have tokens for one (client_id, tenant_id) combination. In most cases, client_id is the same, so 
    /// each node will have 1 token for each tenant_id. 
    /// </summary>
    internal class DistributedCacheInMemory
    : AbstractCacheAdaptor
    {
        private ConcurrentDictionary<string, byte[]> CachePartition { get; }        

        public DistributedCacheInMemory()
        {
            CachePartition =  new ConcurrentDictionary<string, byte[]>();
        }

        protected override byte[] ReadCacheBytes(string cacheKey)
        {
            if (CachePartition.TryGetValue(cacheKey, out byte[] blob))
            {
                return blob;
            }

            return null;
        }

        protected override void RemoveKey(string cacheKey)
        {
            bool removed = CachePartition.TryRemove(cacheKey, out _);
        }

        protected override void WriteCacheBytes(string cacheKey, byte[] bytes)
        {
            // As per https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2?redirectedfrom=MSDN&view=net-5.0#remarks
            // the indexer is ok to store a key/value pair unconditionally
            CachePartition[cacheKey] = bytes;
        }
    }

    internal abstract class AbstractCacheAdaptor
    {

        /// <summary>
        /// Important - do not use SetBefore / SetAfter methods, as these are reserved for app developers
        /// Instead, use AfterAccess = x, BeforeAccess = y        
        /// </summary>
        public void Initialize(ITokenCache tokenCache)
        {
            if (tokenCache == null)
            {
                throw new ArgumentNullException(nameof(tokenCache));
            }

            tokenCache.SetBeforeAccess(OnBeforeAccess);
            tokenCache.SetAfterAccess(OnAfterAccess);
        }

        /// <summary>
        /// Raised AFTER MSAL added the new token in its in-memory copy of the cache.
        /// This notification is called every time MSAL accesses the cache, not just when a write takes place:
        /// If MSAL's current operation resulted in a cache change, the property TokenCacheNotificationArgs.HasStateChanged will be set to true.
        /// If that is the case, we call the TokenCache.SerializeMsalV3() to get a binary blob representing the latest cache content – and persist it.
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void OnAfterAccess(TokenCacheNotificationArgs args)
        {
            // The access operation resulted in a cache update.
            if (args.HasStateChanged)
            {
                if (args.HasTokens)
                {
                    WriteCacheBytes(args.SuggestedCacheKey, args.TokenCache.SerializeMsalV3());
                }
                else
                {
                    // No token in the cache. we can remove the cache entry
                    RemoveKey(args.SuggestedCacheKey);
                }
            }
        }

        private void OnBeforeAccess(TokenCacheNotificationArgs args)
        {
            if (!string.IsNullOrEmpty(args.SuggestedCacheKey))
            {
                byte[] tokenCacheBytes = ReadCacheBytes(args.SuggestedCacheKey);
                args.TokenCache.DeserializeMsalV3(tokenCacheBytes, shouldClearExistingCache: true);
            }
        }

        /// <summary>
        /// Clear the cache.
        /// </summary>
        /// <param name="homeAccountId">HomeAccountId for a user account in the cache.</param>
        /// <returns>A <see cref="Task"/> that represents a completed clear operation.</returns>
        public void ClearAsync(string homeAccountId)
        {
            // This is a user token cache
            RemoveKey(homeAccountId);
        }

        /// <summary>
        /// Method to be implemented by concrete cache serializers to write the cache bytes.
        /// </summary>
        /// <param name="cacheKey">Cache key.</param>
        /// <param name="bytes">Bytes to write.</param>
        /// <returns>A <see cref="Task"/> that represents a completed write operation.</returns>
        protected abstract void WriteCacheBytes(string cacheKey, byte[] bytes);

        /// <summary>
        /// Method to be implemented by concrete cache serializers to Read the cache bytes.
        /// </summary>
        /// <param name="cacheKey">Cache key.</param>
        /// <returns>Read bytes.</returns>
        protected abstract byte[] ReadCacheBytes(string cacheKey);

        /// <summary>
        /// Method to be implemented by concrete cache serializers to remove an entry from the cache.
        /// </summary>
        /// <param name="cacheKey">Cache key.</param>
        /// <returns>A <see cref="Task"/> that represents a completed remove key operation.</returns>
        protected abstract void RemoveKey(string cacheKey);
    }
}
