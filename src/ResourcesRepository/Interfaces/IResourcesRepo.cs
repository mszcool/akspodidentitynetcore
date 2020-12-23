namespace MszCool.Samples.PodIdentityDemo.ResourcesRepository.Interfaces
{
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Entities;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    
    public interface IResourcesRepo 
    {
        Task<List<ResourceEntity>> GetAllAsync();

        Task<ResourceEntity> GetByIdAsync(string id);
    }
}