using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MszCool.Samples.PodIdentityDemo.ResourcesBackend.Configuration;
using MszCool.Samples.PodIdentityDemo.ResourcesRepository.GrpcResourceManagement;
using System;
using System.Threading.Tasks;

namespace MszCool.Samples.PodIdentityDemo.ResourcesBackend.Services
{
    public class ResourcesService : MszCool.Samples.PodIdentityDemo.ResourcesRepository.GrpcResourceManagement.ResourcesService.ResourcesServiceBase
    {
        private readonly ILogger<ResourcesService> _logger;
        private readonly BackendConfig _config;

        public ResourcesService(ILogger<ResourcesService> logger, IOptions<BackendConfig> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public override Task<ResourcesRepository.GrpcResourceManagement.ResourceResponse> Get(ResourcesRepository.GrpcResourceManagement.ResourceRequest request, ServerCallContext context)
        {
            return Task.FromResult<ResourcesRepository.GrpcResourceManagement.ResourceResponse>(CreateDummyResponse());
        }

        public override Task<ResourcesRepository.GrpcResourceManagement.ResourceResponse> CreateStorage(ResourcesRepository.GrpcResourceManagement.ResourceCreationRequest request, ServerCallContext context)
        {
            return Task.FromResult<ResourcesRepository.GrpcResourceManagement.ResourceResponse>(CreateDummyResponse());
        }

        private ResourcesRepository.GrpcResourceManagement.ResourceResponse CreateDummyResponse()
        {
            var entity = new ResourcesRepository.GrpcResourceManagement.ResourceEntity {
                Id = "dummy id",
                Name = "dummy name",
                Type = "dummy type",
                Location = "dummy location"
            };
            entity.Props.Add("test", "dummy");

            var result = new ResourcesRepository.GrpcResourceManagement.ResourceResponse {
                                    Succeeded = true,
                                    Message = "dummy"
                                };
            result.Resources.Add(entity);

            return result;
        }
    }
}