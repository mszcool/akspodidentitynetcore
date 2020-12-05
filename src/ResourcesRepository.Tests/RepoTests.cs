using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

namespace ResourcesRepository.Tests
{
    [TestClass]
    public class RepoTests
    {
        public const string LOCAL_DEV_ENV = "LOCAL_DEV";
        public const string TEST_SUBSCRIPTION_ID = "TEST_SUBSCRIPTION_ID";
        public const string TEST_RG_NAME_ENV = "TEST_RESOURCE_GROUP";
        public const string CLIENT_ID_ENV = "TEST_CLIENT_ID";
        public const string CLIENT_SECRET_ENV = "TEST_CLIENT_SECRET";
        public const string TENANT_ID_ENV = "TEST_TENANT_ID";

        private string resourceGroupName;
        private ResourceGroupInner resourceGroup;
        private List<GenericResourceInner> resourcesInGroup;
        private RestClient restClient;
        private AzureCredentialsFactory credFactory = new AzureCredentialsFactory();
        
        #region Test preparation and clean-up

        [TestInitialize]
        public async Task PrepareForTests()
        {
            // Create the needed ResourceManagementClient based on RestClient and Auth
            CreateRestClient();
            var rmClient = new ResourceManagementClient(this.restClient);
            rmClient.SubscriptionId = Environment.GetEnvironmentVariable(TEST_SUBSCRIPTION_ID) ?? restClient.Credentials.DefaultSubscriptionId;

            // Create a resource group
            resourceGroupName = Environment.GetEnvironmentVariable(TEST_RG_NAME_ENV);
            resourceGroup = await rmClient.ResourceGroups.GetAsync(resourceGroupName);
            if(resourceGroup == null) {
                // The initialize method could deploy resources, but that takes too much time for each test run.
                // I am aware these are more than just "unit tests", making compromises for sake of simplicity and focus.
                throw new InvalidProgramException($"Resource group {resourceGroupName} used for test validation does not exist. Please deploy pre-requisites for test.");
            }
            var resInGroup = await rmClient.Resources.ListByResourceGroupAsync(resourceGroupName);
            resourcesInGroup = new List<GenericResourceInner>(resInGroup);
        }

        [TestCleanup]
        public void CleanUpAfterTests()
        {
            // Nothing to do as decided to not dynamically create resources for the test for simplicity reasons.
        }

        #endregion

        #region TestMethods

        [TestMethod]
        public void GetAllResourcesInGroupTest()
        {
        }

        [TestMethod]
        public void GetSpecificResourceInGroupTest() 
        {
            throw new System.Exception("Haha");
        }

        #endregion

        #region Private Helpers

        private void CreateRestClient() {
            var credentials = default(AzureCredentials);

            // For local dev, rely on an auth file, otherwise on a service principal set in the environment of the build agent.
            var localAuthFile = Environment.GetEnvironmentVariable(LOCAL_DEV_ENV);
            if(string.IsNullOrWhiteSpace(localAuthFile))
            {
                var clientId = Environment.GetEnvironmentVariable(CLIENT_ID_ENV);
                var clientSecret = Environment.GetEnvironmentVariable(CLIENT_SECRET_ENV);
                var tenantId = Environment.GetEnvironmentVariable(TENANT_ID_ENV);

                credentials = credFactory.FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);
            }
            else
            {
                // Create the file with "az ad sp create-for-rbac --sdk-auth"
                credentials = credFactory.FromFile(localAuthFile);
            }

            // Create the rest client based on the authentication above.
            restClient = RestClient.Configure()
                                   .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
                                   .WithCredentials(credentials)
                                   .Build();
        }

        #endregion
    }
}