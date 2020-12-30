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
    using System;
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
            _logger.LogTrace("Entering Error-handler in Error controller.");

            try
            {
                var errMsg = TempData["errorMessage"];

                // Get the original exception data from the exception handling path feature.
                var exHandler = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
                if ((exHandler?.Error != null) && (TempData["errorMessage"] == null))
                {
                    // Get the error summary for the error page
                    if (exHandler.Error is Grpc.Core.RpcException)
                    {
                        errMsg = ((Grpc.Core.RpcException)exHandler.Error).Status.Detail;
                    }
                    else
                    {
                        errMsg = exHandler.Error.Message;
                    }

                    // First, log the error
                    _logger.LogError(exHandler.Error, errMsg.ToString());
                }
                else if(errMsg != null)
                {
                    _logger.LogError(errMsg.ToString());
                }
                else
                {
                    _logger.LogError("Unknown error occured, check sample for bugs!");
                }

                // Construct the ErrorViewModel with available details and populate the Error view.
                var evm = new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    DetailedMessage = (errMsg != null && !string.IsNullOrWhiteSpace(errMsg.ToString()))
                                                       ? errMsg.ToString()
                                                       : "No further details are available."
                };
                return View(evm);
            }
            catch(Exception ex)
            {
                var handlerMsg = $"Error-handler itself failed with exception: {ex.Message}";
                _logger.LogError(ex, handlerMsg);
                var evm = new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    DetailedMessage = handlerMsg
                };
                return View(evm);
            }
            finally
            {
                _logger.LogTrace("Leaving Error-handler in Error Controller.");
            }
        }
    }
}