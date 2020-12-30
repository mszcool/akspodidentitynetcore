using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MszCool.Samples.PodIdentityDemo.ResourcesBackend.Configuration;
using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Entities;
using MszCool.Samples.PodIdentityDemo.ResourcesRepository.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MszCool.Samples.PodIdentityDemo.ResourcesBackend.Services
{
    public class ResourcesService : MszCool.Samples.PodIdentityDemo.GrpcResourceManagement.ResourcesService.ResourcesServiceBase
    {
        private readonly ILogger<ResourcesService> _logger;
        private readonly BackendConfig _config;
        private readonly IResourcesRepo _resourcesRepo;
        private readonly IStorageRepo _storageRepo;

        public ResourcesService(
            ILogger<ResourcesService> logger,
            IOptions<BackendConfig> config,
            IResourcesRepo resourcesRepo,
            IStorageRepo storageRepo)
        {
            _logger = logger;
            _config = config.Value;
            _resourcesRepo = resourcesRepo;
            _storageRepo = storageRepo;
        }

        public override Task<GrpcResourceManagement.ResourceResponse> Get(GrpcResourceManagement.ResourceRequest request, ServerCallContext context)
        {
            return Task.FromResult<GrpcResourceManagement.ResourceResponse>(CreateDummyResponse());
        }

        public override Task<GrpcResourceManagement.ResourceResponse> CreateStorage(GrpcResourceManagement.ResourceCreationRequest request, ServerCallContext context)
        {
            return Task.FromResult<GrpcResourceManagement.ResourceResponse>(CreateDummyResponse());
        }

        private GrpcResourceManagement.ResourceResponse CreateDummyResponse()
        {
            var entity = new GrpcResourceManagement.ResourceEntity {
                Id = "dummy id",
                Name = "dummy name",
                Type = "dummy type",
                Location = "dummy location"
            };
            entity.Props.Add("test", "dummy");

            var result = new GrpcResourceManagement.ResourceResponse {
                                    Succeeded = true,
                                    Message = "dummy"
                                };
            result.Resources.Add(entity);

            return result;
        }
    }
}