using System;

namespace MszCool.PodIdentityDemo.ResourcesRepository
{
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

        public IResourcesRepo CreateResourcesRepo()
        {
            return new ResourcesRepository(this.SubscriptionId, this.ResourceGroupName, RetryCount, SecondsIncreaseBetweenRetries);
        }

        public IStorageRepo CreateStorageRepo()
        {
            return new StorageRepository(this.SubscriptionId, this.ResourceGroupName, RetryCount, SecondsIncreaseBetweenRetries);
        }
    }
}
