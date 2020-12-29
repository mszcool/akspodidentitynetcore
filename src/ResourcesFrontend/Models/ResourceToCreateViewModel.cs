namespace MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public class ResourceToCreateViewModel
    {
        [Required]
        [Display(Name = "Try without privileged backend")]
        public bool TryWithoutPrivilegedBackend { get; set; }

        [Required]
        [StringLength(16, MinimumLength = 3)]
        [Display(Name = "Resource Name")]
        public string ResourceName { get; set; }

        [Required]
        [RegularExpression("^(Datalake|Blob)$")]
        [Display(Name = "Type of Resource")]
        public string FriendlyType { get; set; }

        [Required]
        [Display(Name = "Region / Location")]
        public string Location { get; set; }

        [Required]
        [Display(Name = "Simplified Tier")]
        public MszCool.Samples.PodIdentityDemo.ResourcesRepository.Sku ResourceSku { get; set; }
        
        public Dictionary<string, string> ResourcePropertiesForCreation { get; set; }
    }
}