using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Az = Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Newtonsoft.Json;
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
        private IResourceGroup resourceGroup;
        private List<IGenericResource> resourcesInGroup;
        private Az.IAzure TestAzure;
        private AzureCredentialsFactory credFactory = new AzureCredentialsFactory();

        #region Test preparation and clean-up


        [TestInitialize]
        public async Task PrepareForTests()
        {
            // Authentication and Azure RestClient creation
            InitAzureEnvironment();

            // Create a resource group
            this.resourceGroup = await TestAzure.ResourceGroups.GetByNameAsync(this.resourceGroupName);
            if (resourceGroup == null)
            {
                // The initialize method could deploy resources, but that takes too much time for each test run.
                // I am aware these are more than just "unit tests", making compromises for sake of simplicity and focus.
                throw new InvalidProgramException($"Resource group {resourceGroupName} used for test validation does not exist. Please deploy pre-requisites for test.");
            }
            var resInGroup = await TestAzure.GenericResources.ListByResourceGroupAsync(this.resourceGroupName);
            this.resourcesInGroup = new List<IGenericResource>(resInGroup);
            this.resourcesInGroup.Sort(delegate (IGenericResource r1, IGenericResource r2)
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
        public async Task GetAllResourcesInGroupTest()
        {
            // Instantiate the tested calss
            TestContext.WriteLine($"Creating ResourcesRepository for {this.subscriptionId}...");
            var factory = new Testee.RepositoryFactory(this.subscriptionId, this.resourceGroupName);
            var repoToTest = factory.CreateResourcesRepo();

            // Get the resources in the resource group
            TestContext.WriteLine($"Getting resources from resource group {this.resourceGroupName}...");
            var resourcesAsIs = await repoToTest.GetAllAsync();

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
                Assert.AreEqual(resourcesInGroup[i].RegionName, resourcesAsIs[i].Location, true);
            }

            TestContext.WriteLine("Succeeded!");
        }

        [TestMethod]
        public async Task GetSpecificResourceInGroupTest()
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

                // Execute the method to test
                var resourceAsIs = await repoToTest.GetByIdAsync(resourceExpected.Id);

                // The resource should have been found
                TestContext.WriteLine($"- Assertions for resource {resourceExpected.Id}...");
                Assert.IsNotNull(resourceAsIs);
                Assert.AreEqual(resourceExpected.Id, resourceAsIs.Id, true);
                Assert.AreEqual(resourceExpected.Name, resourceAsIs.Name, true);
                Assert.AreEqual(resourceExpected.RegionName, resourceAsIs.Location, true);
            }

            TestContext.WriteLine("Succeeded!");
        }

        [TestMethod]
        public async Task CreateStorageAccountTest()
        {
            // Instantiate the tested class
            TestContext.WriteLine($"Creating StorageRepository for {this.subscriptionId}...");
            var factory = new Testee.RepositoryFactory(this.subscriptionId, this.resourceGroupName);
            var repoToTest = factory.CreateStorageRepo();

            // Generate a unique name for the storage account
            string uniqueName = GenerateUniqueName();

            // Try to create that storage account
            TestContext.WriteLine($"Trying to create storage account in resource group {this.resourceGroupName}...");
            await repoToTest.CreateAsync(uniqueName, this.resourceGroup.RegionName, Testee.StorageType.Blob, Testee.Sku.Basic);

            // Try to retrieve the storage account
            TestContext.WriteLine("Trying to find storage account in resource group {this.resourceGroupName}...");
            var foundAccount = await TestAzure.StorageAccounts.GetByResourceGroupAsync(this.resourceGroupName, uniqueName);
            Assert.IsNotNull(foundAccount);
            Assert.AreEqual(foundAccount.AccountStatuses.Primary.GetValueOrDefault(), Microsoft.Azure.Management.Storage.Fluent.Models.AccountStatus.Available);

            TestContext.WriteLine("Succeeded!");
        }

        [TestMethod]
        public async Task CreateAdlsStorageAccountTest()
        {
            // Instantiate the tested class
            TestContext.WriteLine($"Creating ADLS StorageRepository for {this.subscriptionId}...");
            var factory = new Testee.RepositoryFactory(this.subscriptionId, this.resourceGroupName);
            var repoToTest = factory.CreateStorageRepo();

            // Generate a unique name for the storage account
             var uniqueName = GenerateUniqueName();

            // Try to create that storage account
            TestContext.WriteLine($"Trying to create ADLS storage account in resource group {this.resourceGroupName}...");
            await repoToTest.CreateAsync(uniqueName, this.resourceGroup.RegionName, Testee.StorageType.Datalake, Testee.Sku.Standard, "http://azure-cli-2020-12-05-20-10-11");

            // Try to retrieve the storage account
            TestContext.WriteLine("Trying to find ADLS storage account in resource group {this.resourceGroupName}...");
            var foundAccount = await TestAzure.StorageAccounts.GetByResourceGroupAsync(this.resourceGroupName, uniqueName);
            Assert.IsNotNull(foundAccount);
            Assert.AreEqual(foundAccount.AccountStatuses.Primary.GetValueOrDefault(), Microsoft.Azure.Management.Storage.Fluent.Models.AccountStatus.Available);

            TestContext.WriteLine("Succeeded!");
        }

        #endregion

        #region Private Helpers

        private void InitAzureEnvironment()
        {
            var credentials = default(AzureCredentials);

            // Read required environment variables
            this.subscriptionId = Environment.GetEnvironmentVariable(TEST_SUBSCRIPTION_ID);
            this.resourceGroupName = Environment.GetEnvironmentVariable(TEST_RG_NAME_ENV);

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
                
                // Assign the default subscription ID if none has been provided in the environment
                if(string.IsNullOrWhiteSpace(this.subscriptionId)) {
                    this.subscriptionId = credentials.DefaultSubscriptionId;
                }
            }

            // Create the rest client based on the authentication above.
            TestAzure = Az.Azure.Configure()
                                .WithLogLevel(Microsoft.Azure.Management.ResourceManager.Fluent.Core.HttpLoggingDelegatingHandler.Level.Headers)
                                .Authenticate(credentials)
                                .WithDefaultSubscription();
        }

        private void SetTesteeEnvironmentVariables()
        {
            var localFilePath = Environment.GetEnvironmentVariable(LOCAL_DEV_ENV);
            if (string.IsNullOrEmpty(localFilePath))
            {
                Environment.SetEnvironmentVariable(Testee.Constants.CLIENT_ID_ENV, 
                                                   Environment.GetEnvironmentVariable(CLIENT_ID_ENV));
                Environment.SetEnvironmentVariable(Testee.Constants.TENANT_ID_ENV, 
                                                   Environment.GetEnvironmentVariable(CLIENT_SECRET_ENV));                
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
                        }
                        else if(jReader.Path.Equals("clientId") && !jReader.Value.ToString().Equals("clientId"))
                        {
                            Environment.SetEnvironmentVariable(Testee.Constants.CLIENT_ID_ENV, jReader.Value.ToString());
                        }
                        else if(jReader.Path.Equals("tenantId") && !jReader.Value.ToString().Equals("tenantId")) 
                        {
                            Environment.SetEnvironmentVariable(Testee.Constants.TENANT_ID_ENV, jReader.Value.ToString());
                        }
                    }
                }
            }
        }

        private static string GenerateUniqueName()
        {
            // Thanks to Mads Kristensen's blog :)
            // https://www.madskristensen.net/blog/generate-unique-strings-and-numbers-in-c/
            var uniqueId = (long)1;
            foreach (byte b in Guid.NewGuid().ToByteArray())
            {
                uniqueId *= ((int)b + 1);
            }
            var uniqueName = string.Format("mszt{0:x}", uniqueId - DateTime.Now.Ticks);
            return uniqueName;
        }

        #endregion
    }
}