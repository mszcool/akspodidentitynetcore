namespace MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Configuration
{
    using MszCool.Samples.PodIdentityDemo.ResourcesAppConfig.Configuration;

    public class FrontendConfig
    {
        public bool DisableDeveloperHomePageInDevMode { get; set; }
        public ResourcesConfig ResourcesConfig { get; set; }
        public SecurityConfig SecurityConfig { get; set; }
        public EndpointsConfig EndpointsConfig { get; set; }
    }
}