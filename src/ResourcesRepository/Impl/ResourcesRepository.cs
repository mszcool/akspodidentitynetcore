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

            // Try to get the resource and return its details.
            var res = await resMgr.Resources.GetByIdAsync(id, resMgr.ApiVersion);
            var retVal = new Resource 
                         {
                             Id = res.Id,
                             Name = res.Name,
                             Location = res.Location,
                             Type = res.Type,
                             Properties = new Dictionary<string, string>() { 
                                 { "Plan", res.Plan.Name },
                                 { "Product", res.Plan.Product },
                                 { "Sku", res.Sku.Name },
                                 { "Size", res.Sku.Size },
                                 { "Kind", res.Kind }
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
            try 
            {
                Trace.TraceInformation("ResourceGroupRepository - acquire token from local MSI.");
                creds = credentialFactory.FromMSI(
                    new MSILoginInformation(MSIResourceType.VirtualMachine),
                    AzureEnvironment.AzureGlobalCloud);
            } 
            catch (MSILoginException msiex)
            {
                Trace.TraceInformation($"Failed acquiring token with local MSI endpint: {msiex.Message}, trying with service principal from environment.");
                try 
                {
                    var clientId = Environment.GetEnvironmentVariable(Constants.CLIENT_ID_ENV);
                    var clientSecret = Environment.GetEnvironmentVariable(Constants.CLIENT_SECRET_ENV);
                    var tenantId = Environment.GetEnvironmentVariable(Constants.TENANT_ID_ENV);

                    if(string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(tenantId)) 
                    {
                        var errorMsg = "Missing details for Service Principal based login in environment!";
                        Trace.TraceError(errorMsg);
                        throw new Exception(errorMsg);
                    }

                    creds = credentialFactory.FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);
                } 
                catch(Exception tokenex)
                {
                    Trace.TraceError($"Failed to acquire token for ResourceProviderRepository; {tokenex.Message}!");
                    throw new Exception("Failed acquiring token!", tokenex);
                }
            }

            // Token acquired, successfully. Now configure the API Endpoint
            var restClient = RestClient.Configure()
                                       .WithBaseUri("https://management.azure.com/")
                                       .WithCredentials(creds)
                                       .Build();

            Trace.TraceInformation("ResourcesRepository.CreateRestClient() succeeded creating ResourceManagementClient!");
        }

        #endregion
    }
}