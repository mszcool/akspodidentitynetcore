using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace ResourcesBackend
{
    public class GreeterService : GrpcGreeter.GreeterService.GreeterServiceBase
    {
        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
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
