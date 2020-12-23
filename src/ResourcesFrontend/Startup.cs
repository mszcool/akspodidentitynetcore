namespace MszCool.Samples.PodIdentityDemo.ResourcesFrontend
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using MszCool.Samples.PodIdentityDemo.ResourcesFrontend.Configuration;

    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private FrontendConfig FrontendConfig { get; set; }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Loading and initalizing configuration
            var configSection = Configuration.GetSection(nameof(FrontendConfig));
            if (configSection != null)
            {
                services.Configure<FrontendConfig>(configSection);
                FrontendConfig = configSection.Get<FrontendConfig>();
            }
            else
            {
                throw new System.Exception("Missing configuration for Frontend service!");
            }

            // Loading and initializing the resouces repositories
            var repoFactory = new ResourcesRepository.RepositoryFactory(
                FrontendConfig.ResourcesConfig.SubscriptionId,
                FrontendConfig.ResourcesConfig.ResourceGroupName
            );
            services.AddSingleton<ResourcesRepository.Interfaces.IResourcesRepo>(repoFactory.CreateResourcesRepo());
            services.AddSingleton<ResourcesRepository.Interfaces.IStorageRepo>(repoFactory.CreateStorageRepo());

            // ASP.NET Specifics
            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var useDevException = (env.IsDevelopment() && !FrontendConfig.DisableDeveloperHomePageInDevMode);
            if (useDevException)
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
