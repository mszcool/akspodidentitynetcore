namespace MszCool.PodIdentityDemo.ResourcesRepository
{
    using System;
    using System.Linq;
    using System.Security;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Rest.Azure.OData;
    using Microsoft.Azure.Management.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

    public class ResourcesRepository : IResourcesRepo
    {
        private const string PROVIDER_SECTION = "providers";

        protected IAzure AzureMgmt { get; private set; }
        protected bool CredentialsUseSp { get; private set; }
        protected string ClientId { get; private set; }
        protected string ClientSecret { get; private set; }
        protected string TenantId { get; private set; }
        protected string SubscriptionId { get; private set; }
        protected string ResourceGroupName { get; private set; }

        internal ResourcesRepository(string subscriptionId, string resourceGroupName)
        {
            this.SubscriptionId = subscriptionId;
            this.ResourceGroupName = resourceGroupName;

            Trace.TraceInformation($"ResourceRepository(subscriptionId={this.SubscriptionId}, resourceGroupName={this.ResourceGroupName})");
        }

        public async Task<List<Resource>> GetAllAsync()
        {
            Trace.TraceInformation($"ResourcesRepository.GetAll() called for {this.SubscriptionId} and {this.ResourceGroupName}.");
            
            // Ensure an instance of the ResourceManagementClient is available
            ConfigureAzure();

            // Try to get all resources for the resource group
            var resourcesInGroup = await AzureMgmt.GenericResources.ListByResourceGroupAsync(this.ResourceGroupName);
            var results = (from l in resourcesInGroup
                           where this.GetResourceFilter(l)
                           select new Resource { Id = l.Id, Name = l.Name, Location = l.RegionName, Type = l.Type }).ToList();
            
            Trace.TraceInformation($"ResourcesRepository.GetAll() returning {results.Count} items!");

            return results;
        }

        public async Task<Resource> GetByIdAsync(string id)
        {
            Trace.TraceInformation($"ResourcesRepository.GetAll() called for {this.SubscriptionId} and {this.ResourceGroupName} to get resource '{id}'.");

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
            var retVal = new Resource 
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
                Trace.TraceInformation("ResourcesRepository.ConfigureAzure() instantiating Azure Management Client...");
            }
            
            // Try acquiring a token (requires refactoring, learning new Fluent libraries as lots has changed from last time (a while ago))
            this.ClientId = Environment.GetEnvironmentVariable(Constants.CLIENT_ID_ENV);
            this.ClientSecret = Environment.GetEnvironmentVariable(Constants.CLIENT_SECRET_ENV);
            this.TenantId = Environment.GetEnvironmentVariable(Constants.TENANT_ID_ENV);

            // If not all details for a service principal are present, try MSI.
            this.CredentialsUseSp = !(string.IsNullOrWhiteSpace(this.ClientId) || string.IsNullOrWhiteSpace(this.ClientSecret) || string.IsNullOrWhiteSpace(this.TenantId));
            if(this.CredentialsUseSp) 
            {
                creds = credentialFactory.FromServicePrincipal(this.ClientId, this.ClientSecret, this.TenantId, AzureEnvironment.AzureGlobalCloud);
            }
            else 
            {
                Trace.TraceInformation($"Incomplete details for service principal in environment (clientId, clientSecret or tenantId misssing), trying managed service identity.");
                try 
                {
                    Trace.TraceInformation("ResourceGroupRepository - acquire token from local MSI.");
                    creds = credentialFactory.FromMSI(new MSILoginInformation(MSIResourceType.VirtualMachine),
                                                      AzureEnvironment.AzureGlobalCloud)
                                             .WithDefaultSubscription(this.SubscriptionId);
                } 
                catch (MSILoginException msiex)
                {
                    Trace.TraceError($"Failed to acquire token for ResourceProviderRepository with managed service identity: {msiex.Message}!");
                    throw new Exception("Failed acquiring token!", msiex);
                }
            }

            // Token acquired, successfully. Now configure the API Endpoint
            this.AzureMgmt = Azure.Configure()
                                   .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                                   .Authenticate(creds)
                                   .WithSubscription(this.SubscriptionId);

            Trace.TraceInformation("ResourcesRepository.ConfigureAzure() succeeded creating fluent Azure Management Client!");
        }

        #endregion
    }
}