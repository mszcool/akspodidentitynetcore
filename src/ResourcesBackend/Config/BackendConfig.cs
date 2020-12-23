namespace MszCool.Samples.PodIdentityDemo.ResourcesBackend.Configuration
{
    using MszCool.Samples.PodIdentityDemo.ResourcesAppConfig.Configuration;

    public class BackendConfig
    {
        public ResourcesConfig ResourcesConfig { get; set; }
        public SecurityConfig SecurityConfig { get; set; }
    }
}