namespace MszCool.PodIdentityDemo.ResourcesRepository
{
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    
    public interface IResourcesRepo 
    {
        Task<List<Resource>> GetAllAsync();

        Task<Resource> GetByIdAsync(string id);
    }
}