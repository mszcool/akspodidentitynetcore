namespace MszCool.PodIdentityDemo.ResourcesRepository
{
    using System;
    using System.Collections.Generic;

    public class Resource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string Type { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }
}