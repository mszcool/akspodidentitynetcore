namespace MszCool.Samples.PodIdentityDemo.ResourcesRepository
{
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

        public void ConfigureEnvironment(string clientId, string clientSecret, string tenantId)
        {
            System.Environment.SetEnvironmentVariable(Constants.CLIENT_ID_ENV, clientId);
            System.Environment.SetEnvironmentVariable(Constants.CLIENT_SECRET_ENV, clientSecret);
            System.Environment.SetEnvironmentVariable(Constants.TENANT_ID_ENV, tenantId);
        }

        public IResourcesRepo CreateResourcesRepo()
        {
            return new ResourcesRepositoryImpl(this.SubscriptionId, this.ResourceGroupName, RetryCount, SecondsIncreaseBetweenRetries);
        }

        public IStorageRepo CreateStorageRepo()
        {
            return new StorageRepositoryImpl(this.SubscriptionId, this.ResourceGroupName, RetryCount, SecondsIncreaseBetweenRetries);
        }
    }
}
