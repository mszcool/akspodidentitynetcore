namespace MszCool.Samples.PodIdentityDemo.ResourcesAppConfig.Configuration
{
    public class SecurityConfig
    {
        public bool UseMSI { get; set; }
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }
}