namespace MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Controllers
{
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
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

        public HomeController(
            ILogger<HomeController> logger, 
            IOptions<FrontendConfig> resourcesSettings,
            IResourcesRepo resourcesRepo,
            IStorageRepo storageRepo,
            GrpcGreeter.GreeterService.GreeterServiceClient grpcGreeterClient)
        {
            _logger = logger;
            _frontendSettings = resourcesSettings.Value;
            _resourcesRepo = resourcesRepo;
            _storageRepo = storageRepo;
            _grpcGreeterClient = grpcGreeterClient;
        }

        public IActionResult Index()
        {
            var resourcesInGroup = _resourcesRepo.GetAllAsync().Result;

            var resourcesViewModel = new ResourcesViewModel {
                SubscriptionId = _frontendSettings.ResourcesConfig.SubscriptionId,
                ResourceGroupName = _frontendSettings.ResourcesConfig.ResourceGroupName,
                ResourcesInGroup = resourcesInGroup
            };

            return View(resourcesViewModel);
        }

        [HttpGet("details/{resourceId}")]
        public IActionResult Details(string resourceId)
        {
            var idDecoded = System.Web.HttpUtility.UrlDecode(resourceId);

            var resourceDetails = _resourcesRepo.GetByIdAsync(idDecoded).Result;

            var resourceDetailsViewModel = new SingleResourceViewModel {
                SubscriptionId = _frontendSettings.ResourcesConfig.SubscriptionId,
                ResourceGroupName = _frontendSettings.ResourcesConfig.ResourceGroupName,
                Resource = resourceDetails
            };

            return View(resourceDetailsViewModel);
        }

        public IActionResult Create([RegularExpression(@"^(Datalake|Blob)$")]string resourceType)
        {
            var creationVm = new ResourceToCreateViewModel {
                TryWithoutPrivilegedBackend = false,
                ResourceName = "",
                FriendlyType = resourceType,
                Location = "",
                ResourceSku = MszCool.Samples.PodIdentityDemo.ResourcesRepository.Sku.Standard
            };

            // For a Datalake in this example a filesystem name and folder name can be passed in.
            if(resourceType == "Datalake")
            {
                creationVm.ResourcePropertiesForCreation = new Dictionary<string, string> {
                    { "Filesystem", "demofs" },
                    { "Folder", "default" }
                };
            }

            return View(creationVm);
        }

        [HttpPost]
        public async Task<IActionResult> Create([Bind]ResourceToCreateViewModel creationInfo)
        {
            if(ModelState.IsValid)
            {
                // Trying without the privileged backend service should demonstrate the value of the concept of
                // creating privileged, private microservices for control plane operations of an PaaS/SaaS platform
                // that needs to provision resources when dynamically provisioning customer instances / tenants for their offering.
                if(creationInfo.TryWithoutPrivilegedBackend)
                {
                    switch(creationInfo.FriendlyType) 
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
                }
                else
                {
                    // TODO: Call the privileged GRPC service which should have all required permissions to 
                    // get the story across for this entire demo.
                }

                return RedirectToAction("Index");
            }
            else
            {
                return View(creationInfo);
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
