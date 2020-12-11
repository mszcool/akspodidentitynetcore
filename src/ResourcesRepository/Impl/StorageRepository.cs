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

        public StorageRepository(string subscriptionId, string resourceGroupName)
        : base(subscriptionId, resourceGroupName)
        {
        }

        public async Task CreateAsync(string name, string location, StorageType typeOfStorage, Sku storageSku, string identityToGiveAccess = "")
        {
            Trace.TraceInformation($"StorageRepository.CreateAsync() wants to create a storage account with name {name}!");

            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"Required parameter {nameof(name)} is missing!");
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException($"Required parameter {nameof(location)} is missing!");
            if ((typeOfStorage == StorageType.Datalake) && string.IsNullOrWhiteSpace(identityToGiveAccess)) throw new ArgumentException($"When creating a storage account you also need to pass in a Service Principal name to give acces to the storage account to!");

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
                    var accountCreated = await CreateBlobStorageAsync(name, location, storageSku);
                    if(typeOfStorage == StorageType.Datalake) 
                    {
                        await CreateAdlsPermissions(accountCreated, identityToGiveAccess);
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

        private async Task<IStorageAccount> CreateBlobStorageAsync(string name, string location, Sku storageSku)
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
                                                           .WithSku(sku);
            
            // Create the account, trace the success message and return it.
            var accountCreated = await accountDef.CreateAsync();
            Trace.TraceInformation($"Storage account submitted for {name}!");
            return accountCreated;
        }

        private async Task CreateAdlsPermissions(IStorageAccount account, string servicePrincipalName)
        {
            Trace.TraceInformation($"Assigning permissions to identity for being able to access and modify the ADLS file system to storage account {account.Name}...");

            var storageRole = await base.AzureMgmt.AccessManagement.RoleDefinitions.GetByScopeAndRoleNameAsync(account.Id, STORAGE_DATA_OWNER_ROLE);
            if(storageRole == null) {
                throw new Exception($"Unable to retrieve role {STORAGE_DATA_OWNER_ROLE} for storage account {account.Name}!");
            }

            var roleAssignmentId = Guid.NewGuid().ToString();
            await base.AzureMgmt.AccessManagement.RoleAssignments.Define(roleAssignmentId)
                                                                 .ForServicePrincipal(servicePrincipalName)
                                                                 .WithRoleDefinition(storageRole.Id)
                                                                 .WithScope(account.Id)
                                                                 .CreateAsync();

            Trace.TraceInformation($"Permissions created for identity for {account.Name}...");
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