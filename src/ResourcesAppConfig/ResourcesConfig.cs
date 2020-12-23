namespace MszCool.Samples.PodIdentityDemo.ResourcesAppConfig.Configuration
{
    public class ResourcesConfig
    {
        public const string ConfigName = "ResourcesConfig";

        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public SecurityConfig SecurityConfig { get; set; }
    }
}
