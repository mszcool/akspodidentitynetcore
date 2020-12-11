namespace MszCool.PodIdentityDemo.ResourcesRepository
{
    using System;
    using System.Linq;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.Storage.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Azure.Identity;
    using Azure.Storage.Files.DataLake;

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
            base.ConfigureAzure();

            // Verify if the storage account does exist, already
            name = name.ToLower();
            var isAvailable = await base.AzureMgmt.StorageAccounts.CheckNameAvailabilityAsync(name);
            if (isAvailable.IsAvailable.HasValue && !isAvailable.IsAvailable.Value)
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
                    await CreateBlobStorageAsync(name, location, storageSku);
                    if(typeOfStorage == StorageType.Datalake) 
                    {
                        // TODO: Change the hardcoded file system name
                        await CreateDatalakeStorageAsync(name, "defaultfs");
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

        protected override bool GetResourceFilter(IGenericResource res)
        {
            return res.ResourceType == "Microsoft.Storage/storageAccounts";
        }

        #endregion

        #region Private Methods

        private async Task CreateBlobStorageAsync(string name, string location, Sku storageSku)
        {
            Trace.TraceInformation($"Creating storage account {name} in location {location}...");

            var sku = StorageAccountSkuType.Standard_LRS;
            switch (storageSku)
            {
                case Sku.Basic:
                    break;
                case Sku.Standard:
                    sku = StorageAccountSkuType.Standard_ZRS;
                    break;
                case Sku.Premium:
                    sku = StorageAccountSkuType.Standard_GRS;
                    break;
            }

            // Create the storage account
            var accountCreated = await base.AzureMgmt.StorageAccounts.Define(name)
                                                                     .WithRegion(location)
                                                                     .WithExistingResourceGroup(base.ResourceGroupName)
                                                                     .WithSku(sku)
                                                                     .WithGeneralPurposeAccountKindV2()
                                                                     .CreateAsync();

            Trace.TraceInformation($"Storage account submitted for {name}!");
        }

        private async Task CreateDatalakeStorageAsync(string accountName, string fileSystemName)
        {
            Trace.TraceInformation($"Creating ADLS Gen2 File System {fileSystemName} in account {accountName}...");

            // Initialize the basic details needed for accessing the ADLS Gen2 Account.
            var creds = default(Azure.Core.TokenCredential);
            var adlsUri = $"https://{accountName}.dfs.core.windows.net";
            if(base.CredentialsUseSp)
            {
                creds = new ClientSecretCredential(base.TenantId, base.ClientId, base.ClientSecret);
            }
            else
            {
                creds = new ManagedIdentityCredential();
            }

            // Create the data service client and then create the file system
            var adlsClient = new DataLakeServiceClient(new Uri(adlsUri), creds);
            var adlsFsClient = await adlsClient.CreateFileSystemAsync(fileSystemName);
            await adlsFsClient.Value.CreateDirectoryAsync("defaultDirectory");
            
            Trace.TraceInformation($"ADLS File System {fileSystemName} created!");
        }

        #endregion
    }
}