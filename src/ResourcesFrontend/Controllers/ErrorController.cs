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

    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;
        private readonly FrontendConfig _frontendSettings;

        public ErrorController(ILogger<ErrorController> logger, IOptions<FrontendConfig> resourcesSettings)
        {
            _logger = logger;
            _frontendSettings = resourcesSettings.Value;
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