namespace MszCool.Samples.PodIdentityDemo.ResourcesRepository.InternalImplementations
{
    using Microsoft.Azure.Management.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
    using Microsoft.Extensions.Logging;
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Entities;
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Interfaces;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Collections.Generic;

    internal class ResourcesRepositoryImpl : IResourcesRepo
    {
        private const string PROVIDER_SECTION = "providers";

        private ILogger<IResourcesRepo> logger;

        protected int RetryCount { get; private set; }
        protected int SecondsIncreasePerRetry { get; private set;}

        protected IAzure AzureMgmt { get; private set; }
        protected bool CredentialsUseSp { get; private set; }
        protected string ClientId { get; private set; }
        protected string ClientSecret { get; private set; }
        protected string TenantId { get; private set; }
        protected string SubscriptionId { get; private set; }
        protected string ResourceGroupName { get; private set; }

        internal ResourcesRepositoryImpl(string subscriptionId, string resourceGroupName, int retryCount, int secondsIncreaseBetweenRetries, ILoggerFactory loggerFactory)
        {
            this.SubscriptionId = subscriptionId;
            this.ResourceGroupName = resourceGroupName;

            this.RetryCount = retryCount;
            this.SecondsIncreasePerRetry = secondsIncreaseBetweenRetries;

            this.logger = loggerFactory.CreateLogger<IResourcesRepo>();

            this.logger.LogInformation($"ResourceRepository(subscriptionId={this.SubscriptionId}, resourceGroupName={this.ResourceGroupName})");
        }

        public async Task<List<ResourceEntity>> GetAllAsync()
        {
            this.logger.LogTrace($"ResourcesRepository.GetAll() called for {this.SubscriptionId} and {this.ResourceGroupName}.");
            
            // Ensure an instance of the ResourceManagementClient is available
            ConfigureAzure();

            // Try to get all resources for the resource group
            var resourcesInGroup = await AzureMgmt.GenericResources.ListByResourceGroupAsync(this.ResourceGroupName);
            var results = (from l in resourcesInGroup
                           where this.GetResourceFilter(l)
                           select new ResourceEntity { Id = l.Id, Name = l.Name, Location = l.RegionName, Type = l.Type }).ToList();
            
            this.logger.LogTrace($"ResourcesRepository.GetAll() returning {results.Count} items!");

            return results;
        }

        public async Task<ResourceEntity> GetByIdAsync(string id)
        {
            this.logger.LogTrace($"ResourcesRepository.GetAll() called for {this.SubscriptionId} and {this.ResourceGroupName} to get resource '{id}'.");

            if(string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Missing argument 'id'!");

            // Ensure an instance of the ResourceManagementClient is created
            ConfigureAzure();

            // First, need to retrieve the available API versions and pick the latest one
            var idSplitted = id.Split("/");
            var providerIdx = Array.IndexOf<string>(idSplitted, PROVIDER_SECTION);
            var providerName = idSplitted[providerIdx + 1];
            var providerResourceType = idSplitted[providerIdx + 2];

            // Get the resource provider and its API version
            var provider = await AzureMgmt.GenericResources.Manager.Providers.GetByNameAsync(providerName);
            var latestApiVersion = (from p in provider.ResourceTypes
                                    where p.ResourceType == providerResourceType
                                    select p).First().ApiVersions.Last();

            // Try to get the resource and return its details.
            var res = await AzureMgmt.GenericResources.GetByIdAsync(id, latestApiVersion);
            var retVal = new ResourceEntity
                         {
                             Id = res.Id,
                             Name = res.Name,
                             Location = res.RegionName,
                             Type = res.Type,
                             Properties = new Dictionary<string, string>() { 
                                 { "ApiVersion", res.ApiVersion },
                                 { "Provider", res.ResourceProviderNamespace },
                                 { "Plan", (res.Plan != null ? res.Plan.Name : "n/a") },
                                 { "Product", (res.Plan != null ? res.Plan.Product : "n/a") },
                             }
                         };

            this.logger.LogTrace($"Returning resource details for {retVal.Id}!");

            return retVal;
        }

        #region Protected Methods that can be overridden by derived classes

        protected virtual bool GetResourceFilter(IGenericResource res) 
        {
            return true;
        }

        #endregion

        #region Private and protected methods for the class and derived classes

        protected void ConfigureAzure()
        {
            // First try to acquire a token from the MSI endpoint
            var creds = default(AzureCredentials);
            var credentialFactory = new AzureCredentialsFactory();

            // If the ResourceManagementClient does exist, already, just return it.
            if(this.AzureMgmt != null)
            {
                return;
            }
            else
            {
                this.logger.LogInformation("ResourcesRepository.ConfigureAzure() instantiating Azure Management Client...");
            }
            
            // Try acquiring a token (requires refactoring, learning new Fluent libraries as lots has changed from last time (a while ago))
            this.ClientId = Environment.GetEnvironmentVariable(Constants.CLIENT_ID_ENV);
            this.ClientSecret = Environment.GetEnvironmentVariable(Constants.CLIENT_SECRET_ENV);
            this.TenantId = Environment.GetEnvironmentVariable(Constants.TENANT_ID_ENV);

            // The tenant Id is always required (due to eventual RBAC assignments using Graph library which needs the tenant ID)
            if(string.IsNullOrWhiteSpace(this.TenantId))
            {
                throw new Exception($"Missing configuration for {Constants.TENANT_ID_ENV} which is always required!");
            }

            // If not all details for a service principal are present, try MSI.
            this.CredentialsUseSp = !(string.IsNullOrWhiteSpace(this.ClientId) || string.IsNullOrWhiteSpace(this.ClientSecret));
            if(this.CredentialsUseSp) 
            {
                creds = credentialFactory.FromServicePrincipal(this.ClientId, this.ClientSecret, this.TenantId, AzureEnvironment.AzureGlobalCloud);
            }
            else 
            {
                this.logger.LogInformation($"Incomplete details for service principal in environment (clientId, clientSecret or tenantId misssing), trying managed service identity.");
                try 
                {
                    this.logger.LogInformation("ResourceGroupRepository - acquire token from local MSI.");
                    creds = credentialFactory.FromMSI(new MSILoginInformation(MSIResourceType.VirtualMachine),
                                                      AzureEnvironment.AzureGlobalCloud,
                                                      tenantId: this.TenantId)
                                             .WithDefaultSubscription(this.SubscriptionId);
                } 
                catch (MSILoginException msiex)
                {
                    this.logger.LogError($"Failed to acquire token for ResourceProviderRepository with managed service identity: {msiex.Message}!");
                    throw new Exception("Failed acquiring token!", msiex);
                }
            }

            // Token acquired, successfully. Now configure the API Endpoint
            this.AzureMgmt = Azure.Configure()
                                  .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                                  .Authenticate(creds)
                                  .WithSubscription(this.SubscriptionId);

            this.logger.LogInformation("ResourcesRepository.ConfigureAzure() succeeded creating fluent Azure Management Client!");
        }

        protected async Task AssignPermissions(string scope, string servicePrincipalName, string roleName)
        {
            this.logger.LogTrace($"Assigning role {roleName} to identity {servicePrincipalName} to scope {scope}...");

            // Requires the following permissions in RBAC (Contributor is not sufficient)!
            // "actions": [
            //     "Microsoft.Authorization/permissions/read",
            //     "Microsoft.Authorization/roleAssignments/read",
            //     "Microsoft.Authorization/roleAssignments/write",
            //     "Microsoft.Authorization/roleAssignments/delete",
            //     "Microsoft.Authorization/operations/read"
            // ],
            var roleDefinition = await this.AzureMgmt.AccessManagement.RoleDefinitions.GetByScopeAndRoleNameAsync(scope, roleName);
            if(roleDefinition == null) {
                throw new Exception($"Unable to retrieve role {roleName} for resource of scope {scope}!");
            }

            await RetryActionAsync(async () => {
                this.logger.LogTrace("- Attempt to create role assignment...");
                var roleAssignmentId = Guid.NewGuid().ToString();
                await this.AzureMgmt.AccessManagement.RoleAssignments.Define(roleAssignmentId)
                                                                     .ForServicePrincipal(servicePrincipalName)
                                                                     .WithRoleDefinition(roleDefinition.Id)
                                                                     .WithScope(scope)
                                                                     .CreateAsync();
                this.logger.LogTrace("- Attempt succeeded!");
            });
            this.logger.LogInformation($"Assignment of {roleName} created for identity {servicePrincipalName} on {scope}!");
        }

        protected async Task RetryActionAsync(Func<Task> a)
        {
            // Note: for production pruposes, I'd suggest using a framework such as Polly.
            //       but for this sample I kept the implementation and dependencies as simple as possible.
            int currentWaitSecs = this.SecondsIncreasePerRetry;
            for(int currentRun = 0; currentRun < this.RetryCount; currentRun++)
            {
                try
                {
                    await a();
                    break;
                }
                catch(Exception ex)
                {
                    if(currentRun == (this.RetryCount - 1))
                    {
                        throw new Exception($"Retry operation failed with {ex.Message}.", ex);
                    }
                    else
                    {
                        await Task.Delay(currentWaitSecs * 1000);
                        currentWaitSecs += SecondsIncreasePerRetry;
                    }
                }
            }
        }

        #endregion
    }
}