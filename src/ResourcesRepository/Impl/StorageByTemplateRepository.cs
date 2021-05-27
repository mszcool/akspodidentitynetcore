namespace MszCool.Samples.PodIdentityDemo.ResourcesRepository.InternalImplementations
{
    using Azure.Identity;
    using Azure.Storage.Files.DataLake;
    using Microsoft.Azure.Management.Storage.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Extensions.Logging;
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Interfaces;
    using System;
    using System.Threading.Tasks;
    using System.IO;

    internal class StorageByTemplateRepositoryImpl : ResourcesRepositoryImpl, IStorageRepo
    {
        protected const string STORAGE_DATA_OWNER_ROLE = "Storage Blob Data Owner";
        protected const string ARM_TEMPLATE_STORAGE_NAME = "MszCool.Samples.PodIdentityDemo.ResourcesRepository.Impl.Resources.arm.storage.json";
        protected const string ARM_TOKEN_NAME = "__NAME__";
        protected const string ARM_TOKEN_LOCATION = "__LOCATION__";
        protected const string ARM_TOKEN_SKU_NAME = "__SKU_NAME__";
        protected const string ARM_TOKEN_HNS_ENABLED = "__HNS_ENABLED__";

        private ILogger<IStorageRepo> logger;

        public StorageByTemplateRepositoryImpl(string subscriptionId, string resourceGroupName, int retryCount, int secondsIncreaseBetweenRetries, ILoggerFactory loggerFactory)
        : base(subscriptionId, resourceGroupName, retryCount, secondsIncreaseBetweenRetries, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<IStorageRepo>();
        }

        public async Task CreateAsync(string name, string location, StorageType typeOfStorage, Sku storageSku, string identityToGiveAccess = "", string defaultFileSystem = "defaultfs", string defaultFolderName = "default")
        {
            this.logger.LogTrace($"StorageRepository.CreateAsync() wants to create a storage account with name {name}!");

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
                this.logger.LogError(notAvailableErrorMsg);
                throw new Exception(notAvailableErrorMsg);
            }
            else
            {
                try
                {
                    // Check if there's quota available
                    var quotaAvailable = await this.IsStorageQuotaAvailable(location);
                    if (!quotaAvailable)
                    {
                        throw new Exception("Unable to create storage account, quota limit reached!");
                    }

                    // The storage account is needed always AFAIK
                    var accountCreated = await CreateBlobStorageAsync(name, location, storageSku, (typeOfStorage == StorageType.Datalake));
                    // Assign permissions to the data plane
                    if (!string.IsNullOrWhiteSpace(identityToGiveAccess))
                    {
                        // First assign the required permissions
                        this.logger.LogTrace($"Assigning permissions to identity for being able to access and modify storage account {accountCreated.Name}...");
                        await base.AssignPermissions(accountCreated.Id, identityToGiveAccess, STORAGE_DATA_OWNER_ROLE);
                    }
                    // Create an ADLS file system if requested
                    if (typeOfStorage == StorageType.Datalake)
                    {
                        await CreateDatalakeStorageAsync(accountCreated, defaultFileSystem, defaultFolderName, identityToGiveAccess);
                    }
                    this.logger.LogInformation($"StorageRepository.CreateAsync() succeeded for storage account {name} in location {location} of type {typeOfStorage}!");
                }
                catch (Exception ex)
                {
                    var failedMsg = $"Failed creating the storage account - {ex.Message}.";
                    this.logger.LogError(failedMsg);
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

        private async Task<bool> IsStorageQuotaAvailable(string location)
        {
            this.logger.LogInformation($"Checking if quota is available for location {location} in subscription {base.SubscriptionId}...");

            var quotaOkay = false;
            var subquota = await base.AzureMgmt.StorageAccounts.Manager.Usages.Inner.ListByLocationAsync(location);
            var subquotaEnumerator = subquota.GetEnumerator();
            while(subquotaEnumerator.MoveNext())
            {
                var quotaFound = subquotaEnumerator.Current;
                if(quotaFound.CurrentValue < quotaFound.Limit)
                {
                    this.logger.LogInformation($"Quota available for {quotaFound.Name.Value} with value {quotaFound.CurrentValue} and limit {quotaFound.Limit}!");
                    quotaOkay = true;
                }
                else
                {
                    this.logger.LogWarning($"Quota {quotaFound.Name.Value} reached its limit {quotaFound.Limit}!");
                }
            }

            return quotaOkay;
        }

        private async Task<IStorageAccount> CreateBlobStorageAsync(string name, string location, Sku storageSku, bool withHns)
        {
            this.logger.LogTrace($"Creating storage account {name} in location {location}...");

            // Evaluate the SKU.
            var sku = "Standard_LRS";
            switch (storageSku)
            {
                case Sku.Basic:
                    break;
                case Sku.Standard:
                    sku = "Standard_ZRS";
                    break;
                case Sku.Premium:
                    sku = "Standard_RAGZRS";
                    break;
            }

            // First read the embedded ARM Template.
            this.logger.LogTrace($"Retrieving ARM template {ARM_TEMPLATE_STORAGE_NAME} from embedded resources...");
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var armStream = new StreamReader(asm.GetManifestResourceStream(ARM_TEMPLATE_STORAGE_NAME));
            var armTemplate = armStream.ReadToEnd();

            // Now replace the tokens in the ARM Template.
            armTemplate = armTemplate.Replace(ARM_TOKEN_NAME, name);
            armTemplate = armTemplate.Replace(ARM_TOKEN_LOCATION, location);
            armTemplate = armTemplate.Replace(ARM_TOKEN_SKU_NAME, sku);
            armTemplate = armTemplate.Replace(ARM_TOKEN_HNS_ENABLED, withHns.ToString());

            // Next, let's perform the template deployment.
            var deploymentName = $"deploy_{name}";
            this.logger.LogTrace($"Starting deployment with name {deploymentName}...");
            var deployment = await this.AzureMgmt.Deployments.Define(deploymentName)
                                                             .WithExistingResourceGroup(this.ResourceGroupName)
                                                             .WithTemplate(armTemplate)
                                                             .WithParameters("{}")
                                                             .WithMode(Microsoft.Azure.Management.ResourceManager.Fluent.Models.DeploymentMode.Incremental)
                                                             .BeginCreateAsync();

            // Wait for the deployment to complete.
            while (!(StringComparer.InvariantCultureIgnoreCase.Equals(deployment.ProvisioningState, "Succeeded")
                   || StringComparer.InvariantCultureIgnoreCase.Equals(deployment.ProvisioningState, "Failed")
                   || StringComparer.InvariantCultureIgnoreCase.Equals(deployment.ProvisioningState, "Cancelled")))
            {
                this.logger.LogTrace($"Waiting for deployment {deploymentName} to complete...");
                SdkContext.DelayProvider.Delay(10000);
                deployment = await this.AzureMgmt.Deployments.GetByResourceGroupAsync(this.ResourceGroupName, deploymentName);
            }

            // Get the created storage account.
            var accountCreated = this.AzureMgmt.StorageAccounts.GetByResourceGroup(this.ResourceGroupName, name);

            this.logger.LogTrace($"Storage account submitted for {name}!");
            await Task.Delay(10);
            return accountCreated;
        }

        private async Task CreateDatalakeStorageAsync(IStorageAccount accountCreated, string fileSystemName, string defaultFolderName, string identityToGiveAccess)
        {
            this.logger.LogTrace($"Creating ADLS Gen2 File System {fileSystemName} in account {accountCreated.Name}...");

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
                this.logger.LogTrace("- Attempt to create file system...");
                var adlsFsClient = await adlsClient.CreateFileSystemAsync(fileSystemName);
                this.logger.LogTrace("- Attempt succeeded!");
            });

            // Creating the folder did require serveral retries until permissions propagated, correctly.
            await RetryActionAsync(async () => {
                this.logger.LogTrace("- Attempt to create folder.");
                var adlsFsClient = adlsClient.GetFileSystemClient(fileSystemName);
                await adlsFsClient.CreateDirectoryAsync(defaultFolderName);
                this.logger.LogTrace("- Attempt succeeded!");
            });

            this.logger.LogTrace($"ADLS File System {fileSystemName} created!");
        }

        #endregion
    }
}