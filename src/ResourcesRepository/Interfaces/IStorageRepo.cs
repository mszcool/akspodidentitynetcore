namespace MszCool.PodIdentityDemo.ResourcesRepository
{
    using System.Threading.Tasks;
    
    public enum StorageType
    {
        Blob,
        Datalake
    }

    public interface IStorageRepo : IResourcesRepo 
    {
        Task CreateAsync(string name, StorageType typeOfStorage);
    }
}