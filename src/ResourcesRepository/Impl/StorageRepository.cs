namespace MszCool.PodIdentityDemo.ResourcesRepository
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.Storage.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Azure.Management.Graph.RBAC.Fluent;
    using Azure.Identity;
    using Azure.Storage.Files.DataLake;

    public class StorageRepository : ResourcesRepository, IStorageRepo
    {
        protected const string STORAGE_DATA_OWNER_ROLE = "Storage Blob Data Owner";

        public StorageRepository(string subscriptionId, string resourceGroupName, int retryCount, int secondsIncreaseBetweenRetries)
        : base(subscriptionId, resourceGroupName, retryCount, secondsIncreaseBetweenRetries)
        {
        }

        public async Task CreateAsync(string name, string location, StorageType typeOfStorage, Sku storageSku, string identityToGiveAccess = "", string defaultFileSystem = "defaultfs", string defaultFolderName = "default")
        {
            Trace.TraceInformation($"StorageRepository.CreateAsync() wants to create a storage account with name {name}!");

            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"Required parameter {nameof(name)} is missing!");
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException($"Required parameter {nameof(location)} is missing!");
            if(typeOfStorage == StorageType.Datalake)
            {
                if(string.IsNullOrWhiteSpace(identityToGiveAccess)) throw new ArgumentException($"When creating a data lake storage account you also need to pass in a Service Principal name to give acces to the storage account to!");
                if(string.IsNullOrWhiteSpace(defaultFileSystem)) throw new ArgumentException($"When creating a data lake storage account you also need to pass in default file system name for the file system to create!");
                if(string.IsNullOrWhiteSpace(defaultFolderName)) throw new ArgumentException($"When creating a data lake storage account you also need to pass in default file system name for the default folder to be created in the default file system!");
            }

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
                    var accountCreated = await CreateBlobStorageAsync(name, location, storageSku, (typeOfStorage == StorageType.Datalake));
                    // Assign permissions to the data plane
                    if (!string.IsNullOrWhiteSpace(identityToGiveAccess))
                    {
                        // First assign the required permissions
                        Trace.TraceInformation($"Assigning permissions to identity for being able to access and modify storage account {accountCreated.Name}...");
                        await base.AssignPermissions(accountCreated.Id, identityToGiveAccess, STORAGE_DATA_OWNER_ROLE);
                    }
                    // Create an ADLS file system if requested
                    if (typeOfStorage == StorageType.Datalake)
                    {
                        await CreateDatalakeStorageAsync(accountCreated, defaultFileSystem, defaultFolderName, identityToGiveAccess);
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

        private async Task<IStorageAccount> CreateBlobStorageAsync(string name, string location, Sku storageSku, bool withHns)
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
            var accountDef = base.AzureMgmt.StorageAccounts.Define(name)
                                                           .WithRegion(location)
                                                           .WithExistingResourceGroup(base.ResourceGroupName)
                                                           .WithGeneralPurposeAccountKindV2()
                                                           .WithSku(sku)
                                                           .WithHnsEnabled(withHns);

            // Create the account, trace the success message and return it.
            var accountCreated = await accountDef.CreateAsync();
            Trace.TraceInformation($"Storage account submitted for {name}!");
            return accountCreated;
        }

        private async Task CreateDatalakeStorageAsync(IStorageAccount accountCreated, string fileSystemName, string defaultFolderName, string identityToGiveAccess)
        {
            Trace.TraceInformation($"Creating ADLS Gen2 File System {fileSystemName} in account {accountCreated.Name}...");

            // Create the basic parameters and the ADLS client proxy
            var creds = default(Azure.Core.TokenCredential);
            var adlsUri = $"https://{accountCreated.Name}.dfs.core.windows.net";
            if (base.CredentialsUseSp)
            {
                creds = new ClientSecretCredential(base.TenantId, base.ClientId, base.ClientSecret);
            }
            else
            {
                creds = new ManagedIdentityCredential();
            }
            var adlsClient = new DataLakeServiceClient(new Uri(adlsUri), creds);

            // Create the file system with retry logic, should work on first attempt.
            await RetryActionAsync(async () => {
                Trace.TraceInformation("- Attempt to create file system...");
                var adlsFsClient = await adlsClient.CreateFileSystemAsync(fileSystemName);
                Trace.TraceInformation("- Attempt succeeded!");
            });

            // Creating the folder did require serveral retries until permissions propagated, correctly.
            await RetryActionAsync(async () => {
                Trace.TraceInformation("- Attempt to create folder.");
                var adlsFsClient = adlsClient.GetFileSystemClient(fileSystemName);
                await adlsFsClient.CreateDirectoryAsync(defaultFolderName);
                Trace.TraceInformation("- Attempt succeeded!");
            });

            Trace.TraceInformation($"ADLS File System {fileSystemName} created!");
        }

        #endregion
    }
}