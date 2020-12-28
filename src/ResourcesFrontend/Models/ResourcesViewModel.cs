namespace MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Models
{
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Entities;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public class ResourcesViewModel
    {
        [Display(Name="Subscription ID")]
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public IList<ResourceEntity> ResourcesInGroup { get; set; }
    }
}
