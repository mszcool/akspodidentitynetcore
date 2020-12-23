﻿namespace MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Controllers
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
            var ch = GrpcChannel.ForAddress(_frontendSettings.EndpointsConfig.BackendServiceEndpointUri);
            var client = new GrpcGreeter.GreeterService.GreeterServiceClient(ch);

            var response = client.SayHello(new GrpcGreeter.HelloRequest { Name = "Mario Szpuszta" });
            ViewData["message"] = response.Message;

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var errMsg = TempData["errorMessage"];

            // Get the original exception data from the exception handling path feature.
            var exHandler = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if ( (exHandler?.Error != null) && (TempData["errorMessage"] == null) ) {
                if(exHandler.Error is Grpc.Core.RpcException) {
                    errMsg = ((Grpc.Core.RpcException)exHandler.Error).Status.Detail;
                }
                else {
                    errMsg = exHandler.Error.Message;
                }
            }

            // Construct the ErrorViewModel with available details and populate the Error view.
            var evm = new ErrorViewModel {
                                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                                DetailedMessage = (errMsg != null && !string.IsNullOrWhiteSpace(errMsg.ToString())) 
                                                   ? errMsg.ToString() 
                                                   : "No further details are available."
                            };
            return View(evm);
        }
    }
}
