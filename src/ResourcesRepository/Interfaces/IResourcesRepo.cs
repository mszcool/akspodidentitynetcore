namespace MszCool.PodIdentityDemo.ResourcesRepository.Interfaces
{
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using MszCool.PodIdentityDemo.ResourcesRepository.Entities;

    
    public interface IResourcesRepo 
    {
        Task<List<ResourceEntity>> GetAllAsync();

        Task<ResourceEntity> GetByIdAsync(string id);
    }
}