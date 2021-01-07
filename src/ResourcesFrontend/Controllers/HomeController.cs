namespace MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Controllers
{
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using MszCool.Samples.PodIdentityDemo.GrpcResourceManagement;
    using MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Configuration;
    using MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Models;
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Interfaces;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;
    using System.Threading.Tasks;

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly FrontendConfig _frontendSettings;
        private readonly IResourcesRepo _resourcesRepo;
        private readonly IStorageRepo _storageRepo;
        private GrpcGreeter.GreeterService.GreeterServiceClient _grpcGreeterClient;
        private ResourcesService.ResourcesServiceClient _grpcResourcesBackend;

        public HomeController(
            ILogger<HomeController> logger,
            IOptions<FrontendConfig> resourcesSettings,
            IResourcesRepo resourcesRepo,
            IStorageRepo storageRepo,
            GrpcGreeter.GreeterService.GreeterServiceClient grpcGreeterClient,
            ResourcesService.ResourcesServiceClient grpcResourcesBackend)
        {
            _logger = logger;
            _frontendSettings = resourcesSettings.Value;
            _resourcesRepo = resourcesRepo;
            _storageRepo = storageRepo;
            _grpcGreeterClient = grpcGreeterClient;
            _grpcResourcesBackend = grpcResourcesBackend;
        }

        public IActionResult Index()
        {
            _logger.LogTrace("HomeController.Index entered.");

            try {
                var resourcesInGroup = _resourcesRepo.GetAllAsync().Result;

                var resourcesViewModel = new ResourcesViewModel
                {
                    SubscriptionId = _frontendSettings.ResourcesConfig.SubscriptionId,
                    ResourceGroupName = _frontendSettings.ResourcesConfig.ResourceGroupName,
                    ResourcesInGroup = resourcesInGroup
                };

                _logger.LogInformation($"Retrieved #{resourcesInGroup.Count} from {_frontendSettings.ResourcesConfig.ResourceGroupName} in {_frontendSettings.ResourcesConfig.SubscriptionId}.");

                return View(resourcesViewModel);
            }
            finally {
                _logger.LogTrace("HomeController.Index completed.");
            }
        }

        [HttpGet("details/{resourceId}")]
        public IActionResult Details(string resourceId)
        {
            _logger.LogTrace($"HomeController.Details for {resourceId} entered.");
            try {
                var idDecoded = System.Web.HttpUtility.UrlDecode(resourceId);

                var resourceDetails = _resourcesRepo.GetByIdAsync(idDecoded).Result;

                var resourceDetailsViewModel = new SingleResourceViewModel
                {
                    SubscriptionId = _frontendSettings.ResourcesConfig.SubscriptionId,
                    ResourceGroupName = _frontendSettings.ResourcesConfig.ResourceGroupName,
                    Resource = resourceDetails
                };

                return View(resourceDetailsViewModel);
            }
            finally {
                _logger.LogTrace($"HomeController.Details for {resourceId} completed.");
            }
        }

        public IActionResult Create([RegularExpression(@"^(Datalake|Blob)$")] string resourceType)
        {
            _logger.LogTrace($"HomeController.Create (GET) for {resourceType} entered.");
            try {
                var creationVm = new ResourceToCreateViewModel
                {
                    TryWithoutPrivilegedBackend = false,
                    ResourceName = "",
                    FriendlyType = resourceType,
                    Location = "",
                    ResourceSku = MszCool.Samples.PodIdentityDemo.ResourcesRepository.Sku.Standard
                };

                // For a Datalake in this example a filesystem name and folder name can be passed in.
                if (resourceType == "Datalake")
                {
                    creationVm.ResourcePropertiesForCreation = new Dictionary<string, string> {
                        { "Filesystem", "demofs" },
                        { "Folder", "default" }
                    };
                }

                return View(creationVm);
            }
            finally {
                _logger.LogTrace($"HomeController.Create (GET) for {resourceType} completed.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([Bind] ResourceToCreateViewModel creationInfo)
        {
            _logger.LogTrace($"HomeController.Create (POST) with {creationInfo.ResourceName} entered.");
            try {
                if (ModelState.IsValid)
                {
                    // Trying without the privileged backend service should demonstrate the value of the concept of
                    // creating privileged, private microservices for control plane operations of an PaaS/SaaS platform
                    // that needs to provision resources when dynamically provisioning customer instances / tenants for their offering.
                    if (creationInfo.TryWithoutPrivilegedBackend)
                    {
                        _logger.LogInformation($"HomeController.Create (POST) trying to create resource of type {creationInfo.FriendlyType} without backend-service.");
                        switch (creationInfo.FriendlyType)
                        {
                            case "Datalake":
                                await _storageRepo.CreateAsync(
                                                creationInfo.ResourceName,
                                                creationInfo.Location,
                                                StorageType.Datalake,
                                                creationInfo.ResourceSku,
                                                _frontendSettings.SecurityConfig.ClientId,
                                                creationInfo.ResourcePropertiesForCreation["Filesystem"],
                                                creationInfo.ResourcePropertiesForCreation["Folder"]);
                                break;

                            case "Blob":
                                await _storageRepo.CreateAsync(
                                                creationInfo.ResourceName,
                                                creationInfo.Location,
                                                StorageType.Blob,
                                                creationInfo.ResourceSku);
                                break;

                            default:
                                throw new System.ArgumentException("Invalid resource type passed in. Please check valid types for this sample!");
                        };
                        _logger.LogInformation($"HomeController.Create (POST) created resource of type {creationInfo.FriendlyType} without backend-service, SUCCESSFULLY.");
                    }
                    else
                    {
                        _logger.LogInformation($"HomeController.Create (POST) trying to create resource of type {creationInfo.FriendlyType} WITH gRPC backend-service.");

                        // Call the privileged backend service. In a setup in which the managed identity of this frontend web app
                        // has reader permissions, only (which should be the case), only by calling the privileged backend service
                        // the resource creation operations should succeed.
    #pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
                        var requestMessage = new ResourceCreationRequest
                        {
                            Name = creationInfo.ResourceName,
                            Location = creationInfo.Location,
                            Sku = creationInfo.ResourceSku switch   // This here caused CS8524 - probably a compiler bug?
                            {
                                ResourcesRepository.Sku.Basic => SupportedSkus.Basic,
                                ResourcesRepository.Sku.Standard => SupportedSkus.Standard,
                                ResourcesRepository.Sku.Premium => SupportedSkus.Premium
                            },
                            ResType = creationInfo.FriendlyType switch
                            {
                                "Datalake" => SupportedResourceTypes.Datalake,
                                "Blob" => SupportedResourceTypes.Storage,
                                _ => SupportedResourceTypes.Generic
                            }
                        };
    #pragma warning restore CS8524

                        // If this is supposed to be an ADLS account, pass in folder- and file-name to the gRPC service.
                        if(requestMessage.ResType == SupportedResourceTypes.Datalake)
                        {
                            requestMessage.Props.Add(creationInfo.ResourcePropertiesForCreation);
                        }

                        // Call the service for the resource creation request
                        _logger.LogInformation($"Trying to call gRPC backend service on {_frontendSettings.EndpointsConfig.BackendServiceEndpointUri}...");
                        var response = await _grpcResourcesBackend.CreateStorageAsync(requestMessage);
                        _logger.LogInformation($"HomeController.Create (POST) calling gRPC backend-service completed, SUCCESSFULLY.");

                        if(!response.Succeeded)
                        {
                            TempData["ErrorMessage"] = $"Failed creating resource through gRPC backend. Message returned from backend: {response.Message}";
                            return RedirectToAction("Error", "Error");
                        }
                    }

                    return RedirectToAction("Index");
                }
                else
                {
                    return View(creationInfo);
                }
            }
            finally {
                _logger.LogTrace($"HomeController.Create (POST) with {creationInfo.ResourceName} completed.");
            }
        }

        public IActionResult Privacy()
        {
            try
            {
                var response = _grpcGreeterClient.SayHello(new GrpcGreeter.HelloRequest { Name = "Mario Szpuszta" });
                ViewData["message"] = response.Message;
                return View();
            }
            catch
            {
                TempData["ErrorMessage"] = "Cannot reach Greeter Service!";
                return RedirectToAction("Error", "Error");
            }
        }
    }
}
