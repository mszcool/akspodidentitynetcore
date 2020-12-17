using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ResourcesBackend
{
    public class GreeterService : GrpcGreeter.GreeterService.GreeterServiceBase
    {
        private readonly ILogger<GreeterService> _logger;
        private readonly IOptions<ResourcesAppConfig.ResourcesConfig> _config;

        public GreeterService(ILogger<GreeterService> logger, IOptions<ResourcesAppConfig.ResourcesConfig> config)
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
