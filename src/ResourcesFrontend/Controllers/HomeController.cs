using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResourcesFrontend.Models;

namespace ResourcesFrontend.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IOptions<ResourcesAppConfig.ResourcesConfig> _resourcesSettings;

        public HomeController(ILogger<HomeController> logger, IOptions<ResourcesAppConfig.ResourcesConfig> resourcesSettings)
        {
            _logger = logger;
            _resourcesSettings = resourcesSettings;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            try
            {
                var ch = GrpcChannel.ForAddress("https://localhost:5243");
                var client = new GrpcGreeter.GreeterService.GreeterServiceClient(ch);

                var response = client.SayHello(new GrpcGreeter.HelloRequest { Name = "Mario Szpuszta" });

                ViewData["message"] = response.Message;
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
            }

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
