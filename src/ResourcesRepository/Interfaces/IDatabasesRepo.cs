namespace MszCool.PodIdentityDemo.ResourcesRepository
{
    using System;
    using System.Security;
    using System.Threading.Tasks;

    public interface IDatabaseRepo : IResourcesRepo
    {
        Task CreateServerAsync(string name, SecureString adminName, SecureString adminPassword);
        Task CreateAsync(string name, string serverName, Sku dbSku);
    }
}