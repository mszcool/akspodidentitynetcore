namespace MszCool.Samples.PodIdentityDemo.ResourcesRepository
{
    using Microsoft.Extensions.Logging;
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Interfaces;
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.InternalImplementations;

    public enum SupportedRepos
    {
        Generic,
        StorageAccounts
    }

    public class RepositoryFactory
    {
        public int RetryCount { get; private set; }
        public int SecondsIncreaseBetweenRetries { get; private set; }
        public string SubscriptionId { get; private set; }
        public string ResourceGroupName { get; private set; }

        public RepositoryFactory(string subscriptionId, string resourceGroupName, int retryCount = 6, int secondsIncreaseBetweenRetries = 10)
        {
            this.SubscriptionId = subscriptionId;
            this.ResourceGroupName = resourceGroupName;

            this.RetryCount = retryCount;
            this.SecondsIncreaseBetweenRetries = secondsIncreaseBetweenRetries;
        }

        public void ConfigureEnvironment(string tenantId)
        {
            System.Environment.SetEnvironmentVariable(Constants.TENANT_ID_ENV, tenantId);
        }

        public void ConfigureEnvironment(string clientId, string clientSecret, string tenantId)
        {
            ConfigureEnvironment(tenantId);
            System.Environment.SetEnvironmentVariable(Constants.CLIENT_ID_ENV, clientId);
            System.Environment.SetEnvironmentVariable(Constants.CLIENT_SECRET_ENV, clientSecret);
        }

        public IResourcesRepo CreateResourcesRepo(ILoggerFactory loggerFactory)
        {
            return new ResourcesRepositoryImpl(this.SubscriptionId, this.ResourceGroupName, RetryCount, SecondsIncreaseBetweenRetries, loggerFactory);
        }

        public IStorageRepo CreateStorageRepo(ILoggerFactory loggerFactory, bool useTemplateStrategy)
        {
            if(useTemplateStrategy)
            {
                return new StorageByTemplateRepositoryImpl(this.SubscriptionId, this.ResourceGroupName, RetryCount, SecondsIncreaseBetweenRetries, loggerFactory);
            }
            else
            {
                return new StorageRepositoryImpl(this.SubscriptionId, this.ResourceGroupName, RetryCount, SecondsIncreaseBetweenRetries, loggerFactory);
            }
        }
    }
}