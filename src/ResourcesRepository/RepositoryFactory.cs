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
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }

        public RepositoryFactory(string subscriptionId, string resourceGroupName)
        {
            this.SubscriptionId = subscriptionId;
            this.ResourceGroupName = resourceGroupName;
        }

        public IResourcesRepo CreateResourcesRepo()
        {
            return new ResourcesRepository(this.SubscriptionId, this.ResourceGroupName);
        }

        public IStorageRepo CreateStorageRepo()
        {
            return new StorageRepository(this.SubscriptionId, this.ResourceGroupName);
        }
    }
}
