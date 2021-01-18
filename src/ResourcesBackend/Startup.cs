namespace MszCool.Samples.PodIdentityDemo.ResourcesBackend
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using MszCool.Samples.PodIdentityDemo.ResourcesBackend.Configuration;
    using MszCool.Samples.PodIdentityDemo.ResourcesBackend.Services;

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        private BackendConfig BackendConfig { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var configSection = Configuration.GetSection(nameof(BackendConfig));
            if(configSection != null) {
                services.Configure<BackendConfig>(configSection);
                BackendConfig = configSection.Get<BackendConfig>();
            }
            else {
                throw new System.Exception("Missing configuration for Backend service!");
            }

            // Loading and initializing the resouces repositories
            var repoFactory = new ResourcesRepository.RepositoryFactory(
                BackendConfig.ResourcesConfig.SubscriptionId,
                BackendConfig.ResourcesConfig.ResourceGroupName
            );
            if(!BackendConfig.SecurityConfig.UseMSI) {
                repoFactory.ConfigureEnvironment(
                    BackendConfig.SecurityConfig.ClientId,
                    BackendConfig.SecurityConfig.ClientSecret,
                    BackendConfig.SecurityConfig.TenantId);
            }
            else {
                repoFactory.ConfigureEnvironment(BackendConfig.SecurityConfig.TenantId);
            }
            services.AddSingleton<ResourcesRepository.Interfaces.IResourcesRepo>(f => {
                var logFac = f.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
                return repoFactory.CreateResourcesRepo(logFac);
            });
            services.AddSingleton<ResourcesRepository.Interfaces.IStorageRepo>(f => {
                var logFac = f.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
                return repoFactory.CreateStorageRepo(logFac);
            });

            // GRPC Stuff
            services.AddGrpc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<GreeterService>();
                endpoints.MapGrpcService<ResourcesService>();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
