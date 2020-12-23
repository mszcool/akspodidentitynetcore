using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MszCool.Samples.PodIdentityDemo.ResourcesAppConfig.Configuration;
using System.Threading.Tasks;

namespace MszCool.Samples.PodIdentityDemo.ResourcesBackend
{
    public class GreeterService : GrpcGreeter.GreeterService.GreeterServiceBase
    {
        private readonly ILogger<GreeterService> _logger;
        private readonly IOptions<ResourcesConfig> _config;

        public GreeterService(ILogger<GreeterService> logger, IOptions<ResourcesConfig> config)
        {
            _logger = logger;
            _config = config;
        }

        public override Task<GrpcGreeter.HelloResponse> SayHello(GrpcGreeter.HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new GrpcGreeter.HelloResponse
            {
                Message = "Hello " + request.Name
            });
        }
    }
}
