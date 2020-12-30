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

        public async override Task<GrpcResourceManagement.ResourceResponse> Get(GrpcResourceManagement.ResourceRequest request, ServerCallContext context)
        {
            _logger.LogTrace("GRPC ResourcesService.Get entering...");

            // Create a skeleton for the response message.
            var responseMsg = new GrpcResourceManagement.ResourceResponse
            {
                Succeeded = false,
                Message = string.Empty
            };

            try
            {
                // Depending on whether an ID was passed in, get all or a specific resource.
                if (string.IsNullOrWhiteSpace(request.Id))
                {
                    var resources = await _resourcesRepo.GetAllAsync();
                    foreach (var r in resources)
                    {
                        responseMsg.Resources.Add(MapRepoResourceToGrpcResource(r));
                    }
                }
                else
                {
                    var resource = await _resourcesRepo.GetByIdAsync(request.Id);
                    responseMsg.Resources.Add(MapRepoResourceToGrpcResource(resource));
                }

                responseMsg.Succeeded = true;
                responseMsg.Message = $"Retrieval succeeded for subscription {_config.ResourcesConfig.SubscriptionId} in group {_config.ResourcesConfig.ResourceGroupName}!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GRPC ResourcesService.Get ran into exception.");

                responseMsg.Succeeded = false;
                responseMsg.Message = $"Failed retrieving resources from {_config.ResourcesConfig.SubscriptionId} of group {_config.ResourcesConfig.ResourceGroupName} due to the following error: {ex.Message}";
            }

            _logger.LogTrace($"GRPC ResourcesService.Get exiting with responseMsg.Succeeded = {responseMsg.Succeeded} and responseMsg.Message = {responseMsg.Message}.");
            return responseMsg;
        }

        public async override Task<GrpcResourceManagement.ResourceResponse> CreateStorage(GrpcResourceManagement.ResourceCreationRequest request, ServerCallContext context)
        {
            _logger.LogTrace("GRPC ResourcesService.CreateStorage entering...");

            // Craft a skeleton for a response message.
            var responseMsg = new GrpcResourceManagement.ResourceResponse
            {
                Succeeded = false,
                Message = string.Empty
            };

            try
            {
                // Some basic request message processing.
                var fsName = request.Props.GetValueOrDefault("Filesystem", string.Empty);
                var folderName = request.Props.GetValueOrDefault("Folder", string.Empty);
                var repoSku = request.Sku switch
                {
                    GrpcResourceManagement.SupportedSkus.Basic => ResourcesRepository.Sku.Basic,
                    _ => ResourcesRepository.Sku.Basic
                };

                // Try creating the resources.
                switch (request.ResType)
                {
                    case GrpcResourceManagement.SupportedResourceTypes.Datalake:
                        if (string.IsNullOrWhiteSpace(fsName) || string.IsNullOrWhiteSpace(folderName))
                        {
                            throw new ArgumentException("Both, Filesystem and Folder properties need to be passed with none-empty strings!");
                        }

                        await _storageRepo.CreateAsync(
                            request.Name,
                            request.Location,
                            StorageType.Datalake,
                            repoSku,
                            _config.SecurityConfig.ClientId,
                            fsName,
                            folderName);
                        break;

                    case GrpcResourceManagement.SupportedResourceTypes.Storage:
                        await _storageRepo.CreateAsync(
                            request.Name,
                            request.Location,
                            StorageType.Blob,
                            repoSku);
                        break;

                    default:
                        throw new Exception("Unsupported resource type used in request!");
                }

                responseMsg.Succeeded = true;
                responseMsg.Message = $"Successfully created {request.ResType} in {_config.ResourcesConfig.ResourceGroupName} of subscription {_config.ResourcesConfig.SubscriptionId}!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GRPC ResourcesService.Create ran into exception.");

                responseMsg.Succeeded = false;
                responseMsg.Message = $"Failed creating a new {request.ResType.ToString()} in {_config.ResourcesConfig.ResourceGroupName} of subscription {_config.ResourcesConfig.SubscriptionId} due to the following error: {ex.Message}";
            }
            
            _logger.LogTrace($"GRPC ResourcesService.Create exiting with responseMsg.Succeeded = {responseMsg.Succeeded} and responseMsg.Message = {responseMsg.Message}.");
            return responseMsg;
        }

        private GrpcResourceManagement.ResourceEntity MapRepoResourceToGrpcResource(ResourceEntity entity)
        {
            var g = new GrpcResourceManagement.ResourceEntity
            {
                Id = entity.Id,
                Name = entity.Name,
                Location = entity.Location,
                Type = entity.Type
            };
            g.Props.Add(entity.Properties);

            return g;
        }
    }
}