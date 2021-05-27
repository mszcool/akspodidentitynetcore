using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Az = Microsoft.Azure.Management.Fluent;
using Testee = MszCool.Samples.PodIdentityDemo.ResourcesRepository;
using System.Globalization;
using System.Security.Cryptography;

namespace MszCool.Samples.PodIdentityDemo.ResourcesRepository.Tests
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
        private ILoggerFactory loggingBuilder;

        #region Test preparation and clean-up


        [TestInitialize]
        public async Task PrepareForTests()
        {
            // Create a logging factory that's passed into the testee
            loggingBuilder = LoggerFactory.Create(builder => {
                builder.AddConsole();
                builder.AddDebug();
                builder.AddFilter("Microosft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);
                builder.AddFilter("Default", LogLevel.Trace);
            });

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
        }

        [TestCleanup]
        public void CleanUpAfterTests()
        {
            // Nothing to do as decided to not dynamically create resources for the test for simplicity reasons.
        }

        #endregion

        #region TestMethods

        [TestMethod]
        public Task GenerateUniqueNamesTest()
        {
            int namesToGen = 100000000;
            var existingNames = new Dictionary<string, int>();

            // Generate 10 million names
            for(int i = 0; i < namesToGen; i ++)
            {
                var name = GenerateUniqueName("msz");
                existingNames.Add(name, i);
            }

            // Output the number of names generated
            TestContext.WriteLine($"Created unique names {existingNames.Keys.Count}...");
            Assert.AreEqual(namesToGen, existingNames.Keys.Count);
            
            // No exception happened, test passed.
            return Task.CompletedTask;
        }

        [TestMethod]
        public async Task GetAllResourcesInGroupTest()
        {
            // Instantiate the tested calss
            TestContext.WriteLine($"Creating ResourcesRepository for {this.subscriptionId}...");
            var factory = new Testee.RepositoryFactory(this.subscriptionId, this.resourceGroupName);
            var repoToTest = factory.CreateResourcesRepo(this.loggingBuilder);
            SetTesteeEnvironmentVariables(factory);

            // Get the resources in the resource group
            TestContext.WriteLine($"Getting resources from resource group {this.resourceGroupName}...");
            var resourcesAsIs = await repoToTest.GetAllAsync();

            // It should return not null all the time and the number of resources should be equal to what the test determined in its initialization.
            TestContext.WriteLine("Test assertions...");
            Assert.IsNotNull(resourcesAsIs);
            Assert.AreEqual(resourcesInGroup.Count, resourcesAsIs.Count);

            // Compare the resources found ordered by ID
            resourcesAsIs.Sort(delegate (Testee.Entities.ResourceEntity a, Testee.Entities.ResourceEntity b)
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
            var repoToTest = factory.CreateResourcesRepo(this.loggingBuilder);
            SetTesteeEnvironmentVariables(factory);

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
            await this.CreateStorageAccountTestImpl(false);
        }

        [TestMethod]
        public async Task CreateAdlsStorageAccountTest()
        {
            await this.CreateAdlsStorageAccountTestImpl(false);
        }

        [TestMethod]
        public async Task CreateStorageAccountByTemplateTest()
        {
            await this.CreateStorageAccountTestImpl(true);
        }

        [TestMethod]
        public async Task CreateAdlsStorageAccountByTemplateTest()
        {
            await this.CreateAdlsStorageAccountTestImpl(true);
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

        private void SetTesteeEnvironmentVariables(RepositoryFactory factory)
        {
            var localFilePath = Environment.GetEnvironmentVariable(LOCAL_DEV_ENV);
            if (string.IsNullOrEmpty(localFilePath))
            {
                factory.ConfigureEnvironment(
                    Environment.GetEnvironmentVariable(CLIENT_ID_ENV),
                    Environment.GetEnvironmentVariable(CLIENT_SECRET_ENV),
                    Environment.GetEnvironmentVariable(TENANT_ID_ENV)
                );
            }
            else
            {
                var clientId = "";
                var clientSecret = "";
                var tenantId = "";

                // Read the JSON content from the file
                using (var jReader = new JsonTextReader(new System.IO.StreamReader(localFilePath)))
                {
                    while (jReader.Read())
                    {
                        // A bit of a hack to get it running for now
                        // When the path is the property name, but the value not, then the current token is the actual value.
                        if (jReader.Path.Equals("clientSecret") && !jReader.Value.ToString().Equals("clientSecret"))
                        {
                            clientSecret = jReader.Value.ToString();
                        }
                        else if(jReader.Path.Equals("clientId") && !jReader.Value.ToString().Equals("clientId"))
                        {
                            clientId = jReader.Value.ToString();
                        }
                        else if(jReader.Path.Equals("tenantId") && !jReader.Value.ToString().Equals("tenantId")) 
                        {
                            tenantId = jReader.Value.ToString();
                        }
                    }
                }

                // Update the environment
                factory.ConfigureEnvironment(clientId, clientSecret, tenantId);
            }
        }

        private static string GenerateUniqueName(string prefix)
        {
            // Thanks to Mads Kristensen's blog :)
            // https://www.madskristensen.net/blog/generate-unique-strings-and-numbers-in-c/
            // var uniqueId = (long)1;
            // foreach (byte b in Guid.NewGuid().ToByteArray())
            // {
            //     uniqueId *= ((int)b + 1);
            // }
            // var uniqueName = string.Format("{0}{1:x}", prefix, uniqueId - DateTime.Now.Ticks);
            // return uniqueName;

            var allowableCharacters = "abcdefghijklmnopqrstuvwxyz0123456789";
            var bytes = new byte[24 - prefix.Length];

            // Generate a random string.
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return string.Concat(
                prefix,
                new string(bytes.Select(x => allowableCharacters[x % allowableCharacters.Length]).ToArray())
            );
        }

        #endregion

        #region Private Test Implementation Helpers

        public async Task CreateStorageAccountTestImpl(bool useTemplateStrategy)
        {
            // Instantiate the tested class
            TestContext.WriteLine($"Creating StorageRepository for {this.subscriptionId}...");
            var factory = new Testee.RepositoryFactory(this.subscriptionId, this.resourceGroupName);
            var repoToTest = factory.CreateStorageRepo(this.loggingBuilder, useTemplateStrategy);
            SetTesteeEnvironmentVariables(factory);

            // Generate a unique name for the storage account
            string uniqueName = GenerateUniqueName("mszstbl");

            // Try to create that storage account
            TestContext.WriteLine($"Trying to create storage account in resource group {this.resourceGroupName}...");
            await repoToTest.CreateAsync(uniqueName, this.resourceGroup.RegionName, Testee.Interfaces.StorageType.Blob, Testee.Sku.Basic);

            // Try to retrieve the storage account
            TestContext.WriteLine("Trying to find storage account in resource group {this.resourceGroupName}...");
            var foundAccount = await TestAzure.StorageAccounts.GetByResourceGroupAsync(this.resourceGroupName, uniqueName);
            Assert.IsNotNull(foundAccount);
            Assert.AreEqual(foundAccount.AccountStatuses.Primary.GetValueOrDefault(), Microsoft.Azure.Management.Storage.Fluent.Models.AccountStatus.Available);

            TestContext.WriteLine("Succeeded!");
        }

        public async Task CreateAdlsStorageAccountTestImpl(bool useTemplateStrategy)
        {
            // Instantiate the tested class
            TestContext.WriteLine($"Creating ADLS StorageRepository for {this.subscriptionId}...");
            var factory = new Testee.RepositoryFactory(this.subscriptionId, this.resourceGroupName);
            var repoToTest = factory.CreateStorageRepo(this.loggingBuilder, useTemplateStrategy);
            SetTesteeEnvironmentVariables(factory);

            // Generate a unique name for the storage account
            var uniqueName = GenerateUniqueName("mszstdl");

            // Try to create that storage account
            TestContext.WriteLine($"Trying to create ADLS storage account in resource group {this.resourceGroupName}...");
            await repoToTest.CreateAsync(uniqueName, this.resourceGroup.RegionName, Testee.Interfaces.StorageType.Datalake, Testee.Sku.Standard, "http://azure-cli-2020-12-05-20-10-11");

            // Try to retrieve the storage account
            TestContext.WriteLine("Trying to find ADLS storage account in resource group {this.resourceGroupName}...");
            var foundAccount = await TestAzure.StorageAccounts.GetByResourceGroupAsync(this.resourceGroupName, uniqueName);
            Assert.IsNotNull(foundAccount);
            Assert.AreEqual(foundAccount.AccountStatuses.Primary.GetValueOrDefault(), Microsoft.Azure.Management.Storage.Fluent.Models.AccountStatus.Available);

            TestContext.WriteLine("Succeeded!");
        }
        
        #endregion
    }
}