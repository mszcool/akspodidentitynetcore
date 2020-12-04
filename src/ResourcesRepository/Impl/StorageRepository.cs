namespace MszCool.PodIdentityDemo.ResourcesRepository
{
    using System;
    using System.Linq;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Rest.Azure.OData;
    using Microsoft.Azure.Management.Storage.Fluent;
    using Microsoft.Azure.Management.Storage.Fluent.Models;
    using Microsoft.Azure.Management.Storage.Fluent.StorageAccount;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Models;

    public class StorageRepository : ResourcesRepository, IStorageRepo
    {

        public StorageRepository(string subscriptionId, string resourceGroupName)
        : base(subscriptionId, resourceGroupName)
        {
        }

        public async Task CreateAsync(string name, string location, StorageType typeOfStorage, Sku storageSku)
        {
            Trace.TraceInformation($"StorageRepository.CreateAsync() wants to create a storage account with name {name}!");

            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"Required parameter {nameof(name)} is missing!");
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException($"Required parameter {nameof(location)} is missing!");

            // Ensure the RestClient is available and create the storage Management client
            CreateRestClient();
            var storageMgr = new StorageManagementClient(base.RestClient);

            // Verify if the storage account does exist, already
            name = name.ToLower();
            var isAvailable = await storageMgr.StorageAccounts.CheckNameAvailabilityAsync(name);
            if (!isAvailable.NameAvailable.GetValueOrDefault())
            {
                var notAvailableErrorMsg = $"Storage account name {name} is not available, anymore!";
                Trace.TraceError(notAvailableErrorMsg);
                throw new Exception(notAvailableErrorMsg);
            }
            else
            {
                try
                {
                    // The storage account is needed always AFAIK
                    await CreateBlobStorageAsync(storageMgr, name, location, storageSku);
                    if(typeOfStorage == StorageType.Datalake) 
                    {
                        // TODO: Change the hardcoded file system name
                        await CreateDatalakeStorageAsync(name, "testFileSystem");
                    }
                    Trace.TraceInformation("StorageRepository.CreateAsync() succeeded!");
                }
                catch (Exception ex)
                {
                    var failedMsg = $"Failed creating the storage account - {ex.Message}.";
                    Trace.TraceError(failedMsg);
                    throw new Exception(failedMsg, ex);
                }
            }

        }

        #region Methods overridden from base class

        protected override ODataQuery<GenericResourceFilter> GetOdataQueryString()
        {
            var ret = new ODataQuery<GenericResourceFilter>();
            ret.SetFilter(f => f.ResourceType == "Microsoft.Storage/storageAccounts");
            return ret;
        }

        #endregion

        #region Private Methods

        private async Task CreateBlobStorageAsync(StorageManagementClient storageMgr, string name, string location, Sku storageSku)
        {
            var storageOptions = new StorageAccountCreateParameters();
            storageOptions.Kind = Kind.StorageV2;
            storageOptions.Location = location;
            switch (storageSku)
            {
                case Sku.Basic:
                    storageOptions.Kind = Kind.BlobStorage;
                    storageOptions.Sku.Name = SkuName.StandardLRS;
                    break;
                case Sku.Standard:
                    storageOptions.Kind = Kind.StorageV2;
                    storageOptions.Sku.Name = SkuName.StandardZRS;
                    break;
                case Sku.Premium:
                    storageOptions.Kind = Kind.StorageV2;
                    storageOptions.Sku.Name = SkuName.StandardRAGRS;
                    break;
            }

            await storageMgr.StorageAccounts.CreateAsync(
                base.ResourceGroupName,
                name,
                storageOptions
            );
        }

        private Task CreateDatalakeStorageAsync(string name, string fileSystemName)
        {
            var httpClient = new System.Net.Http.HttpClient(RestClient.RootHttpHandler);
            
            throw new NotImplementedException();
        }

        #endregion
    }
}