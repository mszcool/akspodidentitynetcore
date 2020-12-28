namespace MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Controllers
{
    using Grpc.Net.Client;
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Configuration;
    using MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Models;
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Entities;
    using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Interfaces;
    using System.Diagnostics;

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly FrontendConfig _frontendSettings;
        private readonly IResourcesRepo _resourcesRepo;

        public HomeController(
            ILogger<HomeController> logger, 
            IOptions<FrontendConfig> resourcesSettings,
            IResourcesRepo resourcesRepo)
        {
            _logger = logger;
            _frontendSettings = resourcesSettings.Value;
            _resourcesRepo = resourcesRepo;
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

        [HttpGet("{resourceId}")]
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

        public IActionResult NewResource()
        {
            return View();
        }

        [HttpPost]
        public IActionResult NewResource(ResourceEntity entity)
        {
            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            try
            {
                var ch = GrpcChannel.ForAddress(_frontendSettings.EndpointsConfig.BackendServiceEndpointUri);
                var client = new GrpcGreeter.GreeterService.GreeterServiceClient(ch);

                var response = client.SayHello(new GrpcGreeter.HelloRequest { Name = "Mario Szpuszta" });
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
