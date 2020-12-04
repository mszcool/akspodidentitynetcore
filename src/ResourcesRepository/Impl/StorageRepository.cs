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

    public class StorageRepository : ResourcesRepository, IStorageRepo
    {

        public StorageRepository(string subscriptionId, string resourceGroupName)
        : base(subscriptionId, resourceGroupName)
        {
        }

        public Task CreateAsync(string name, StorageType typeOfStorage)
        {
            throw new NotImplementedException();
        }

        #region Methods overridden from base class

        protected override ODataQuery<GenericResourceFilter> GetOdataQueryString() 
        {
            return null;
        }
        #endregion
    }
}