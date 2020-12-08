using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Testee = MszCool.PodIdentityDemo.ResourcesRepository;

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

        public TestContext TestContext { get; set; }

        private string subscriptionId;
        private string resourceGroupName;
        private ResourceGroupInner resourceGroup;
        private List<GenericResourceInner> resourcesInGroup;
        private RestClient restClient;
        private AzureCredentialsFactory credFactory = new AzureCredentialsFactory();

        #region Test preparation and clean-up


        [TestInitialize]
        public async Task PrepareForTests()
        {
            // Get the needed baseline parameters
            this.subscriptionId = Environment.GetEnvironmentVariable(TEST_SUBSCRIPTION_ID) ?? restClient.Credentials.DefaultSubscriptionId;
            this.resourceGroupName = Environment.GetEnvironmentVariable(TEST_RG_NAME_ENV);

            // Create the needed ResourceManagementClient based on RestClient and Auth
            CreateRestClient();
            var rmClient = new ResourceManagementClient(this.restClient);
            rmClient.SubscriptionId = subscriptionId;

            // Create a resource group
            this.resourceGroup = await rmClient.ResourceGroups.GetAsync(resourceGroupName);
            if (resourceGroup == null)
            {
                // The initialize method could deploy resources, but that takes too much time for each test run.
                // I am aware these are more than just "unit tests", making compromises for sake of simplicity and focus.
                throw new InvalidProgramException($"Resource group {resourceGroupName} used for test validation does not exist. Please deploy pre-requisites for test.");
            }
            var resInGroup = await rmClient.Resources.ListByResourceGroupAsync(resourceGroupName);
            this.resourcesInGroup = new List<GenericResourceInner>(resInGroup);
            this.resourcesInGroup.Sort(delegate (GenericResourceInner r1, GenericResourceInner r2)
            {
                return string.Compare(r1.Id, r2.Id, StringComparison.InvariantCultureIgnoreCase);
            });

            // Set the environment variables needed for the test target
            SetTesteeEnvironmentVariables();
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
            // Instantiate the tested calss
            TestContext.WriteLine($"Creating ResourcesRepository for {this.subscriptionId}...");
            var factory = new Testee.RepositoryFactory(this.subscriptionId, this.resourceGroupName);
            var repoToTest = factory.CreateResourcesRepo();

            // Get the resources in the resource group
            TestContext.WriteLine($"Getting resources from resource group {this.resourceGroupName}...");
            var testExecTask = repoToTest.GetAllAsync();
            testExecTask.Wait();
            var resourcesAsIs = testExecTask.Result;

            // It should return not null all the time and the number of resources should be equal to what the test determined in its initialization.
            TestContext.WriteLine("Test assertions...");
            Assert.IsNotNull(resourcesAsIs);
            Assert.AreEqual(resourcesInGroup.Count, resourcesAsIs.Count);

            // Compare the resources found ordered by ID
            resourcesAsIs.Sort(delegate (Testee.Resource a, Testee.Resource b)
            {
                return string.Compare(a.Id, b.Id, StringComparison.InvariantCulture);
            });
            for (int i = 0; i < resourcesAsIs.Count; i++)
            {
                Assert.AreEqual(resourcesInGroup[i].Id, resourcesAsIs[i].Id, true);
                Assert.AreEqual(resourcesInGroup[i].Name, resourcesAsIs[i].Name, true);
                Assert.AreEqual(resourcesInGroup[i].Location, resourcesAsIs[i].Location, true);
            }

            TestContext.WriteLine("Succeeded!");
        }

        [TestMethod]
        public void GetSpecificResourceInGroupTest()
        {
            // Instantiate the tested calss
            TestContext.WriteLine($"Creating ResourcesRepository for {this.subscriptionId}...");
            var factory = new Testee.RepositoryFactory(this.subscriptionId, this.resourceGroupName);
            var repoToTest = factory.CreateResourcesRepo();

            // Get the resources in the resource group
            TestContext.WriteLine($"Trying to get {resourcesInGroup.Count} resources from {this.resourceGroupName}...");
            foreach(var resourceExpected in this.resourcesInGroup)
            {
                TestContext.WriteLine($"- Getting resource {resourceExpected.Id}...");
                var testTaskExec = repoToTest.GetByIdAsync(resourceExpected.Id);
                testTaskExec.Wait();
                var resourceAsIs = testTaskExec.Result;

                // The resource should have been found
                TestContext.WriteLine($"- Assertions for resource {resourceExpected.Id}...");
                Assert.IsNotNull(resourceAsIs);
                Assert.AreEqual(resourceExpected.Id, resourceAsIs.Id, true);
                Assert.AreEqual(resourceExpected.Name, resourceAsIs.Name, true);
                Assert.AreEqual(resourceExpected.Location, resourceAsIs.Location, true);
            }

            TestContext.WriteLine("Succeeded!");
        }

        #endregion

        #region Private Helpers

        private void CreateRestClient()
        {
            var credentials = default(AzureCredentials);

            // For local dev, rely on an auth file, otherwise on a service principal set in the environment of the build agent.
            var localAuthFile = Environment.GetEnvironmentVariable(LOCAL_DEV_ENV);
            if (string.IsNullOrWhiteSpace(localAuthFile))
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

        private void SetTesteeEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable(Testee.Constants.CLIENT_ID_ENV, restClient.Credentials.ClientId);
            Environment.SetEnvironmentVariable(Testee.Constants.TENANT_ID_ENV, restClient.Credentials.TenantId);

            var localFilePath = Environment.GetEnvironmentVariable(LOCAL_DEV_ENV);
            if (string.IsNullOrEmpty(localFilePath))
            {
                Environment.SetEnvironmentVariable(Testee.Constants.CLIENT_SECRET_ENV,
                                                   Environment.GetEnvironmentVariable(CLIENT_SECRET_ENV));
            }
            else
            {
                // Read the JSON content from the file
                using (var jReader = new JsonTextReader(new System.IO.StreamReader(localFilePath)))
                {
                    while (jReader.Read())
                    {
                        // A bit of a hack to get it running for now
                        // When the path is the property name, but the value not, then the current token is the actual value.
                        if (jReader.Path.Equals("clientSecret") && !jReader.Value.ToString().Equals("clientSecret"))
                        {
                            Environment.SetEnvironmentVariable(Testee.Constants.CLIENT_SECRET_ENV, jReader.Value.ToString());
                            break;
                        }
                    }
                }
            }
        }

        #endregion
    }
}