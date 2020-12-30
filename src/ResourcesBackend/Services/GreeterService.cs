using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MszCool.Samples.PodIdentityDemo.ResourcesBackend.Configuration;
using System;
using System.Threading.Tasks;

namespace MszCool.Samples.PodIdentityDemo.ResourcesBackend
{
    public class GreeterService : GrpcGreeter.GreeterService.GreeterServiceBase
    {
        private readonly ILogger<GreeterService> _logger;
        private readonly BackendConfig _config;

        public GreeterService(ILogger<GreeterService> logger, IOptions<BackendConfig> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public override Task<GrpcGreeter.HelloResponse> SayHello(GrpcGreeter.HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new GrpcGreeter.HelloResponse
            {
                Message = $"Hello {request.Name} on {DateTime.Now.ToString("yyyy-mm-dd HH:MM:ss")}!"
            });
        }
    }
}
