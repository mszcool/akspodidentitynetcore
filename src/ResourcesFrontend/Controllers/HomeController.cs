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
    using System.Diagnostics;

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly FrontendConfig _frontendSettings;

        public HomeController(ILogger<HomeController> logger, IOptions<FrontendConfig> resourcesSettings)
        {
            _logger = logger;
            _frontendSettings = resourcesSettings.Value;
        }

        public IActionResult Index()
        {
            return View();
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
