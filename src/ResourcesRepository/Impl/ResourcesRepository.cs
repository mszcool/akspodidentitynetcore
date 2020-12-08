namespace MszCool.PodIdentityDemo.ResourcesRepository
{
    using System;
    using System.Linq;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Rest.Azure.OData;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

    public class ResourcesRepository : IResourcesRepo
    {
        private const string PROVIDER_SECTION = "providers";

        internal string SubscriptionId { get; set; }
        internal string ResourceGroupName { get; set; }

        internal RestClient RestClient { get; set; } = default(RestClient);

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
            CreateRestClient();
            var resMgr = new ResourceManagementClient(this.RestClient);
            resMgr.SubscriptionId = this.SubscriptionId;

            // Try to get all resources for the resource group
            var filter = GetOdataQueryString();
            var resourcesInGroup = await resMgr.Resources.ListByResourceGroupAsync(this.ResourceGroupName, filter);
            var results = (from l in resourcesInGroup
                           select new Resource { Id = l.Id, Name = l.Name, Location = l.Location, Type = l.Type }).ToList();
            
            Trace.TraceInformation($"ResourcesRepository.GetAll() returning {results.Count} items!");

            return results;
        }

        public async Task<Resource> GetByIdAsync(string id)
        {
            Trace.TraceInformation($"ResourcesRepository.GetAll() called for {this.SubscriptionId} and {this.ResourceGroupName} to get resource '{id}'.");

            if(string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Missing argument 'id'!");

            // Ensure an instance of the ResourceManagementClient is created
            CreateRestClient();
            var resMgr = new ResourceManagementClient(this.RestClient);
            resMgr.SubscriptionId = this.SubscriptionId;

            // First, need to retrieve the available API versions and pick the latest one
            var idSplitted = id.Split("/");
            var providerIdx = Array.IndexOf<string>(idSplitted, PROVIDER_SECTION);
            var providerName = idSplitted[providerIdx + 1];
            var providerResourceType = idSplitted[providerIdx + 2];
            var providers = await resMgr.Providers.GetAsync(providerName);
            var latestApiVersion = (from p in providers.ResourceTypes
                                    where p.ResourceType == providerResourceType
                                    select p).First().ApiVersions.Last();

            // Try to get the resource and return its details.
            var res = await resMgr.Resources.GetByIdAsync(id, latestApiVersion);
            var retVal = new Resource 
                         {
                             Id = res.Id,
                             Name = res.Name,
                             Location = res.Location,
                             Type = res.Type,
                             Properties = new Dictionary<string, string>() { 
                                 { "Plan", (res.Plan != null ? res.Plan.Name : "n/a") },
                                 { "Product", (res.Plan != null ? res.Plan.Product : "n/a") },
                                 { "Sku", (res.Sku != null ? res.Sku.Name : "n/a") },
                                 { "Size", (res.Sku != null ? res.Sku.Size : "n/a") },
                                 { "Kind", (res.Kind ?? "n/a") }
                             }
                         };
            return retVal;
        }

        #region Protected Methods that can be overridden by derived classes

        protected virtual ODataQuery<GenericResourceFilter> GetOdataQueryString() 
        {
            return null;
        }

        #endregion

        #region Private and protected methods for the class and derived classes

        protected void CreateRestClient()
        {
            // First try to acquire a token from the MSI endpoint
            var creds = default(AzureCredentials);
            var credentialFactory = new AzureCredentialsFactory();

            // If the ResourceManagementClient does exist, already, just return it.
            if(this.RestClient != default(RestClient)) 
            {
                return;
            }
            else
            {
                Trace.TraceInformation("ResourcesRepository.CreateRestClient() instantiating ResourceManagementClient...");
            }
            
            // Try acquiring a token (requires refactoring, learning new Fluent libraries as lots has changed from last time (a while ago))
            var clientId = Environment.GetEnvironmentVariable(Constants.CLIENT_ID_ENV);
            var clientSecret = Environment.GetEnvironmentVariable(Constants.CLIENT_SECRET_ENV);
            var tenantId = Environment.GetEnvironmentVariable(Constants.TENANT_ID_ENV);

            // If not all details for a service principal are present, try MSI.
            if(string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(tenantId)) 
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
            else 
            {
                creds = credentialFactory.FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);
            }

            // Token acquired, successfully. Now configure the API Endpoint
            this.RestClient = RestClient.Configure()
                                        .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
                                        .WithCredentials(creds)
                                        .Build();

            Trace.TraceInformation("ResourcesRepository.CreateRestClient() succeeded creating ResourceManagementClient!");
        }

        #endregion
    }
}